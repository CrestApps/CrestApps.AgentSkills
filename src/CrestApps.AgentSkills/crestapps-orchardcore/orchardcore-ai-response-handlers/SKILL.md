---
name: orchardcore-ai-response-handlers
description: Skill for implementing CrestApps chat response handlers in Orchard Core. Covers IChatResponseHandler, streaming and deferred results, external relay contracts, notifications, and handler registration. Use this skill when requests mention Orchard Core Chat Response Handlers, IChatResponseHandler, ChatResponseHandlerResult, streaming responses, deferred chat replies, IExternalChatRelay, ChatNotification, or live-agent handoff.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Chat Response Handlers

## Route a chat prompt to a handler

`IChatResponseHandler` is a shared CrestApps.Core contract used by Orchard chat
sessions and chat interactions. A handler either returns response updates now or
defers the assistant response until an external system supplies it.

```csharp
using CrestApps.Core.AI.ResponseHandling;

namespace MyCompany.OrchardCore.Chat;

public sealed class LiveAgentResponseHandler : IChatResponseHandler
{
    public string Name => "LiveAgent";

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        await SendToAgentSystemAsync(context.Prompt, cancellationToken);

        return ChatResponseHandlerResult.Deferred();
    }

    private static Task SendToAgentSystemAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

Register a custom handler as an enumerable scoped service:

```csharp
using CrestApps.Core.AI.ResponseHandling;
using Microsoft.Extensions.DependencyInjection;

services.TryAddEnumerable(
    ServiceDescriptor.Scoped<IChatResponseHandler, LiveAgentResponseHandler>());
```

The active handler is selected from `AIChatSession.ResponseHandlerName` or
`ChatInteraction.ResponseHandlerName`. The built-in AI path handles the normal
case. Do not claim a custom handler is selected for a Conversation-mode session
without checking the active chat feature's resolver behavior.

## Return streaming or deferred output

Use `Deferred()` only when the handler has handed work to an external process
that will deliver the reply later. The hub persists the user prompt and does
not wait for an assistant response.

For immediate output, return an async sequence of `ChatResponseUpdate` values:

```csharp
using CrestApps.Core.AI.ResponseHandling;
using Microsoft.Extensions.AI;

return ChatResponseHandlerResult.Streaming(StreamUpdatesAsync(cancellationToken));

static async IAsyncEnumerable<ChatResponseUpdate> StreamUpdatesAsync(
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
{
    yield return new ChatResponseUpdate(ChatRole.Assistant, "Connecting you to an agent.");
    await Task.CompletedTask;
}
```

The factory is `ChatResponseHandlerResult.Streaming(...)`, not `Stream(...)`.
Use a deferred handler only with an authenticated, idempotent callback or relay
strategy; a successful handoff does not make an arbitrary anonymous webhook
safe.

## Send transient chat notifications

`IChatNotificationSender` sends UI-only notifications to the appropriate
SignalR clients. A notification is identified by its required `Type`; there is
no `ChatNotification.Id` property.

```csharp
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;

await notifications.SendAsync(
    sessionId,
    ChatContextType.AIChatSession,
    new ChatNotification(ChatNotificationTypes.Transfer)
    {
        Content = "Transferring you to a live agent.",
        Icon = "fa-solid fa-headset",
        Dismissible = true,
    });

await notifications.RemoveAsync(
    sessionId,
    ChatContextType.AIChatSession,
    ChatNotificationTypes.Transfer);
```

`SendAsync` replaces an active notification with the same type.
`UpdateAsync` updates only an existing matching type, and `RemoveAsync` accepts
the notification type string. Use `ChatNotificationTypes` and
`ChatNotificationActionNames` for built-in values. Register a custom action
handler as a keyed `IChatNotificationActionHandler`.

## Use the external relay contracts exactly

For persistent WebSocket, SSE, gRPC, or queue-based handoff, implement
`IExternalChatRelay`. It is protocol agnostic, but its signatures are fixed:

```csharp
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;

namespace MyCompany.OrchardCore.Chat;

public sealed class LiveAgentRelay : IExternalChatRelay
{
    public Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task ConnectAsync(
        ExternalChatRelayContext context,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendPromptAsync(string text, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendSignalAsync(
        string signalName,
        IDictionary<string, string> data = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

The singleton `IExternalChatRelayManager` owns connection lifetime. Create or
reuse a relay with:

```csharp
var relay = await relayManager.GetOrCreateAsync(
    context.SessionId,
    new ExternalChatRelayContext
    {
        SessionId = context.SessionId,
        ChatType = context.ChatType,
    },
    () => new LiveAgentRelay(),
    cancellationToken);

await relay.SendPromptAsync(context.Prompt, cancellationToken);
```

The relay event callback is also identifier-based, not context-based:

```csharp
await relayEventHandler.HandleEventAsync(
    sessionId,
    ChatContextType.AIChatSession,
    relayEvent,
    cancellationToken);
```

Register keyed `IExternalChatRelayNotificationBuilder` services for custom
event types. The default event handler selects the builder by
`ExternalChatRelayEvent.EventType`.

## Transfer through an AI tool

When an AI tool changes the response-handler selection, implement a real
`AITool` and register it with the current Core API:

```csharp
services.AddCoreAITool<TransferToLiveAgentTool>("transfer_to_live_agent");
```

Do not use the removed `AddAITool` registration name. Keep the tool's
authorization, input validation, and response-handler mutation in the same
tenant scope as the session or interaction being changed.

## Operational checklist

- Make external callbacks authenticate and authorize their session or
  interaction target before writing prompts or emitting notifications.
- Make callback and relay delivery idempotent because external systems retry.
- Keep `ChatResponseHandlerResult.Streaming` updates ordered and cancellation
  aware.
- Close relays through `IExternalChatRelayManager.CloseAsync` when a session
  ends; it disconnects and disposes the registered relay.
- Do not put provider tokens or callback secrets in browser code.

## Related skills

- Use `crestapps-core-response-handlers` for the underlying CrestApps.Core `IChatResponseHandler` contract outside Orchard Core.
- Use `crestapps-core-external-relays` for the underlying CrestApps.Core `IExternalChatRelay` and relay-manager contracts.
- Use `orchardcore-ai-tools` for Orchard-aware tool registration, selection,
  and authorization.
- Use `orchardcore-ai-workflows` for AI completion workflow activities.
- Use `orchardcore-ai-chat-interactions` for the interaction UI and its
  capability configuration.
