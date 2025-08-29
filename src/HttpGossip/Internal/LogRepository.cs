using Dapper;
using System.Data.Common;
using System.Text;

namespace HttpGossip.Internal
{
    internal sealed class LogRepository : ILogRepository
    {
        private readonly HttpGossipOptions _options;
        private readonly string _sql;

        public LogRepository(HttpGossipOptions options)
        {
            _options = options;
            _options.Validate();

            _sql = BuildInsertSql(_options.TableQualifiedName);
        }

        public async Task InsertAsync(HttpGossipRecord record, CancellationToken ct)
        {
            await using DbConnection conn = DbConnectionFactory.Create(_options.DatabaseName, _options.ConnectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            await conn.ExecuteAsync(new CommandDefinition(_sql, record, cancellationToken: ct)).ConfigureAwait(false);
        }

        private static string BuildInsertSql(string tableQualifiedName)
        {
            // Mirror columns from your table
            var cols = new[]
            {
                "LogRequestId",
                "LogRequestStart",
                "LogRequestEnd",
                "LogElapsedSeconds",
                "LogIsSuccess",
                "LogStatusCode",
                "LogException",
                "LogUserName",
                "LogLocale",
                "LogMethod",
                "LogIsHttps",
                "LogProtocol",
                "LogScheme",
                "LogPath",
                "LogQueryString",
                "LogRouteValues",
                "LogAuthorization",
                "LogHeaders",
                "LogCookies",
                "LogReferer",
                "LogUserAgent",
                "LogSecChUa",
                "LogSecChUaMobile",
                "LogSecChUaPlatform",
                "LogAppName",
                "LogHost",
                "LogContentLength",
                "LogRequestBody",
                "LogResponseBody",
                "LogResponseContentType",
                "LogLocalIp",
                "LogLocalPort",
                "LogRemoteIp",
                "LogRemotePort",
                "LogConnectionId"
            };

            var sb = new StringBuilder();
            sb.Append("INSERT INTO ").Append(tableQualifiedName).Append(" (");
            sb.Append(string.Join(", ", cols));
            sb.Append(") VALUES (");
            sb.Append(string.Join(", ", cols.Select(c => "@" + c)));
            sb.Append(')');
            return sb.ToString();
        }
    }
}