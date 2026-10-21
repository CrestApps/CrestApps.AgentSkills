---
name: orchardcore-ai-mcp-client
description: Skill for connecting Orchard Core to external MCP servers with the CrestApps MCP client features CrestApps.OrchardCore.AI.Mcp (remote SSE and HTTP transport) and CrestApps.OrchardCore.AI.Mcp.Stdio (local process transport). Covers the MCP Hosts admin screen, SseMcpConnectionMetadata authentication types (Anonymous, ApiKey, Basic, OAuth2ClientCredentials, OAuth2PrivateKeyJwt, OAuth2Mtls, CustomHeaders), StdioMcpConnectionMetadata for Docker-based servers, the McpConnection recipe step with its required Source value, deployment export with secrets cleared, encrypted credentials, the ManageMcpConnections permission, and assigning MCP connections to AI profiles, profile templates, chat interactions and the AI Completion using Direct Config workflow task. Use this skill when requests mention Orchard Core MCP client, MCP connections, MCP Hosts, connecting to a remote MCP server, running an MCP server in Docker, or troubleshooting MCP connection imports.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core MCP Client

The MCP client features let AI profiles, chat interactions, and workflow tasks call tools, prompts, and resources hosted by external MCP servers.

## Guidelines

- `CrestApps.OrchardCore.AI.Mcp` adds the client runtime and the remote **Server-Sent Events** transport (source `sse`). It depends on `CrestApps.OrchardCore.AI` and `CrestApps.OrchardCore.Resources`.
- `CrestApps.OrchardCore.AI.Mcp.Stdio` adds the local process transport (source `stdIo`, matched case-insensitively). Enable it only on hosts that may start local processes.
- Install `CrestApps.OrchardCore.AI.Mcp` in the web/startup project.
- Connections are managed under **Artificial Intelligence → Model Context Protocol → MCP Hosts** (permission `ManageMcpConnections`).
- Credentials are encrypted at rest with ASP.NET Core Data Protection and cleared from deployment exports. Never commit plaintext secrets in recipes.

## Enable the features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Mcp",
        "CrestApps.OrchardCore.AI.Mcp.Stdio"
      ],
      "disable": []
    }
  ]
}
```

Drop `CrestApps.OrchardCore.AI.Mcp.Stdio` when you only connect to remote servers.

## Add a remote connection

1. Open **Artificial Intelligence → Model Context Protocol → MCP Hosts**.
2. Click **Add Connection** and choose **Server-Sent Events**.
3. Enter a **Title**, the server **Endpoint** (for example `https://mcp.example.com/`), and an **Authentication** method.
4. Save.

### Authentication types (`SseMcpConnectionMetadata.AuthenticationType`)

| Value | Fields |
|---|---|
| `Anonymous` | None |
| `ApiKey` | `ApiKey`, `ApiKeyHeaderName` (default `Authorization`), optional `ApiKeyPrefix` such as `Bearer` |
| `Basic` | `BasicUsername`, `BasicPassword` |
| `OAuth2ClientCredentials` | `OAuth2TokenEndpoint`, `OAuth2ClientId`, `OAuth2ClientSecret`, `OAuth2Scopes` |
| `OAuth2PrivateKeyJwt` | `OAuth2TokenEndpoint`, `OAuth2ClientId`, `OAuth2PrivateKey` (PEM), optional `OAuth2KeyId`, `OAuth2Scopes` |
| `OAuth2Mtls` | `OAuth2TokenEndpoint`, `OAuth2ClientId`, `OAuth2ClientCertificate` (Base64 PFX), optional `OAuth2ClientCertificatePassword`, `OAuth2Scopes` |
| `CustomHeaders` | `AdditionalHeaders` as a JSON object |

OAuth tokens are acquired with the client credentials grant and cached. The editor shares **Token Endpoint**, **Client ID**, and **Scopes** across the OAuth variants.

## Add a local (stdio) connection

1. Install Docker and pull an MCP image, for example `mcp/time`.
2. Open **MCP Hosts**, click **Add Connection**, and choose **Standard Input/Output**.
3. Enter a **Title**, the **Command** `docker`, and **Command arguments** `["run", "-i", "--rm", "mcp/time"]`.
4. Save.

`StdioMcpConnectionMetadata` also supports `WorkingDirectory` and `EnvironmentVariables`.

## Recipe step

The `McpConnection` step creates or updates connections. `Source` is required for a new connection (`sse` or `stdio`); without it the import reports "Could not find provider name". An entry whose `ItemId` matches an existing connection is updated.

```json
{
  "steps": [
    {
      "name": "McpConnection",
      "Connections": [
        {
          "Source": "sse",
          "DisplayText": "Remote tools server",
          "Properties": {
            "SseMcpConnectionMetadata": {
              "Endpoint": "https://mcp.example.com/",
              "AuthenticationType": "ApiKey",
              "ApiKeyHeaderName": "Authorization",
              "ApiKeyPrefix": "Bearer",
              "ApiKey": "{{McpToolsApiKey}}"
            }
          }
        },
        {
          "Source": "stdio",
          "DisplayText": "Global time capabilities",
          "Properties": {
            "StdioMcpConnectionMetadata": {
              "Command": "docker",
              "Arguments": [ "run", "-i", "--rm", "mcp/time" ]
            }
          }
        }
      ]
    }
  ]
}
```

Sensitive values supplied in a recipe (`ApiKey`, `BasicPassword`, `OAuth2ClientSecret`, `OAuth2PrivateKey`, `OAuth2ClientCertificate`, `OAuth2ClientCertificatePassword`) are encrypted on import. Do not paste already-encrypted values.

With `OrchardCore.Deployment` enabled, an **MCP Connection** deployment step exports all or selected connections, with those secret fields blanked. Re-enter them on the target tenant.

## Assign connections

After a connection exists, select it where the model should use it:

| Surface | Where |
|---|---|
| AI Profile | **Capabilities** tab, **MCP Connections** |
| AI Profile Template (Profile source) | **Capabilities** tab |
| Chat Interaction | **Capabilities** tab, **Connections** |
| AI Completion using Direct Config workflow task | **Capabilities** tab (needs `CrestApps.OrchardCore.AI.Chat.Interactions` and `OrchardCore.Workflows`) |

The selector renders only when at least one connection exists.

## Admin list

**MCP Hosts** uses Orchard Core's `AdminList` shape (list name `McpConnections`), so it follows the site's List or Grid layout. See `orchardcore-admin-lists` for columns and layout overrides.

## Upgrading from 2.x

- Older samples of the `McpConnection` step omitted `Source`, which the step has always required for new connections; add it.
- Theme overrides of the old connection list rows only apply to the List layout; see `orchardcore-admin-lists`.

## Troubleshooting

| Symptom | Check |
|---|---|
| Recipe reports "Could not find provider name" | Add `"Source": "sse"` or `"Source": "stdio"`. |
| "Invalid source used for MCP connection" | The stdio feature is not enabled, or the source name is misspelled. |
| Stdio connection never starts | The command is not on the server PATH, or the host blocks process creation. |
| Remote tools never appear in chat | The connection is not selected on the profile or interaction, or the remote server rejects the credentials. |
| Imported connection fails to authenticate | Secrets are blanked in exports; re-enter them. |

See `orchardcore-ai-mcp-server` to expose Orchard Core itself as an MCP server.
