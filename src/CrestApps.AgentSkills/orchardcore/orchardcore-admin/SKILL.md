---
name: orchardcore-admin
description: Guidance for the Orchard Core admin panel, including admin controllers and routes, admin menu registration with AdminNavigationProvider, admin views with breadcrumbs and ocat editor forms, TheAdmin theme customization and menu colors, admin branding, the Navbar shape, the Ctrl+K quick navigation palette with QuickNavigationSource and IQuickNavigationIndex, AdminSettings such as MenuBehavior, ShowBreadcrumb, DisplayQuickNavigation and list layouts, the AdminDashboard shape, settings pages, and admin filters and zones. Use this skill when requests mention Orchard Core Admin Panel, TheAdmin Theme, Admin Controllers, Admin Menu Registration, Adding an Icon to an Admin Menu Item, AdminSettings recipe step, Quick Navigation, Navigation menu behavior, admin URL prefix, or related implementation, upgrade, or troubleshooting work. Strong matches include OrchardCore.Admin, AdminSettings, AdminPermissions, AdminOptions, AdminAttribute, SiteDisplayDriver, OrchardCore.Contents.QuickNavigation.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Admin Panel

The Orchard Core admin panel is the back-office interface for content management and site configuration. It is rendered by the **TheAdmin** theme for every request routed through the admin pipeline (`[Admin]` attribute). Related skills cover the details of specific admin areas:

- `orchardcore-admin-edit-views` for editor views and forms built with the `ocat-*` classes.
- `orchardcore-admin-lists` for admin index (list) pages rendered with the `AdminList` shape.
- `orchardcore-breadcrumbs` for the `<breadcrumb>` page title trail.
- `orchardcore-admin-menu` for the admin-configurable `OrchardCore.AdminMenu` node tree.
- `orchardcore-admin-dashboard` for `DashboardWidget` content.

## TheAdmin Theme

### Layout Zones

| Zone | Purpose |
|---|---|
| `HeadMeta` | Extra `<meta>`/`<link>` tags in `<head>` |
| `Title` | Page title; holds the `<breadcrumb>` tag helper |
| `Breadcrumbs` | Rendered right above `Title`; TheAdmin moves visible trails here |
| `Navigation` | Admin sidebar menu |
| `Header` | Top of the content area |
| `Messages` | Notifications and alerts |
| `Content` | Main body (the view itself) |
| `Footer` | Bottom of the layout |

```html
<zone Name="Messages">
    <div class="alert alert-info">@T["Remember to publish your changes."]</div>
</zone>
```

### Admin Menu Layout (4.0)

- First-level items holding children render as **groups** (a muted caption above their items). An item that links to a page and has children renders its label as a link plus a separate `button.nav-toggle`.
- Below the `md` breakpoint the menu is an off-canvas drawer toggled by `button.ta-sidebar-toggler` and closed by `div.ta-sidebar-backdrop`, <kbd>Esc</kbd>, or opening an item. The compact rail only applies from `md` up, where groups open as flyouts.
- The section holding the current page always opens. **Navigation menu behavior** (`AdminSettings.MenuBehavior`) decides the rest: `Focused` (default) opens only the current section and closes siblings; `Persistent` restores what each user left open, remembered per user in the browser.
- Markup changes for overrides of `Navigation-admin.cshtml`, `NavigationItem-admin.cshtml`, or `NavigationItemLink-admin.cshtml`: the selected `li` also has `current` (`active` marks it and its ancestors), group `ul` elements carry `nav-group-items` with an `id` of `m{hash}`, first-level `li` elements carry `nav-group`, no `ul` renders with `show` and toggles render `aria-expanded="false"` (the script applies state), `figcaption` carries `nav-item-row`, and the filter input is wrapped in `div.nav-filter`.
- The admin menu checks each permission/resource once and drops the children of an unauthorized item together with it.

### Menu Colors

The menu has its own surface built on the Bootstrap gray scale. Rebrand it by redefining the custom properties in a theme that depends on TheAdmin:

