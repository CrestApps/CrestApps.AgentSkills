---
name: crestapps-core-mcp-file-resources
description: Skill for exposing FTP, FTPS and SFTP files as CrestApps.Core MCP resource types with the consolidated CrestApps.Core.AI.Ftp and CrestApps.Core.AI.Sftp packages. Covers AddFtpResources, AddSftpResources, AddCoreAIFtpMcpResources and AddCoreAISftpMcpResources, FtpResourceTypeHandler and SftpResourceTypeHandler, the path URI variable and its sanitization, FtpConnectionMetadata and SftpConnectionMetadata with Data Protection purposes, SFTP host key fingerprints with SftpHostKeys, FtpOptions AllowAnyCertificate and AllowUnencrypted, MaxResourceBytes and RemoteFileResourceHandlerBase, the matching file source ingestion connectors in the same packages, and upgrading from CrestApps.Core.AI.Mcp.Ftp and CrestApps.Core.AI.Mcp.Sftp.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core MCP File Resources

Expose files on an FTP, FTPS or SFTP server to MCP clients as resources. In 2.0 each protocol lives in one package that also carries the file source ingestion connector for the same protocol (see `crestapps-core-file-sources`). Registering one side does not register the other.

| Package | MCP resource registration | Ingestion connector registration |
|---|---|---|
| `CrestApps.Core.AI.Ftp` | `AddFtpResources()` / `AddCoreAIFtpMcpResources()` | `AddCoreFtpIngestionConnector()` |
| `CrestApps.Core.AI.Sftp` | `AddSftpResources()` / `AddCoreAISftpMcpResources()` | `AddCoreSftpIngestionConnector()` |

## Register Both Resource Types

```csharp
using CrestApps.Core.AI.Ftp;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Sftp;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddMcpServer(mcpServer => mcpServer
            .AddYesSqlStores()
            .AddFtpResources()
            .AddSftpResources())));
```

Without the builder:

```csharp
builder.Services
    .AddCoreAIMcpServer()
    .AddCoreAIFtpMcpResources()
    .AddCoreAISftpMcpResources();
```

Both registration methods take an optional `Action<McpResourceTypeEntry>` to change the display name, description or variables. The MCP server itself (SDK transport, `WithCrestAppsHandlers()`, `MapMcp`) is covered in `crestapps-core-mcp`.

## Handlers

- `FtpResourceTypeHandler` serves type `ftp` (`FtpResourceConstants.Type`), using FluentFTP.
- `SftpResourceTypeHandler` serves type `sftp` (`SftpResourceConstants.Type`), using SSH.NET.
- Both expose one optional `path` variable, for example a resource URI template `ftp://reports/{path}`. An omitted value becomes `/`.
- `SanitizePath` rejects null bytes and `.` or `..` segments, normalizes `\` to `/` and collapses empty segments, and the handler prefixes the result with `/`.
- Content is returned as `TextResourceContents` with the resource's MIME type, or one detected from the file extension, falling back to `application/octet-stream`.
- Both derive from `RemoteFileResourceHandlerBase<TMetadata>`, which stops reading at the metadata's `MaxResourceBytes` (5 MB when not set) and answers with an error instead of buffering a larger file. Derive a custom remote-file handler from it to get the same limit.

## Attach FTP Metadata

```csharp
using CrestApps.Core;
using CrestApps.Core.AI.Ftp;
using CrestApps.Core.AI.Ftp.Models;

var protector = dataProtectionProvider.CreateProtector(FtpResourceConstants.DataProtectionPurpose);

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

A connection with a `Username` must use `EncryptionMode` `Explicit` or `Implicit`; unset, `None` and `Auto` can run in plain text and are refused unless the host sets `FtpOptions.AllowUnencrypted`. `ValidateAnyCertificate` is honored only when the host sets `FtpOptions.AllowAnyCertificate`; otherwise the connection is refused with `FtpOptions.AnyCertificateNotAllowedMessage`. Anonymous connections are not affected. Both checks are `FtpOptions.GetPolicyViolation(metadata)`, used by the resource handler and the ingestion connector:

