# My account area and site notifications

## My Account (`CrestApps.OrchardCore.Users.Account`)

Enabled by dependency only (Commerce, Tickets and other modules turn it on). It moved here from Commerce; the `Commerce*` names are kept so existing themes and modules keep working.

| Piece | Value |
|---|---|
| Home page | `/account`, route `CommerceAccount` (`UsersConstants.Account.HomeRouteName`), signed-in users only |
| Account menu | Named navigation menu `commerce-account` (`UsersConstants.Account.MenuName`); the home item **Overview** is at position `0` |
| Page frame shape | `CommerceAccountPage` (`UsersConstants.Account.PageShape`) with `Title`, `Content` (a shape or HTML) and optional `Actions` |
| Menu shape | `CommerceAccountNavigation` (`UsersConstants.Account.NavigationShape`) |
| Home tiles | `AccountOverview` shape, one tile per menu item |
| User menu | An **Account** link in Orchard Core's user menu (`AccountUserMenuDisplayDriver`, template `UserMenuItems-CommerceAccount`) |

Menu items are sorted by position with `AccountMenuItems.Arrange` (Orchard Core `FlatPositionComparer`), because a named menu otherwise keeps provider registration order.

### Add a page to the account area

```csharp
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace MyModule.Navigation;

public sealed class WarrantyAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public WarrantyAccountMenu(IStringLocalizer<WarrantyAccountMenu> stringLocalizer)
        : base(UsersConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Warranties"], "40", warranties => warranties
            .Id("account-warranties")
            .AddClass("fa-solid fa-shield")
            .Action("Index", "Warranties", new { area = "MyModule" }));

        return ValueTask.CompletedTask;
    }
}
```

Register it in a startup marked `[RequireFeatures(UsersConstants.Feature.Account)]` (or make your feature depend on it) with `services.AddNavigationProvider<WarrantyAccountMenu>()`. Render the page inside the frame and keep your own breadcrumb:

```cshtml
<zone Name="Title">
    <breadcrumb name="CommerceAccount" title="@T["Your account"]" />
</zone>

@await DisplayAsync(await New.CommerceAccountPage(Title: T["Warranties"], Content: Model))
```

## Site notifications

With Orchard Core `OrchardCore.Notifications` enabled, the Users module adds front-end notification pages for users who never open the admin. Both need the Notifications module's **Manage notifications** permission (signed-in users have it by default).

| Page | Route name | Purpose |
|---|---|---|
| `/notifications` | `UserNotifications` (`UsersConstants.Notifications.RouteName`) | Lists the user's notifications, all or unread, newest first, a page at a time; **Mark all as read** posts to `UserNotificationsMarkAllRead` |
| `/notifications/preferences` | `UserNotificationPreferences` (`UsersConstants.Notifications.PreferencesRouteName`) | Edits the Notifications module's `UserNotificationPreferencesPart`: methods received at all (`Optout`) and per-type methods |

- It reuses Orchard Core pieces: `INotificationManager`, `INotificationVisibilityHandler`, the `Notification` shape in the `Header` display type (so a `Notification-{Type}.Header.cshtml` template shows here too) and the `/Notifications/Open/{id}` endpoint that marks a notification read.
- A **Notifications** link is added to the user menu on site pages and, with My Account on, to the account menu (position `90`); the pages are then framed as account pages.
- With no delivery method (email, SMS) enabled, the preferences page says there is nothing to choose.
