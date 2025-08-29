namespace HttpGossip
{
    public sealed class HttpGossipOptions
    {
        // Required
        public string DatabaseName { get; set; } = default!;        // "SqlServer" | "PostgreSQL" | "MySql" | "SQLite"
        public string ConnectionString { get; set; } = default!;
        public string TableQualifiedName { get; set; } = default!;

        // Optional
        public string[]? SensitivePaths { get; set; }   // redact request/response bodies when path matches
        public string[]? BypassPaths { get; set; }      // skip logging when path matches

        // Only these two have safe defaults (and are configurable)
        public int QueueCapacity { get; set; } = 10_000;
        public int MaxBodyBytes { get; set; } = 64 * 1024; // 64KB

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(DatabaseName))
                throw new ArgumentException("DatabaseName is required.", nameof(DatabaseName));
            if (string.IsNullOrWhiteSpace(ConnectionString))
                throw new ArgumentException("ConnectionString is required.", nameof(ConnectionString));
            if (string.IsNullOrWhiteSpace(TableQualifiedName))
                throw new ArgumentException("TableQualifiedName is required.", nameof(TableQualifiedName));
            if (QueueCapacity <= 0) QueueCapacity = 10_000;
            if (MaxBodyBytes <= 0) MaxBodyBytes = 64 * 1024;
        }
    }
}