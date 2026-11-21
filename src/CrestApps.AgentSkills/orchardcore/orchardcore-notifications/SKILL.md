---
name: orchardcore-notifications
description: Skill for Orchard Core notifications. Covers the Notifications, Email and SMS Notifications features, the bell and Notification Center, sending messages with INotificationService and NotificationSendResult, notification types with AddNotificationType, per-type preferences and DisabledMethods, URLs and reference ids, INotificationManager and NotificationFilter, display drivers per type and the Header, SummaryAdmin, SummaryAdminGrid and DetailAdmin display types, visibility handlers, method providers, events, NotificationOptions, the navbar cache, admin list filters, NotificationIndex, workflow tasks, ManageNotifications, and INotifier UI messages. Use it for Orchard Core notifications, the notification center, unread notifications, or a custom notification type. Strong matches include OrchardCore.Notifications, INotificationService, INotificationMethodProvider, AddNotificationType, INotificationManager, NotificationFilter, and INotificationVisibilityHandler.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Notifications

You are an Orchard Core expert. Send persistent notifications to users, give each module its own notification types, and keep the notification center in sync with the records notifications are about.

## Guidelines

- Use `INotificationService` for persistent notifications stored in the user's notification center (and optionally sent by email or SMS). Use `INotifier` (`OrchardCore.DisplayManagement.Notify`) for transient per-request messages such as "Saved".
- Send a `NotificationMessage` (or any `INotificationMessage`); the service creates and stores the `Notification`.
- Pass an `OrchardCore.Users.Models.User` as the recipient. The notification's `UserId` is only set for a `User`, so any other object gets a stored notification that no notification center shows.
- Every notification is stored first; methods (email, SMS, custom) only send copies. `NotificationSendResult.Status == None` means no method ran, not that the notification was lost.
- Give each kind of notification a **type**: set `Type`, `ReferenceId` and `Url` on the message, register the type with `services.AddNotificationType(...)`, and keep sent notifications in sync with `INotificationManager`. There are no built-in types; each module registers its own.
- Keep type names in constants; they are stored on notifications (max 64 characters; reference ids max 255). Longer values throw `ArgumentException` from `SendAsync`.
- The only permission is `ManageNotifications`. Do not invent others.
- Declare display drivers `internal sealed` unless another assembly must reference the type.

## Features