```csharp
using CrestApps.Core.AI.Ftp;

// Development servers only.
builder.Services.Configure<FtpOptions>(options =>
{
    options.AllowAnyCertificate = true;
    options.AllowUnencrypted = true;
});
```

## Attach SFTP Metadata

```csharp
using CrestApps.Core;
using CrestApps.Core.AI.Sftp;
using CrestApps.Core.AI.Sftp.Models;

var protector = dataProtectionProvider.CreateProtector(SftpResourceConstants.DataProtectionPurpose);

resource.Put(new SftpConnectionMetadata
{
    Host = "sftp.example.com",
    Port = 22,
    Username = "reader",
    PrivateKey = protector.Protect(privateKeyPem),
    Passphrase = protector.Protect(passphrase),
    HostKeyFingerprint = "SHA256:47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU", // as ssh-keygen -lf prints it
    ConnectionTimeout = 30,
    KeepAliveInterval = 30,
});
```

The handler requires `Host`, at least one protected `Password` or `PrivateKey`, and `HostKeyFingerprint` (`SHA256:...` or the legacy MD5 hex form; separate several with commas to rotate a key). `SftpHostKeys` refuses the connection when no fingerprint is stored or the server presents a different key, so credentials never reach an unverified server (the factory throws `InvalidOperationException`, the handler returns an error result). It reads `Username` for the SSH authentication methods. `SftpConnectionMetadata` declares `ProxyType`, `ProxyHost`, `ProxyPort`, `ProxyUsername` and `ProxyPassword`, but the resource handler does not apply them to its `ConnectionInfo`.

| Constant | Value |
|---|---|
| `FtpResourceConstants.DataProtectionPurpose` | `FtpResourcePassword` |
| `SftpResourceConstants.DataProtectionPurpose` | `SftpResourceCredentials` |

The ingestion connectors decrypt with the same purposes, so a file source and an MCP resource can share the same stored connection shape. A mismatched purpose reads back as no credential rather than an error.

## Resource Records

Create an `McpResource` whose `Source` is `ftp` or `sftp` and whose `Resource.Uri` is a fixed URI or a template containing `{path}`, then `Put(...)` the connection metadata on it. The metadata is read at resource-read time and is never shown to the MCP client. Use an `IMcpResourceHandler.Exporting` handler to remove credentials when resources are exported.

## Upgrading Within 2.0 Previews

- Store `HostKeyFingerprint` on every SFTP resource and file source, or those connections stop working.
- Set `EncryptionMode` to `Explicit` or `Implicit` on FTP connections with credentials, or allow plain FTP with `FtpOptions.AllowUnencrypted`; `ValidateAnyCertificate` needs `FtpOptions.AllowAnyCertificate`.
- `FtpRemoteFileClientFactory` and `FtpResourceTypeHandler` take an `IOptions<FtpOptions>`; only code that constructs them changes.
- Reads above `MaxResourceBytes` (resources, 5 MB) or `MaxFileBytes` (ingestion, 100 MB) are refused instead of buffered.

## Upgrading from 1.x

- Replace package references `CrestApps.Core.AI.Mcp.Ftp` and `CrestApps.Core.AI.Mcp.Sftp` with `CrestApps.Core.AI.Ftp` and `CrestApps.Core.AI.Sftp`.
- Rename namespaces `CrestApps.Core.AI.Mcp.Ftp[.Models|.Handlers]` to `CrestApps.Core.AI.Ftp[.Models|.Handlers]`, and the same for SFTP.
- Registration method names and behavior are unchanged.
- Stored data needs no migration; metadata is stored under the short type names `FtpConnectionMetadata` and `SftpConnectionMetadata`.
