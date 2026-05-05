---
name: crestapps-core-external-relays
description: Skill for building persistent bidirectional relay connections to external live-agent platforms in CrestApps.Core using IExternalChatRelay and the relay manager, event handler, and notification pipeline.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core External Chat Relays

## Overview

External Chat Relays provide persistent bidirectional connections between your AI chat system and external live-agent platforms (e.g., customer service desks, human escalation systems). Unlike webhook-based integrations where the external system calls back into your application, relays maintain a persistent connection so that events such as typing indicators, agent-connected notifications, wait-time updates, and chat messages flow in real time without polling.

The relay system is protocol-agnostic. Implementations can use WebSocket, SSE (Server-Sent Events), gRPC streaming, WebRTC data channels, message queues, or any other transport.

## Key Interfaces

| Interface | Purpose |
|---|---|
| `IExternalChatRelay` | Persistent connection to an external system. Methods for connect, disconnect, send prompts, and send signals. Extends `IAsyncDisposable`. |
| `IExternalChatRelayManager` | Singleton that tracks active relay instances by session ID. Creates, retrieves, and closes relays. |
| `IExternalChatRelayEventHandler` | Routes incoming relay events to keyed notification builders by event type. |
| `IExternalChatRelayNotificationBuilder` | Keyed scoped service that builds a `ChatNotification` for a specific event type. |
| `IExternalChatRelayNotificationHandler` | Sends or removes notifications produced by the builder. |

## Event Flow

1. External system sends event to relay connection
2. `IExternalChatRelayEventHandler.HandleEventAsync` receives the `ExternalChatRelayEvent`
3. Handler resolves a keyed `IExternalChatRelayNotificationBuilder` by `EventType`
4. Builder populates a `ChatNotification` and `ExternalChatRelayNotificationResult`
5. `IExternalChatRelayNotificationHandler.HandleAsync` removes old notifications and sends new ones

## Well-Known Event Types

Use constants from `ExternalChatRelayEventTypes`:

| Constant | Value | Description |
|---|---|---|
| `AgentTyping` | `agent-typing` | Agent is typing a response |
| `AgentStoppedTyping` | `agent-stopped-typing` | Agent stopped typing |
| `AgentConnected` | `agent-connected` | Live agent joined the session |
| `AgentDisconnected` | `agent-disconnected` | Live agent left the session |
| `AgentReconnecting` | `agent-reconnecting` | Agent reconnecting after disruption |
| `ConnectionLost` | `connection-lost` | Connection to external system lost |
| `ConnectionRestored` | `connection-restored` | Connection restored after loss |
| `Message` | `message` | External system sent a chat message |
| `WaitTimeUpdated` | `wait-time-updated` | Estimated wait time updated |
| `SessionEnded` | `session-ended` | External session ended |

Event types are strings (not enums) so third-party integrations can define custom types without modifying the framework.

## Implement a Custom Relay

Create a class that implements `IExternalChatRelay`. Handle connection establishment, prompt forwarding, signal sending, and graceful disconnection.

```csharp
public sealed class LiveAgentRelay : IExternalChatRelay
{
    private readonly IExternalChatRelayEventHandler _eventHandler;
    private WebSocket _socket;
    private ExternalChatRelayContext _context;

    public LiveAgentRelay(IExternalChatRelayEventHandler eventHandler)
    {
        _eventHandler = eventHandler;
    }

    public Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_socket?.State == WebSocketState.Open);
    }

    public async Task ConnectAsync(ExternalChatRelayContext context, CancellationToken cancellationToken = default)
    {
        _context = context;
        _socket = new ClientWebSocket();
        await ((ClientWebSocket)_socket).ConnectAsync(
            new Uri("wss://live-agent.example.com/relay"),
            cancellationToken);

        // Start background listener for incoming events.
        _ = ListenForEventsAsync(cancellationToken);
    }

    public async Task SendPromptAsync(string text, CancellationToken cancellationToken = default)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    public async Task SendSignalAsync(
        string signalName,
        IDictionary<string, string> data = null,
        CancellationToken cancellationToken = default)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new { signal = signalName, data });
        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_socket?.State == WebSocketState.Open)
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket != null)
        {
            _socket.Dispose();
            _socket = null;
        }
    }

    private async Task ListenForEventsAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        while (_socket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await _socket.ReceiveAsync(buffer, cancellationToken);
            var json = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
            var relayEvent = System.Text.Json.JsonSerializer.Deserialize<ExternalChatRelayEvent>(json);

            if (relayEvent != null)
            {
                await _eventHandler.HandleEventAsync(
                    _context.SessionId,
                    _context.ChatType,
                    relayEvent,
                    cancellationToken);
            }
        }
    }
}
```

