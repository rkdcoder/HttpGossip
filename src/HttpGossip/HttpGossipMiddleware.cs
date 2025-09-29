using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;

namespace HttpGossip
{
    public sealed class HttpGossipMiddleware : IMiddleware
    {
        private readonly HttpGossipOptions _options;
        private readonly ILogQueueWriter _queueWriter;
        private readonly ILogger<HttpGossipMiddleware> _logger;

        public HttpGossipMiddleware(IOptions<HttpGossipOptions> options, ILogQueueWriter queueWriter, ILogger<HttpGossipMiddleware> logger)
        {
            _options = options.Value;
            _queueWriter = queueWriter;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var path = context.Request.Path.ToString();

            // Bypass only if user provided patterns
            if (PathMatchers.MatchesAny(path, _options.BypassPaths))
            {
                await next(context);
                return;
            }

            var requestStart = DateTime.Now;
            var sw = Stopwatch.StartNew();

            string requestBody = string.Empty;
            string responseBody = string.Empty;
            string responseContentType = string.Empty;
            int statusCode = 0;
            bool isSuccess = false;
            string? exceptionString = null;

            var originalBodyStream = context.Response.Body;

            try
            {
                // Always capture bodies (not optional)
                requestBody = await GetRequestBodyAsync(context.Request, _options.MaxBodyBytes);

                using var memoryStream = new MemoryStream();
                context.Response.Body = memoryStream;

                await next(context);

                responseBody = await GetResponseBodyAsync(memoryStream, _options.MaxBodyBytes);
                await memoryStream.CopyToAsync(originalBodyStream);

                statusCode = context.Response.StatusCode;
                responseContentType = context.Response.ContentType ?? string.Empty;
                isSuccess = statusCode < 400;
            }
            catch (Exception ex)
            {
                statusCode = 500;
                isSuccess = false;
                exceptionString = ex.ToString();
                responseBody = "[unavailable due to exception]";
                responseContentType = string.Empty;
                _logger.LogError(ex, "[HttpGossip] Error while processing the request.");
                throw; // Propagate to preserve API behavior
            }
            finally
            {
                sw.Stop();
                context.Response.Body = originalBodyStream;

                // Redact only if user provided SensitivePaths and matched
                if (PathMatchers.MatchesAny(path, _options.SensitivePaths))
                {
                    requestBody = "[REDACTED]";
                    responseBody = "[REDACTED]";
                }

                // Authorization: keep only scheme (e.g., "Bearer")
                var authorization = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authorization))
                {
                    var spaceIdx = authorization.IndexOf(' ');
                    authorization = spaceIdx > 0 ? authorization[..spaceIdx] : "[REDACTED]";
                }

                var record = new HttpGossipRecord
                {
                    LogRequestId = context.TraceIdentifier,
                    LogRequestStart = requestStart,
                    LogRequestEnd = DateTime.Now, // Local time
                    LogElapsedSeconds = sw.Elapsed.TotalSeconds,
                    LogIsSuccess = isSuccess,
                    LogStatusCode = statusCode,
                    LogException = exceptionString,
                    LogUserName = GetUserName(context.User),
                    LogLocale = context.Request.Headers["Accept-Language"].FirstOrDefault(),
                    LogMethod = context.Request.Method,
                    LogIsHttps = context.Request.IsHttps,
                    LogProtocol = context.Request.Protocol,
                    LogScheme = context.Request.Scheme,
                    LogPath = context.Request.Path,
                    LogQueryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                    LogRouteValues = context.Request.RouteValues.Count > 0
                        ? string.Join(", ", context.Request.RouteValues.Select(kv => $"{kv.Key}={kv.Value}"))
                        : null,
                    LogAuthorization = authorization,
                    LogHeaders = SerializeHeaders(context.Request.Headers),
                    LogCookies = SerializeCookies(context.Request.Cookies),
                    LogReferer = context.Request.Headers["Referer"].FirstOrDefault(),
                    LogAppName = context.Request.PathBase.HasValue
                        ? context.Request.PathBase.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                        : null,
                    LogHost = context.Request.Host.Value,
                    LogUserAgent = context.Request.Headers["User-Agent"].FirstOrDefault(),
                    LogSecChUa = context.Request.Headers["sec-ch-ua"].FirstOrDefault(),
                    LogSecChUaMobile = context.Request.Headers["sec-ch-ua-mobile"].FirstOrDefault(),
                    LogSecChUaPlatform = context.Request.Headers["sec-ch-ua-platform"].FirstOrDefault(),
                    LogContentLength = context.Request.ContentLength,
                    LogRequestBody = requestBody,
                    LogResponseBody = responseBody,
                    LogResponseContentType = responseContentType,
                    LogLocalIp = context.Connection.LocalIpAddress?.ToString(),
                    LogLocalPort = context.Connection.LocalPort,
                    LogRemoteIp = context.Connection.RemoteIpAddress?.ToString(),
                    LogRemotePort = context.Connection.RemotePort,
                    LogConnectionId = context.Connection.Id
                };

                // Fire-and-forget: drop if queue is full (never blocks)
                if (!_queueWriter.TryWrite(record))
                    _logger.LogDebug("[HttpGossip] Log queue is full; record dropped.");
            }
        }

        private static async Task<string> GetResponseBodyAsync(Stream body, int maxBytes)
        {
            body.Seek(0, SeekOrigin.Begin);
            var text = await ReadUpToAsync(body, maxBytes);
            body.Seek(0, SeekOrigin.Begin);
            return text;
        }

        private static async Task<string> GetRequestBodyAsync(HttpRequest request, int maxBytes)
        {
            request.EnableBuffering();
            request.Body.Seek(0, SeekOrigin.Begin);
            var text = await ReadUpToAsync(request.Body, maxBytes);
            request.Body.Seek(0, SeekOrigin.Begin);
            return text;
        }

        private static async Task<string> ReadUpToAsync(Stream stream, int maxBytes)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[8192];
            int remaining = maxBytes;
            int read;
            while (remaining > 0 && (read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)))) > 0)
            {
                await ms.WriteAsync(buffer.AsMemory(0, read));
                remaining -= read;
            }
            var text = Encoding.UTF8.GetString(ms.ToArray());
            // Conservative hint if we exactly hit the limit
            if (remaining == 0)
                return text + " [TRUNCATED]";
            return text;
        }

        static string? GetUserName(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true) return null;

            return user.Identity?.Name
                ?? user.FindFirst(ClaimTypes.Name)?.Value
                ?? user.FindFirst("name")?.Value
                ?? user.FindFirst("preferred_username")?.Value
                ?? user.FindFirst("unique_name")?.Value
                ?? user.FindFirst(ClaimTypes.Email)?.Value;
        }

        private static string SerializeHeaders(IHeaderDictionary headers) =>
            string.Join("\n", headers.Select(h => $"{h.Key}: {h.Value}"));

        private static string SerializeCookies(IRequestCookieCollection cookies) =>
            string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}"));
    }
}
