---
name: crestapps-core-response-handlers
description: Skill for routing CrestApps.Core 2.0 chat messages through IChatResponseHandler implementations instead of always calling the AI. Covers the built-in AI handler (ChatResponseHandlerNames.AI), streaming and deferred results with ChatResponseHandlerResult, ChatResponseHandlerContext members, registering custom webhook or live-agent handlers, IChatResponseHandlerResolver selection by session or interaction handler name and chat mode, ResponseHandlerProfileSettings for profile-created sessions, the conversation-mode fallback to the AI handler, and how the AI handler sets AIInvocationScope before building the orchestration context. Use it when handing chats to a live agent, a webhook or an external platform, or when a custom handler is ignored.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Response Handlers

## Implement Response Handlers

Implement `IChatResponseHandler` when a message should not always use the default AI orchestration path. A handler returns either `ChatResponseHandlerResult.Streaming(...)` with an `IAsyncEnumerable<ChatResponseUpdate>` or `ChatResponseHandlerResult.Deferred()` when a later callback, background task, or relay will deliver the response.

`ChatResponseHandlerContext` exposes `Prompt`, `ConnectionId`, `SessionId`, `ChatType`, `ConversationHistory`, `Services`, `Profile`, `ChatSession`, `Interaction`, `AssistantAppearance`, and `Properties`. `Profile` and `ChatSession` are populated only for `AIChatSession`; `Interaction` is populated only for `ChatInteraction`.

## Deferred Webhook Example

```csharp
using System.Net.Http.Json;
using CrestApps.Core.AI.ResponseHandling;

public sealed class WebhookResponseHandler(IHttpClientFactory httpClientFactory) : IChatResponseHandler
{
    public string Name => "webhook";

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "https://example.com/chat",
            new
            {
                context.SessionId,
                context.ConnectionId,
                context.ChatType,
                prompt = context.Prompt,
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return ChatResponseHandlerResult.Deferred();
    }
}
```

Register the handler as scoped:

```csharp
builder.Services.AddScoped<IChatResponseHandler, WebhookResponseHandler>();
```

## Handler Selection

`IChatResponseHandlerResolver.Resolve(handlerName, chatMode)` selects by `AIChatSession.ResponseHandlerName` or `ChatInteraction.ResponseHandlerName`; `GetAll()` lists every registered handler. For a profile-created session, set `ResponseHandlerProfileSettings.InitialResponseHandlerName` (profile templates carry it as `ProfileTemplateMetadata.InitialResponseHandlerName`); the active session can change its handler name later. An empty or unknown name falls back to the built-in handler named `ChatResponseHandlerNames.AI` with a warning. `ChatMode.Conversation` always uses the AI handler, even when a custom name is requested, because voice conversations need the orchestration pipeline.

```csharp
profile.WithSettings(new ResponseHandlerProfileSettings
{
    InitialResponseHandlerName = "webhook",
});
```

## The Built-in AI Handler

`AIChatResponseHandler` builds the orchestration context for the profile or interaction and streams the orchestrator's output. It stores the chat interaction, the chat session and the `AIChatSession` item in `AIInvocationScope.Current` **before** building the context, so context handlers and `IAIProfileProvider`s can read the active chat, and adds the completion context and data source afterwards. A custom handler that runs the orchestrator itself should do the same with `AIInvocationScope.Begin()`.

Use a unique, stable handler name and make deferred handlers responsible for their own eventual delivery. Do not return a made-up handled result: the supported result factories are `Streaming(...)` and `Deferred()`.

## Related skills

- Use `orchardcore-ai-response-handlers` for the Orchard Core module wrapper (admin-configured chat sessions and chat interactions) built on this same `IChatResponseHandler` contract.
- Use `crestapps-core-external-relays` for the `IExternalChatRelay` contract used by deferred, relay-backed handlers.

## Upgrading from 1.x

- `ChatMode.Realtime` is gone. `Resolve` takes a `ChatMode`, and only `Conversation` forces the AI handler.
- The supported results remain `ChatResponseHandlerResult.Streaming(...)` and `Deferred()`; there is no `Handled`, `Transferred` or `NotHandled` result, whatever older docs show.
