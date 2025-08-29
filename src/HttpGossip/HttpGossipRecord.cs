namespace HttpGossip
{
    // Persistence model
    public sealed class HttpGossipRecord
    {
        public string? LogRequestId { get; set; }
        public DateTime LogRequestStart { get; set; }
        public DateTime? LogRequestEnd { get; set; }
        public double? LogElapsedSeconds { get; set; }
        public bool? LogIsSuccess { get; set; }
        public int? LogStatusCode { get; set; }
        public string? LogException { get; set; }
        public string? LogUserName { get; set; }
        public string? LogLocale { get; set; }
        public string? LogMethod { get; set; }
        public bool? LogIsHttps { get; set; }
        public string? LogProtocol { get; set; }
        public string? LogScheme { get; set; }
        public string? LogPath { get; set; }
        public string? LogQueryString { get; set; }
        public string? LogRouteValues { get; set; }
        public string? LogAuthorization { get; set; }
        public string? LogHeaders { get; set; }
        public string? LogCookies { get; set; }
        public string? LogReferer { get; set; }
        public string? LogUserAgent { get; set; }
        public string? LogSecChUa { get; set; }
        public string? LogSecChUaMobile { get; set; }
        public string? LogSecChUaPlatform { get; set; }
        public string? LogAppName { get; set; }
        public string? LogHost { get; set; }
        public long? LogContentLength { get; set; }
        public string? LogRequestBody { get; set; }
        public string? LogResponseBody { get; set; }
        public string? LogResponseContentType { get; set; }
        public string? LogLocalIp { get; set; }
        public int? LogLocalPort { get; set; }
        public string? LogRemoteIp { get; set; }
        public int? LogRemotePort { get; set; }
        public string? LogConnectionId { get; set; }
    }
}