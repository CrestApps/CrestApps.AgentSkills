---
name: crestapps-core-mcp-file-resources
description: Skill for exposing FTP, FTPS and SFTP files as CrestApps.Core MCP resource types with the consolidated CrestApps.Core.AI.Ftp and CrestApps.Core.AI.Sftp packages. Covers AddFtpResources, AddSftpResources, AddCoreAIFtpMcpResources and AddCoreAISftpMcpResources, FtpResourceTypeHandler and SftpResourceTypeHandler, the path URI variable and its sanitization, FtpConnectionMetadata and SftpConnectionMetadata with Data Protection purposes, the matching file source ingestion connectors in the same packages, and upgrading from CrestApps.Core.AI.Mcp.Ftp and CrestApps.Core.AI.Mcp.Sftp.
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

`ValidateAnyCertificate` disables TLS certificate validation; use it only against a server you control.

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
    ConnectionTimeout = 30,
    KeepAliveInterval = 30,
});
```

The handler requires `Host` and at least one protected `Password` or `PrivateKey`, and reads `Username` for the SSH authentication methods. `SftpConnectionMetadata` declares `ProxyType`, `ProxyHost`, `ProxyPort`, `ProxyUsername` and `ProxyPassword`, but the resource handler does not apply them to its `ConnectionInfo`.

| Constant | Value |
|---|---|
| `FtpResourceConstants.DataProtectionPurpose` | `FtpResourcePassword` |
| `SftpResourceConstants.DataProtectionPurpose` | `SftpResourceCredentials` |

The ingestion connectors decrypt with the same purposes, so a file source and an MCP resource can share the same stored connection shape. A mismatched purpose reads back as no credential rather than an error.

## Resource Records

Create an `McpResource` whose `Source` is `ftp` or `sftp` and whose `Resource.Uri` is a fixed URI or a template containing `{path}`, then `Put(...)` the connection metadata on it. The metadata is read at resource-read time and is never shown to the MCP client. Use an `IMcpResourceHandler.Exporting` handler to remove credentials when resources are exported.

## Upgrading from 1.x

- Replace package references `CrestApps.Core.AI.Mcp.Ftp` and `CrestApps.Core.AI.Mcp.Sftp` with `CrestApps.Core.AI.Ftp` and `CrestApps.Core.AI.Sftp`.
- Rename namespaces `CrestApps.Core.AI.Mcp.Ftp[.Models|.Handlers]` to `CrestApps.Core.AI.Ftp[.Models|.Handlers]`, and the same for SFTP.
- Registration method names and behavior are unchanged.
- Stored data needs no migration; metadata is stored under the short type names `FtpConnectionMetadata` and `SftpConnectionMetadata`.
