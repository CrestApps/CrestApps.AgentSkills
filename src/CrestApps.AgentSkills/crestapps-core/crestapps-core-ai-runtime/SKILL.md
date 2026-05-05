---
name: crestapps-core-ai-runtime
description: Skill for provider-agnostic AI runtime setup, connections, deployments, completion services, and context-building in CrestApps.Core.
---

# CrestApps.Core AI Runtime - Prompt Templates

## Configure the AI Runtime

You are a CrestApps.Core expert. Generate code and configuration for the provider-agnostic AI runtime in CrestApps.Core.

### Guidelines

- Use `AddCoreAIServices()` only when the host needs the raw service registrations.
- Prefer `AddCrestAppsCore(...).AddAISuite(...)` for application composition.
- Program against `IAIClientFactory`, `IAICompletionService`, and `IAICompletionContextBuilder`.
- Treat connections as provider credentials and endpoints.
- Treat deployments as named model selections.
- Resolve deployments through the runtime instead of hardcoding model calls in business logic.

### Raw Service Registration

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIOpenAI();
```

### Builder-Based Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
    )
);
```

### Key Runtime Services

| Service | Use |
|---|---|
| `IAIClientFactory` | Create typed chat, embedding, image, speech, and other provider clients |
| `IAICompletionService` | Send deployment-aware completions without the full orchestration loop |
| `IAICompletionContextBuilder` | Build enriched AI completion contexts through handlers |
| `IAIDeploymentStore` | Read named deployments from config and store sources |
| `IAIProviderConnectionStore` | Read provider connections from config and store sources |

### Example Completion Service

```csharp
public sealed class QuestionService(IAICompletionService completionService)
{
    public async Task<string> AskAsync(AIDeployment deployment, string question)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, question),
        };

        var response = await completionService.CompleteAsync(deployment, messages);
        return response.Text;
    }
}
```
