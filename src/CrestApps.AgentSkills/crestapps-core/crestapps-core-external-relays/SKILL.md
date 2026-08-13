---
name: crestapps-core-external-relays
description: Skill for implementing and managing persistent external chat relays in CrestApps.Core using IExternalChatRelay, IExternalChatRelayManager, ExternalChatRelayConnectionManager, IExternalChatRelayEventHandler, IExternalChatRelayNotificationHandler, and IExternalChatRelayNotificationBuilder.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core External Chat Relays

## Scope

`IExternalChatRelay` is the contract for a persistent, bidirectional connection to a live-agent or other external chat platform. An implementation supplies connection, prompt, signal, disconnect, and async-disposal behavior; the transport is application-defined.

`IExternalChatRelayManager` is the only relay implementation shipped by the current core package. `ExternalChatRelayConnectionManager` is registered as a singleton and tracks relay instances by session ID.

## Connect and Close a Relay

Create the relay in the factory passed to the manager. The manager connects it, disposes a failed connection attempt, and disposes an extra instance if a concurrent caller won the same session ID.

```csharp
var relay = await relayManager.GetOrCreateAsync(
    sessionId,
    new ExternalChatRelayContext
    {
        SessionId = sessionId,
        ChatType = ChatContextType.AIChatSession,
    },
    () => new MyExternalChatRelay(),
    cancellationToken);

await relay.SendPromptAsync(prompt, cancellationToken);
await relayManager.CloseAsync(sessionId, cancellationToken);
```

`MyExternalChatRelay` must implement `IExternalChatRelay`. The manager does not construct relays or choose a platform.

## Event and Notification Integration

The current packages ship no default `IExternalChatRelayEventHandler`, `IExternalChatRelayNotificationHandler`, or `IExternalChatRelayNotificationBuilder` implementation. Consequently, registering a relay alone does not route incoming external events into chat messages or notifications.

An integration that needs event routing must implement and register its own event handler and notification handler. It can register keyed `IExternalChatRelayNotificationBuilder` services keyed by `ExternalChatRelayEvent.EventType`. `ExternalChatRelayEventTypes` supplies string constants for common event names, while integrations may use custom strings.

Keep platform credentials and reconnection policy inside the relay implementation. Call `CloseAsync` when the corresponding session ends so the manager disconnects and disposes the relay.

## Related skills

- Use `crestapps-core-response-handlers` for the `IChatResponseHandler` contract that a deferred relay-backed handler typically pairs with.
- Use `orchardcore-ai-response-handlers` for the Orchard Core module wrapper that consumes this relay contract in chat sessions and chat interactions.
