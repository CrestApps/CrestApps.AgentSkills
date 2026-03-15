---
name: orchardcore-ai-response-handlers
description: Skill for implementing custom Chat Response Handlers in Orchard Core. Covers IChatResponseHandler, deferred and streaming handlers, webhook endpoints, live agent handoff, mid-conversation transfer via AI functions, UI notifications, and handler registration.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Chat Response Handlers - Prompt Templates

## Implement Custom Chat Response Handlers

You are an Orchard Core expert. Generate code for implementing custom chat response handlers that route chat prompts to external systems (live agent platforms, custom backends) instead of AI.

### Guidelines

- The `IChatResponseHandler` interface processes chat prompts and returns either a **streaming** result (immediate response) or a **deferred** result (response arrives later via webhook).
- Handlers are registered in `Startup.cs` using `services.TryAddEnumerable(ServiceDescriptor.Scoped<IChatResponseHandler, YourHandler>())`.
- When a session's `ResponseHandlerName` is `null` or empty, the built-in AI handler processes prompts.
- Custom response handlers are NOT supported in Conversation mode (`ChatMode.Conversation`). The resolver always returns the AI handler in Conversation mode.
- Deferred handlers return `ChatResponseHandlerResult.Deferred()` — the hub saves the user prompt and completes without an assistant message. The external system responds later via webhook.
- For deferred responses, create a webhook endpoint that writes the response to chat history and sends it to the client via SignalR.
- Reference `CrestApps.OrchardCore.AI.Chat.Core` (not the module projects) when resolving `IHubContext<AIChatHub>` or `IHubContext<ChatInteractionHub>`.
- Use `AIChatHub.GetSessionGroupName(sessionId)` and `ChatInteractionHub.GetInteractionGroupName(itemId)` for SignalR group names.
- For AI-function-based transfers, use `AIInvocationScope.Current` to access the active session or interaction.
- The hub automatically saves the session after AI response completes — do NOT call `SaveAsync` manually in transfer functions.
- Use `IChatNotificationSender` to send UI feedback (typing indicators, transfer status, session endings) — no JavaScript required.
- Register notification action handlers as keyed services: `services.AddKeyedScoped<IChatNotificationActionHandler, YourHandler>("your-action-name")`.
- Seal all service classes. Use `internal sealed` for implementations in modules.

### Handler Types

| Type | When to Use | Result |
|------|------------|--------|
| Streaming | External system returns response immediately | `ChatResponseHandlerResult.Stream(asyncEnumerable)` |
| Deferred | External system responds later via webhook | `ChatResponseHandlerResult.Deferred()` |

### Creating a Deferred Response Handler

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;

public sealed class GenesysResponseHandler : IChatResponseHandler
{
    public string Name => "Genesys";

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        var genesysClient = context.Services.GetRequiredService<IGenesysClient>();

        await genesysClient.SendMessageAsync(new GenesysMessage
        {
            SessionId = context.SessionId,
            ConnectionId = context.ConnectionId,
            ChatType = context.ChatType.ToString(),
            Text = context.Prompt,
        });

        return ChatResponseHandlerResult.Deferred();
    }
}
```

### Registering a Handler

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IChatResponseHandler, GenesysResponseHandler>());
    }
}
```

### Webhook for Deferred Responses (AI Chat Session)

```csharp
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;

internal static class WebhookEndpoint
{
    public static IEndpointRouteBuilder MapWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/agent/webhook", HandleAsync).AllowAnonymous().DisableAntiforgery();
        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        AgentWebhookPayload payload,
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore,
        IHubContext<AIChatHub> chatHubContext)
    {
        var session = await sessionManager.FindByIdAsync(payload.SessionId);

        if (session is null)
        {
            return TypedResults.NotFound();
        }

        var prompt = new AIChatSessionPrompt
        {
            ItemId = IdGenerator.GenerateId(),
            SessionId = session.SessionId,
            Role = ChatRole.Assistant,
            Content = payload.AgentMessage,
        };
        await promptStore.CreateAsync(prompt);

        var groupName = AIChatHub.GetSessionGroupName(session.SessionId);
        await chatHubContext.Clients.Group(groupName).SendAsync("ReceiveMessage", new
        {
            sessionId = session.SessionId,
            messageId = prompt.ItemId,
            content = payload.AgentMessage,
            role = "assistant",
        });

        return TypedResults.Ok();
    }
}
```

