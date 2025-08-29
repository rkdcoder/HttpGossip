using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using System.Data.Common;

namespace HttpGossip.Internal
{
    internal static class DbConnectionFactory
    {
        internal enum DbKind
        {
            SqlServer,
            PostgreSQL,
            MySql,
            SQLite
        }

        internal static DbKind ParseKind(string databaseName)
        {
            var key = databaseName?.Trim().ToLowerInvariant() ?? "";
            return key switch
            {
                "sqlserver" or "mssql" => DbKind.SqlServer,
                "postgresql" or "postgres" or "pgsql" => DbKind.PostgreSQL,
                "mysql" => DbKind.MySql,
                "sqlite" => DbKind.SQLite,
                _ => throw new ArgumentOutOfRangeException(nameof(databaseName),
                    $"Unsupported DatabaseName '{databaseName}'. Use 'SqlServer', 'PostgreSQL', 'MySql' or 'SQLite'.")
            };
        }

        internal static DbConnection Create(string databaseName, string connectionString)
        {
            return ParseKind(databaseName) switch
            {
                DbKind.SqlServer => new SqlConnection(connectionString),
                DbKind.PostgreSQL => new NpgsqlConnection(connectionString),
                DbKind.MySql => new MySqlConnection(connectionString),
                DbKind.SQLite => new SqliteConnection(connectionString),
                _ => throw new ArgumentOutOfRangeException(nameof(databaseName))
            };
        }
    }
}
