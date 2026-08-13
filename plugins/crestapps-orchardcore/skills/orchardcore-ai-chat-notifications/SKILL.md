---
name: orchardcore-ai-chat-notifications
description: Skill for sending transient SignalR chat UI notifications in Orchard Core using the CrestApps AI Chat feature. Covers IChatNotificationSender, keyed IChatNotificationTransport implementations, keyed IChatNotificationActionHandler callbacks, notification models, built-in transfer and session actions, and custom notification delivery. Use this skill when requests mention Orchard Core Chat Notifications, AI Chat System Messages, typing indicators, transfer status, SignalR chat notifications, IChatNotificationSender, IChatNotificationActionHandler, or closely related CrestApps implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.AI.Chat, IChatNotificationSender, IChatNotificationTransport, IChatNotificationActionHandler, AIChatNotificationTransport, ChatInteractionNotificationTransport, ChatHubBase, ChatNotificationActionContext.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Chat Notifications - Prompt Templates

## Send Transient Chat UI Notifications

You are an Orchard Core expert. Generate C# integration code for transient, non-persistent system messages delivered to connected chat clients through SignalR. These notifications are an **AI Chat** feature and are distinct from response handlers and persisted chat messages.

### Guidelines
- Enable `CrestApps.OrchardCore.AI.Chat` to use the AI Chat session notification transport.
- Notifications are transient UI state. Do not use them for conversation history, durable audit records, or model prompt content.
- Inject `IChatNotificationSender` to send, update, and remove notifications.
- Route every request with the correct `ChatContextType`: `AIChatSession` for profile chat or `ChatInteraction` for ad-hoc interactions.
- The sender resolves `IChatNotificationTransport` as a keyed service using the chat context type.
- `AIChatNotificationTransport` broadcasts to the `AIChatHub` session group.
- `ChatInteractionNotificationTransport` broadcasts to the `ChatInteractionHub` interaction group when the interactions feature is enabled.
- Send a `ChatNotification` with a required type supplied to its constructor.
- A notification type identifies the UI style and replacement/removal target. Reusing a type replaces the active notification of that type.
- Use `SendAsync` for a new notification, `UpdateAsync` for a changed notification, and `RemoveAsync` to clear it.
- Register custom action callbacks as keyed `IChatNotificationActionHandler` services where the key exactly equals `ChatNotificationAction.Name`.
- `ChatHubBase.HandleNotificationAction` resolves the keyed handler in a child shell scope and passes a `ChatNotificationActionContext`.
- Localize UI text through `IStringLocalizer`; do not hard-code user-facing messages in reusable modules.
- Do not expose sensitive data in notification content, metadata, action names, or client-visible CSS classes.
- Install CrestApps packages in the web/startup project.

### Feature Overview

| Feature | Feature ID | Transport |
|---|---|---|
| AI Chat | `CrestApps.OrchardCore.AI.Chat` | `AIChatNotificationTransport` for `AIChatSession` |
| AI Chat Interactions | `CrestApps.OrchardCore.AI.Chat.Interactions` | `ChatInteractionNotificationTransport` for `ChatInteraction` |

### Install and Enable

Install the chat package in the web/startup project:

```shell
dotnet add package CrestApps.OrchardCore.AI.Chat
```

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat"
      ],
      "disable": []
    }
  ]
}
```

For notifications on ad-hoc chat interactions, also enable `CrestApps.OrchardCore.AI.Chat.Interactions`.

### Send and Remove a Typing Indicator

```csharp
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Localization;

namespace MyModule;

public sealed class AgentStatusNotifier
{
    private readonly IChatNotificationSender _notifications;
    private readonly IStringLocalizer T;

    public AgentStatusNotifier(
        IChatNotificationSender notifications,
        IStringLocalizer<AgentStatusNotifier> localizer)
    {
        _notifications = notifications;
        T = localizer;
    }

    public Task ShowTypingAsync(string sessionId)
    {
        return _notifications.SendAsync(
            sessionId,
            ChatContextType.AIChatSession,
            new ChatNotification(ChatNotificationTypes.Typing)
            {
                Content = T["An agent is typing"].Value,
                Icon = "fa-solid fa-ellipsis",
            });
    }

