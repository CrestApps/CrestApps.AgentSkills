---
name: crestapps-core-azure-openai
description: Skill for registering Azure OpenAI in CrestApps.Core and configuring Azure connections, deployments, and enterprise defaults.
---

# CrestApps.Core Azure OpenAI - Prompt Templates

## Add Azure OpenAI Support

You are a CrestApps.Core expert. Generate code and configuration for Azure OpenAI with CrestApps.Core.

### Guidelines

- Use Azure OpenAI for enterprise production needs such as regional control and managed identity scenarios.
- Register Azure OpenAI only when the host actually uses it.
- Keep the Azure endpoint and credentials on the connection.
- Keep deployment names and model intent on the deployment entry.

### Builder Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddAzureOpenAI()
    )
);
```

### Raw Registration

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIAzureOpenAI();
```

### Configuration

```json
{
  "CrestApps": {
    "AI": {
      "Connections": [
        {
          "Name": "azure-primary",
          "ClientName": "Azure",
          "ApiKey": "YOUR_AZURE_OPENAI_KEY",
          "Endpoint": "https://my-resource.openai.azure.com"
        }
      ],
      "Deployments": [
        {
          "Name": "gpt-4o",
          "ConnectionName": "azure-primary",
          "ModelName": "gpt-4o",
          "Type": "Chat"
        }
      ]
    }
  }
}
```
