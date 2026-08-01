---
name: orchardcore-ai-mcp-resources
description: Skill for exposing FTP, FTPS, and SFTP files as CrestApps Orchard Core MCP resources. Covers connection metadata, URI templates, resource registration, secure credential storage, deployment export safety, and file-transfer troubleshooting. Use this skill when requests mention MCP FTP resources, MCP SFTP resources, remote file MCP resources, FtpConnectionMetadata, SftpConnectionMetadata, or file-transfer resource handlers. Strong matches include work with CrestApps.OrchardCore.AI.Mcp.Resources.Ftp, CrestApps.OrchardCore.AI.Mcp.Resources.Sftp, AddCoreAIFtpMcpResources, AddCoreAISftpMcpResources, IMcpResourceHandler, and McpResource.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core MCP FTP and SFTP Resources

## Configure file-transfer MCP resources

You are an Orchard Core expert. Configure remote file content as MCP resources using
the CrestApps FTP/FTPS and SFTP modules. Use the built-in source registrations,
connection metadata, deployment-safe credential handling, and URI patterns.

### Guidelines

- Enable `CrestApps.OrchardCore.AI.Mcp.Resources.Ftp` for FTP and FTPS resources.
- Enable `CrestApps.OrchardCore.AI.Mcp.Resources.Sftp` for SSH File Transfer Protocol resources.
- Both exact feature IDs depend on the MCP server feature `McpPermissions.Feature.Server`; enable the MCP server before configuring resources.
- The FTP module registers the source through `AddCoreAIFtpMcpResources`; the SFTP module uses `AddCoreAISftpMcpResources`.
- Both sources support one URI variable named `path`, which is the remote file path.
- Configure resources through **Artificial Intelligence → MCP Resources** or the `McpResource` recipe step.
- The modules expose remote file content as resources. They do not provide arbitrary command execution on remote hosts.
- Install the selected package or packages in the web or startup project.
- Save credentials only in protected resource metadata, use deployment-safe secrets for imports, and do not commit private keys or passwords.

### Protocol comparison

| Source | Feature ID | URI | Port | Authentication |
|---|---|---|---|---|
| FTP/FTPS | `CrestApps.OrchardCore.AI.Mcp.Resources.Ftp` | `ftp://{itemId}/{path}` | Configured per resource | Username and password |
| SFTP | `CrestApps.OrchardCore.AI.Mcp.Resources.Sftp` | `sftp://{itemId}/{path}` | Configured per resource | Password or private key |

FTP/FTPS uses FluentFTP. SFTP uses SSH.NET. SFTP is SSH-based and is not FTP with a
different port; choose the module matching the server protocol.

### Enable both resource types

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Mcp.Server",
        "CrestApps.OrchardCore.AI.Mcp.Resources.Ftp",
        "CrestApps.OrchardCore.AI.Mcp.Resources.Sftp"
      ],
      "disable": []
    }
  ]
}
```

`CrestApps.OrchardCore.AI.Mcp.Server` is the server dependency declared by both
resource feature manifests. The two resource feature IDs above are also their exact
manifest IDs.

### FTP and FTPS resources

The FTP source is displayed as **FTP/FTPS** and supports `ftp://` resource URIs.
`FtpConnectionMetadata` contains:

| Property | Purpose |
|---|---|
| `Host` | FTP server hostname or IP address. |
| `Port` | Optional port, normally 21 or 990 for implicit FTPS. |
| `Username` | FTP username. |
| `Password` | Protected password. |
| `EncryptionMode` | `None`, `Implicit`, `Explicit`, or `Auto`. |
| `DataConnectionType` | Passive or active data-mode selection such as `AutoPassive`, `PASV`, `EPSV`, or `AutoActive`. |
| `ValidateAnyCertificate` | Accepts any TLS certificate when true. |
| `ConnectTimeout` | Optional connect timeout in seconds. |
| `ReadTimeout` | Optional read timeout in seconds. |
| `RetryAttempts` | Optional retry count. |

Prefer explicit or implicit FTPS over plaintext FTP. Set `ValidateAnyCertificate` to
false in production unless there is a deliberately managed certificate-validation
exception.

### SFTP resources

The SFTP source is displayed as **SFTP** and supports `sftp://` resource URIs.
`SftpConnectionMetadata` contains:

