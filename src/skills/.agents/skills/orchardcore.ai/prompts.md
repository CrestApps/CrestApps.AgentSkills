# Orchard Core AI - Prompt Templates

## Configure AI Integration

You are an Orchard Core expert. Generate code and configuration for AI integrations in Orchard Core.

### Guidelines

- Orchard Core supports AI integrations through the CrestApps AI module ecosystem.
- AI providers include OpenAI, Azure OpenAI, and other compatible services.
- MCP (Model Context Protocol) can be enabled for agent-based interactions.
- Configure AI services through `appsettings.json` or environment variables.
- Use dependency injection to access AI services in modules.
- Always secure API keys using user secrets or environment variables, never hardcode them.

### Enabling AI Features

```json
{
  "name": "Feature",
  "enable": [
    "CrestApps.OrchardCore.OpenAI",
    "CrestApps.OrchardCore.OpenAI.Azure"
  ],
  "disable": []
}
```

### Configuring OpenAI in appsettings.json

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "DefaultConnectionName": "openai-default",
      "Connections": {
        "openai-default": {
          "ProviderName": "OpenAI",
          "ApiKey": "{{YourApiKey}}",
          "DefaultModelId": "gpt-4o"
        }
      }
    }
  }
}
```

### Configuring Azure OpenAI

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "DefaultConnectionName": "azure-default",
      "Connections": {
        "azure-default": {
          "ProviderName": "AzureOpenAI",
          "Endpoint": "https://{{YourResourceName}}.openai.azure.com/",
          "ApiKey": "{{YourApiKey}}",
          "DefaultDeploymentName": "{{YourDeploymentName}}"
        }
      }
    }
  }
}
```

### MCP Server Configuration

To enable the MCP server for agent integrations:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "McpServer": {
        "Enabled": true,
        "TransportType": "Sse"
      }
    }
  }
}
```

### Using AI Services in Code

```csharp
using CrestApps.OrchardCore.AI.Abstractions;

public class MyService
{
    private readonly IAICompletionService _aiCompletionService;

    public MyService(IAICompletionService aiCompletionService)
    {
        _aiCompletionService = aiCompletionService;
    }

    public async Task<string> GetCompletionAsync(string prompt)
    {
        var result = await _aiCompletionService.CompleteAsync(prompt);
        return result?.Text ?? string.Empty;
    }
}
```

### Registering AI Agent Skills

Agent skills can be registered to provide context-specific knowledge:

```csharp
public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISkillProvider, MyCustomSkillProvider>();
    }
}
```

### Security Best Practices

- Store API keys in user secrets during development: `dotnet user-secrets set "OrchardCore:CrestApps_AI:Connections:openai-default:ApiKey" "your-key"`
- Use environment variables in production.
- Apply appropriate permissions to restrict AI feature access.
- Monitor token usage and set rate limits for production deployments.
