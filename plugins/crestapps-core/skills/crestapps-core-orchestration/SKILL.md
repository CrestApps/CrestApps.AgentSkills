---
name: crestapps-core-orchestration
description: Skill for the default CrestApps.Core 2.0 orchestration pipeline. Covers AddCoreAIOrchestration and AddAISuite registration, IOrchestrator ExecuteStreamingAsync, IOrchestratorResolver and OrchestratorOptions DefaultOrchestratorName, building an OrchestrationContext with IOrchestrationContextBuilder, the tool registry and its providers (system, profile, agent and tool instance tools), progressive tool scoping with DefaultOrchestratorOptions ScopingThreshold and PlanningThreshold, the utility-deployment planning call with its model parameters, chat-slot deployment resolution, preemptive RAG with DefaultOrchestratorSettings, built-in system tools, registering a custom orchestrator with AddOrchestrator, error handling, and usage labels for planning. Use it when running orchestrated chat in a host, writing a custom orchestrator, or tuning how many tools reach the model.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Orchestration

The orchestrator is the agentic loop: it builds context from the profile, scopes tools, runs preemptive retrieval, calls the model with function invocation, and streams the answer. `DefaultOrchestrator` (name `"default"`) is registered by `AddCoreAIOrchestration()`; Claude (`"anthropic"`) and Copilot (`"copilot"`) are alternatives.

## Guidelines

- Register orchestration through `AddAISuite(...)` or `AddCoreAIOrchestration()`.
- Build the context with `IOrchestrationContextBuilder.BuildAsync(resource, configure, cancellationToken)` instead of filling `OrchestrationContext` by hand; the handlers add the system message, tools, documents, RAG and security preamble.
- Resolve orchestrators with `IOrchestratorResolver.Resolve(name)`. An empty or unknown name falls back to `OrchestratorOptions.DefaultOrchestratorName`.
- Let the orchestrator scope large tool sets; do not hand-pick tools per request.
- The chat deployment is resolved through the `chat` slot and planning runs on the utility deployment (falling back to chat). See `crestapps-core-ai-runtime`.

## Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
    )
);
```

Raw form:

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIOrchestration()
    .AddCoreAIOpenAI();
```

## Run an Orchestrated Turn

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using Microsoft.Extensions.AI;

public sealed class ProfileChatService
{
    private readonly IOrchestrationContextBuilder _contextBuilder;
    private readonly IOrchestratorResolver _orchestratorResolver;