| Feature | ID | Depends on |
|---|---|---|
| Notifications | `OrchardCore.Notifications` | `OrchardCore.Liquid` |
| Email Notifications | `OrchardCore.Notifications.Email` | `OrchardCore.Notifications`, `OrchardCore.Email` |
| SMS Notifications (declared in the `OrchardCore.Sms` module) | `OrchardCore.Notifications.Sms` | `OrchardCore.Notifications`, `OrchardCore.Sms` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Notifications",
        "OrchardCore.Notifications.Email"
      ]
    }
  ]
}
```

The email method (`Method` = `Email`) needs a configured email provider and sends `Subject` with `HtmlBody` when `IsHtmlPreferred` and an HTML body exist, otherwise `TextBody`; it fails for a user without an email address. The SMS method (`Method` = `SMS`) needs an SMS provider and sends `TextBody` to the user's `PhoneNumber`, failing when it is empty.

## Where Users See Notifications

- **Bell**: `NotificationNavbarDisplayDriver` adds `UserNotificationNavbar` to the `Navbar` (`Content:9`, `Detail` and `DetailAdmin`) for users with `ManageNotifications`. It lists up to `TotalUnreadNotifications` unread notifications, each built with the `Header` display type, a badge with the unread count, and a **Notification Center** link. Below 576px the menu spans the screen under the bell (`.notification-navbar[data-bs-popper]` is `position: fixed`); restyle it after `UserNotificationNavbarStyles`.
- **Notification Center**: admin route `ListNotifications` at `/Admin/notifications`, reached from the bell (there is no admin menu item). It lists only the current user's notifications (the always-run `owner` default term). Each notification has a page at `/Admin/notifications/{notificationId}` (`DisplayNotification`).
- **Profile**: the user editor shows **Notification Methods** (`UserNotificationPreferencesPart_Edit`, `Content:11`) when at least one method feature is enabled: order the methods, opt out of some, and, when types are registered, a **Notification Types** grid with a checkbox per type and method.
- Opening a notification goes through `GET /Notifications/Open/{notificationId}`, which marks it read and redirects to its `Url` (or to its notification center page when it has none). `POST /Notifications/MarkAsRead` marks one read from the bell.

`ManageNotifications` is granted by default to Administrator, Editor, Moderator, Author, Contributor and Authenticated, so every signed-in user has a bell.

## Configuration

`NotificationOptions` binds `OrchardCore:Notifications` (the deprecated `OrchardCore_Notifications` still binds):

```json
{
  "OrchardCore": {
    "Notifications": {
      "TotalUnreadNotifications": 10,
      "MaxEvaluatedUnreadNotifications": 100,
      "DisableNotificationHtmlBodySanitizer": false,
      "AbsoluteCacheExpirationSeconds": 0,
      "SlidingCacheExpirationSeconds": 1800
    }
  }
}
```

| Option | Default | Purpose |
|---|---|---|
| `TotalUnreadNotifications` | `10` | Unread notifications shown in the bell menu. |
| `MaxEvaluatedUnreadNotifications` | `100` | With a visibility handler registered, the most recent unread notifications evaluated to count the badge; more shows as `100+`. |
| `DisableNotificationHtmlBodySanitizer` | `false` | Skips sanitizing the **HTML Body** of the notify workflow tasks when they are saved. |
| `AbsoluteCacheExpirationSeconds` | `0` | Absolute lifetime of the cached bell menu; `0` means none. |
| `SlidingCacheExpirationSeconds` | `1800` | Sliding lifetime of the cached bell menu; `0` disables it. |

The bell menu is cached per user (tags `top-unread-user-notifications` and per-user keys from `NotificationsHelper`) only when one expiration is greater than `0`. `INotificationManager` and the built-in actions clear it; a visibility change alone shows when the entry expires.

## Sending a Notification

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users.Models;

namespace MyModule.Services;

public sealed class ApprovalNotifier
{
    private readonly INotificationService _notificationService;
    private readonly IStringLocalizer S;

    public ApprovalNotifier(
        INotificationService notificationService,
        IStringLocalizer<ApprovalNotifier> stringLocalizer)
    {
        _notificationService = notificationService;
        S = stringLocalizer;
    }

    public async Task<NotificationSendResult> NotifyAuthorAsync(User author, string contentItemId, string title)
    {
        return await _notificationService.SendAsync(author, new NotificationMessage
        {
            Subject = S["Your article was approved"],
            Summary = S["'{0}' is now published.", title],
            TextBody = S["Your article '{0}' was reviewed and published.", title],
            Url = $"~/Admin/Contents/ContentItems/{contentItemId}/Edit",
            Type = "Approval",
            ReferenceId = contentItemId,
        });
    }
}
```

| Message property | Purpose |
|---|---|
| `Subject` | Required title; no HTML. Email subject. |
| `Summary` | Short text under the subject in the bell and the center; HTML is sanitized when shown. |
| `TextBody`, `HtmlBody`, `IsHtmlPreferred` | Body sent by the methods and shown on the notification page (stored as `NotificationBodyInfo`). |
| `Url` | `~/` app-relative (resolved with the tenant prefix when opened), a local `/` path, or an absolute `http`/`https` URL. Anything else, such as `javascript:`, is dropped with a warning. |
| `Type` | Kind of notification (indexed). |
| `ReferenceId` | Id of the record it is about (indexed). |

`NotificationMessage` is an `Entity`: data added with `message.Put(new MyInfo { ... })` is copied to the stored `Notification` for display drivers and visibility handlers. `Notification` has `NotificationId`, `Subject`, `Summary`, `Type`, `ReferenceId`, `Url`, `UserId`, `CreatedUtc`, plus `NotificationBodyInfo` and `NotificationReadInfo` (`IsRead`, `ReadAtUtc`). Notifications are stored in the YesSql `Notification` collection.

