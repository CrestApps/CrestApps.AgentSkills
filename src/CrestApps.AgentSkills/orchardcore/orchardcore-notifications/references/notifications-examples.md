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

    internal readonly IStringLocalizer S;

    public AuditNotificationMethodProvider(
        ILogger<AuditNotificationMethodProvider> logger,
        IStringLocalizer<AuditNotificationMethodProvider> stringLocalizer)
    {
        _logger = logger;
        S = stringLocalizer;
    }

    public string Method => "Audit";

    public LocalizedString Name => S["Audit notification"];

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

## Example 6: A Custom Notification Type as a Module Inbox

A tasks module notifies a user when a task is assigned to them, shows the
project on the notification, and hides the notification when the user leaves
the project. `IProjectMembershipService` stands for the module's own service.

```csharp
public sealed class TaskAssignedInfo
{
    public string ProjectId { get; set; }
    public string ProjectName { get; set; }
    public string AssignedBy { get; set; }
    public DateTime? DueUtc { get; set; }
}
```

Display driver, which shows nothing for restricted notifications:

```csharp
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Notifications;

public sealed class TaskAssignedNotificationDisplayDriver : DisplayDriver<Notification>
{
    private readonly INotificationVisibilityEvaluator _visibilityEvaluator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TaskAssignedNotificationDisplayDriver(
        INotificationVisibilityEvaluator visibilityEvaluator,
        IHttpContextAccessor httpContextAccessor)
    {
        _visibilityEvaluator = visibilityEvaluator;
        _httpContextAccessor = httpContextAccessor;
    }

    public override bool CanHandleModel(Notification notification)
        => notification.Type == TaskNotificationConstants.TaskAssigned;

    public override async Task<IDisplayResult> DisplayAsync(Notification notification, BuildDisplayContext context)
    {
        if (!notification.TryGet<TaskAssignedInfo>(out var info) ||
            await _visibilityEvaluator.EvaluateAsync(notification, _httpContextAccessor.HttpContext?.User) == NotificationVisibility.Restricted)
        {
            return null;
        }

        return Combine(
            View("TaskAssignedNotificationProject", info)
                .Location(NotificationConstants.DisplayTypes.Header, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Subject:10")
                .Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Meta:5"),
            View("TaskAssignedNotificationDue", info)
                .Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:5"));
    }
}
```

`Views/TaskAssignedNotificationProject.cshtml` (`View()` wraps the model in a
`ShapeViewModel<T>`; import `OrchardCore.DisplayManagement.Views`):

```cshtml
@model ShapeViewModel<TaskAssignedInfo>

<span class="small text-body-secondary">@Model.Value.ProjectName &middot; @T["by {0}", Model.Value.AssignedBy]</span>
```

Visibility handler, called with every notification shown together:

```csharp
using OrchardCore.Notifications;

public sealed class TaskVisibilityHandler : INotificationVisibilityHandler
{
    private readonly IProjectMembershipService _membershipService;

    public TaskVisibilityHandler(IProjectMembershipService membershipService)
    {
        _membershipService = membershipService;
    }

    public async Task EvaluateAsync(IReadOnlyList<NotificationVisibilityContext> contexts, CancellationToken cancellationToken = default)
    {
        var tasks = contexts
            .Where(context => context.Notification.Type == TaskNotificationConstants.TaskAssigned)
            .ToList();

        if (tasks.Count == 0)
        {
            return;
        }

        var projectIds = tasks.Select(context => context.Notification.GetOrCreate<TaskAssignedInfo>().ProjectId).ToHashSet();
        var memberOf = await _membershipService.GetProjectsOfUserAsync(projectIds, contexts[0].User);

        foreach (var context in tasks)
        {
            if (!memberOf.Contains(context.Notification.GetOrCreate<TaskAssignedInfo>().ProjectId))
            {
                context.Hide();
            }
        }
    }
}
```

A handler that throws is retried with smaller groups; notifications it still
fails on are hidden and logged, and the navbar is cached for at most one minute
after a failure. When no handler is registered, counting and paging stay in
the database.

Mark a completed task's notifications as read, and link to the filtered
notification center from the module:

```csharp
await _notificationManager.MarkAsReadAsync(new NotificationFilter
{
    Type = TaskNotificationConstants.TaskAssigned,
    ReferenceId = task.ContentItemId,
});
```

```cshtml
<a href="@Url.Content("~/Admin/notifications?q=type:TaskAssigned")">@T["My assigned tasks"]</a>
```
