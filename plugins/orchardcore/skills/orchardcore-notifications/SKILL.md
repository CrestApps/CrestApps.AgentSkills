---
name: orchardcore-notifications
description: Skill for Orchard Core notifications. Covers sending notification messages, email and SMS delivery methods, lifecycle events, NotificationOptions, using the notification center as the inbox of a module (URLs, notification types, reference ids, INotificationManager, display drivers per type, visibility handlers, preferences per type), workflow tasks, the notification center admin list, and permissions, plus the difference with INotifier UI messages. Use this skill when requests mention Orchard Core Notifications, sending notifications with INotificationService, notification center, unread notifications navbar, a custom notification type, mentions or assigned-task notifications, or closely related Orchard Core implementation or troubleshooting work. Strong matches include OrchardCore.Notifications, OrchardCore.Notifications.Email, OrchardCore.Notifications.Sms, INotificationMethodProvider, AddNotificationType, INotificationManager, NotificationFilter, and INotificationVisibilityHandler.
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
- When a module notifies users about events aimed at them (mentions, assigned
  tasks, approvals), use the notification center as its inbox instead of
  building a page: set `Url`, `Type` and `ReferenceId` on the message, register
  the type with `services.AddNotificationType(...)`, and keep sent
  notifications in sync with `INotificationManager`.

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
      "MaxEvaluatedUnreadNotifications": 100,
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
| `MaxEvaluatedUnreadNotifications` | `100` | Unread notifications checked by visibility handlers to compute the navbar count; a larger count shows as `100+`. Only used when a handler is registered. |
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
| `Url` | Page the notification opens: `~/` application-relative, a local `/` path, or an absolute `http`/`https` URL. Other schemes (for example `javascript:`) are ignored with a warning. |
| `Type` | Kind of notification, for example `Mention` (max 64 characters, indexed). |
| `ReferenceId` | Id of the record the notification is about (max 255 characters, indexed). |

Only `Subject` is required; it cannot contain HTML. `Summary` may contain HTML,
sanitized when shown. `NotificationMessage` is an `Entity`: data stored on it
with `Put()` is copied to the stored notification for its display drivers.

The persisted `Notification` entity has `NotificationId`, `Subject`, `Summary`,
`UserId`, `CreatedUtc`, `Type`, `ReferenceId` and `Url`. The core event handler
stores the message text and HTML body as notification metadata.
`NotificationSendResult.Notification` returns the stored notification.

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

### Using the Notification Center as the Inbox of a Module

Every addition below is optional; existing senders keep working, and a
migration adds the `Type` and `ReferenceId` columns to `NotificationIndex`.

1. **Register the type** (namespace `Microsoft.Extensions.DependencyInjection`,
   package `OrchardCore.Notifications.Abstractions`). Keep the name in a
   constant; it is stored on notifications and must not change.

```csharp
public static class TaskNotificationConstants
{
    public const string TaskAssigned = "TaskAssigned";
}

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(
            TaskNotificationConstants.TaskAssigned,
            LocalizationSource.Create("Assigned tasks", typeof(Startup)),
            type =>
            {
                type.Description = LocalizationSource.Create("When a task is assigned to you.", typeof(Startup));

                // Off by default for this type until the user turns it on.
                type.DisabledMethods.Add("SMS");
            });

        services.AddDisplayDriver<Notification, TaskAssignedNotificationDisplayDriver>();
        services.AddScoped<INotificationVisibilityHandler, TaskVisibilityHandler>();
    }
}
```

   A registered type gets a badge, a value in the **Type** filter of the
   notification center (also `?q=type:TaskAssigned status:Unread`), and a row
   in each user's notification preferences, with a checkbox per method.
   Preferences only store choices that differ from the type defaults
   (`UserNotificationPreferencesPart.Types`).

2. **Send** with `Type`, `ReferenceId`, `Url`, and extra data via `Put()`:

```csharp
var message = new NotificationMessage
{
    Subject = $"{currentUserName} assigned you a task",
    Summary = task.DisplayText,
    Url = $"~/tasks/{task.ContentItemId}",
    Type = TaskNotificationConstants.TaskAssigned,
    ReferenceId = task.ContentItemId,
};

message.Put(new TaskAssignedInfo { ProjectName = project.DisplayText, AssignedBy = currentUserName });

await _notificationService.SendAsync(assignee, message);
```

   The subject links through `/Notifications/Open/{notificationId}`, which
   marks the notification as read and redirects to `Url`. A notification
   without a URL opens its own page (`DetailAdmin`) in the notification center.