### Mid-Conversation Transfer via AI Function

```csharp
using System.Text.Json;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

public sealed class TransferToAgentFunction : AIFunction
{
    public const string TheName = "transfer_to_live_agent";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "queue_name": { "type": "string", "description": "The agent queue name." },
            "reason": { "type": "string", "description": "Why the user is being transferred." }
          },
          "required": ["queue_name"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;
    public override string Description => "Transfers the user to a live support agent.";
    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirstString("queue_name", out var queueName))
        {
            return "Unable to find a 'queue_name' argument.";
        }

        var invocationScope = AIInvocationScope.Current;

        if (invocationScope?.Items.TryGetValue(nameof(AIChatSession), out var sessionObj) == true
            && sessionObj is AIChatSession chatSession)
        {
            // Check Conversation mode — custom handlers not supported.
            var profileManager = arguments.Services.GetRequiredService<IAIProfileManager>();
            var profile = await profileManager.FindByIdAsync(chatSession.ProfileId);

            if (profile != null
                && profile.TryGetSettings<ChatModeProfileSettings>(out var settings)
                && settings.ChatMode == ChatMode.Conversation)
            {
                return "Transfer not available in Conversation mode.";
            }

            chatSession.ResponseHandlerName = "Genesys";
        }
        else if (invocationScope?.ToolExecutionContext?.Resource is ChatInteraction interaction)
        {
            interaction.ResponseHandlerName = "Genesys";
        }
        else
        {
            return "No active chat session found.";
        }

        return $"Transferring to '{queueName}' queue. Please wait...";
    }
}
```

Register the transfer function:

```csharp
using CrestApps.OrchardCore.AI.Core.Extensions;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<TransferToAgentFunction>(TransferToAgentFunction.TheName);
    }
}
```

### Sending UI Notifications from Webhooks

Use `IChatNotificationSender` to send typing indicators, transfer status, and session endings from webhooks. All extension methods that produce user-facing text accept an `IStringLocalizer` parameter to ensure messages are localized:

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

internal static class AgentEventEndpoints
{
    public static IEndpointRouteBuilder MapAgentEventEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/agent/typing", OnAgentTyping).AllowAnonymous().DisableAntiforgery();
        builder.MapPost("api/agent/transfer-started", OnTransferStarted).AllowAnonymous().DisableAntiforgery();
        builder.MapPost("api/agent/transfer-completed", OnTransferCompleted).AllowAnonymous().DisableAntiforgery();
        return builder;
    }

    private static async Task<IResult> OnAgentTyping(
        AgentTypingPayload payload,
        IChatNotificationSender notifications,
        IStringLocalizer<AgentEventEndpoints> localizer)
    {
        if (payload.IsTyping)
        {
            await notifications.ShowTypingAsync(
                payload.SessionId,
                ChatContextType.AIChatSession,
                localizer,
                payload.AgentName);
        }
        else
        {
            await notifications.HideTypingAsync(
                payload.SessionId,
                ChatContextType.AIChatSession);
        }

        return TypedResults.Ok();
    }

    private static async Task<IResult> OnTransferStarted(
        TransferPayload payload,
        IChatNotificationSender notifications,
        IStringLocalizer<AgentEventEndpoints> localizer)
    {
        await notifications.ShowTransferAsync(
            payload.SessionId,
            ChatContextType.AIChatSession,
            localizer,
            estimatedWaitTime: localizer["About 2 minutes"].Value,
            cancellable: true);

        return TypedResults.Ok();
    }

    private static async Task<IResult> OnTransferCompleted(
        TransferPayload payload,
        IChatNotificationSender notifications,
        IStringLocalizer<AgentEventEndpoints> localizer)
    {
        await notifications.HideTransferAsync(
            payload.SessionId,
            ChatContextType.AIChatSession);

        await notifications.ShowAgentConnectedAsync(
            payload.SessionId,
            ChatContextType.AIChatSession,
            localizer,
            payload.AgentName);

        return TypedResults.Ok();
    }
}
```

### Custom Notification Action Handler

Handle user-initiated actions on notification bubbles (e.g., feedback buttons):

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;

public sealed class FeedbackActionHandler : IChatNotificationActionHandler
{
    public async Task HandleAsync(
        ChatNotificationActionContext context,
        CancellationToken cancellationToken = default)
    {
        var feedbackService = context.Services.GetRequiredService<IFeedbackService>();
        await feedbackService.RecordAsync(context.SessionId, positive: true);

        var notifications = context.Services.GetRequiredService<IChatNotificationSender>();
        await notifications.RemoveAsync(context.SessionId, context.ChatType, context.NotificationId);
    }
}
```