```css
:root {
    --oc-admin-accent-rgb: 73, 80, 87;
    --oc-admin-accent-strong-color: #343a40;
    --oc-admin-menu-bg-color: #f1f3f5;
    --oc-admin-menu-border-color: #dee2e6;
}
```

New properties include `--oc-admin-menu-group-font-color`, `--oc-admin-menu-border-color`, `--oc-admin-accent-rgb`, `--oc-admin-accent-color`, and `--oc-admin-accent-strong-color` (darkest gray in light mode, lightest in dark mode). `--oc-admin-menu-separator-color`, `--oc-admin-menu-item-hover-bg-color`, and `--oc-admin-menu-item-active-bg-color` are washes of `--oc-admin-accent-rgb`, and `--oc-selected-indicator-color` is now a gray instead of `var(--bs-primary)`. The sidebar uses `var(--bs-body-font-family)`, labels wrap instead of truncating, and animations are disabled under `prefers-reduced-motion`.

### Scripts and Styles

- TheAdmin no longer includes `bootstrap-select` styles. Pages that use `bootstrap-select` must require them: `<style asp-name="bootstrap-select" at="Head"></style>`.
- Admin scripts no longer depend on jQuery or jQuery UI, and admin Vue apps use Vue 3. Front-end jQuery resources are still registered; require them explicitly (`<script asp-name="jQuery" at="Foot"></script>`) if your admin view still needs them. AJAX-injected field and part editors are ES modules.

### Admin Editor Forms (`ocat-*`)

Admin editor views lay out their rows with the static `ocat-` (Orchard Core Admin Theme) classes (`ocat-wrapper`, `ocat-label`, `ocat-label-required`, `ocat-end`, `ocat-end-offset`, `ocat-start`, `ocat-limited-wrapper`, `ocat-limited`), so an admin theme can restyle every form, for example as a horizontal layout, by overriding those classes in CSS. Use them only in admin views, never in front-end views:

```cshtml
<div class="ocat-wrapper" asp-validation-class-for="Title">
    <label asp-for="Title" class="ocat-label">@T["Title"]</label>
    <div class="ocat-end">
        <input asp-for="Title" class="form-control" />
        <span asp-validation-for="Title"></span>
        <span class="hint">@T["Hint text."]</span>
    </div>
</div>
```

