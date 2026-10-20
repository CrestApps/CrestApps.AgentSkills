---
name: orchardcore-navigation
description: Skill for navigation in Orchard Core. Covers navigation providers (INavigationProvider, NamedNavigationProvider, AdminNavigationProvider), NavigationBuilder positions and permissions, NavigationConstants, rendering named navigation menus, Navigation shape alternates, the Pager and PagerSlim shapes, page size selection with PagerOptions, GetPageSize, PagerSlimParameters.PageSize and the Pager_PageSizeSelector shape, menu content items, and the breadcrumb shapes of OrchardCore.Navigation. Use this skill when requests mention Orchard Core Navigation, Create Navigation and Menus, Custom Admin Navigation Provider, Registering Navigation Provider, Menu Recipe, pagers, items per page, page size selector, or related implementation, upgrade, or troubleshooting work. Strong matches include OrchardCore.Navigation, OrchardCore.Navigation.Core, OrchardCore.Menu, INavigationProvider, NavigationBuilder, Pager, PagerSlim, PagerOptions, PagerParameters, MenuItemsListPart, LinkMenuItemPart.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Navigation - Prompt Templates

## Create Navigation and Menus

You are an Orchard Core expert. Generate navigation providers, menus, and pagers for Orchard Core.

### Guidelines

- `OrchardCore.Navigation` is always enabled. It provides the `Navigation`, `Breadcrumb`, `Pager`, and `PagerSlim` shapes; the APIs live in `OrchardCore.Navigation.Core`.
- Content-managed menus are `Menu` content items from `OrchardCore.Menu` (see `orchardcore-menus`).
- Code-first menu items come from `INavigationProvider` implementations. Inherit from `AdminNavigationProvider` for the admin menu or `NamedNavigationProvider` for any other named menu, and register with `services.AddNavigationProvider<T>()`.
- Use `PrefixPosition()` for alphabetical ordering inside a group, or explicit positions (`"1"`, `"1.1"`) when order matters. Lower positions come first.
- Breadcrumb trails are not built by navigation providers; use the `<breadcrumb>` tag helper (see `orchardcore-breadcrumbs`).

### Enabling Menu Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Menu",
        "OrchardCore.AdminMenu"
      ],
      "disable": []
    }
  ]
}
```

### Custom Admin Navigation Provider

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

public sealed class AdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> localizer)
    {
        S = localizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["{{SubMenuName}}"], S["{{SubMenuName}}"].PrefixPosition(), item => item
                    .Id("{{menuId}}")
                    .Action("Index", "Admin", "{{ModuleName}}")
                    .Permission({{PermissionsClass}}.{{PermissionName}})
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
```

- Top-level admin groups are **Content**, **Design**, **Search**, **Settings**, **Tools**, and **Access Control**. `NavigationConstants` keeps `AdminMenuContentPosition` (`1`), `AdminMenuDesignPosition` (`2`), `AdminMenuSearchPosition` (`6`), and `AdminMenuAccessControlPosition` (`7`), plus `AdminId` (`admin`), `AdminMenuId`, and `CssClassPrefix` (`icon-class-`).
- `Permission(...)` (chainable, any-of) and `Resource(...)` hide items from unauthorized users; an unauthorized item's children are removed with it.
- Add an icon with `.Id("myid")` plus a `Views/NavigationItemText-myid.Id.cshtml` template, not `AddClass` with Font Awesome classes.

### Front-End Navigation Provider

```csharp
public sealed class MainMenu : NamedNavigationProvider
{
    private readonly IStringLocalizer S;

    public MainMenu(IStringLocalizer<MainMenu> localizer)
        : base("main")
    {
        S = localizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Notifications"], notifications => notifications
            .Action("Index", "Notifications", "My.Module")
            .LocalNav());

        return ValueTask.CompletedTask;
    }
}
```

Render it from a theme layout:

```cshtml
@await DisplayAsync(await New.Navigation(MenuName: "main", RouteData: ViewContext.RouteData))
```

