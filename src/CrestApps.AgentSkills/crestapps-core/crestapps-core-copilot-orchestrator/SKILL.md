---
name: crestapps-core-copilot-orchestrator
description: Skill for GitHub Copilot orchestrator setup, GitHub OAuth mode, and BYOK mode in CrestApps.Core.
---

# CrestApps.Core Copilot Orchestrator - Prompt Templates

## Add the Copilot Orchestrator

You are a CrestApps.Core expert. Generate code and configuration for the Copilot orchestrator in CrestApps.Core.

### Guidelines

- Use the Copilot orchestrator when the host should run through the GitHub Copilot Extensions SDK.
- Support the configured state explicitly with `NotConfigured`, `GitHubOAuth`, or `ApiKey`.
- Use GitHub OAuth for per-user Copilot subscription access.
- Use BYOK mode for a shared OpenAI-compatible endpoint.
- Provide an `ICopilotCredentialStore` implementation when GitHub OAuth is enabled.

### Registration

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIOrchestration()
    .AddCoreAICopilotOrchestrator();
```

### Resolve by Name

```csharp
var orchestrator = resolver.Resolve("copilot");
```

### BYOK Configuration

```json
{
  "CopilotOptions": {
    "AuthenticationType": "ApiKey",
    "ProviderType": "openai",
    "BaseUrl": "https://api.openai.com/v1",
    "ApiKey": "sk-...",
    "DefaultModel": "gpt-4o",
    "WireApi": "completions"
  }
}
```

### GitHub OAuth Configuration

```json
{
  "CopilotOptions": {
    "AuthenticationType": "GitHubOAuth",
    "ClientId": "Iv1.abc123",
    "ClientSecret": "your-client-secret",
    "Scopes": ["user:email", "read:org"]
  }
}
```