Content field hints render through the `Hint_Admin` shape (TheAdmin's `Hint.Admin.cshtml`), which a custom admin theme can override. See `orchardcore-admin-edit-views` for the full class catalog, layout rules, validation, hints, part and field wrapper classes, editor tabs and cards, and complete editor examples, and `orchardcore-admin-lists` for admin index (list) pages.

## Admin Controllers

Apply `[Admin]` to a controller or action to render it in the admin theme and require admin access. Its optional route template and route name work like `[Route]`, with the admin prefix prepended; the name can contain `{area}`, `{controller}`, and `{action}`. An action-level attribute takes precedence.

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

[Admin("Person/{action}/{id?}", "Person{action}")]
public sealed class PersonController : Controller
{
    private readonly INotifier _notifier;
    private readonly IHtmlLocalizer H;

    public PersonController(INotifier notifier, IHtmlLocalizer<PersonController> htmlLocalizer)
    {
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [Admin("Person", "Person")]
    public IActionResult Index() => View();

    public IActionResult Edit(string id) => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update()
    {
        await _notifier.SuccessAsync(H["Settings updated."]);

        return RedirectToAction(nameof(Index));
    }
}
```

With the default prefix, `Index` is `~/Admin/Person` (route `Person`) and `Edit` is `~/Admin/Person/{id}` (route `PersonEdit`). Use `AdminAttribute.IsApplied(httpContext)` to detect admin requests in filters and services.

### Admin URL Prefix

```json
{
  "OrchardCore": {
    "Admin": {
      "AdminUrlPrefix": "Backoffice"
    }
  }
}
```

Read it through `IOptions<AdminOptions>` (`AdminUrlPrefix`). The legacy `OrchardCore_Admin` section still binds but is deprecated.

### Admin Route URL Generation

Prefer tag helpers in views, and `LinkGenerator` with the current `HttpContext` in code, so tenant and admin prefixes are preserved:

```cshtml
<a asp-action="Index" asp-controller="Settings" asp-area="MyModule">@T["Open settings"]</a>
```

```csharp
using Microsoft.AspNetCore.Routing;

public sealed class AdminUrlService
{
    private readonly LinkGenerator _linkGenerator;

    public AdminUrlService(LinkGenerator linkGenerator)
    {
        _linkGenerator = linkGenerator;
    }

    public string GetSettingsUrl(HttpContext httpContext)
        => _linkGenerator.GetPathByAction(httpContext, action: "Index", controller: "Settings", values: new { area = "MyModule" });
}
```

Do not use `@Url.Content("~/...")` for application links.

## Admin Views

Admin views declare their title with the `<breadcrumb>` tag helper (add `@addTagHelper *, OrchardCore.Navigation.Core` to `_ViewImports.cshtml`). It renders the trail, the `<h1>`, and the browser title segment, so do not add a separate heading:

```cshtml
<zone Name="Title">
    <breadcrumb name="MyModuleSettings" title="@T["My Module Settings"]" />
</zone>

<form asp-action="Update" method="post">
    <div class="ocat-wrapper" asp-validation-class-for="DisplayName">
        <label asp-for="DisplayName" class="ocat-label">@T["Display Name"]</label>
        <div class="ocat-end">
            <input asp-for="DisplayName" class="form-control" />
            <span asp-validation-for="DisplayName"></span>
        </div>
    </div>

    <div class="ocat-wrapper">
        <div class="ocat-end-offset">
            <button type="submit" class="btn btn-primary">@T["Save"]</button>
        </div>
    </div>
</form>
```

Edit pages and shape-based editors follow `orchardcore-admin-edit-views`; list pages render their items with `<admin-list>` (see `orchardcore-admin-lists`).

## Admin Menu Registration

Inherit from `AdminNavigationProvider` (a `NamedNavigationProvider` scoped to the `admin` menu) and register it with `AddNavigationProvider<T>()`:

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

internal sealed class AdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> localizer)
    {
        S = localizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Content"], NavigationConstants.AdminMenuContentPosition, content => content
                .Add(S["Taxonomies"], S["Taxonomies"].PrefixPosition(), taxonomies => taxonomies
                    .Permission(MyModulePermissions.ManageTaxonomies)
                    .Action("Index", "Admin", "MyModule")
                    .LocalNav()
                )
            )
            .Add(S["Settings"], settings => settings
                .Add(S["My Module"], S["My Module"].PrefixPosition(), entry => entry
                    .Id("mymodule")
                    .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = "mymodule" })
                    .Permission(MyModulePermissions.ManageMyModuleSettings)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
```

```csharp
services.AddNavigationProvider<AdminMenu>();
```

- Standard top-level groups are **Content**, **Design**, **Search**, **Settings**, **Tools**, and **Access Control**. Positions that remain in `NavigationConstants` are `AdminMenuContentPosition`, `AdminMenuDesignPosition`, `AdminMenuSearchPosition`, and `AdminMenuAccessControlPosition`. Settings pages go under **Settings** (often in a sub-group such as **Communication**), utilities under **Tools**.
- `PrefixPosition()` sorts items alphabetically by their localized text. Nest `Add` calls for sub-groups.
- `Permission(...)` hides an item from users who lack it (chain several for "any of"); hiding a parent also hides its children. Use `.Resource(...)` for resource-based checks.
- Use `INavigationProvider` directly only for providers that contribute to several named menus.

### Adding an Icon to an Admin Menu Item

Do not put Font Awesome classes on items with `AddClass(...)`. Give the item an id and add `Views/NavigationItemText-{id}.Id.cshtml`:

```csharp
builder.Add(S["Sports"], sports => sports
    .Id("sports")
    .Add(S["Calendar"], calendar => calendar
        .Action("Index", "Admin", "MyModule")
        .LocalNav()
    )
);
```

```cshtml
<span class="icon">
    <i class="fa-regular fa-futbol" aria-hidden="true"></i>
</span>
<span class="title">@Model.Text</span>
```

## Quick Navigation (Ctrl+K)

`OrchardCore.Admin` provides a quick navigation palette opened with <kbd>Ctrl</kbd>+<kbd>K</kbd> (<kbd>Cmd</kbd>+<kbd>K</kbd> on macOS) or the navbar magnifier. It filters the admin menu items the user can access (case- and accent-insensitive, including parent names shown as a breadcrumb). It is enabled by default; turn it off with **Enable quick navigation** in **Configuration → Settings → Admin** or `AdminSettings.DisplayQuickNavigation`.

- The palette loads a fresh, localized, permission-filtered JSON index from the `AdminQuickNavigationIndex` route (`~/Admin/QuickNavigation/Index`, requires `AccessAdminPanel`, `404` when disabled) on every open, with `Cache-Control: no-store`. Entries have `source`, `id`, `title`, `path`, `href`, and `target`.
- Admin themes that render the `Navbar` shape with the `DetailAdmin` display type inherit the palette (`QuickNavigationNavbarItem` shape plus `admin-quick-navigation` script and style). Restyle it after that stylesheet: `<style asp-name="my-admin-theme" asp-src="~/MyAdminTheme/Styles/theme.css" depends-on="admin-quick-navigation" at="Head"></style>`.
- Enable **Content Quick Navigation** (`OrchardCore.Contents.QuickNavigation`) to add edit links for the most recently updated content items the user can edit; `ContentQuickNavigationSettings.MaxItems` (default `50`) is configurable in the Admin settings.

### Adding a Source

Derive from `QuickNavigationSource` (`OrchardCore.Admin.QuickNavigation` in `OrchardCore.Admin.Abstractions`) in an opt-in feature that depends on `OrchardCore.Admin`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin.QuickNavigation;

public sealed class ReportNavigationSource : QuickNavigationSource
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IReportStore _reportStore;
    private readonly IStringLocalizer S;

    public ReportNavigationSource(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        LinkGenerator linkGenerator,
        IReportStore reportStore,
        IStringLocalizer<ReportNavigationSource> localizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _linkGenerator = linkGenerator;
        _reportStore = reportStore;
        S = localizer;
    }

    public override string Name => "Reports";

    public override async ValueTask<IEnumerable<string>> GetEntryIdsAsync()
        => await _reportStore.GetRecentIdsAsync(20);

    public override async ValueTask<QuickNavigationResult> DisplayAsync(string entryId)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var report = await _reportStore.GetAsync(entryId);

        if (report is null || !await _authorizationService.AuthorizeAsync(httpContext.User, ReportPermissions.ViewReports, report))
        {
            return null;
        }

        return new QuickNavigationResult
        {
            Title = report.Name,
            Path = [S["Reports"].Value],
            Href = _linkGenerator.GetPathByAction(httpContext, "View", "Report", new { area = "My.Reports", id = report.Id }),
        };
    }
}
```

```csharp
[Feature("My.Reports.QuickNavigation")]
public sealed class QuickNavigationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<QuickNavigationSource, ReportNavigationSource>();
    }
}
```

- `Name` must be unique, stable, and culture-independent. `Source` and `Id` of the result are assigned by the endpoint; return `null` for missing or unauthorized entries.
- For expensive sources, keep identifiers (never localized labels or pre-authorized results) in the tenant-singleton, in-memory `IQuickNavigationIndex` from a background task or event handler: `index.Replace("Reports", ids)` atomically replaces that source's partition (an empty collection clears it). It is not persisted or distributed, so repopulate it after restarts and on every server.
- Always authorize again and generate links from the current request in `DisplayAsync`.

## Navbar Shape

The `Navbar` shape is rendered with `DetailAdmin` on the admin and `Detail` on the front end. Add items with a display driver:

```csharp
using OrchardCore.Admin.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

