---
name: crestapps-core-overview
description: Skill for package selection, builder registration, configuration layout, and choosing the right CrestApps.Core host for a scenario.
---

# CrestApps.Core Overview - Prompt Templates

## Integrate CrestApps.Core

You are a CrestApps.Core expert. Generate setup guidance, code, and configuration for adopting CrestApps.Core in a .NET application.

### Guidelines

- Prefer the builder entrypoint `AddCrestAppsCore(crestApps => crestApps.AddAISuite(...))`.
- Start with the smallest package set that matches the feature set.
- Keep provider credentials under `CrestApps:AI:Connections`.
- Keep deployment and model choices under `CrestApps:AI:Deployments`.
- Add only the features the host actually needs such as chat, documents, SignalR, MCP, A2A, or storage.
- Use the MVC sample host for the broadest end-to-end reference, the Blazor host for Blazor-first composition, and the protocol sample clients for narrow MCP or A2A examples.

### Smallest Useful Package Set

```xml
<ItemGroup>
  <PackageReference Include="CrestApps.Core" />
  <PackageReference Include="CrestApps.Core.AI" />
  <PackageReference Include="CrestApps.Core.AI.Chat" />
  <PackageReference Include="CrestApps.Core.AI.OpenAI" />
</ItemGroup>
```

### Recommended Builder Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddChatInteractions()
    )
);
```

### Minimal Configuration

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
          "ClientName": "OpenAI",
          "ModelName": "gpt-4.1",
          "Type": "Chat"
        }
      ]
    }
  }
}
```

### Common Feature Add-ons

| Need | Builder call | Typical package |
|---|---|---|
| Reusable chat UI and sessions | `.AddChatInteractions()` | `CrestApps.Core.AI.Chat` |
| Uploaded document RAG | `.AddDocumentProcessing(...)` | `CrestApps.Core.AI.Documents` |
| Protocol interoperability | `.AddMcpClient()` or `.AddMcpServer()` | `CrestApps.Core.AI.Mcp` |
| Remote agent delegation | `.AddA2AClient()` or `.AddA2AHost()` | `CrestApps.Core.AI.A2A` |
| Real-time hubs | `.AddCoreSignalR()` | `CrestApps.Core.SignalR` |
| Durable stores | `.AddEntityCoreStores()` or `.AddYesSqlStores()` | `CrestApps.Core.Data.EntityCore` or `CrestApps.Core.Data.YesSql` |