    public ProfileChatService(
        IOrchestrationContextBuilder contextBuilder,
        IOrchestratorResolver orchestratorResolver)
    {
        _contextBuilder = contextBuilder;
        _orchestratorResolver = orchestratorResolver;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        AIProfile profile,
        string userMessage,
        IList<ChatMessage> history,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var context = await _contextBuilder.BuildAsync(
            profile,
            ctx =>
            {
                ctx.UserMessage = userMessage;
                ctx.ConversationHistory = history;
            },
            cancellationToken);

        var orchestrator = _orchestratorResolver.Resolve(profile.OrchestratorName);

        await foreach (var update in orchestrator.ExecuteStreamingAsync(context, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }
}
```

There is no separate non-streaming API; collect the updates when you need the full text. The built-in chat hubs go through `IChatResponseHandler` (see `crestapps-core-response-handlers`), which also sets `AIInvocationScope` for tools and agents; code that calls the orchestrator directly should wrap the call in `using var scope = AIInvocationScope.Begin();` when it uses tool-capable agents or citation tools.

## Key Services

| Service | Purpose |
|---|---|
| `IOrchestrator` | `Name` and `ExecuteStreamingAsync(OrchestrationContext, CancellationToken)` returning `IAsyncEnumerable<ChatResponseUpdate>` |
| `IOrchestratorResolver` | `Resolve(orchestratorName)` with fallback to the default |
| `IOrchestrationContextBuilder` | Runs `IOrchestrationContextBuilderHandler`s |
| `IToolRegistry` | `GetAllAsync(AICompletionContext, CancellationToken)` merges every `IToolRegistryProvider` |
| `IAIToolsService` | Resolve a registered keyed `AITool` by name |
| `IChatResponseHandlerResolver` | Pick the response handler for a session or interaction |

Tool registry providers: `SystemToolRegistryProvider`, `ProfileToolRegistryProvider`, `AgentToolRegistryProvider`, and `ToolInstanceRegistryProvider` when tool instances are enabled. Built-in orchestration handlers: `CompletionContextOrchestrationHandler`, `PreemptiveRagOrchestrationHandler`, `RealtimeRagGuidanceHandler`, `AIToolExecutionContextOrchestrationHandler`, `SecurityPromptOrchestrationHandler`, plus data source, memory and document handlers from their features.

Built-in system tools: `GenerateImageTool` and `GenerateChartTool` (purpose `content_generation`) and `CurrentDateTimeTool` (selectable, category Utilities).

## Progressive Tool Scoping

| Tool count | Behavior |
|---|---|
| At or below `ScopingThreshold` (30) | All tools are passed through |
| Above that, at or below `PlanningThreshold` (100), no MCP tools | Token relevance scoring against the latest user and assistant messages; top `InitialToolCount` (20) plus must-include tools |
| Above `PlanningThreshold`, or any MCP tools | A planning call on the utility deployment, then relevance scoring |

If planning fails, the orchestrator logs a warning and still scopes by the user message and recent conversation. Planning resolves the utility deployment with `ResolveUtilityOrDefaultAsync`, applies `UseDefaultResilience()` and the utility model parameters (`UseModelParameters(..., AIDeploymentParameterScope.Utility)`), and is labeled `AIUsagePurposes.Planning` in usage records.

```csharp
builder.Services.Configure<DefaultOrchestratorOptions>(options =>
{
    options.InitialToolCount = 20;
    options.ScopingThreshold = 30;
    options.PlanningThreshold = 100;
    options.MaxExpansionRounds = 3;
    options.MaxToolCount = 30;
    options.PlanningHistoryMessageCount = 10;
});

builder.Services.Configure<DefaultOrchestratorSettings>(settings =>
{
    settings.EnablePreemptiveRag = true;
});
```

The tool-calling loop iteration cap comes from `DefaultAIOptions.MaximumIterationsPerRequest` (default 10, absolute cap 100), which a site can override through `GeneralAIOptions`.

## Custom Orchestrator

```csharp
using System.Runtime.CompilerServices;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using Microsoft.Extensions.AI;

public sealed class SingleShotOrchestrator : IOrchestrator
{
    public const string OrchestratorName = "single-shot";

    private readonly IAICompletionService _completionService;
    private readonly IAIDeploymentManager _deploymentManager;

    public SingleShotOrchestrator(
        IAICompletionService completionService,
        IAIDeploymentManager deploymentManager)
    {
        _completionService = completionService;
        _deploymentManager = deploymentManager;
    }

    public string Name => OrchestratorName;

    public async IAsyncEnumerable<ChatResponseUpdate> ExecuteStreamingAsync(
        OrchestrationContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var deployment = await _deploymentManager.ResolveSlotAsync(
            AIDeploymentSlotNames.Chat,
            deploymentName: context.CompletionContext?.ChatDeploymentName,
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Unable to resolve a chat deployment.");

        var messages = new List<ChatMessage>(context.ConversationHistory)
        {
            new(ChatRole.User, context.UserMessage),
        };

        await foreach (var update in _completionService.CompleteStreamingAsync(
            deployment, messages, context.CompletionContext, cancellationToken))
        {
            yield return update;
        }
    }
}
```

```csharp
builder.Services.AddOrchestrator<SingleShotOrchestrator>(SingleShotOrchestrator.OrchestratorName)
    .WithTitle("Single shot");
```

The completion context already carries the system message the handlers built, so a custom orchestrator does not add it again. Set `AIProfile.OrchestratorName` to route a profile to it.

## Errors

| Situation | Behavior |
|---|---|
| Planning failure | Logged; falls back to relevance scoring |
| No chat deployment resolves | `InvalidOperationException` |
| `OperationCanceledException` | Always rethrown |
| Provider error after retries | Propagates to the caller |
| Tool error | Returned to the model as the tool result |

## Upgrading from 1.x

- The orchestrator resolves the chat deployment with `ResolveSlotAsync(AIDeploymentSlotNames.Chat, ...)` and planning with `ResolveUtilityOrDefaultAsync(...)`; custom orchestrators that used `ResolveOrDefaultAsync(AIDeploymentPurpose...)` must switch.
- Planning now honors the utility deployment's model parameters (for example a `reasoningEffort` of `None` for a reasoning utility model).
- Per-user tool gating no longer runs for AI sessions; the profile is the authorization boundary. Chat Interactions still re-check selectable tools with `IAIToolAccessEvaluator`.
