using Dapper;
using HttpGossip.Internal;
using System.Data.Common;

namespace HttpGossip
{
    /// <summary>
    /// helper to ensure the log table exists.
    /// Uses a separate connection (usually with elevated privileges).
    /// </summary>
    public static class HttpGossipSchema
    {
        public static async Task EnsureLogTableAsync(HttpGossipSchemaOptions options, CancellationToken ct = default)
        {
            options.Validate();

            await using DbConnection conn = DbConnectionFactory.Create(options.DatabaseName, options.ConnectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            var kind = DbConnectionFactory.ParseKind(options.DatabaseName);
            var (schema, table) = SplitQualifiedName(options.TableQualifiedName, kind);

            // Build provider-specific DDL
            var ddl = BuildCreateTableDdl(kind, schema, table);

            // Some providers need schema creation separately
            var createSchemaSql = BuildCreateSchemaIfNeeded(kind, schema);

            // Execute "create schema" if needed
            if (!string.IsNullOrEmpty(createSchemaSql))
                await conn.ExecuteAsync(new CommandDefinition(createSchemaSql, cancellationToken: ct)).ConfigureAwait(false);

            // Execute "create table if not exists" (or SQL Server guarded block)
            try
            {
                await conn.ExecuteAsync(new CommandDefinition(ddl, cancellationToken: ct)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Be tolerant to concurrent creators / already-exists races.
                if (!IsAlreadyExistsError(kind, ex))
                    throw;
            }
        }

        private static (string? schema, string table) SplitQualifiedName(string qualified, DbConnectionFactory.DbKind kind)
        {
            // Accept "schema.table" or just "table"
            var raw = qualified.Trim();

            // SQLite has no schema concept
            if (kind == DbConnectionFactory.DbKind.SQLite)
                return (null, raw);

            var parts = raw.Split('.', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 1)
            {
                // Provide sensible defaults per provider
                var defaultSchema = kind switch
                {
                    DbConnectionFactory.DbKind.SqlServer => "dbo",
                    DbConnectionFactory.DbKind.PostgreSQL => "public",
                    DbConnectionFactory.DbKind.MySql => null, // MySQL uses database instead of schema; keep null
                    _ => null
                };
                return (defaultSchema, parts[0]);
            }
            return (parts[0], parts[1]);
        }

        private static string QuoteIdent(DbConnectionFactory.DbKind kind, string ident)
        {
            return kind switch
            {
                DbConnectionFactory.DbKind.SqlServer => $"[{ident.Replace("]", "]]")}]",
                DbConnectionFactory.DbKind.PostgreSQL => $"\"{ident.Replace("\"", "\"\"")}\"",
                DbConnectionFactory.DbKind.MySql => $"`{ident.Replace("`", "``")}`",
                DbConnectionFactory.DbKind.SQLite => $"\"{ident.Replace("\"", "\"\"")}\"",
                _ => ident
            };
        }

        private static string Qualify(DbConnectionFactory.DbKind kind, string? schema, string table)
        {
            var qt = QuoteIdent(kind, table);
            if (kind == DbConnectionFactory.DbKind.SQLite || string.IsNullOrWhiteSpace(schema))
                return qt;

            var qs = QuoteIdent(kind, schema!);
            return $"{qs}.{qt}";
        }

        private static string BuildCreateSchemaIfNeeded(DbConnectionFactory.DbKind kind, string? schema)
        {
            if (string.IsNullOrWhiteSpace(schema))
                return string.Empty;

            return kind switch
            {
                DbConnectionFactory.DbKind.SqlServer =>
                    $@"IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{schema}')
                        BEGIN
                            EXEC('CREATE SCHEMA {QuoteIdent(kind, schema)}');
                        END",
                DbConnectionFactory.DbKind.PostgreSQL =>
                    $@"CREATE SCHEMA IF NOT EXISTS {QuoteIdent(kind, schema)};",
                // MySQL: schema == database; we do not create databases here.
                _ => string.Empty
            };
        }

        private static string BuildCreateTableDdl(DbConnectionFactory.DbKind kind, string? schema, string table)
        {
            var qname = Qualify(kind, schema, table);

            // Column sets: keep close to your SQL Server definition, but portable.
            // PK "Id" auto-increment, remaining columns as in HttpGossipRecord.
            return kind switch
            {
                DbConnectionFactory.DbKind.SqlServer => $@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.tables t
                    JOIN sys.schemas s ON s.schema_id = t.schema_id
                    WHERE t.name = N'{table}' AND s.name = N'{(schema ?? "dbo")}'
                )
                BEGIN
                    CREATE TABLE {qname}(
                        [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [LogRequestId] NVARCHAR(4000) NULL,
                        [LogRequestStart] DATETIME2(7) NOT NULL,
                        [LogRequestEnd] DATETIME2(7) NULL,
                        [LogElapsedSeconds] FLOAT NULL,
                        [LogIsSuccess] BIT NULL,
                        [LogStatusCode] INT NULL,
                        [LogException] NVARCHAR(MAX) NULL,
                        [LogUserName] NVARCHAR(4000) NULL,
                        [LogLocale] NVARCHAR(4000) NULL,
                        [LogMethod] NVARCHAR(4000) NULL,
                        [LogIsHttps] BIT NULL,
                        [LogProtocol] NVARCHAR(4000) NULL,
                        [LogScheme] NVARCHAR(4000) NULL,
                        [LogPath] NVARCHAR(4000) NULL,
                        [LogQueryString] NVARCHAR(MAX) NULL,
                        [LogRouteValues] NVARCHAR(MAX) NULL,
                        [LogAuthorization] NVARCHAR(MAX) NULL,
                        [LogHeaders] NVARCHAR(MAX) NULL,
                        [LogCookies] NVARCHAR(MAX) NULL,
                        [LogReferer] NVARCHAR(4000) NULL,
                        [LogUserAgent] NVARCHAR(4000) NULL,
                        [LogSecChUa] NVARCHAR(4000) NULL,
                        [LogSecChUaMobile] NVARCHAR(4000) NULL,
                        [LogSecChUaPlatform] NVARCHAR(4000) NULL,
                        [LogAppName] NVARCHAR(100) NULL,
                        [LogHost] NVARCHAR(200) NULL,
                        [LogContentLength] BIGINT NULL,
                        [LogRequestBody] NVARCHAR(MAX) NULL,
                        [LogResponseBody] NVARCHAR(MAX) NULL,
                        [LogResponseContentType] NVARCHAR(4000) NULL,
                        [LogLocalIp] NVARCHAR(4000) NULL,
                        [LogLocalPort] INT NULL,
                        [LogRemoteIp] NVARCHAR(4000) NULL,
                        [LogRemotePort] INT NULL,
                        [LogConnectionId] NVARCHAR(4000) NULL
                    );
                END",
                DbConnectionFactory.DbKind.PostgreSQL => $@"
                    CREATE TABLE IF NOT EXISTS {qname} (
                        Id BIGSERIAL PRIMARY KEY,
                        LogRequestId TEXT NULL,
                        LogRequestStart TIMESTAMP WITHOUT TIME ZONE NOT NULL,
                        LogRequestEnd TIMESTAMP WITHOUT TIME ZONE NULL,
                        LogElapsedSeconds DOUBLE PRECISION NULL,
                        LogIsSuccess BOOLEAN NULL,
                        LogStatusCode INTEGER NULL,
                        LogException TEXT NULL,
                        LogUserName TEXT NULL,
                        LogLocale TEXT NULL,
                        LogMethod TEXT NULL,
                        LogIsHttps BOOLEAN NULL,
                        LogProtocol TEXT NULL,
                        LogScheme TEXT NULL,
                        LogPath TEXT NULL,
                        LogQueryString TEXT NULL,
                        LogRouteValues TEXT NULL,
                        LogAuthorization TEXT NULL,
                        LogHeaders TEXT NULL,
                        LogCookies TEXT NULL,
                        LogReferer TEXT NULL,
                        LogUserAgent TEXT NULL,
                        LogSecChUa TEXT NULL,
                        LogSecChUaMobile TEXT NULL,
                        LogSecChUaPlatform TEXT NULL,
                        LogAppName TEXT NULL,
                        LogHost TEXT NULL,
                        LogContentLength BIGINT NULL,
                        LogRequestBody TEXT NULL,
                        LogResponseBody TEXT NULL,
                        LogResponseContentType TEXT NULL,
                        LogLocalIp TEXT NULL,
                        LogLocalPort INTEGER NULL,
                        LogRemoteIp TEXT NULL,
                        LogRemotePort INTEGER NULL,
                        LogConnectionId TEXT NULL
                    );",
                DbConnectionFactory.DbKind.MySql => $@"
                    CREATE TABLE IF NOT EXISTS {qname} (
                        Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                        LogRequestId VARCHAR(4000) NULL,
                        LogRequestStart DATETIME NOT NULL,
                        LogRequestEnd DATETIME NULL,
                        LogElapsedSeconds DOUBLE NULL,
                        LogIsSuccess TINYINT(1) NULL,
                        LogStatusCode INT NULL,
                        LogException LONGTEXT NULL,
                        LogUserName VARCHAR(4000) NULL,
                        LogLocale VARCHAR(4000) NULL,
                        LogMethod VARCHAR(4000) NULL,
                        LogIsHttps TINYINT(1) NULL,
                        LogProtocol VARCHAR(4000) NULL,
                        LogScheme VARCHAR(4000) NULL,
                        LogPath VARCHAR(4000) NULL,
                        LogQueryString LONGTEXT NULL,
                        LogRouteValues LONGTEXT NULL,
                        LogAuthorization LONGTEXT NULL,
                        LogHeaders LONGTEXT NULL,
                        LogCookies LONGTEXT NULL,
                        LogReferer VARCHAR(4000) NULL,
                        LogUserAgent VARCHAR(4000) NULL,
                        LogSecChUa VARCHAR(4000) NULL,
                        LogSecChUaMobile VARCHAR(4000) NULL,
                        LogSecChUaPlatform VARCHAR(4000) NULL,
                        LogAppName VARCHAR(100) NULL,
                        LogHost VARCHAR(200) NULL,
                        LogContentLength BIGINT NULL,
                        LogRequestBody LONGTEXT NULL,
                        LogResponseBody LONGTEXT NULL,
                        LogResponseContentType VARCHAR(4000) NULL,
                        LogLocalIp VARCHAR(4000) NULL,
                        LogLocalPort INT NULL,
                        LogRemoteIp VARCHAR(4000) NULL,
                        LogRemotePort INT NULL,
                        LogConnectionId VARCHAR(4000) NULL
                    ) ENGINE=InnoDB;",
                DbConnectionFactory.DbKind.SQLite => $@"
                    CREATE TABLE IF NOT EXISTS {qname} (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        LogRequestId TEXT NULL,
                        LogRequestStart TEXT NOT NULL,
                        LogRequestEnd TEXT NULL,
                        LogElapsedSeconds REAL NULL,
                        LogIsSuccess INTEGER NULL,
                        LogStatusCode INTEGER NULL,
                        LogException TEXT NULL,
                        LogUserName TEXT NULL,
                        LogLocale TEXT NULL,
                        LogMethod TEXT NULL,
                        LogIsHttps INTEGER NULL,
                        LogProtocol TEXT NULL,
                        LogScheme TEXT NULL,
                        LogPath TEXT NULL,
                        LogQueryString TEXT NULL,
                        LogRouteValues TEXT NULL,
                        LogAuthorization TEXT NULL,
                        LogHeaders TEXT NULL,
                        LogCookies TEXT NULL,
                        LogReferer TEXT NULL,
                        LogUserAgent TEXT NULL,
                        LogSecChUa TEXT NULL,
                        LogSecChUaMobile TEXT NULL,
                        LogSecChUaPlatform TEXT NULL,
                        LogAppName TEXT NULL,
                        LogHost TEXT NULL,
                        LogContentLength INTEGER NULL,
                        LogRequestBody TEXT NULL,
                        LogResponseBody TEXT NULL,
                        LogResponseContentType TEXT NULL,
                        LogLocalIp TEXT NULL,
                        LogLocalPort INTEGER NULL,
                        LogRemoteIp TEXT NULL,
                        LogRemotePort INTEGER NULL,
                        LogConnectionId TEXT NULL
                    );",
                _ => throw new NotSupportedException("Unsupported provider.")
            };
        }

        private static bool IsAlreadyExistsError(DbConnectionFactory.DbKind kind, Exception ex)
        {
            // Best-effort detection. Most paths won't hit this since we guard with IF NOT EXISTS / IF block.
            var msg = ex.Message.ToLowerInvariant();
            return kind switch
            {
                DbConnectionFactory.DbKind.SqlServer => msg.Contains("already an object named")
                                                      || msg.Contains("there is already an object named"),
                DbConnectionFactory.DbKind.PostgreSQL => msg.Contains("already exists"),
                DbConnectionFactory.DbKind.MySql => msg.Contains("already exists"),
                DbConnectionFactory.DbKind.SQLite => msg.Contains("already exists"),
                _ => false
            };
        }
    }
}