`NotificationSendResult` has `Notification`, `SuccessfulCount`, `FailedCount`, `Errors`, and `Status`: `Success`, `PartiallySuccessful`, `Failed` (every method that ran failed), or `None` (no method ran, for example no method feature enabled or the user opted out of all).

To notify a role, resolve its users with `UserManager<IUser>.GetUsersInRoleAsync("Editor")` and call `SendAsync` once per user (see [references/notifications-examples.md](references/notifications-examples.md)).

## Notification Types

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace MyModule;

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

                // Off for this type until a user turns it on.
                type.DisabledMethods.Add("SMS");
            });
    }
}
```

`AddNotificationType` (namespace `Microsoft.Extensions.DependencyInjection`, package `OrchardCore.Notifications.Abstractions`) adds a `NotificationTypeDescriptor` (`Name`, `DisplayName`, `Description`, `DisabledMethods`, case-insensitive) to `NotificationTypeOptions.Types`; registering a name again replaces it. A registered type gets, with no more code:

- Its display name as a badge in the center, in the bell's meta line, and on the notification page (`NotificationsType_Header`, `NotificationsType_SummaryAdmin`).
- A value in the center's **Type** filter and the `type:` search term, so links can open it filtered: `~/Admin/notifications?q=type:TaskAssigned status:Unread`.
- A row in each user's **Notification Types** preferences. `UserNotificationPreferencesPart.Types` stores only choices that differ from the type defaults (`NotificationTypePreference.Optin` for methods the type disables, `Optout` for methods it enables); a method in the part's global `Optout` stays off for every type. `NotificationTypePreferences.IsMethodEnabledForType(part, type, method)` applies these rules, and `INotificationMethodProviderAccessor.GetProvidersAsync(notify, message)` uses them when sending.

Unregistered types still work (filter, index, queries) but show no badge or preferences row.

## Managing Sent Notifications

```csharp
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Notifications;

namespace MyModule.Handlers;

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

`INotificationManager`: `FindByIdAsync`, `ListAsync(filter)` (most recent first), `UpdateAsync`, `DeleteAsync(notification)`, `DeleteAsync(filter)` (returns the count), `MarkAsReadAsync(notification)` / `MarkAsUnreadAsync(notification)` (return whether the state changed), `MarkAsReadAsync(filter)`, and `IsRead`. `NotificationFilter` combines `UserId`, `Type`, `ReferenceId`, `IsRead`, plus `Skip`/`Take` for `ListAsync`; at least one of `UserId`, `Type` or `ReferenceId` is required (`HasCriteria`). Filter deletes and marks run in batches inside the request's transaction, and every change refreshes the affected users' bell menu. The module also has `FindForUserAsync` and `FindVisibleForUserAsync` extension methods that check ownership (and visibility).

## Display

Notifications render through `IDisplayManager<Notification>`:

| Display type | Where | Zones |
|---|---|---|
| `Header` (`NotificationConstants.DisplayTypes.Header`) | Bell menu item | `Content`, `Meta`, `Actions` |
| `SummaryAdmin` | Center row, List layout | `Checkbox`, `Content`, `Tags`, `Meta`, `Actions`, `ActionsMenu` |
| `SummaryAdminGrid` | Center row, Grid layout | `Select`, `Subject`, `Received`, `Actions`, `ActionsMenu` |
| `DetailAdmin` | Notification page | `Content`, `Meta`, `Actions` |

Built-in shapes (move, hide or override them): `NotificationsSubject_Header`, `NotificationsSubject_SummaryAdmin`, `NotificationsBody_DetailAdmin`, `NotificationsRestricted_SummaryAdmin` (restricted notifications), `NotificationsType_Header`, `NotificationsType_SummaryAdmin`, `NotificationsMeta_Header`, `NotificationsMeta_SummaryAdmin`, `NotificationsCheckbox_SummaryAdmin`, `NotificationsActions_SummaryAdmin`, `NotificationsButtonActions_SummaryAdmin`, `NotificationsActions_DetailAdmin`.

Add data per type with a driver whose `CanHandleModel` checks the type:

