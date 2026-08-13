---
name: crestapps-core-response-handlers
description: Skill for routing CrestApps.Core chat responses through AI, deferred webhook, relay, and custom handlers.
---

# CrestApps.Core Response Handlers - Prompt Templates

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

The resolver selects by `AIChatSession.ResponseHandlerName` or `ChatInteraction.ResponseHandlerName`. For a profile-created session, set `ResponseHandlerProfileSettings.InitialResponseHandlerName`; the active session can change its handler name later. An empty or unknown name falls back to the built-in `AI` handler. Conversation mode always uses that AI handler, even when a custom name is requested.

Use a unique, stable handler name and make deferred handlers responsible for their own eventual delivery. Do not return a made-up handled result: the supported result factories are `Streaming(...)` and `Deferred()`.

## Related skills

- Use `orchardcore-ai-response-handlers` for the Orchard Core module wrapper (admin-configured chat sessions and chat interactions) built on this same `IChatResponseHandler` contract.
- Use `crestapps-core-external-relays` for the `IExternalChatRelay` contract used by deferred, relay-backed handlers.