public sealed class HelpNavbarDisplayDriver : DisplayDriver<Navbar>
{
    public override IDisplayResult Display(Navbar model, BuildDisplayContext context)
    {
        return View("HelpNavbarItem", model)
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:25");
    }
}
```

Register it with `services.AddDisplayDriver<Navbar, HelpNavbarDisplayDriver>()`.

## Admin Branding

Override the `AdminBranding` shape (from a custom admin theme or with Admin Templates) to change the logo, site name, and favicon:

```cshtml
<zone Name="HeadMeta">
    <link asp-src="~/media/favicon.ico" type="image/x-icon" rel="shortcut icon" />
</zone>
<a class="ta-navbar-brand" asp-route-area="OrchardCore.Admin" asp-controller="Admin" asp-action="Index">
    <div class="d-flex align-items-center">
        <img src="~/media/logo.png" alt="@Site.SiteName" class="pe-2" />
        <span>@Site.SiteName</span>
    </div>
</a>
```

## Admin Home Page Content

Without `OrchardCore.AdminDashboard`, `/admin` renders the `AdminDashboard` shape, which displays an `AdminDashboardContent` child shape. Features add positioned content with a shape table provider:

```csharp
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Views;

public sealed class AdminDashboardShapeTableProvider : ShapeTableProvider
{
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("AdminDashboardContent")
            .OnDisplaying(async displaying =>
            {
                await displaying.Shape.AddAsync(new ShapeViewModel("MyAdminDashboardContent"), "10");
            });