```csharp
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Notifications;

namespace MyModule.Drivers;

internal sealed class TaskAssignedNotificationDisplayDriver : DisplayDriver<Notification>
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
        // A restricted notification must not show the data of its record.
        if (!notification.TryGet<TaskAssignedInfo>(out var info) ||
            await _visibilityEvaluator.EvaluateAsync(notification, _httpContextAccessor.HttpContext?.User) == NotificationVisibility.Restricted)
        {
            return null;
        }

        return View("TaskAssignedNotificationProject", info)
            .Location(NotificationConstants.DisplayTypes.Header, "Meta:5")
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:10")
            .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Subject:10")
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Meta:5");
    }
}
```

Register with `services.AddDisplayDriver<Notification, TaskAssignedNotificationDisplayDriver>()`. The bell's `Meta` zone is one line of small muted text separated by dots: render plain text there and keep buttons for `Actions`. To replace a whole template, use the alternates `Notification__{Type}` or `Notification_{DisplayType}__{Type}` (for example `Notification-TaskAssigned.Header.cshtml`); restricted notifications always use the default templates. Templates read `Model.Notification` and `Model.NotificationVisibility`.

## Visibility Handlers

When a user can lose access to the record after the notification was sent, implement `INotificationVisibilityHandler`. It runs whenever notifications are shown (bell, badge count, center, opening):

```csharp
using OrchardCore.Notifications;

namespace MyModule.Services;

public sealed class TaskVisibilityHandler : INotificationVisibilityHandler
{
    private readonly IProjectMembershipService _membershipService;

    public TaskVisibilityHandler(IProjectMembershipService membershipService)
    {
        _membershipService = membershipService;
    }

    public async Task EvaluateAsync(IReadOnlyList<NotificationVisibilityContext> contexts, CancellationToken cancellationToken = default)
    {
        // Every handler sees every shown notification (one user): skip other types and query once.
        var tasks = contexts.Where(context => context.Notification.Type == TaskNotificationConstants.TaskAssigned).ToList();

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

- `context.Restrict()` keeps it listed and counted but replaces subject, summary, body and URL with "You no longer have access to this notification."; the user can still mark it read or delete it. `context.Hide()` removes it from lists and counts and blocks opening. The most restrictive decision of all handlers wins (`NotificationVisibility.Visible`, `Restricted`, `Hidden`) and is cached for the request.
- A handler that throws is retried with smaller groups; notifications it still fails on are hidden and logged, repeated failures stop calling it, and the bell is cached for at most one minute after a failure.
- With a handler registered, the badge counts the most recent `MaxEvaluatedUnreadNotifications` unread notifications; the center removes hidden ones from the current page. Without handlers, counting and paging stay in the database.
- `INotificationVisibilityEvaluator` (`HasHandlers`, `HasFailures`, `EvaluateAsync`, `FilterAsync`) exposes the decisions to drivers and controllers.
- A handler is a safety net: when the change is known (a user leaves a project), also delete or update the notifications with `INotificationManager`.

## Method Providers and Events

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Infrastructure;
using OrchardCore.Notifications;

namespace MyModule.Services;

public sealed class WebhookNotificationMethodProvider : INotificationMethodProvider
{
    internal readonly IStringLocalizer S;

    public WebhookNotificationMethodProvider(IStringLocalizer<WebhookNotificationMethodProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    // Stored in user preferences and type DisabledMethods: keep it stable.
    public string Method => "Webhook";

    public LocalizedString Name => S["Webhook Notifications"];

    public Task<Result> SendAsync(object notify, INotificationMessage message, CancellationToken cancellationToken = default)
    {
        // Deliver message.Subject / TextBody; return Result.Failed(...) on error.
        return Task.FromResult(Result.Success());
    }
}
```

Register with `services.AddScoped<INotificationMethodProvider, WebhookNotificationMethodProvider>()`, normally in its own feature so users see it only when enabled. Methods are ordered by the user's preference, then by `Name`.

