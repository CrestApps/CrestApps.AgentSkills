---
name: crestapps-core-response-handlers
description: Skill for routing chat responses through AI, webhook, relay, and custom handlers in CrestApps.Core.
---

# CrestApps.Core Response Handlers - Prompt Templates

## Implement Response Handlers

You are a CrestApps.Core expert. Generate code and architecture guidance for response handlers in CrestApps.Core.

### Guidelines

- Use response handlers when not every message should go to the default AI path.
- Implement `IChatResponseHandler`.
- Keep handler names unique.
- Use handlers for AI, webhook, live-agent, and external relay scenarios.
- Build timeouts and cancellation handling into custom handlers.

### Interface

```csharp
public interface IChatResponseHandler
{
    string Name { get; }

    Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken = default);
}
```

### Webhook Handler Example

```csharp
public sealed class WebhookResponseHandler(IHttpClientFactory httpClientFactory) : IChatResponseHandler
{
    public string Name => "webhook";

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        var payload = new
        {
            sessionId = context.Session.Id,
            message = context.Messages.Last().Text,
        };

        var response = await client.PostAsJsonAsync("https://example.com/chat", payload, cancellationToken);
        return response.IsSuccessStatusCode
            ? ChatResponseHandlerResult.Handled()
            : ChatResponseHandlerResult.NotHandled();
    }
}
```

### Register a Handler

```csharp
builder.Services.AddScoped<IChatResponseHandler, WebhookResponseHandler>();
```