        return ValueTask.CompletedTask;
    }
}
```

Register it with `services.AddShapeTableProvider<AdminDashboardShapeTableProvider>()` and add a `MyAdminDashboardContent.cshtml` template.

## Admin Settings Pages

Store settings on the site and edit them with a `SiteDisplayDriver<T>`:

```csharp
public sealed class NotificationSettings
{
    public bool EnableEmailNotifications { get; set; }

    public string DefaultRecipient { get; set; }
}
```

```csharp
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

public sealed class NotificationSettingsDisplayDriver : SiteDisplayDriver<NotificationSettings>
{
    public const string GroupId = "notifications";

    protected override string SettingsGroupId => GroupId;

    public override IDisplayResult Edit(ISite site, NotificationSettings settings, BuildEditorContext context)
    {
        return Initialize<NotificationSettingsViewModel>("NotificationSettings_Edit", model =>
        {
            model.EnableEmailNotifications = settings.EnableEmailNotifications;
            model.DefaultRecipient = settings.DefaultRecipient;
        }).Location("Content:5")
          .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, NotificationSettings settings, UpdateEditorContext context)
    {
        var model = new NotificationSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.EnableEmailNotifications = model.EnableEmailNotifications;
        settings.DefaultRecipient = model.DefaultRecipient;

        return Edit(site, settings, context);
    }
}

public class NotificationSettingsViewModel
{
    public bool EnableEmailNotifications { get; set; }

