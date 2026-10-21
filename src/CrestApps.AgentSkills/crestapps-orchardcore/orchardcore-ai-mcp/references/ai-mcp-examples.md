# Orchard Core MCP Practical Examples

## Recipe: MCP client with a remote SSE connection

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.AI.Mcp"
      ]
    },
    {
      "name": "McpConnection",
      "Connections": [
        {
          "Source": "sse",
          "DisplayText": "Remote tools server",
          "Properties": {
            "SseMcpConnectionMetadata": {
              "Endpoint": "https://mcp-tools.example.com/",
              "AuthenticationType": "OAuth2ClientCredentials",
              "OAuth2TokenEndpoint": "https://auth.example.com/oauth2/token",
              "OAuth2ClientId": "orchard-site",
              "OAuth2ClientSecret": "{{McpToolsClientSecret}}",
              "OAuth2Scopes": "tools.read"
            }
          }
        }
      ]
    }
  ]
}
```

Add your AI provider connection and deployment with the steps described in `orchardcore-ai-connections` and `orchardcore-ai-providers`, then select the connection on an AI profile's **Capabilities** tab.

## Recipe: MCP client with a local Docker server (stdio)

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Mcp",
        "CrestApps.OrchardCore.AI.Mcp.Stdio"
      ]
    },
    {
      "name": "McpConnection",
      "Connections": [
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

## Recipe: several connections at once

```json
{
  "steps": [
    {
      "name": "McpConnection",
      "Connections": [
        {
          "Source": "sse",
          "DisplayText": "Remote database tools",
          "Properties": {
            "SseMcpConnectionMetadata": {
              "Endpoint": "https://db-tools.example.com/mcp",
              "AuthenticationType": "CustomHeaders",
              "AdditionalHeaders": {
                "X-Api-Key": "{{DbToolsApiKey}}"
              }
            }
          }
        },
        {
          "Source": "stdio",
          "DisplayText": "Local file system tools",
          "Properties": {
            "StdioMcpConnectionMetadata": {
              "Command": "docker",
              "Arguments": [ "run", "-i", "--rm", "mcp/filesystem", "/data" ]
            }
          }
        }
      ]
    }
  ]
}
```

## Recipe: MCP server with the AI Agent tools

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Agent",
        "CrestApps.OrchardCore.AI.Mcp.Server",
        "OrchardCore.Contents"
      ]
    }
  ]
}
```

Enabling the features exposes nothing yet. Opt in the tools on **Settings → Artificial Intelligence → MCP Server**, or in configuration:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "McpServer": {
          "AuthenticationType": "ApiKey",
          "ApiKey": "your-long-secure-random-api-key",
          "Tools": [ "searchForContentItems", "getContentItemById", "getLinkForContentItem" ]
        }
      }
    }
  }
}
```

Store the key outside source control:

```bash
dotnet user-secrets set "OrchardCore:CrestApps:AI:McpServer:ApiKey" "your-long-secure-random-api-key"
```

For `OpenId` mode, enable the Orchard Core OpenID server, register a client application, and grant `AccessMcpServer` to the role or application that calls the server.

## Connecting VS Code to the Orchard Core MCP server

```json
{
  "mcpServers": {
    "orchard-core-site": {
      "transport": {
        "type": "http",
        "url": "https://your-orchard-site.com/mcp",
        "headers": {
          "Authorization": "ApiKey your-secure-api-key"
        }
      }
    }
  }
}
```

## Registering a custom MCP resource type

```csharp
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ModelContextProtocol.Protocol;
using OrchardCore.Modules;

[RequireFeatures("CrestApps.OrchardCore.AI.Mcp.Server")]
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient();

        services.AddCoreAIMcpResourceType<ApiEndpointResourceHandler>("api", entry =>
        {
            entry.DisplayName = S["API Endpoint"];
            entry.Description = S["Fetch data from REST API endpoints."];
            entry.SupportedVariables =
            [
                new McpResourceVariable("path") { Description = S["The API resource path."] },
            ];
        });
    }
}

public sealed class ApiEndpointResourceHandler : McpResourceTypeHandlerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ApiEndpointResourceHandler(IHttpClientFactory httpClientFactory)
        : base("api")
    {
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(
        McpResource resource,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        if (!variables.TryGetValue("path", out var path))
        {
            return CreateErrorResult(resource.Resource.Uri, "The path variable is required.");
        }

        var client = _httpClientFactory.CreateClient();
        var response = await client.GetStringAsync(
            new Uri(new Uri("https://api.example.com/"), SanitizePath(path)),
            cancellationToken);

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Text = response,
                    MimeType = resource.Resource.MimeType ?? "application/json",
                    Uri = resource.Resource.Uri,
                },
            ],
        };
    }
}
```

## Recipe: create MCP resources

`Uri` may be the path portion; the import expands it to `{source}://{itemId}/{path}`.

```json
{
  "steps": [
    {
      "name": "McpResource",
      "Resources": [
        {
          "Source": "file",
          "DisplayText": "Site configuration",
          "Resource": {
            "Uri": "{providerName}/{fileName}",
            "Name": "site-config",
            "Description": "Files from a named file provider",
            "MimeType": "application/json"
          }
        },
        {
          "Source": "content-type",
          "DisplayText": "Published items by type",
          "Resource": {
            "Uri": "{contentType}",
            "Name": "content-by-type",
            "Description": "All published content items of the requested content type",
            "MimeType": "application/json"
          }
        }
      ]
    }
  ]
}
```
