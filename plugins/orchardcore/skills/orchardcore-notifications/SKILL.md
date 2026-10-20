---
name: orchardcore-notifications
description: Skill for managing Orchard Core notifications. Covers notification messages, email and SMS notification delivery methods, notification lifecycle events, the OrchardCore Notifications configuration section and NotificationOptions, workflow activities, the notifications admin list, and notification permissions, plus the difference with INotifier UI messages. Use this skill when requests mention Orchard Core Notifications, Create and Manage Notifications, Enabling Notifications, Notification Model, Sending Notifications with INotificationService, notification center, unread notifications navbar, TotalUnreadNotifications, OrchardCore.Notifications.Sms, upgrading notifications configuration to Orchard Core 4, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include OrchardCore.Notifications, OrchardCore.Notifications.Email, and OrchardCore.Notifications.Sms. It also helps with Implementing a Notification Method Provider.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Notifications - Prompt Templates

## Create and Manage Notifications

Use `INotificationService` to create a persistent notification and dispatch it
through every delivery method available for the recipient. The service contract
is `SendAsync(object notify, INotificationMessage message, CancellationToken)`;
it returns a `NotificationSendResult`.

### Guidelines

- Enable `OrchardCore.Notifications` before using notification services.
- Enable `OrchardCore.Notifications.Email` (requires a configured email
  provider) and/or `OrchardCore.Notifications.Sms` (requires a configured SMS
  provider) for out-of-band delivery. Users opt in or out of each enabled method
  in their profile.
- Use `INotificationService` for persistent notifications in the notification
  center; use `INotifier` (`OrchardCore.DisplayManagement.Notify`) for
  transient per-request UI messages such as "Saved successfully".
- Send a `NotificationMessage`, not a `Notification`; the service creates the
  `Notification` entity.
- Pass the recipient as the `notify` object. Use application users when sending
  user notifications.
- Inspect `NotificationSendResult.Status`, counts, and errors when delivery
  outcomes affect application flow.
- Implement `INotificationMethodProvider` for a delivery method. Its `SendAsync`
  returns `Task<Result>`.
- Derive lifecycle handlers from `NotificationEventsHandler`.
- The module provides the `ManageNotifications` permission. Do not invent
  additional notification permissions.
- `NotifyUserTask` and `NotifyContentOwnerTask` are workflow activities
  registered when their required features are enabled.

### Enabling Notifications

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Notifications",
        "OrchardCore.Notifications.Email"
      ],
      "disable": []
    }
  ]
}
```

### Configuration

`NotificationOptions` binds `OrchardCore:Notifications` (also per tenant under
`OrchardCore:{TenantName}:Notifications`):

```json
{
  "OrchardCore": {
    "Notifications": {
      "TotalUnreadNotifications": 10,
      "DisableNotificationHtmlBodySanitizer": false,
      "AbsoluteCacheExpirationSeconds": 3600,
      "SlidingCacheExpirationSeconds": 1800
    }
  }
}
```

| Option | Default | Purpose |
|---|---|---|
| `TotalUnreadNotifications` | `10` | Unread notifications shown in the navbar. |
| `DisableNotificationHtmlBodySanitizer` | `false` | Skips sanitizing `HtmlBody` of workflow notifications. |
| `AbsoluteCacheExpirationSeconds` | `0` | Absolute lifetime of the cached unread list; `0` means no absolute expiration. |
| `SlidingCacheExpirationSeconds` | `1800` | Sliding lifetime of the cached unread list; `0` disables it. |

The unread list is cached only when one of the expirations is greater than `0`.

### Notification and Message Models

`NotificationMessage` supplies the delivery content:

| Property | Purpose |
|---|---|
| `Subject` | Delivery subject, such as an email subject. |
| `Summary` | Short persistent notification summary. |
| `TextBody` | Plain-text body. |
| `HtmlBody` | HTML body when a provider supports it. |
| `IsHtmlPreferred` | Requests HTML when `HtmlBody` is available. |

The persisted `Notification` entity has `NotificationId`, `Subject`, `Summary`,
`UserId`, and `CreatedUtc`. The core event handler stores the message text and
HTML body as notification metadata.

### Sending Notifications with INotificationService

```csharp
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;

public sealed class ContentApprovalHandler
{
    private readonly INotificationService _notificationService;
    private readonly IStringLocalizer S;

    public ContentApprovalHandler(
        INotificationService notificationService,
        IStringLocalizer<ContentApprovalHandler> stringLocalizer)
    {
        _notificationService = notificationService;
        S = stringLocalizer;
    }