    public string DefaultRecipient { get; set; }
}
```

```csharp
services.AddSiteDisplayDriver<NotificationSettingsDisplayDriver>();
```

Link it from the **Settings** group with `.Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = NotificationSettingsDisplayDriver.GroupId })`. When options are populated from these settings, consume them through `IOptionsMonitor<T>.CurrentValue` and refresh them with `IOptionsChangeTokenSource<T>` plus `IOptionsUpdateNotifier` (see `orchardcore-site-settings`).

## Admin Settings Reference

**Configuration → Settings → Admin** requires `AdminPermissions.ManageAdminSettings`; the admin itself requires `AdminPermissions.AccessAdminPanel`.

```json
{
  "steps": [
    {
      "name": "settings",
      "AdminSettings": {
        "DisplayThemeToggler": true,
        "DisplayMenuFilter": true,
        "DisplayQuickNavigation": true,
        "DisplayNewMenu": true,
        "DisplayTitlesInTopbar": false,
        "ShowBreadcrumb": true,
        "MenuBehavior": "Persistent",
        "ListLayout": "Grid",
        "ListActionsLayout": "Menu",
        "AllowUserListLayoutSelection": true
      },
      "ContentQuickNavigationSettings": {
        "MaxItems": 25
      }
    }
  ]
}
```

| Property | Default | Description |
|---|---|---|
| `DisplayThemeToggler` | `true` | Light/dark theme toggler. |
| `DisplayMenuFilter` | `false` | Filter input above the admin menu. |
| `DisplayQuickNavigation` | `true` | Ctrl+K quick navigation palette. |
| `DisplayNewMenu` | `false` | "New" content menu in the admin navigation. |
| `DisplayTitlesInTopbar` | `false` | Page titles in the top bar. |
| `ShowBreadcrumb` | `true` | Admin breadcrumb trails (see `orchardcore-breadcrumbs`). |
| `MenuBehavior` | `Focused` | `Focused` or `Persistent` menu sections. |
| `ListLayout` | `null` | `List` or `Grid`; `null` uses `AdminListOptions.DefaultLayout` (see `orchardcore-admin-lists`). |
| `ListActionsLayout` | `null` | `Buttons` or `Menu`; `null` uses `AdminListOptions.DefaultActionsLayout`. |
| `AllowUserListLayoutSelection` | `false` | Per-user List/Grid selector on every list. |

## Admin Filters

Run logic for every admin page with an MVC result filter guarded by `AdminAttribute.IsApplied`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Layout;

public sealed class AdminBannerFilter : IAsyncResultFilter
{
    private readonly ILayoutAccessor _layoutAccessor;
    private readonly IShapeFactory _shapeFactory;

    public AdminBannerFilter(ILayoutAccessor layoutAccessor, IShapeFactory shapeFactory)
    {
        _layoutAccessor = layoutAccessor;
        _shapeFactory = shapeFactory;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ViewResult && AdminAttribute.IsApplied(context.HttpContext))
        {
            var layout = await _layoutAccessor.GetLayoutAsync();
            await layout.Zones["Messages"].AddAsync(await _shapeFactory.CreateAsync("AdminBanner"), "0");
        }

        await next();
    }
}
```

```csharp
services.Configure<MvcOptions>(options => options.Filters.Add<AdminBannerFilter>());
```

The same pattern can require an admin stylesheet defined in a resource manifest (`_resourceManager.RequireStyle("MyModule.AdminStyles")`).

## Upgrading from 3.x

- Remove `AppContext.SetSwitch("LegacyAdminMenuNavigation", true)`; the legacy admin menu layout is gone. `NavigationHelper.UseLegacyFormat()`, `NavigationConstants.AdminMenuSecurityPosition`, `AdminMenuAuditTrailPosition`, `AdminMenuWorkflowsPosition`, and `AdminMenuConfigurationPosition` were removed; use the **Settings**, **Tools**, and **Access Control** groups.
- `OrchardCore.Admin.PermissionsAdminSettings.ManageAdminSettings` was removed; use `OrchardCore.Admin.AdminPermissions.ManageAdminSettings`.
- Replace `<zone Name="Title"><h1>@RenderTitleSegments(...)</h1></zone>` with the `<breadcrumb>` tag helper.
- Re-check overrides of the admin menu templates and custom menu CSS against the new markup and color variables; require `bootstrap-select` styles explicitly.
- Overridden list views must render `<admin-list>` (see `orchardcore-admin-lists`).
- Use `OrchardCore:Admin` instead of `OrchardCore_Admin` in configuration.

For practical end-to-end examples, see `references/admin-examples.md`.
