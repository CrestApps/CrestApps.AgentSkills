---
name: crestapps-core-context-builders
description: Skill for enriching CrestApps.Core 2.0 completion and orchestration contexts with handler pipelines. Covers IAICompletionContextBuilder and IAICompletionContextBuilderHandler producing AICompletionContext, IOrchestrationContextBuilder and IOrchestrationContextBuilderHandler producing OrchestrationContext, the BuildingAsync, configure delegate and BuiltAsync phases, reverse registration order, the built-in profile, chat interaction, persona, tool instance, data source and MCP handlers, chat and utility model parameters copied onto the context, SystemMessageBuilder, reading the chat from AIInvocationScope during a build, error isolation that logs and skips a failing handler, and one-off configuration through the caller delegate. Use it when injecting tenant, user or policy context into every AI request, adding orchestration behavior, or debugging a profile whose system message, tools or parameters vanish.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Context Builders

Two handler pipelines prepare every AI request:

| Level | Builder | Handler | Produces |
|---|---|---|---|
| Completion | `IAICompletionContextBuilder.BuildAsync(resource, configure, cancellationToken)` | `IAICompletionContextBuilderHandler` | `AICompletionContext` (deployments, system message, tools, agents, model parameters, data source) |
| Orchestration | `IOrchestrationContextBuilder.BuildAsync(resource, configure, cancellationToken)` | `IOrchestrationContextBuilderHandler` | `OrchestrationContext` (user message, history, documents, `SystemMessageBuilder`, completion context) |

## Guidelines

- Use a completion handler for anything every completion of a resource needs, including non-orchestrated calls and sub-agents. Use an orchestration handler for work that only belongs to orchestrated chat.
- Both builders run `BuildingAsync` on every handler, then the caller's `configure` delegate, then `BuiltAsync` on every handler.
- Handlers run in reverse registration order: the last registered runs first. Register an override after the handler whose values it replaces.
- A handler that throws (other than cancellation) is logged and skipped, and the build continues. A failing built-in handler can leave a profile without its system message, model parameters, tools and agents, so watch for "Error in completion context building handler" in logs.
- Completion handler methods have no `CancellationToken`; orchestration handler methods take an optional one.
- Pattern-match `Resource` (`AIProfile`, `ChatInteraction`, or your own type) instead of assuming one resource type.

## Built-in Handlers

| Completion handler | Adds |
|---|---|
| `AIProfileCompletionContextBuilderHandler` | Profile system message, sampling, deployments, prompt templates, tools, agents, chat and utility model parameters |
| `ChatInteractionCompletionContextBuilderHandler` | The same for a `ChatInteraction` |
| `AIPersonaCompletionContextBuilderHandler` | In `BuiltAsync`, prepends the linked persona block and stores it under `AICompletionContextKeys.Persona` (when personas are enabled) |
| `AIToolInstanceCompletionContextBuilderHandler` | `ToolInstanceNames` from `AIToolInstanceMetadata` (when tool instances are enabled) |
| `DataSourceAICompletionContextBuilderHandler` | The selected data source id |
| `McpAICompletionContextBuilderHandler` | MCP connection ids (when MCP is enabled) |

Orchestration handlers include `CompletionContextOrchestrationHandler` (builds the completion context and seeds `SystemMessageBuilder`), `PreemptiveRagOrchestrationHandler`, `RealtimeRagGuidanceHandler`, `AIToolExecutionContextOrchestrationHandler`, `SecurityPromptOrchestrationHandler`, plus data source, memory, document, extracted-data, Claude and Copilot handlers from their features. After all orchestration handlers run, the builder flushes `SystemMessageBuilder` back to `CompletionContext.SystemMessage`.

## Completion Context Handler

```csharp
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;

public sealed class TenantCompletionContextHandler : IAICompletionContextBuilderHandler
{
    private readonly ITenantAccessor _tenantAccessor;

    public TenantCompletionContextHandler(ITenantAccessor tenantAccessor)
    {
        _tenantAccessor = tenantAccessor;
    }

    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
    {
        if (context.Resource is not AIProfile profile || profile.Type == AIProfileType.Agent)
        {
            return Task.CompletedTask;
        }

        var tenant = _tenantAccessor.Current;

        context.Context.SystemMessage = string.Join(
            "\n\n",
            new[] { context.Context.SystemMessage, $"Only use data that belongs to tenant '{tenant.Name}'." }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

        context.Context.AdditionalProperties["tenant-id"] = tenant.Id;

        return Task.CompletedTask;
    }
}
```

```csharp
builder.Services.AddScoped<IAICompletionContextBuilderHandler, TenantCompletionContextHandler>();
```

`ITenantAccessor` stands for the host's own service. `AICompletionContextBuildingContext` and `AICompletionContextBuiltContext` both expose `Resource` and the mutable `Context`. The profile handler sets the system message in `BuildingAsync`, so appending in `BuiltAsync` keeps your text after it. The persona handler also works in `BuiltAsync`; register your handler after `AddPersonas(...)` so it runs first and the persona block still ends up at the top.

## Orchestration Context Handler

```csharp
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
        context.OrchestrationContext.SystemMessageBuilder
            .AppendLine()
            .Append("Do not access data outside the active tenant.");

        return Task.CompletedTask;
    }
}
```

```csharp
builder.Services.AddScoped<IOrchestrationContextBuilderHandler, TenantOrchestrationContextHandler>();
```

`OrchestrationContextBuildingContext` exposes `Resource`, `GetResource<T>()` and `Context`; `OrchestrationContextBuiltContext` exposes `Resource` and `OrchestrationContext`.

## Reading the Current Chat

During a chat turn, the response handler stores the chat interaction or session in `AIInvocationScope.Current` (`ChatInteraction`, `ChatSession`) before it builds the orchestration context, and the completion context and data source after it. Handlers and `IAIProfileProvider`s can therefore read the active chat during the build. Do not rely on `IHttpContextAccessor` inside SignalR hubs; use `IUserAccessor.User` for the caller.

## Model Parameters on the Context

The profile and chat interaction handlers copy `AIDeploymentParametersMetadata` onto `AICompletionContext.ModelParameters` (chat) and `UtilityModelParameters` (utility). A handler can adjust them or mark a background call with `IsUtilityCompletion = true`:

```csharp
public Task BuiltAsync(AICompletionContextBuiltContext context)
{
    if (context.Resource is AIProfile { Name: "nightly-summary" })
    {
        context.Context.IsUtilityCompletion = true;
        context.Context.UtilityModelParameters["reasoningEffort"] = "Low";
    }

    return Task.CompletedTask;
}
```

## Build Explicitly

```csharp
var completionContext = await completionContextBuilder.BuildAsync(
    profile,
    context => context.DisableTools = true,
    cancellationToken);

var orchestrationContext = await orchestrationContextBuilder.BuildAsync(
    profile,
    context => context.UserMessage = prompt,
    cancellationToken);
```

Use the delegate for a one-request adjustment and a handler for reusable policy.

## Upgrading from 1.x

- The persona handler is new and runs in `BuiltAsync`. A custom handler that prepends text in `BuiltAsync` and runs after it (registered earlier) puts that text above the persona block.
- `AIProfileSystemPromptTemplateProvider` now ships with `AddCoreAITemplating()`, which fixes profiles that lost their whole context because the template handler threw.
- `AICompletionContext` gained `UtilityModelParameters` and `IsUtilityCompletion`; usage labels go in `AdditionalProperties` under `AICompletionContextKeys.UsageContextType` and `UsagePurpose`.