3. **Keep sent notifications in sync** with `INotificationManager`
   (`FindByIdAsync`, `ListAsync`, `UpdateAsync`, `DeleteAsync`,
   `MarkAsReadAsync`, `MarkAsUnreadAsync`, `IsRead`). Filter overloads take a
   `NotificationFilter` (`UserId`, `Type`, `ReferenceId`, `IsRead`, `Skip`,
   `Take`); at least one of `UserId`, `Type` or `ReferenceId` is required. Each
   call refreshes the cached navbar of the affected users.

```csharp
public sealed class TaskNotificationsContentHandler : ContentHandlerBase
{
    private readonly INotificationManager _notificationManager;

    public TaskNotificationsContentHandler(INotificationManager notificationManager)
    {
        _notificationManager = notificationManager;
    }

    public override async Task RemovedAsync(RemoveContentContext context)
    {
        if (context.ContentItem.ContentType == "Task")
        {
            await _notificationManager.DeleteAsync(new NotificationFilter
            {
                Type = TaskNotificationConstants.TaskAssigned,
                ReferenceId = context.ContentItem.ContentItemId,
            });
        }
    }
}
```

4. **Customize the display** with a `DisplayDriver<Notification>` whose
   `CanHandleModel` checks the type, or replace the template with the
   `Notification__{Type}` / `Notification_{DisplayType}__{Type}` alternates
   (for example `Notification-TaskAssigned.Header.cshtml`).

| Display type | Where | Zones |
|---|---|---|
| `Header` (`NotificationConstants.DisplayTypes.Header`) | Navbar item | `Content`, `Meta`, `Actions` |
| `SummaryAdmin` | Notification center row, List layout | `Checkbox`, `Content`, `Tags`, `Meta`, `Actions`, `ActionsMenu` |
| `SummaryAdminGrid` | Notification center row, Grid layout | `Select`, `Subject`, `Received`, `Actions`, `ActionsMenu` |
| `DetailAdmin` | Notification page | `Content`, `Meta`, `Actions` |

   The navbar `Meta` zone is one line of muted text; render plain text there and
   keep buttons for `Actions`.

5. **Hide what the user can no longer see** with an
   `INotificationVisibilityHandler`. `EvaluateAsync` receives every
   notification shown together (one user), so skip other types and load data in
   one query. Call `context.Hide()` (not listed, counted or openable) or
   `context.Restrict()` (listed with a "no longer has access" message); the most
   restrictive decision wins. A driver that shows record data must return
   `null` when `INotificationVisibilityEvaluator.EvaluateAsync(notification, user)`
   returns `NotificationVisibility.Restricted`. Handlers run on every display;
   also delete or update notifications with `INotificationManager` when the
   change is known.

`INotificationMethodProviderAccessor.GetProvidersAsync(notify, message)` honors
the per-type preferences. See the full walkthrough in
[references/notifications-examples.md](references/notifications-examples.md#example-6-a-custom-notification-type-as-a-module-inbox).

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
`NotifyContentOwnerTask`. Both tasks also have optional **URL** (Liquid) and
**Type** fields.

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
class and read rows `notification-is-read`. `INotificationService` only
delivers; use `INotificationManager` to find, update, delete or mark
notifications as read/unread. The notification center has a **Type** filter
(and `type:` in the search text) and a page per notification. Filter the list
with custom `INotificationAdminListFilterProvider` implementations.

Transient `INotifier` messages survive redirects in a cookie that is now
`HttpOnly`, and `Secure` over HTTPS.

### Upgrading from 3.x

- The `OrchardCore_Notifications` section is now `OrchardCore:Notifications`;
  the old name still binds but is deprecated.
- `NotificationPermissions.ManageNotifications` has a `LocalizationSource`
  description translated with PO files.
- `INotificationMessage` gained default `Type`, `ReferenceId` and `Url`
  members, so custom implementations keep compiling; `NotificationMessage` now
  derives from `Entity`.
- The `NotificationMethodProviderAccessor` constructor takes an
  `IOptions<NotificationTypeOptions>` parameter; code constructing it directly
  must pass it.
- The navbar items are now built by `Notification` display drivers (`Header`
  display type). Overrides of `UserNotificationNavbar` or
  `Notification.Header` should be reviewed. Since 2.0 the shipped
  `UserNotificationNavbar` views rendered `data-mark-as-read-url` with a stray
  `)`, which broke marking as read from the navbar; theme overrides copied from
  them must drop it.
- The bulk **Mark as read** action now records when notifications were read.