| Property | Purpose |
|---|---|
| `Host` | SFTP server hostname or IP address. |
| `Port` | Optional SSH port, normally 22. |
| `Username` | SSH username. |
| `Password` | Protected password authentication value. |
| `PrivateKey` | Protected private-key content. |
| `Passphrase` | Protected private-key passphrase. |
| `ProxyType` | `None`, `Socks4`, `Socks5`, or `Http`. |
| `ProxyHost` and `ProxyPort` | Optional proxy endpoint. |
| `ProxyUsername` and `ProxyPassword` | Optional protected proxy credentials. |
| `ConnectionTimeout` | Optional timeout in seconds. |
| `KeepAliveInterval` | Optional SSH keep-alive interval in seconds. |

Prefer a restricted, passphrase-protected private key over password authentication
when the remote server supports it. Grant the remote identity read access only to
approved resource paths.

### Create an FTP resource recipe

```json
{
  "steps": [
    {
      "name": "McpResource",
      "Resources": [
        {
          "Source": "ftp",
          "DisplayText": "Remote configuration",
          "Resource": {
            "Uri": "ftp://resource-id/config/settings.json",
            "Name": "remote-configuration",
            "Description": "Approved remote configuration file",
            "MimeType": "application/json"
          },
          "Properties": {
            "FtpConnectionMetadata": {
              "Host": "ftp.example.com",
              "Port": 21,
              "Username": "resource-reader",
              "Password": "",
              "EncryptionMode": "Explicit",
              "DataConnectionType": "AutoPassive",
              "ValidateAnyCertificate": false
            }
          }
        }
      ]
    }
  ]
}
```

Set the password after import through a protected operational configuration path.
The resource URI item ID identifies the Orchard Core MCP resource; the `path` portion
identifies the remote file.

### Create an SFTP resource recipe

```json
{
  "steps": [
    {
      "name": "McpResource",
      "Resources": [
        {
          "Source": "sftp",
          "DisplayText": "Remote application log",
          "Resource": {
            "Uri": "sftp://resource-id/var/log/app.log",
            "Name": "remote-application-log",
            "Description": "Approved application log file",
            "MimeType": "text/plain"
          },
          "Properties": {
            "SftpConnectionMetadata": {
              "Host": "sftp.example.com",
              "Port": 22,
              "Username": "resource-reader",
              "Password": "",
              "PrivateKey": "",
              "Passphrase": ""
            }
          }
        }
      ]
    }
  ]
}
```

Populate one supported authentication method securely after deployment. Do not include
private key material, passphrases, or passwords in recipes.

### Registration and export behavior

Each module adds an editor display driver for `McpResource` and an
`IMcpResourceHandler`:

| Handler | Export behavior |
|---|---|
| `FtpMcpResourceHandler` | Clears `FtpConnectionMetadata.Password`. |
| `SftpMcpResourceHandler` | Clears password, private key, passphrase, and proxy password. |

The platform protects credentials at rest through Data Protection. Export clearing is
defense in depth, not a substitute for secret-management procedures. Imported
resources need credentials re-entered or injected through an approved secure channel.

### Resource flow

1. An administrator creates an MCP resource with source `ftp` or `sftp`.
2. The selected module provides the source definition and connection editor.
3. An MCP client resolves a resource URI and supplies its `path` variable.
4. FluentFTP or SSH.NET authenticates to the configured server and reads file content.
5. The resource response carries detected MIME type based on file extension.

Keep resource names, descriptions, and allowed remote paths narrowly focused. An MCP
resource may expose content to AI clients, so it must not point at credentials,
unfiltered logs, or tenant data outside the intended authorization boundary.

### Troubleshooting

| Symptom | Check |
|---|---|
| FTP or SFTP is not an available source | Confirm the exact feature ID, MCP server dependency, deployed package, and tenant feature state. |
| Resource cannot connect | Check host, port, DNS, firewall, outbound network access, and protocol selection. |
| FTPS certificate error | Use a trusted certificate and correct encryption mode. Do not broadly enable `ValidateAnyCertificate` in production. |
| SFTP key authentication fails | Verify key format, key authorization on the server, passphrase, username, and permissions on the server-side `.ssh` files. |
| Recipe import has blank credentials | Expected behavior. Export handlers deliberately clear sensitive values. Set them securely after import. |
| Resource reads the wrong file | Verify the `ftp://` or `sftp://` URI item ID and normalized remote `path` variable. |
| Connection drops on long reads | Tune timeouts, retry attempts for FTP, or SFTP keep-alive settings after verifying the server policy. |

### Security checklist

- Prefer SFTP or FTPS over plaintext FTP.
- Use least-privilege remote accounts restricted to explicit read-only directories.
- Use host key and certificate validation in production.
- Keep passwords, private keys, passphrases, and proxy credentials out of recipes, logs, and source control.
- Limit MCP resource access through Orchard Core permissions and validate the data classification of every exposed path.