### Registering Navigation Provider

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<AdminMenu>();
        services.AddNavigationProvider<MainMenu>();
    }
}
```

### Navigation Theming

TheAdmin overrides `Navigation-admin.cshtml`, `NavigationItem-admin.cshtml`, and `NavigationItemLink-admin.cshtml`. Available alternates are `Navigation__{MenuName}`, `NavigationItem__level__{level}`, `NavigationItem__{MenuName}`, `NavigationItem__{MenuName}__level__{level}`, the same three for `NavigationItemLink`, and `NavigationItemText-{id}.Id` for an item with an id (e.g. `NavigationItem-main-level-2.cshtml`). Navigation item shapes are `NavigationItemViewModel` instances (`Text`, `Href`, `Target`, `Item`, `Level`, `Selected`, `HasItems`, ...), so templates can cast `Model` to it instead of using dynamic access.

## Pagers

### `Pager` and `PagerSlim`

```csharp
public async Task<IActionResult> List(PagerParameters pagerParameters)
{
    // Validates ?pageSize= against the allowed page sizes; falls back to the site default.
    var pager = new Pager(pagerParameters, _pagerOptions);

    var query = _session.Query<ContentItem, ContentItemIndex>(x => x.Published);
    var items = await query.Skip(pager.GetStartIndex()).Take(pager.PageSize).ListAsync();

    var routeData = new RouteData();
    var pagerShape = await _shapeFactory.PagerAsync(pager, await query.CountAsync(), routeData);

    return View(new MyListViewModel { Items = items, Pager = pagerShape });
}
```

- Inject `IOptions<PagerOptions>`; the always-enabled Settings module post-configures it from the **General** site settings (`PageSize`, `MaxPageSize`, `MaxPagedCount`, `AllowPageSizeSelection`, `PageSizeOptions`).
- `PagerAsync(pager, totalItemCount, routeData | routeValues)` and `PagerSlimAsync(pagerSlim[, values])` are `IShapeFactory` extensions in `OrchardCore.DisplayManagement`.
- `Pager` parameters include `Page`, `PageSize`, `TotalItemCount`, `Quantity`, `FirstText`, `PreviousText`, `NextText`, `LastText`, `GapText`, `PagerId` (alternate `Pager__{PagerId}`, e.g. `Pager-MainBlog.cshtml`), and `ShowNext`. Customize parts with `Pager_First`, `Pager_Previous`, `Pager_Next`, `Pager_Last`, `Pager_Gap`, and `Pager_CurrentPage` (all morphed into `Pager_Link`).
- `PagerSlim` renders cursor-based Previous/Next links (`Before`/`After`) with `PreviousClass`, `NextClass`, `PreviousText`, `NextText`, and `UrlParams`.

### Page Size Selection

When **Page size selection** is enabled in **Configuration → Settings → General**, `Pager` and `PagerSlim` render an **Items per page** selector next to the links on admin and front-end lists. The value travels as `?pageSize=` and is honored only when selection is enabled and the value is one of the allowed sizes (sorted, de-duplicated positive numbers, default `10, 25, 50, 100`); otherwise the default page size applies. Pager links keep the selection. When disabled (the default), pagers render as before.

```json
{
  "steps": [
    {
      "name": "settings",
      "AllowPageSizeSelection": true,
      "PageSizeOptions": [ 10, 20, 50 ]
    }
  ]
}
```

Opt custom listings into the same validation:

| API | Use |
|---|---|
| `new Pager(PagerParameters, PagerOptions)` | Resolves the page and validated page size in one step. |
| `PagerOptions.GetPageSize()` | Site default, capped by `MaxPageSize`. |
| `PagerOptions.GetPageSize(int? selected)` / `GetPageSize(int? selected, int defaultPageSize)` | Validated selection, or the given default (e.g. a list's own page size). |
| `PagerOptions.AllowPageSizeSelection`, `PageSizeOptions` | The site settings. |
| `PagerSlimParameters.PageSize` | Optional selection for cursor-based pagers: `new PagerSlim(pagerSlimParameters, _pagerOptions.GetPageSize(pagerSlimParameters.PageSize))`. |
| `PageSizeSelector.BuildOptions(serviceProvider, currentPageSize)` | Builds the selector items to render the selector separately (then set `ShowPageSizeSelector = false` on the pager). |

The selector is the `Pager_PageSizeSelector` shape. `OrchardCore.Navigation` renders it by default as unstyled `<div class="pager-page-size">` markup with a `<label>` and `<select>`; TheAdmin and TheTheme style it with Bootstrap. Override `Views/Pager_PageSizeSelector.cshtml` in a theme to restyle it. The shape receives `PageSizes` (`List<SelectListItem>`, `Text` = size, `Value` = URL, `Selected` = current) and `CurrentPageSize`; do not read `Model.Items`, which holds child shapes.

Pager defaults can also be set per tenant in `appsettings.json` under `OrchardCore:Navigation:PagerOptions` (`OrchardCore_Navigation` is deprecated), but the General settings override them.

### SEO

Add `rel="nofollow"` to pager links with `Model.Pager.Attributes["rel"] = "nofollow"` (Razor) or `{% shape_pager Model.Pager attributes: "{\"rel\": \"nofollow\"}" %}` (Liquid).

## Menu Recipe (Creating a Menu via Recipe)

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "main-menu",
          "ContentType": "Menu",
          "DisplayText": "Main Menu",
          "Latest": true,
          "Published": true,
          "MenuPart": {},
          "AliasPart": {
            "Alias": "main-menu"
          },
          "MenuItemsListPart": {
            "MenuItems": [
              {
                "ContentType": "LinkMenuItem",
                "ContentItemId": "menu-home",
                "DisplayText": "Home",
                "LinkMenuItemPart": {
                  "Url": "~/"
                }
              },
              {
                "ContentType": "ContentMenuItem",
                "ContentItemId": "menu-blog",
                "DisplayText": "Blog",
                "ContentMenuItemPart": {
                  "ContentItem": {
                    "ContentItemId": "{{BlogContentItemId}}"
                  }
                }
              }
            ]
          }
        }
      ]
    }
  ]
}
```

### Rendering Menu in Liquid Template

```liquid
{% shape "Menu", alias: "alias:main-menu" %}
```

## Upgrading from 3.x

- `NavigationHelper.UseLegacyFormat()` and the `LegacyAdminMenuNavigation` switch were removed, together with `NavigationConstants.AdminMenuSecurityPosition`, `AdminMenuAuditTrailPosition`, `AdminMenuWorkflowsPosition`, `AdminMenuConfigurationPosition`, and `LegacyAdminMenuNavigationSwitchKey`.
- `NavigationHelper.PopulateMenuAsync` and `PopulateMenuLevelAsync` now take `IShapeFactory` and `IShape` instead of `dynamic` arguments.
- Replace manual `if (name != "admin")` checks in `INavigationProvider` implementations with `AdminNavigationProvider` or `NamedNavigationProvider`.
- Use `new Pager(pagerParameters, pagerOptions)` instead of `new Pager(pagerParameters, siteSettings.PageSize)` so page size selection works.

## Related skills

- Use `orchardcore-menus` for built-in menu item types, shape alternates, and the `<menu>` tag helper.
- Use `orchardcore-admin-menu` for the admin-configurable `OrchardCore.AdminMenu` node tree.
- Use `orchardcore-breadcrumbs` for the `<breadcrumb>` tag helper and `IBreadcrumbProvider`.
- Use `orchardcore-admin-lists` for admin list pages, which render the pager and page size selector for you.
