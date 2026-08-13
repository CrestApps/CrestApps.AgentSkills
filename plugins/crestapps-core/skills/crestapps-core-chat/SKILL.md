---
name: crestapps-core-chat
description: Skill for building deployment-aware chat completion and streaming loops in CrestApps.Core.
---

# CrestApps.Core Chat - Prompt Templates

## Build a Chat Completion Loop

You are a CrestApps.Core expert. Generate code and guidance for deployment-aware chat completions, streaming, and orchestration loops.

### Guidelines

- CrestApps.Core exposes `IAICompletionService`, not `IChatCompletionService`; it returns Microsoft.Extensions.AI `ChatResponse` and `ChatResponseUpdate` values.
- Register the AI suite and at least one provider, then configure a Chat-capable deployment and connection.
- Resolve an `AIDeployment` through `IAIDeploymentManager` rather than constructing provider clients in application code.
- Use `CompleteAsync(...)` for one result and `CompleteStreamingAsync(...)` with `await foreach` for incremental output.
- Pass an `AICompletionContext` for system instructions, sampling settings, deployment names, and tool choices.
- Use `IOrchestrator.ExecuteStreamingAsync(...)` when the request needs planning, tool scoping, or an iterative agent loop.
- This skill is the completion and orchestration surface. `crestapps-core-chat-interactions` is the persisted session, history, handler-routing, and SignalR interaction feature.
- The direct completion guidance here corresponds to the AI core and orchestration documentation. The `/docs/core/chat` topic documents `crestapps-core-chat-interactions`, not this direct completion loop.

### Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
    )
);
```

Configure an OpenAI connection and a deployment whose `Purpose` includes `AIDeploymentPurpose.Chat`.

### Stream a Response

```csharp
using System.Runtime.CompilerServices;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.AI;

namespace MyApp;

public sealed class ChatCompletionLoop
{
    private readonly IAICompletionService _completionService;
    private readonly IAIDeploymentManager _deploymentManager;

    public ChatCompletionLoop(
        IAICompletionService completionService,
        IAIDeploymentManager deploymentManager)
    {
        _completionService = completionService;
        _deploymentManager = deploymentManager;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var deployment = await _deploymentManager.ResolveOrDefaultAsync(
            AIDeploymentPurpose.Chat,
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("No Chat deployment is configured.");

        var context = new AICompletionContext
        {
            SystemMessage = "You are a concise support assistant.",
            Temperature = 0.2f,
        };

        await foreach (var update in _completionService.CompleteStreamingAsync(
            deployment,
            [new ChatMessage(ChatRole.User, prompt)],
            context,
            cancellationToken))
        {
            yield return update;
        }
    }
}
```

### Choose the Chat Surface

- `IAICompletionService` — direct deployment-aware completion and streaming; use `CompleteAsync(...)` for one response.
- `IOrchestrator` — streaming completion with planning and progressively scoped tools.
- `crestapps-core-chat-interactions` — managed conversations with stored prompts, sessions, response handlers, and optional SignalR hubs.
