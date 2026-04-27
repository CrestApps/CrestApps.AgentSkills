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

### Raw Registration

```csharp
builder.Services
    .AddCoreAIServices()
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
          "ConnectionName": "azure-ai-inference",
          "ModelName": "gpt-4o-mini",
          "Type": "Chat"
        }
      ]
    }
  }
}
```