    public Task RemoveTypingAsync(string sessionId)
    {
        return _notifications.RemoveAsync(
            sessionId,
            ChatContextType.AIChatSession,
            ChatNotificationTypes.Typing);
    }
}
```

### Notification Model

| Member | Use |
|---|---|
| `Id` | Optional instance identifier for client targeting |
| `Type` | Required visual and replacement type set by the constructor |
| `Content` | Localized display text |
| `Icon` | Font Awesome icon class |
| `CssClass` | Additional container class |
| `Dismissible` | Shows a client dismissal control |
| `Actions` | Buttons that invoke server-side handlers |
| `Metadata` | Non-sensitive extensibility data sent to the client |

The chat UI applies `ai-chat-notification-{type}` in addition to base styling. Add theme CSS for custom types.

### Send a Transfer Notification

```csharp
await notifications.SendAsync(
    sessionId,
    ChatContextType.AIChatSession,
    new ChatNotification(ChatNotificationTypes.Transfer)
    {
        Content = T["Transferring you to a live agent."].Value,
        Icon = "fa-solid fa-headset",
        Actions =
        [
            new ChatNotificationAction
            {
                Name = ChatNotificationActionNames.CancelTransfer,
                Label = T["Cancel Transfer"].Value,
                CssClass = "btn-outline-danger",
                Icon = "fa-solid fa-xmark",
            },
        ],
    });
```

Update the same `Transfer` type when wait-time information changes. Remove it when transfer completes.

### Add a Custom Action

Define a handler for a custom action name:

```csharp
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace MyModule;

public sealed class FeedbackActionHandler : IChatNotificationActionHandler
{
    public Task HandleAsync(
        ChatNotificationActionContext context,
        CancellationToken cancellationToken = default)
    {
        var notifications = context.Services.GetRequiredService<IChatNotificationSender>();

        return notifications.RemoveAsync(
            context.SessionId,
            context.ChatType,
            context.NotificationType);
    }
}

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped<IChatNotificationActionHandler, FeedbackActionHandler>("feedback-positive");
    }
}
```

Send an action whose `Name` is exactly `feedback-positive`:

```csharp
await notifications.SendAsync(sessionId, ChatContextType.AIChatSession, new ChatNotification("feedback")
{
    Content = T["Was this helpful?"].Value,
    Dismissible = true,
    Actions =
    [
        new ChatNotificationAction
        {
            Name = "feedback-positive",
            Label = T["Yes"].Value,
            CssClass = "btn-outline-success",
        },
    ],
});
```

### Built-In Types and Actions

| Constant | Purpose |
|---|---|
| `ChatNotificationTypes.Typing` | Agent typing indicator |
| `ChatNotificationTypes.Transfer` | Live-agent transfer state |
| `ChatNotificationTypes.AgentConnected` | Agent-connected state |
| `ChatNotificationTypes.AgentReconnecting` | Reconnect warning |
| `ChatNotificationTypes.ConnectionLost` | Connection failure |
| `ChatNotificationTypes.ConversationEnded` | Conversation-ended state |
| `ChatNotificationTypes.SessionEnded` | Session-ended state |
| `ChatNotificationActionNames.CancelTransfer` | Reset response-handler routing and remove transfer notification |
| `ChatNotificationActionNames.EndSession` | Close the session and show session-ended state |

### Create a Custom Transport

Use a transport only when adding a new chat hub and context type. Implement `IChatNotificationTransport`, broadcast to the appropriate SignalR group, then register it keyed by the new `ChatContextType`.

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped<IChatNotificationTransport, MyChatNotificationTransport>(
            MyChatContextType);
    }
}
```

Do not replace the standard `AIChatNotificationTransport` or `ChatInteractionNotificationTransport` for ordinary profile and interaction notifications.

### Security Checklist

- Verify the current user may operate on the referenced session before initiating a notification from an endpoint.
- Keep notification metadata non-sensitive because all clients in the chat group receive it.
- Use keyed action handlers with unique, stable action names.
- Revalidate authorization inside handlers that mutate sessions, external systems, or user data.
- Prefer notifications for status feedback and response handlers for external conversation routing.
