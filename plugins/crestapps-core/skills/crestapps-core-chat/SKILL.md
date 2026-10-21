---
name: crestapps-core-chat
description: Skill for building deployment-aware chat completion and streaming loops in CrestApps.Core 2.0. Covers IAICompletionService CompleteAsync and CompleteStreamingAsync with AICompletionContext, resolving chat and utility deployments through slots with ResolveSlotAsync and ResolveUtilityOrDefaultAsync instead of the removed AIDeploymentPurpose, model parameters such as reasoningEffort through AICompletionContext ModelParameters and UtilityModelParameters, IsUtilityCompletion for background work, capability enforcement that strips tools, structured outputs and streaming a deployment does not declare, reasoning requests with tools routed to the Responses API, and running a profile or interaction through IOrchestrationContextBuilder, IOrchestratorResolver and IOrchestrator ExecuteStreamingAsync inside an AIInvocationScope. Use it when writing server code that sends prompts to a model, streams a reply, runs the full orchestration pipeline outside the chat hubs, or upgrades completion code from 1.x.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Chat Completions

This skill is the server-side completion and orchestration surface. Persisted conversations, hubs, chat UI and response handlers are covered by `crestapps-core-chat-interactions`; voice sessions by `crestapps-core-realtime-voice`; providers by `crestapps-core-openai`, `crestapps-core-azure-openai`, `crestapps-core-ollama` and `crestapps-core-azure-ai-inference`.

## Guidelines

- CrestApps.Core exposes `IAICompletionService` (namespace `CrestApps.Core.AI.Completions`), not `IChatCompletionService`. It returns Microsoft.Extensions.AI `ChatResponse` and `ChatResponseUpdate`.
- Resolve the `AIDeployment` first, through a slot: `ResolveSlotAsync(AIDeploymentSlotNames.Chat, ...)` for answers and `ResolveUtilityOrDefaultAsync(...)` for background work. Do not construct provider clients in application code.
- Pass an `AICompletionContext` for the system message, sampling, tool selection, data source, and model parameters.
- Use `IOrchestrator.ExecuteStreamingAsync(...)` when the request needs a profile's tools, agents, RAG, MCP connections, or progressive tool scoping.
- The deployment's declared capabilities are enforced on every request. Tools, a JSON response format, reasoning options and streaming are removed or downgraded when the deployment does not declare `toolCalling`, `structuredOutputs`, `reasoning` or `streaming`.

## Registration

```csharp
using CrestApps.Core.AI.OpenAI;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
    )
);
```

Configure a connection and at least one deployment that declares `textGeneration` (plus `toolCalling` and `streaming` for chat). See the provider skills for the configuration shape.

## Stream a Completion

```csharp
using System.Runtime.CompilerServices;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.AI;

namespace MyApp.Services;

public sealed class SupportAnswerService
{
    private readonly IAICompletionService _completionService;
    private readonly IAIDeploymentManager _deploymentManager;

    public SupportAnswerService(
        IAICompletionService completionService,
        IAIDeploymentManager deploymentManager)
    {
        _completionService = completionService;
        _deploymentManager = deploymentManager;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        string deploymentName = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var deployment = await _deploymentManager.ResolveSlotAsync(
            AIDeploymentSlotNames.Chat,
            deploymentName,
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("No deployment can fill the chat slot.");

        var context = new AICompletionContext
        {
            SystemMessage = "You are a concise support assistant.",
            Temperature = 0.2f,
            MaxTokens = 800,
        };

        // Applied only when the deployment declares the reasoning feature and the reasoningEffort parameter.
        context.ModelParameters[AIDeploymentParameterNames.ReasoningEffort] = "Low";

        await foreach (var update in _completionService.CompleteStreamingAsync(
            deployment,
            [new ChatMessage(ChatRole.User, prompt)],
            context,
            cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }
}
```

Use `CompleteAsync(...)` for a single `ChatResponse` (read `response.Text`).

## Background (Utility) Completions

Titles, summaries, data extraction and query rewriting belong on the utility deployment:

```csharp
var deployment = await deploymentManager.ResolveUtilityOrDefaultAsync(
    utilityDeploymentName: profile.UtilityDeploymentName,
    chatDeploymentName: profile.ChatDeploymentName)
    ?? throw new InvalidOperationException("No text deployment is configured.");

var context = new AICompletionContext
{
    SystemMessage = "Summarize the conversation in one sentence.",
    IsUtilityCompletion = true,
    DisableTools = true,
};

context.UtilityModelParameters[AIDeploymentParameterNames.ReasoningEffort] = "None";
```

