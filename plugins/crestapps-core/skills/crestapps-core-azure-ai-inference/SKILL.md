---
name: crestapps-core-azure-ai-inference
description: Skill for Azure AI Inference and GitHub Models integration in CrestApps.Core.
---

# CrestApps.Core Azure AI Inference - Prompt Templates

## Add Azure AI Inference Support

You are a CrestApps.Core expert. Generate code and configuration for Azure AI Inference with CrestApps.Core.

### Guidelines

- Use Azure AI Inference when one endpoint should expose multiple model families.
- It is a strong fit for GitHub Models and multi-model evaluation.
- Keep the endpoint and credentials on the connection.
- Keep deployment names and types in the deployment list.

### Builder Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddAzureAIInference()
    )
);
```

### Provider-Only Registration

`AddCoreAIAzureAIInference()` adds only the Azure AI Inference provider registrations. Use it alone only when the application has already registered the core AI services it needs.

```csharp
builder.Services.AddCoreAIAzureAIInference();
```

For a normal application composition that resolves deployments or uses orchestration, register the core and orchestration services as well:

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIOrchestration()
    .AddCoreAIAzureAIInference();
```

### Configuration

```json
{
  "CrestApps": {
    "AI": {
      "Connections": [
        {
          "Name": "azure-ai-inference",
          "ClientName": "AzureAIInference",
          "ApiKey": "YOUR_TOKEN",
          "Endpoint": "https://models.inference.ai.azure.com"
        }
      ],
      "Deployments": [
        {
          "Name": "gpt-4o-mini",
          "ClientName": "AzureAIInference",
          "ConnectionName": "azure-ai-inference",
          "ModelName": "gpt-4o-mini",
          "Type": "Chat"
        }
      ]
    }
  }
}
```
