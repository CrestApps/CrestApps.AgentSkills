# Chat Response Handlers - Complete Integration Example

## Full Genesys Live Agent Integration

This example shows a complete module that:
1. Registers a deferred response handler for Genesys
2. Creates an AI transfer function
3. Handles webhooks for agent messages
4. Sends UI notifications for typing, transfer status, and session endings

### Module Structure

```
MyModule.Genesys/
├── Services/
│   └── GenesysResponseHandler.cs
├── Tools/
│   └── TransferToAgentFunction.cs
├── Endpoints/
│   ├── GenesysWebhookEndpoint.cs
│   └── AgentEventEndpoint.cs
├── Models/
│   ├── GenesysWebhookPayload.cs
│   └── AgentEventPayload.cs
├── Manifest.cs
└── Startup.cs
```

### Startup.cs

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace MyModule.Genesys;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register the deferred response handler.
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IChatResponseHandler, GenesysResponseHandler>());

        // Register the AI transfer function.
        services.AddAITool<TransferToAgentFunction>(TransferToAgentFunction.TheName);
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapGenesysWebhookEndpoint();
        routes.MapAgentEventEndpoints();
    }
}
```

### GenesysResponseHandler.cs

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MyModule.Genesys.Services;

internal sealed class GenesysResponseHandler : IChatResponseHandler
{
    public string Name => "Genesys";

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        var genesysClient = context.Services.GetRequiredService<IGenesysClient>();

        // Forward user prompt to Genesys.
        await genesysClient.SendMessageAsync(new GenesysMessage
        {
            SessionId = context.SessionId,
            ConnectionId = context.ConnectionId,
            ChatType = context.ChatType.ToString(),
            Text = context.Prompt,
        });

        // Show typing indicator while agent processes.
        var notifications = context.Services.GetRequiredService<IChatNotificationSender>();
        await notifications.ShowTypingAsync(
            context.SessionId,
            context.ChatType);

        // Deferred: hub completes without an assistant response.
        return ChatResponseHandlerResult.Deferred();
    }
}
```

### GenesysWebhookEndpoint.cs

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;

namespace MyModule.Genesys.Endpoints;

internal static class GenesysWebhookEndpoint
{
    public static IEndpointRouteBuilder MapGenesysWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/genesys/webhook", HandleAsync).AllowAnonymous().DisableAntiforgery();
        return builder;
    }

    private static async Task<IResult> HandleAsync(
        GenesysWebhookPayload payload,
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore,
        IHubContext<AIChatHub> hubContext,
        IChatNotificationSender notifications)
    {
        // TODO: Validate webhook signature.

        var session = await sessionManager.FindByIdAsync(payload.SessionId);

        if (session is null)
        {
            return TypedResults.NotFound();
        }

        // Hide typing indicator since agent responded.
        await notifications.HideTypingAsync(
            session.SessionId,
            ChatContextType.AIChatSession);

        // Save the agent's response.
        var prompt = new AIChatSessionPrompt
        {
            ItemId = IdGenerator.GenerateId(),
            SessionId = session.SessionId,
            Role = ChatRole.Assistant,
            Content = payload.AgentMessage,
        };
        await promptStore.CreateAsync(prompt);

        // Notify connected clients.
        var groupName = AIChatHub.GetSessionGroupName(session.SessionId);
        await hubContext.Clients.Group(groupName).SendAsync("ReceiveMessage", new
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

### AgentEventEndpoint.cs (Typing, Transfer, Session End Events)

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyModule.Genesys.Endpoints;

internal static class AgentEventEndpoint
{
    public static IEndpointRouteBuilder MapAgentEventEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/genesys/typing", OnTyping).AllowAnonymous().DisableAntiforgery();
        builder.MapPost("api/genesys/transfer", OnTransfer).AllowAnonymous().DisableAntiforgery();
        builder.MapPost("api/genesys/transfer-update", OnTransferUpdate).AllowAnonymous().DisableAntiforgery();
        builder.MapPost("api/genesys/session-end", OnSessionEnd).AllowAnonymous().DisableAntiforgery();
        return builder;
    }

    private static async Task<IResult> OnTyping(
        AgentTypingPayload payload,
        IChatNotificationSender notifications)
    {
        if (payload.IsTyping)
        {
            await notifications.ShowTypingAsync(
                payload.SessionId,
                ChatContextType.AIChatSession,
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

    private static async Task<IResult> OnTransfer(
        TransferPayload payload,
        IChatNotificationSender notifications)
    {
        await notifications.ShowTransferAsync(
            payload.SessionId,
            ChatContextType.AIChatSession,
            message: $"Transferring you to {payload.QueueName}...",
            estimatedWaitTime: payload.EstimatedWait,
            cancellable: true);

        return TypedResults.Ok();
    }

    private static async Task<IResult> OnTransferUpdate(
        TransferPayload payload,
        IChatNotificationSender notifications)
    {
        await notifications.UpdateTransferAsync(
            payload.SessionId,
            ChatContextType.AIChatSession,
            message: "Still waiting for an available agent...",
            estimatedWaitTime: payload.EstimatedWait,
            cancellable: true);

        return TypedResults.Ok();
    }

    private static async Task<IResult> OnSessionEnd(
        SessionEndPayload payload,
        IChatNotificationSender notifications)
    {
        await notifications.ShowSessionEndedAsync(
            payload.SessionId,
            ChatContextType.AIChatSession,
            "The agent has ended this session. Thank you!");

        return TypedResults.Ok();
    }
}
```

### TransferToAgentFunction.cs

```csharp
using System.Text.Json;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace MyModule.Genesys.Tools;

public sealed class TransferToAgentFunction : AIFunction
{
    public const string TheName = "transfer_to_live_agent";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "queue_name": { "type": "string", "description": "Agent queue name." },
            "reason": { "type": "string", "description": "Transfer reason." }
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
            return "Missing 'queue_name' argument.";
        }

        var invocationScope = AIInvocationScope.Current;

        if (invocationScope?.Items.TryGetValue(nameof(AIChatSession), out var obj) == true
            && obj is AIChatSession chatSession)
        {
            // Reject transfer in Conversation mode.
            var profileManager = arguments.Services.GetRequiredService<IAIProfileManager>();
            var profile = await profileManager.FindByIdAsync(chatSession.ProfileId);

            if (profile != null
                && profile.TryGetSettings<ChatModeProfileSettings>(out var settings)
                && settings.ChatMode == ChatMode.Conversation)
            {
                return "Transfer not available in Conversation mode.";
            }

            chatSession.ResponseHandlerName = "Genesys";

            // Show transfer notification with cancel button.
            var notifications = arguments.Services.GetRequiredService<IChatNotificationSender>();
            await notifications.ShowTransferAsync(
                chatSession.SessionId,
                ChatContextType.AIChatSession,
                cancellable: true);
        }
        else if (invocationScope?.ToolExecutionContext?.Resource is ChatInteraction interaction)
        {
            interaction.ResponseHandlerName = "Genesys";

            var notifications = arguments.Services.GetRequiredService<IChatNotificationSender>();
            await notifications.ShowTransferAsync(
                interaction.ItemId,
                ChatContextType.ChatInteraction,
                cancellable: true);
        }
        else
        {
            return "No active chat session found.";
        }

        return $"Transferring to '{queueName}' queue. Please wait...";
    }
}
```