## Connection Lifecycle Management

The `IExternalChatRelayManager` (registered as a singleton) manages relay instances using a thread-safe concurrent dictionary keyed by session ID.

### Get or Create a Relay

Use `GetOrCreateAsync` with a factory function. The manager checks for an existing connected relay before creating a new one and handles race conditions between concurrent callers.

```csharp
public sealed class ChatRelayService
{
    private readonly IExternalChatRelayManager _relayManager;
    private readonly IExternalChatRelayEventHandler _eventHandler;

    public ChatRelayService(
        IExternalChatRelayManager relayManager,
        IExternalChatRelayEventHandler eventHandler)
    {
        _relayManager = relayManager;
        _eventHandler = eventHandler;
    }

    public async Task<IExternalChatRelay> ConnectToLiveAgentAsync(
        string sessionId,
        ChatContextType chatType,
        CancellationToken cancellationToken)
    {
        var context = new ExternalChatRelayContext
        {
            SessionId = sessionId,
            ChatType = chatType,
        };

        return await _relayManager.GetOrCreateAsync(
            sessionId,
            context,
            () => new LiveAgentRelay(_eventHandler),
            cancellationToken);
    }
}
```

### Close a Relay

Call `CloseAsync` when the session ends. The manager gracefully disconnects and disposes the relay.

```csharp
await relayManager.CloseAsync(sessionId, cancellationToken);
```

## Custom Notification Builder

Register a keyed `IExternalChatRelayNotificationBuilder` to handle specific event types. The key must match the event type string.

```csharp
public sealed class AgentConnectedNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public string NotificationType => "agent-connected-notice";

    public void Build(
        ExternalChatRelayEvent relayEvent,
        ChatNotification notification,
        ExternalChatRelayNotificationResult result,
        IStringLocalizer T)
    {
        notification.Message = T["Agent {0} has joined the conversation.", relayEvent.AgentName ?? "Support"];

        // Remove any pending transfer or wait-time notifications.
        result.RemoveNotificationTypes.Add("transfer");
        result.RemoveNotificationTypes.Add("wait-time");
    }
}
```

### Register the Builder

Use keyed service registration with the event type as the key:

```csharp
services.AddKeyedScoped<IExternalChatRelayNotificationBuilder, AgentConnectedNotificationBuilder>(
    ExternalChatRelayEventTypes.AgentConnected);
```

## Models

### ExternalChatRelayContext

Provides the session identity for establishing a relay connection.

```csharp
var context = new ExternalChatRelayContext
{
    SessionId = "session-123",
    ChatType = ChatContextType.AIChatSession,
};
```

### ExternalChatRelayEvent

Represents an event from the external system with `EventType`, `Content`, `AgentName`, and extensible `Metadata`.

```csharp
var relayEvent = new ExternalChatRelayEvent
{
    EventType = ExternalChatRelayEventTypes.Message,
    Content = "Hello, how can I help you?",
    AgentName = "Sarah",
    Metadata = new Dictionary<string, string>
    {
        ["department"] = "billing",
    },
};
```

### ExternalChatRelayNotificationResult

Describes which notifications to remove and which to send. Set `IsUpdate` to `true` to replace an existing notification instead of adding a new one.

## Service Registration

The default `ExternalChatRelayConnectionManager` is registered as a singleton:

```csharp
services.TryAddSingleton<IExternalChatRelayManager, ExternalChatRelayConnectionManager>();
```
