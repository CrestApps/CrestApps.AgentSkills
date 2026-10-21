---
name: crestapps-core-external-relays
description: Skill for implementing and managing persistent external chat relays in CrestApps.Core 2.0 using IExternalChatRelay, IExternalChatRelayManager and its ExternalChatRelayConnectionManager singleton, ExternalChatRelayContext, IExternalChatRelayEventHandler, IExternalChatRelayNotificationHandler, IExternalChatRelayNotificationBuilder and ExternalChatRelayEventTypes. Use it when bridging AI chat sessions or chat interactions to a live-agent or other external chat platform through a deferred response handler.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core External Chat Relays

## Scope

`IExternalChatRelay` is the contract for a persistent, bidirectional connection to a live-agent or other external chat platform. An implementation supplies connection, prompt, signal, disconnect, and async-disposal behavior; the transport is application-defined.

The core packages ship no `IExternalChatRelay` implementation, only the manager: `ExternalChatRelayConnectionManager` is registered by `AddCoreAIOrchestration()` as the singleton `IExternalChatRelayManager` and tracks relay instances by session ID. Its members are `GetOrCreateAsync(sessionId, context, factory, cancellationToken)`, `Get(sessionId)` and `CloseAsync(sessionId, cancellationToken)`.

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

An integration that needs event routing must implement and register its own `IExternalChatRelayEventHandler` (`HandleEventAsync`) and `IExternalChatRelayNotificationHandler` (`HandleAsync`). `IExternalChatRelayNotificationBuilder` exposes `NotificationType` and `Build(relayEvent, notification, result, T)`; nothing in the core packages resolves builders, so your notification handler decides how to pick one, for example by matching `NotificationType` to `ExternalChatRelayEvent.EventType`. `ExternalChatRelayEventTypes` supplies constants such as `agent-typing`, `agent-stopped-typing`, `agent-connected`, `agent-disconnected`, `agent-reconnecting` and `connection-lost`; integrations may add custom strings. `ExternalChatRelayEvent` carries `EventType`, `Content`, `AgentName` and `Metadata`.

Keep platform credentials and reconnection policy inside the relay implementation. Call `CloseAsync` when the corresponding session ends so the manager disconnects and disposes the relay.

## Related skills

- Use `crestapps-core-response-handlers` for the `IChatResponseHandler` contract that a deferred relay-backed handler typically pairs with.
- Use `orchardcore-ai-response-handlers` for the Orchard Core module wrapper that consumes this relay contract in chat sessions and chat interactions.

## Upgrading from 1.x

- The 2.0 release notes list no relay changes. `ExternalChatRelayContext` still carries only `SessionId` and `ChatType`; keep platform credentials in the relay implementation.
- Older samples that read an API key from the relay context or returned `Handled`/`Transferred` results from the response handler do not match the current contracts.