    public async Task NotifyAuthorAsync(IUser user, string contentItemId)
    {
        var result = await _notificationService.SendAsync(user, new NotificationMessage
        {
            Subject = S["Content approved"],
            Summary = S["Your content has been approved"],
            TextBody = S["The content item {0} was reviewed and approved.", contentItemId],
        });

        if (result.Status is NotificationSendStatus.Failed or NotificationSendStatus.None)
        {
            throw new InvalidOperationException("The notification was not delivered.");
        }
    }
}
```

### Sending Notifications to a Role

Use `UserManager<IUser>.GetUsersInRoleAsync` to find recipients. Each call to
`INotificationService.SendAsync` creates a notification for one recipient.

```csharp
using Microsoft.AspNetCore.Identity;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;

public sealed class BulkNotificationSender
{
    private readonly INotificationService _notificationService;
    private readonly UserManager<IUser> _userManager;
    private readonly IStringLocalizer S;

    public BulkNotificationSender(
        INotificationService notificationService,
        UserManager<IUser> userManager,
        IStringLocalizer<BulkNotificationSender> stringLocalizer)
    {
        _notificationService = notificationService;
        _userManager = userManager;
        S = stringLocalizer;
    }

    public async Task NotifyEditorsAsync(string contentItemDisplayText)
    {
        var editors = await _userManager.GetUsersInRoleAsync("Editor");

        foreach (var editor in editors)
        {
            await _notificationService.SendAsync(editor, new NotificationMessage
            {
                Subject = S["Content requires review"],
                Summary = S["New content requires review: {0}", contentItemDisplayText],
                TextBody = S["A new content item was submitted for editorial review."],
            });
        }
    }
}
```

### Handling Notification Events

`NotificationEventsHandler` provides virtual lifecycle methods. Override the
event that corresponds to the information needed by the handler.

```csharp
using OrchardCore.Notifications;
using OrchardCore.Notifications.Services;

public sealed class NotificationAuditEvents : NotificationEventsHandler
{
    private readonly ILogger _logger;

    public NotificationAuditEvents(ILogger<NotificationAuditEvents> logger)
    {
        _logger = logger;
    }

    public override Task SentAsync(
        INotificationMethodProvider provider,
        NotificationContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Notification '{NotificationId}' was sent using '{Method}'.",
            context.Notification.NotificationId,
            provider.Method);

        return Task.CompletedTask;
    }
}
```

### Creating a Notification Method Provider

Implement `INotificationMethodProvider` for a delivery channel. Return
`Result.Success()` or `Result.Failed(...)` so `INotificationService` can
aggregate the results from every available method.

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
            "Recorded notification '{Subject}' for {RecipientType}.",
            message.Subject,
            notify.GetType().Name);

        return Task.FromResult(Result.Success());
    }
}
```

### Registering Notification Extensions

```csharp
using OrchardCore.Modules;
using OrchardCore.Notifications;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<INotificationEvents, NotificationAuditEvents>();
        services.AddScoped<INotificationMethodProvider, AuditNotificationMethodProvider>();
    }
}
```

### Notification Permissions

`ManageNotifications` is the notification permission exposed by the module.
Use it to protect administration UI or application operations that manage
notifications.

```csharp
using OrchardCore.Notifications;

if (!await authorizationService.AuthorizeAsync(
    user,
    NotificationPermissions.ManageNotifications))
{
    return Forbid();
}
```

### Notification Workflow Activities

Enable Workflows with Notifications to use the built-in `NotifyUserTask`.
The activity targets the configured users by name and sends the configured
subject and message. Enable Contents and Users as well to use
`NotifyContentOwnerTask`.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Notifications",
        "OrchardCore.Workflows"
      ],
      "disable": []
    }
  ]
}
```

### Administration

The notification center and notification list are administered through the
built-in module UI. The list renders with the shared `AdminList` shape, so it
follows the site's List/Grid layout and page size settings (see
`orchardcore-admin-lists`); unread rows carry the `notification-is-unread` CSS
class and read rows `notification-is-read`. Notification read state is handled
by the module endpoint; `INotificationService` exposes delivery only, not read,
unread, count, or delete operations. Filter the list with custom
`INotificationAdminListFilterProvider` implementations.

### Upgrading from 3.x

- The `OrchardCore_Notifications` section is now `OrchardCore:Notifications`;
  the old name still binds but is deprecated.
- `NotificationPermissions.ManageNotifications` has a `LocalizationSource`
  description translated with PO files.
