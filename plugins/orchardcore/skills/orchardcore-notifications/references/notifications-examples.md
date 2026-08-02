# Notification Examples

## Example 1: Notify Administrators on Content Submission

Use `UserManager<IUser>` to resolve role members, then send a
`NotificationMessage` to each recipient.

```csharp
using Microsoft.AspNetCore.Identity;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;

public sealed class ContentReviewNotificationHandler : ContentHandlerBase
{
    private readonly INotificationService _notificationService;
    private readonly UserManager<IUser> _userManager;
    private readonly IStringLocalizer S;

    public ContentReviewNotificationHandler(
        INotificationService notificationService,
        UserManager<IUser> userManager,
        IStringLocalizer<ContentReviewNotificationHandler> stringLocalizer)
    {
        _notificationService = notificationService;
        _userManager = userManager;
        S = stringLocalizer;
    }

    public override async Task PublishedAsync(PublishContentContext context)
    {
        if (context.ContentItem.ContentType != "BlogPost")
        {
            return;
        }

        var administrators = await _userManager.GetUsersInRoleAsync("Administrator");

        foreach (var administrator in administrators)
        {
            await _notificationService.SendAsync(administrator, new NotificationMessage
            {
                Subject = S["Content submitted for review"],
                Summary = S["New blog post: {0}", context.ContentItem.DisplayText],
                TextBody = S["A new blog post is ready for review."],
            });
        }
    }
}
```

Register the content handler:

```csharp
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentHandler, ContentReviewNotificationHandler>();
    }
}
```

## Example 2: Reporting Partial Delivery

`NotificationSendResult` represents the combined outcome from the notification
methods available for the recipient.

```csharp
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;

public sealed class OrderNotificationSender
{
    private readonly INotificationService _notificationService;

    public OrderNotificationSender(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<bool> NotifyOrderCompletedAsync(object recipient, string orderId)
    {
        var result = await _notificationService.SendAsync(recipient, new NotificationMessage
        {
            Subject = $"Order {orderId} completed",
            Summary = $"Order {orderId} has been completed",
            TextBody = "Your order has been processed and shipped.",
        });

        return result.Status is NotificationSendStatus.Success or
            NotificationSendStatus.PartiallySuccessful;
    }
}
```

## Example 3: Custom Delivery Method

This provider records an audit event and returns the `Result` required by
`INotificationMethodProvider`.

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Infrastructure;
using OrchardCore.Notifications;

public sealed class AuditNotificationMethodProvider : INotificationMethodProvider
{
    private readonly ILogger _logger;

    public AuditNotificationMethodProvider(ILogger<AuditNotificationMethodProvider> logger)
    {
        _logger = logger;
    }

    public string Method => "Audit";

    public LocalizedString Name => new("Audit notification");

    public Task<Result> SendAsync(
        object notify,
        INotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Notification '{Subject}' sent to {RecipientType}.",
            message.Subject,
            notify.GetType().Name);

        return Task.FromResult(Result.Success());
    }
}
```

## Example 4: Tracking a Delivery Method Event

```csharp
using OrchardCore.Notifications;
using OrchardCore.Notifications.Services;

public sealed class DeliveryEvents : NotificationEventsHandler
{
    private readonly ILogger _logger;

    public DeliveryEvents(ILogger<DeliveryEvents> logger)
    {
        _logger = logger;
    }

    public override Task FailedAsync(
        INotificationMethodProvider provider,
        NotificationContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Method '{Method}' failed to send notification '{NotificationId}'.",
            provider.Method,
            context.Notification.NotificationId);

        return Task.CompletedTask;
    }
}
```

Register the event handler and method provider as scoped tenant services:

```csharp
using OrchardCore.Modules;
using OrchardCore.Notifications;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<INotificationEvents, DeliveryEvents>();
        services.AddScoped<INotificationMethodProvider, AuditNotificationMethodProvider>();
    }
}
```

## Example 5: Built-In Workflow Tasks

Enable `OrchardCore.Notifications` and `OrchardCore.Workflows`, then add the
built-in **Notify User** task to a workflow. It is implemented by
`NotifyUserTask`; do not duplicate it with a custom activity when its
user-name, subject, and message fields meet the workflow requirement.
