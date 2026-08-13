---
name: crestapps-core-mcp-file-resources
description: Skill for exposing FTP and SFTP files as CrestApps.Core MCP resource types.
---

# CrestApps.Core MCP File Resources - Prompt Templates

## Expose Remote Files as MCP Resources

You are a CrestApps.Core expert. Generate code and guidance for FTP and SFTP MCP resource types.

### Guidelines

- Reference `CrestApps.Core.AI.Mcp.Ftp` for FTP and FTPS, and `CrestApps.Core.AI.Mcp.Sftp` for SFTP.
- Register `AddCoreAIFtpMcpResources()` or `AddCoreAISftpMcpResources()` directly on services after `AddCoreAIMcpServer()`, or use `AddFtpResources()` and `AddSftpResources()` on `CrestAppsMcpServerBuilder`.
- The handlers are `FtpResourceTypeHandler` for resource type `ftp` and `SftpResourceTypeHandler` for resource type `sftp`.
- Both resource types support a `path` URI variable. The variable is optional in the type metadata; an omitted value becomes `/`. Handlers reject null bytes and `.` or `..` path segments, normalize separators, prefix the resulting path with `/`, and return text content with the configured or detected MIME type.
- Store credentials protected with the matching data-protection purpose. Do not put plaintext secrets in resource metadata.

### Register Both Types

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddMcpServer(mcpServer => mcpServer
            .AddYesSqlStores()
            .AddFtpResources()
            .AddSftpResources())));
```

The equivalent direct registration is:

```csharp
builder.Services
    .AddCoreAIMcpServer()
    .AddCoreAIFtpMcpResources()
    .AddCoreAISftpMcpResources();
```

### Attach FTP Metadata

```csharp
var protector = dataProtectionProvider.CreateProtector(
    FtpResourceConstants.DataProtectionPurpose);

resource.Put(new FtpConnectionMetadata
{
    Host = "files.example.com",
    Port = 21,
    Username = "reader",
    Password = protector.Protect(password),
    EncryptionMode = "Explicit",
    DataConnectionType = "AutoPassive",
    ConnectTimeout = 30,
    ReadTimeout = 30,
    RetryAttempts = 2,
});
```

`FtpConnectionMetadata` also supports `ValidateAnyCertificate`. Enable it only for development because it disables TLS certificate validation.

### Attach SFTP Metadata

```csharp
var protector = dataProtectionProvider.CreateProtector(
    SftpResourceConstants.DataProtectionPurpose);

resource.Put(new SftpConnectionMetadata
{
    Host = "sftp.example.com",
    Port = 22,
    Username = "reader",
    PrivateKey = protector.Protect(privateKeyPem),
    Passphrase = protector.Protect(passphrase),
    ConnectionTimeout = 30,
    KeepAliveInterval = 30,
});
```

`SftpConnectionMetadata` contains proxy fields (`ProxyType`, `ProxyHost`, `ProxyPort`, `ProxyUsername`, and `ProxyPassword`), but the current handler does not apply them to its `ConnectionInfo`. The handler validates `Host` and requires at least one protected password or private key. It reads protected `Password`, `PrivateKey`, and `Passphrase`; provide `Username` because the SSH authentication methods use it.

### Connection Metadata

| Resource type | Required metadata | Optional transport settings |
|---|---|---|
| FTP | `Host` | `Port`, `Username`, protected `Password`, `EncryptionMode`, `DataConnectionType`, certificate acceptance, timeouts, retries |
| SFTP | `Host`, plus protected password or private key | `Port`, `Username`, protected passphrase, proxy settings, connection timeout, keep-alive interval |

Use an `McpResource` with the matching type and URI, then store the connection model with `Put(...)`. The metadata is read by the handler at resource-read time, not exposed to the MCP client as tool arguments.
