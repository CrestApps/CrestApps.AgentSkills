---
name: crestapps-core-context-builders
description: Skill for enriching CrestApps.Core completion and orchestration contexts with handler pipelines.
---

# CrestApps.Core Context Builders - Prompt Templates

## Enrich AI Request Context

You are a CrestApps.Core expert. Generate code and guidance for context-builder handlers that add request-specific AI settings and orchestration behavior.

### Guidelines

- `IAICompletionContextBuilder.BuildAsync(...)` produces `AICompletionContext`; use completion handlers for deployment settings, system messages, tools, and completion metadata.
- `IOrchestrationContextBuilder.BuildAsync(...)` produces `OrchestrationContext`; use orchestration handlers for work that belongs to the orchestrated pipeline.
- Both builders run `BuildingAsync`, then the optional caller configuration delegate, then `BuiltAsync`.
- Handlers run in reverse registration order. Register an override after a handler whose values it must replace.
- The default builders log and continue after non-cancellation handler exceptions. Keep handlers focused and fail explicitly only when the calling flow must stop.
- Completion-handler methods have no cancellation-token parameter. Orchestration-handler methods accept an optional `CancellationToken`.

### Completion Context Handler

```csharp
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;

public sealed class TenantCompletionContextHandler : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        context.Context.SystemMessage = string.Join(
            "\n\n",
            [context.Context.SystemMessage, "Respect the active tenant data boundary."]);

        context.Context.AdditionalProperties["tenant-mode"] = "restricted";

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
    {
        return Task.CompletedTask;
    }
}
```

Register it with the existing completion pipeline:

```csharp
builder.Services.AddScoped<IAICompletionContextBuilderHandler, TenantCompletionContextHandler>();
```

`AICompletionContextBuildingContext` exposes the source `Resource` and mutable `Context`. Use `context.GetResource<T>()` when the handler applies only to a known source type.

### Orchestration Context Handler

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;

public sealed class TenantOrchestrationContextHandler : IOrchestrationContextBuilderHandler
{
    public Task BuildingAsync(
        OrchestrationContextBuildingContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task BuiltAsync(
        OrchestrationContextBuiltContext context,
        CancellationToken cancellationToken = default)
    {
        context.OrchestrationContext.SystemMessageBuilder.AppendLine();
        context.OrchestrationContext.SystemMessageBuilder.Append(
            "Do not access data outside the active tenant.");

        return Task.CompletedTask;
    }
}
```

```csharp
builder.Services.AddScoped<IOrchestrationContextBuilderHandler, TenantOrchestrationContextHandler>();
```

The built-in `CompletionContextOrchestrationHandler` creates the completion context during orchestration and seeds `SystemMessageBuilder` with its system message. The default orchestration builder flushes that builder back to `CompletionContext.SystemMessage` after all handlers complete.

### Build Explicitly

```csharp
var completionContext = await completionContextBuilder.BuildAsync(
    profile,
    context => context.DisableTools = true,
    cancellationToken);

var orchestrationContext = await orchestrationContextBuilder.BuildAsync(
    profile,
    context => context.DisableTools = true,
    cancellationToken);
```

Use the caller configuration delegate for a one-request adjustment. Use a handler for reusable policy.