### Custom Notification with Action Buttons

```csharp
await notifications.SendAsync(sessionId, ChatContextType.AIChatSession, new ChatNotification
{
    Id = "feedback-request",
    Type = "info",
    Content = "Was this helpful?",
    Icon = "fa-solid fa-star",
    Dismissible = true,
    Actions =
    [
        new ChatNotificationAction
        {
            Name = "feedback-positive",
            Label = "Yes!",
            CssClass = "btn-outline-success",
            Icon = "fa-solid fa-thumbs-up",
        },
        new ChatNotificationAction
        {
            Name = "feedback-negative",
            Label = "No",
            CssClass = "btn-outline-secondary",
            Icon = "fa-solid fa-thumbs-down",
        },
    ],
});
```

### Handler Context Properties

| Property | Type | Description |
|----------|------|-------------|
| `Prompt` | `string` | The user's message text |
| `ConnectionId` | `string` | The SignalR connection ID |
| `SessionId` | `string` | The session or interaction ID |
| `ChatType` | `ChatContextType` | `AIChatSession` or `ChatInteraction` |
| `ConversationHistory` | `IList<ChatMessage>` | Previous messages in the conversation |
| `Services` | `IServiceProvider` | Scoped service provider |
| `Profile` | `AIProfile` | The AI profile (for AI Chat Sessions) |
| `ChatSession` | `AIChatSession` | The chat session (for AI Chat Sessions) |
| `Interaction` | `ChatInteraction` | The interaction (for Chat Interactions) |

### Notification Extension Methods

All extension methods that produce user-facing text accept an `IStringLocalizer` parameter to ensure messages are localized.

| Method | Description |
|--------|-------------|
| `ShowTypingAsync(sessionId, chatType, localizer, agentName?)` | Shows a typing indicator |
| `HideTypingAsync(sessionId, chatType)` | Removes the typing indicator |
| `ShowTransferAsync(sessionId, chatType, localizer, message?, estimatedWaitTime?, cancellable?)` | Shows transfer status |
| `UpdateTransferAsync(sessionId, chatType, localizer, message?, estimatedWaitTime?, cancellable?)` | Updates transfer status |
| `HideTransferAsync(sessionId, chatType)` | Removes transfer indicator |
| `ShowAgentConnectedAsync(sessionId, chatType, localizer, agentName?, message?)` | Shows agent connected bubble |
| `HideAgentConnectedAsync(sessionId, chatType)` | Removes agent connected notification |
| `ShowConversationEndedAsync(sessionId, chatType, localizer, message?)` | Shows conversation ended bubble |
| `ShowSessionEndedAsync(sessionId, chatType, localizer, message?)` | Shows session ended bubble |

### Built-In Notification Action Handlers

| Action Name | Behavior |
|-------------|----------|
| `cancel-transfer` | Resets `ResponseHandlerName` to `null` (back to AI), removes transfer notification |
| `end-session` | Closes session (`Status = Closed`), shows session ended notification |

### Configuring Initial Response Handler

Via AI Profile settings:
```csharp
profile.AlterSettings<ResponseHandlerProfileSettings>(settings =>
{
    settings.InitialResponseHandlerName = "Genesys";
});
```

### SignalR Group Names

| Chat Type | Group Name Pattern |
|-----------|-------------------|
| AI Chat Session | `aichat-session-{sessionId}` |
| Chat Interaction | `chat-interaction-{itemId}` |
