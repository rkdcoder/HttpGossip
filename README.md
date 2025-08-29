# HttpGossip – Fire-and-Forget HTTP Logging for ASP.NET Core (.NET 8+)

<p align="center">
  <img src="https://raw.githubusercontent.com/rkdcoder/HttpGossip/main/src/HttpGossip/icon.png" width="128" alt="HttpGossip logo" />
</p>

[![NuGet](https://img.shields.io/nuget/v/HttpGossip.svg)](https://www.nuget.org/packages/HttpGossip)
[![Build & Publish](https://github.com/rkdcoder/HttpGossip/actions/workflows/main.yml/badge.svg)](https://github.com/rkdcoder/HttpGossip/actions/workflows/main.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)


**HttpGossip** is a lightweight, production‑ready HTTP logging middleware for ASP.NET Core. It captures request/response metadata and bodies, pushes logs into an in‑memory **bounded queue**, and persists them asynchronously via **Dapper** to **SQL Server**, **PostgreSQL**, **MySQL**, or **SQLite**.

* **Fire‑and‑forget**: logging never blocks requests.
* **Safe by design**: if persistence fails, your API is **never penalized** (warnings only).
* **Single‑project library**: easy to drop in, easy to ship.
* **Optional schema helper**: create the table with an elevated connection when needed.

---

## Quickstart

### 1) Register and use the middleware

```csharp
using HttpGossip;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpGossip(options =>
{
    options.DatabaseName       = "SqlServer"; // "SqlServer" | "PostgreSQL" | "MySql" | "SQLite"
    options.ConnectionString   = builder.Configuration.GetConnectionString("Default")!;
    options.TableQualifiedName = "logs.WSLOG_IdentityAndAccess"; // schema.table (or [schema].[table] for SQL Server)

    // Optional (no defaults applied if omitted)
    // options.SensitivePaths = new[] { "/login", "/password", "/token" };
    // options.BypassPaths    = new[] { "/swagger", ".js", ".css", "/health" };

    // Only these have safe defaults (configurable)
    options.QueueCapacity = 10_000;       // default 10_000
    options.MaxBodyBytes  = 64 * 1024;    // default 64 KB
});

var app = builder.Build();

app.UseHttpGossip(); // adds the middleware

app.MapControllers();
app.Run();
```

> **Always-on body capture**: Request and response bodies are captured by default; no option is exposed to disable them here. Bodies are truncated to `MaxBodyBytes` and marked with `[TRUNCATED]` when the limit is reached.

### 2) Bind from configuration (optional)

If you prefer configuration binding, you can bind a section into the same `HttpGossipOptions` shape and pass it to `AddHttpGossip`.

```json
{
  "ConnectionStrings": {
    "Default": "Server=.;Database=YourDb;User Id=usr;Password=pwd;TrustServerCertificate=True;"
  },
  "HttpGossip": {
    "DatabaseName": "SqlServer",
    "ConnectionString": "Server=.;Database=YourDb;User Id=usr;Password=pwd;TrustServerCertificate=True;",
    "TableQualifiedName": "logs.WSLOG_IdentityAndAccess",
    "QueueCapacity": 10000,
    "MaxBodyBytes": 65536,
    "SensitivePaths": ["/login", "/password", "/token"],
    "BypassPaths": ["/swagger", ".js", ".css", "/health"]
  }
}
```

```csharp
var section = builder.Configuration.GetSection("HttpGossip");
builder.Services.AddHttpGossip(section.Bind);
```

---

## Optional: create the table with an elevated connection

Use the schema helper **outside** the request pipeline (e.g., at startup, a one‑off admin endpoint, or a migration job). This allows you to use a different connection string with higher privileges.

```csharp
using HttpGossip;

await HttpGossipSchema.EnsureLogTableAsync(new HttpGossipSchemaOptions
{
    DatabaseName       = "SqlServer", // or PostgreSQL/MySql/SQLite
    ConnectionString   = builder.Configuration.GetConnectionString("AdminDdl")!,
    TableQualifiedName = "logs.WSLOG_IdentityAndAccess"
});
```

* Idempotent: creates schema/table **if missing**.
* Provider‑aware DDL for SQL Server, PostgreSQL, MySQL, and SQLite.

---

## What gets logged

* **Timing**: `LogRequestStart` / `LogRequestEnd` (local time), `LogElapsedSeconds`
* **Outcome**: `LogIsSuccess`, `LogStatusCode`, `LogException`
* **Request**: `LogMethod`, `LogPath`, `LogQueryString`, `LogRouteValues`, `LogContentLength`
* **Redaction**: `LogAuthorization` keeps **only the scheme** (e.g., `Bearer`)
* **Headers & Client**: `LogHeaders`, `LogCookies`, `LogReferer`, `LogUserAgent`, `LogSecChUa*`
* **Host & Network**: `LogHost`, `LogLocalIp`/`Port`, `LogRemoteIp`/`Port`, `LogConnectionId`
* **Bodies**: `LogRequestBody`, `LogResponseBody`, `LogResponseContentType` (bodies may be `[REDACTED]` for sensitive paths; they are truncated to `MaxBodyBytes`)

> **Sensitive paths** are redacted only if you configure `SensitivePaths`. If you omit it, **no redaction by path** is applied.

---

## Options

**Required**

* `DatabaseName` (string): `"SqlServer" | "PostgreSQL" | "MySql" | "SQLite"`
* `ConnectionString` (string)
* `TableQualifiedName` (string): e.g., `"logs.WSLOG_IdentityAndAccess"`

**Optional** (no defaults applied if omitted)

* `SensitivePaths` (`string[]?`): redact request/response bodies when the path contains any entry.
* `BypassPaths` (`string[]?`): skip logging for matching paths (e.g., swagger, health, static files).

**Defaults (safe & configurable)**

* `QueueCapacity` (int, default **10,000**): bounded in‑memory queue; when full, new writes are **dropped** to avoid backpressure.
* `MaxBodyBytes` (int, default **65,536**): max bytes captured for request/response bodies; appends `[TRUNCATED]` at the limit.

> Bodies are **always captured** in HttpGossip. Use `SensitivePaths` to redact and `BypassPaths` to skip endpoints entirely.

---

## Supported databases

* **SQL Server** (Microsoft.Data.SqlClient + Dapper)
* **PostgreSQL** (Npgsql + Dapper)
* **MySQL** (MySqlConnector + Dapper)
* **SQLite** (Microsoft.Data.Sqlite + Dapper)

---

## Example DDL (SQL Server)

> You can also rely on `HttpGossipSchema.EnsureLogTableAsync(...)` instead of hand‑writing DDL.

```sql
CREATE TABLE [logs].[WSLOG_IdentityAndAccess](
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
```

---

## Behavior & guarantees

* **Non‑blocking**: requests do not wait for database I/O.
* **Bounded queue**: protects your API from backpressure; full queue **drops** new items.
* **Background persistence**: a `BackgroundService` reads from the queue and inserts via Dapper.
* **Failure‑tolerant**: DB errors are logged as warnings; the request flow is unaffected.
* **Local timestamps**: `DateTime.Now` is used for start/end to match local expectations.

---

## Install

```bash
# once published to NuGet
dotnet add package HttpGossip
```

---

## License

**MIT**

---

## Links

* Repository: [https://github.com/rkdcoder/httpgossip](https://github.com/rkdcoder/httpgossip)
* NuGet: [https://www.nuget.org/packages/HttpGossip](https://www.nuget.org/packages/HttpGossip)
