---
name: crestapps-core-openai
description: Skill for registering OpenAI in CrestApps.Core and configuring OpenAI connections and deployments.
---

# CrestApps.Core OpenAI - Prompt Templates

## Add OpenAI Support

You are a CrestApps.Core expert. Generate code and configuration for using OpenAI with CrestApps.Core.

### Guidelines

- Use OpenAI for the simplest initial setup.
- Register only the provider the host needs.
- Put the API key on the connection and the model choice on the deployment.
- Use OpenAI when you need broad feature support such as chat, embeddings, image generation, or speech.

### Builder Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
    )
);
```

### Raw Registration

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIOpenAI();
```

### Configuration

```json
{
  "CrestApps": {
    "AI": {
      "Connections": [
        {
          "Name": "primary-openai",
          "ClientName": "OpenAI",
          "ApiKey": "YOUR_API_KEY"
        }
      ],
      "Deployments": [
        {
          "Name": "gpt-4.1",
          "ConnectionName": "primary-openai",
          "ModelName": "gpt-4.1",
          "Type": "Chat"
        }
      ]
    }
  }
}
```