The chain is explicit utility name, site default utility (`DefaultAIDeploymentSettings.DefaultUtilityDeploymentName`), explicit chat name, site default chat, then the first text-capable deployment. `IsUtilityCompletion` makes the named completion path use the utility deployment and the `UtilityModelParameters`. A host that resolves an `IChatClient` directly from `IAIClientFactory` applies the same selections with `ChatClientBuilder.UseModelParameters(...)` or `UseUtilityModelParameters(...)`.

## Run the Full Orchestration Pipeline

Outside the chat hubs, build the orchestration context from a profile (or a `ChatInteraction`), open an `AIInvocationScope` in the method that enumerates the whole stream, and push tokens to the caller:

```csharp
using System.Text;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using Microsoft.Extensions.AI;

namespace MyApp.Services;

public sealed class ProfileRunner
{
    private readonly IAIProfileManager _profileManager;
    private readonly IOrchestrationContextBuilder _contextBuilder;
    private readonly IOrchestratorResolver _orchestratorResolver;

    public ProfileRunner(
        IAIProfileManager profileManager,
        IOrchestrationContextBuilder contextBuilder,
        IOrchestratorResolver orchestratorResolver)
    {
        _profileManager = profileManager;
        _contextBuilder = contextBuilder;
        _orchestratorResolver = orchestratorResolver;
    }

    public async Task<string> RunAsync(
        string profileName,
        string prompt,
        IList<ChatMessage> history,
        Func<string, Task> onToken,
        CancellationToken cancellationToken = default)
    {
        var profile = await _profileManager.FindByNameAsync(profileName, cancellationToken)
            ?? throw new InvalidOperationException($"Profile '{profileName}' was not found.");

        // Tools such as data source search read their context from this ambient scope. It is an AsyncLocal,
        // so open it in the method that enumerates the whole stream, not in an async iterator that yields.
        using var invocationScope = AIInvocationScope.Begin();

        var context = await _contextBuilder.BuildAsync(
            profile,
            ctx =>
            {
                ctx.UserMessage = prompt;
                ctx.ConversationHistory = history;
            },
            cancellationToken);

        invocationScope.Context.CompletionContext = context.CompletionContext;
        invocationScope.Context.DataSourceId = context.CompletionContext.DataSourceId;

        var orchestrator = _orchestratorResolver.Resolve(profile.OrchestratorName);
        var answer = new StringBuilder();

        await foreach (var update in orchestrator.ExecuteStreamingAsync(context, cancellationToken))
        {
            if (string.IsNullOrEmpty(update.Text))
            {
                continue;
            }

            answer.Append(update.Text);
            await onToken(update.Text);
        }

        return answer.ToString();
    }
}
```

`IOrchestratorResolver.Resolve(null)` returns the default orchestrator. The built-in `AIChatResponseHandler` follows the same steps, storing the chat session or interaction on `AIInvocationScope.Current` before the build.

## Choose the Surface

| Need | Use |
|---|---|
| One prompt, one answer, no profile | `IAICompletionService.CompleteAsync` |
| Streamed tokens without tools or RAG | `IAICompletionService.CompleteStreamingAsync` |
| A profile's tools, agents, RAG and MCP | `IOrchestrator.ExecuteStreamingAsync` |
| Stored sessions, hubs, chat UI | `crestapps-core-chat-interactions` |
| Speech-to-speech conversations | `crestapps-core-realtime-voice` |

## Provider Behavior Worth Knowing

- On OpenAI and Azure OpenAI, a request with function tools and a reasoning effort other than `None` is sent to the Responses API with `store: false`; everything else uses Chat Completions. The connection's `ChatApi` (`Auto`, `ChatCompletions`, `Responses`) controls it.
- A streaming call to a deployment that does not declare `streaming` is completed as one response and replayed as a single update.
- A model parameter value not exposed by the deployment is ignored; a missing or invalid stored value falls back to the deployment default.

## Upgrading from 1.x

- `ResolveOrDefaultAsync(AIDeploymentPurpose.Chat, ...)` becomes `ResolveSlotAsync(AIDeploymentSlotNames.Chat, ...)`; `AIDeploymentManagerExtensions.ResolveAsync(purpose, ...)` becomes `ResolveSlotOrThrowAsync(slotName, ...)`; `GetByPurposeAsync`/`GetAllByPurposeAsync` become `GetAllBySlotAsync`.
- `AIDeploymentPurpose`, `AIDeploymentType`, `AIDeployment.Purpose`, `SupportsPurpose` and `CanServeTextCompletion()` are removed. A realtime deployment can never fill the `chat` or `utility` slot.
- `AICompletionServiceBase.ResolveDeploymentAsync` takes a slot name instead of a purpose.
- Background completions should set `IsUtilityCompletion = true` so they run on the utility deployment with its own model parameters.
