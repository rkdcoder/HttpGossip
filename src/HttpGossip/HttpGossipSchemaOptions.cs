namespace HttpGossip
{
    /// <summary>
    /// Options for schema creation (separate elevated connection).
    /// </summary>
    public sealed class HttpGossipSchemaOptions
    {
        public string DatabaseName { get; set; } = default!;       // "SqlServer" | "PostgreSQL" | "MySql" | "SQLite"
        public string ConnectionString { get; set; } = default!;
        public string TableQualifiedName { get; set; } = default!; // e.g. "logs.WSLOG_IdentityAndAccess" or "WSLOG_IdentityAndAccess"

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(DatabaseName))
                throw new ArgumentException("DatabaseName is required.", nameof(DatabaseName));
            if (string.IsNullOrWhiteSpace(ConnectionString))
                throw new ArgumentException("ConnectionString is required.", nameof(ConnectionString));
            if (string.IsNullOrWhiteSpace(TableQualifiedName))
                throw new ArgumentException("TableQualifiedName is required.", nameof(TableQualifiedName));
        }
    }
}