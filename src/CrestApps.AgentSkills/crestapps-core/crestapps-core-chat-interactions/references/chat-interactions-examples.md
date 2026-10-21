# Chat Interactions Examples

## AI Chat Session Hub

`AIChatHubCore<TClient>` serves profile-based sessions (chat pages and the floating widget). It is not decorated with `[Authorize]`, and `AuthorizeProfileAsync` returns `true` by default, so an anonymous-capable hub must decide per profile.

```csharp
using CrestApps.Core.AI.Chat.Hubs;
using CrestApps.Core.AI.Chat.Services;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.ResponseHandling;
using CrestApps.Core.AI.Security;
using Microsoft.AspNetCore.Authorization;

namespace MyApp.Hubs;

[AllowAnonymous]
public sealed class AIChatHub : AIChatHubCore<IAIChatHubClient>
{
    private static readonly HashSet<string> _publicProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "support-widget",
    };

    public AIChatHub(
        IServiceProvider services,
        TimeProvider timeProvider,
        ILogger<AIChatHub> logger)
        : base(services, timeProvider, logger)
    {
    }

    protected override Task<bool> AuthorizeProfileAsync(IServiceProvider services, AIProfile profile)
    {
        if (_publicProfiles.Contains(profile.Name))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(Context.User?.Identity?.IsAuthenticated == true);
    }

    protected override string GetRateLimitedMessage(PromptSecurityResult result)
        => "You are sending messages too quickly. Please wait a moment and try again.";

    protected override void CollectStreamingReferences(
        IServiceProvider services,
        ChatResponseHandlerContext handlerContext,
        Dictionary<string, AICompletionReference> references,
        HashSet<string> contentItemIds)
    {
        var citationCollector = services.GetRequiredService<CitationReferenceCollector>();

        if (handlerContext.Properties.TryGetValue("OrchestrationContext", out var value) &&
            value is OrchestrationContext orchestrationContext)
        {
            citationCollector.CollectPreemptiveReferences(orchestrationContext, references, contentItemIds);
            handlerContext.Properties.Remove("OrchestrationContext");
        }

        citationCollector.CollectToolReferences(references, contentItemIds);
    }
}
```

Register its hub options separately, because `HubOptions<THub>` is per hub type:

```csharp
builder.Services.ConfigureCrestAppsChatHubOptions<AIChatHub>();
app.MapHub<AIChatHub>("/hubs/ai-chat");
```

## A Custom Interaction Setting

`IChatInteractionSettingsHandler` lets a feature read its own keys from the `SaveSettings` payload and store them on the interaction. `UpdatingAsync` runs before the interaction is saved; `UpdatedAsync` runs after.

```csharp
using System.Text.Json;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;

namespace MyApp.ChatInteractions;

public sealed class AudienceChatInteractionMetadata
{
    public string Audience { get; set; }
}

public sealed class AudienceChatInteractionSettingsHandler : IChatInteractionSettingsHandler
{
    private static readonly string[] _allowedAudiences = ["internal", "customer", "partner"];

    public Task UpdatingAsync(ChatInteraction interaction, JsonElement settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        if (settings.ValueKind != JsonValueKind.Object ||
            !settings.TryGetProperty("audience", out var audience) ||
            audience.ValueKind != JsonValueKind.String)
        {
            return Task.CompletedTask;
        }

        var value = audience.GetString()?.Trim();

        interaction.Alter<AudienceChatInteractionMetadata>(metadata =>
        {
            metadata.Audience = _allowedAudiences.Contains(value, StringComparer.OrdinalIgnoreCase)
                ? value
                : null;
        });

        return Task.CompletedTask;
    }

    public Task UpdatedAsync(ChatInteraction interaction, JsonElement settings, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

```csharp
builder.Services.AddScoped<IChatInteractionSettingsHandler, AudienceChatInteractionSettingsHandler>();
```

Read the value later with `interaction.TryGet<AudienceChatInteractionMetadata>(out var metadata)`, for example in an `IAICompletionContextBuilderHandler` that adjusts the system message.

## Per-User Tool Permissions

Chat Interactions re-check every listable tool at send time. Replace the allow-all default to enforce your own rules:

```csharp
using System.Security.Claims;
using CrestApps.Core.AI.Tooling;

namespace MyApp.Security;

public sealed class RoleBasedToolAccessEvaluator : IAIToolAccessEvaluator
{
    private static readonly HashSet<string> _adminOnlyTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "send_email",
    };

    public Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, string toolName)
    {
        if (!_adminOnlyTools.Contains(toolName))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(user?.IsInRole("Administrator") == true);
    }
}
```

```csharp
builder.Services.AddScoped<IAIToolAccessEvaluator, RoleBasedToolAccessEvaluator>();
```

