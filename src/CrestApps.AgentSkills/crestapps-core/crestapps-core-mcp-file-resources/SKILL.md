---
name: crestapps-core-mcp-file-resources
description: Skill for exposing FTP and SFTP files as CrestApps.Core MCP resource types.
---

# CrestApps.Core MCP File Resources - Prompt Templates

## Expose Remote Files as MCP Resources

You are a CrestApps.Core expert. Generate code and guidance for FTP and SFTP MCP resource types.

### Guidelines

- Reference `CrestApps.Core.AI.Mcp.Ftp` for FTP and FTPS, and `CrestApps.Core.AI.Mcp.Sftp` for SFTP.
- Register `AddCoreAIFtpMcpResources()` or `AddCoreAISftpMcpResources()` directly on services, or use `AddFtpResources()` and `AddSftpResources()` on `CrestAppsMcpServerBuilder`.
- The handlers are `FtpResourceTypeHandler` for resource type `ftp` and `SftpResourceTypeHandler` for resource type `sftp`.
- Both resource types declare a required `path` variable. Handlers sanitize it, prefix it with `/`, download the file, and return text content with the configured or detected MIME type.
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

`SftpConnectionMetadata` contains proxy fields (`ProxyType`, `ProxyHost`, `ProxyPort`, `ProxyUsername`, and `ProxyPassword`), but the current handler does not apply them to its `ConnectionInfo`. It requires at least one password or private key and reads protected `Password`, `PrivateKey`, and `Passphrase`.

### Connection Metadata

| Resource type | Required metadata | Optional transport settings |
|---|---|---|
| FTP | `Host` | `Port`, `Username`, `Password`, `EncryptionMode`, `DataConnectionType`, certificate acceptance, timeouts, retries |
| SFTP | `Host`, `Username`, and password or private key | `Port`, protected passphrase, proxy settings, connection timeout, keep-alive interval |

Use an `McpResource` with the matching type and URI, then store the connection model with `Put(...)`. The metadata is read by the handler at resource-read time, not exposed to the MCP client as tool arguments.
