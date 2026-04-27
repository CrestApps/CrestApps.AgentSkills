---
name: crestapps-core-ollama
description: Skill for local Ollama integration in CrestApps.Core for development, privacy-sensitive workloads, and self-hosted models.
---

# CrestApps.Core Ollama - Prompt Templates

## Add Ollama Support

You are a CrestApps.Core expert. Generate code and configuration for using local Ollama models with CrestApps.Core.

### Guidelines

- Use Ollama for local development, offline work, and privacy-sensitive scenarios.
- Expect capability differences to be model-dependent.
- Keep the local endpoint on the connection.
- Keep the selected model name on the deployment.

### Builder Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOllama()
    )
);
```

### Raw Registration

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIOllama();
```

### Configuration

```json
{
  "CrestApps": {
    "AI": {
      "Connections": [
        {
          "Name": "local-ollama",
          "ClientName": "Ollama",
          "Endpoint": "http://localhost:11434"
        }
      ],
      "Deployments": [
        {
          "Name": "llama3.1",
          "ConnectionName": "local-ollama",
          "ModelName": "llama3.1",
          "Type": "Chat"
        }
      ]
    }
  }
}
```