`INotificationEvents` has `CreatingAsync`, `CreatedAsync` (around saving), `SendingAsync`/`SentAsync` (whole send), and per-method `SendingAsync`, `SentAsync`, `FailedAsync`. Derive from `NotificationEventsHandler` (`OrchardCore.Notifications.Services`) and override what you need; register with `services.AddScoped<INotificationEvents, MyEvents>()`. A failing handler is logged and does not stop the send.

## Notification Center Admin List

The center renders with the shared `AdminList` shape (list name `Notifications`), so it follows the List/Grid and actions layout settings (see `orchardcore-admin-lists`). Rows carry `notification-is-unread` or `notification-is-read` (`NotificationsAdminList.UnreadCssClass`, `ReadCssClass`).

| Search term | Values |
|---|---|
| `status:` | `Read`, `Unread` (the **Show** filter: All, Read, Unread) |
| `sort:` | `Latest` (**Recently created**, default), `Oldest` (**Previously created**) |
| `type:` | A type name (the **Type** filter, shown when types are registered) |
| text | Bare text searches `NotificationIndex.Content` (summary plus text body, HTML stripped, 705 characters) |

Bulk actions (`NotificationBulkAction`): **Mark as read**, **Mark as unread**, **Remove**, plus **Mark All as Read**. Add terms with an `INotificationAdminListFilterProvider` (builds the `QueryEngineBuilder<Notification>`); filter on `NotificationIndex`.

`NotificationIndex` (`NotificationId`, `UserId`, `IsRead`, `ReadAtUtc`, `CreatedAtUtc`, `Type`, `ReferenceId`, `Content`) is declared `.ExclusiveToDocumentType().SingleRecordPerDocument()`, so counts read the index table without joining documents. Query it with `collection: NotificationConstants.NotificationCollection`.

## Workflow Tasks

With `OrchardCore.Workflows`, the **Notifications** category adds **Notify Specific Users Task** (`NotifyUserTask`, **User names** comma-separated, Liquid allowed) and, with `OrchardCore.Users` and `OrchardCore.Contents`, **Notify Content's Owner Task** (`NotifyContentOwnerTask`). Both have **Subject** (required, Liquid), **Summary**, **URL** (Liquid), **Type**, **Text Body**, **HTML Body** and **Use HTML Body when possible**, with outcomes **Done**, **Failed** and **Failed: no user found**.

## Permissions

| Permission | Purpose | Default roles |
|---|---|---|
| `ManageNotifications` (`NotificationPermissions.ManageNotifications`) | The bell, the center, opening and marking one's own notifications | Administrator, Editor, Moderator, Author, Contributor, Authenticated |

## Troubleshooting

| Symptom | Cause |
|---|---|
| No bell | Feature disabled, or the role lacks `ManageNotifications`. |
| Notification sent but not in the center | The recipient was not a `User`, so `UserId` is empty. |
| No email or SMS | Method feature disabled, provider not configured, user opted out, or the type disables the method (`Status` is `None`). |
| Bell count stale | Cached menu: change notifications through `INotificationManager`, or lower the cache expirations. |
| Type shows as raw name | The type is not registered with `AddNotificationType` in an enabled feature. |
| `ArgumentException` on send | `Type` over 64 or `ReferenceId` over 255 characters. |

## Upgrading from 3.x

- Rename `OrchardCore_Notifications` to `OrchardCore:Notifications`.
- `INotificationMessage` gained default `Type`, `ReferenceId` and `Url` members; `NotificationMessage` derives from `Entity`. A migration adds `Type` and `ReferenceId` to `NotificationIndex`.
- `NotificationMethodProviderAccessor` takes `IOptions<NotificationTypeOptions>`.
- Bell items are built by `Notification` display drivers (`Header`); review overrides of `UserNotificationNavbar` and `Notification.Header`, and drop the stray `)` in `data-mark-as-read-url` copied from 2.x/3.x views.
- `ManageNotifications` has a `LocalizationSource` description translated with PO files.
- The bulk **Mark as read** records `ReadAtUtc`. Transient `INotifier` messages survive redirects in an `HttpOnly` cookie, `Secure` over HTTPS.

See [references/notifications-examples.md](references/notifications-examples.md) for role notifications, partial delivery, custom methods and events, workflow tasks, and the full custom type walkthrough.
