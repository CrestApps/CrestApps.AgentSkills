---
name: orchardcore-admin-menu
description: Skill for creating admin menus in Orchard Core. Covers admin menu content, the link, placeholder, content types, and lists admin node types, the AdminMenu recipe and deployment step, ManageAdminMenu, ViewAdminMenuAll and per-menu ViewAdminMenu permissions from AdminMenuPermissions, custom admin nodes with AdminNode, IAdminNodeNavigationBuilder and AddAdminNode, data localization of node text, rendering in the 4.0 TheAdmin menu, and upgrading from the removed legacy admin menu layout. Use this skill when requests mention Orchard Core Admin Menu, Create and Manage Admin Menus, Admin Menu Deployment Recipe Step, Built-In Admin Node Types, Custom Admin Node Class, LegacyAdminMenuNavigation, or related implementation, setup, or troubleshooting work. Strong matches include OrchardCore.AdminMenu, OrchardCore.AdminMenu.Abstractions, AdminMenuPermissions, LinkAdminNode, PlaceholderAdminNode, OrchardCore.Lists, OrchardCore.Navigation.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Admin Menu - Prompt Templates

## Create and Manage Admin Menus

You are an Orchard Core expert. Generate admin menus, custom admin node types, and admin menu configurations for Orchard Core.

### Guidelines

- Enable `OrchardCore.AdminMenu` for custom admin menu support. Admin menus are managed under **Tools → Admin Menus** (the `AdminMenus` admin list).
- An **Admin Menu** is a tree of **Admin Nodes** merged into the standard TheAdmin menu. First-level nodes that hold children render as menu groups.
- Admin Nodes are nested via drag-and-drop in the admin UI.
- Built-in node types: **Link Admin Node**, **Placeholder Admin Node**, **Content Types Admin Node**, and **Lists Admin Node**.
- A disabled menu is not shown; a disabled node hides itself and all descendants.
- Use deployment plans to export admin menus as `AdminMenu` recipe steps.
- Permissions live in the static `OrchardCore.AdminMenu.AdminMenuPermissions` class (`OrchardCore.AdminMenu.Abstractions`).
- Custom admin node types require a node class, a display driver, a navigation builder, and views; register them with `services.AddAdminNode<TNode, TBuilder, TDriver>()`.
- Store non-view admin node classes in an `AdminNodes` folder by convention; admin node views must live in `Views/Items/`.
- For code-first sidebar items, use `AdminNavigationProvider` (see `orchardcore-admin`) instead of this feature.
- Do not use `AddClass(...)` to attach Font Awesome classes to code-first navigation items; use an item id with `NavigationItemText-{id}.Id.cshtml`. Admin nodes store their icon in `IconClass`, which the builder adds with the `icon-class-` prefix for TheAdmin.
- Always seal classes.

### Enabling Admin Menu Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.AdminMenu"
      ],
      "disable": []
    }
  ]
}
```

### Admin Menu Deployment Recipe Step

Export admin menus via a deployment plan (Admin Menu step), then use the generated JSON as a recipe step:

```json
{
  "steps": [
    {
      "name": "AdminMenu",
      "data": [
        {
          "Id": "{{AdminMenuId}}",
          "Name": "{{AdminMenuName}}",
          "Enabled": true,
          "MenuItems": [
            {
              "$type": "PlaceholderAdminNode",
              "MenuName": "{{AdminMenuName}}",
              "LinkText": "{{GroupText}}",
              "IconClass": "fa-solid fa-{{icon}}",
              "UniqueId": "{{UniqueGroupNodeId}}",
              "Enabled": true,
              "Items": [
                {
                  "$type": "LinkAdminNode",
                  "MenuName": "{{AdminMenuName}}",
                  "LinkText": "{{LinkText}}",
                  "LinkUrl": "{{LinkUrl}}",
                  "PermissionNames": [],
                  "UniqueId": "{{UniqueNodeId}}",
                  "Enabled": true,
                  "Items": []
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

- An empty or missing `Id` generates a new menu; an existing `Id` replaces that menu.
- `MenuName` must match the `Name` of the containing admin menu; the step fills it in for every node on import.
- Content Types Admin Node entries use `ContentTypeName` and `ContentTypeDisplayName`. Older recipes that only set `ContentTypeId` are migrated on import, but do not write `ContentTypeId` in new recipes.
- Relative `LinkUrl` values (`~/...` or `Contents/...`) are prefixed with the admin URL prefix; absolute URLs and paths starting with `/` are kept as is.

### Built-In Admin Node Types

| Node Type | Module | Description |
|---|---|---|
| **Link Admin Node** (`LinkAdminNode`) | `OrchardCore.AdminMenu` | Text, URL, optional target, Font Awesome `IconClass`, and `PermissionNames` required to see it. |
| **Placeholder Admin Node** (`PlaceholderAdminNode`) | `OrchardCore.AdminMenu` | Text, `IconClass`, and `PermissionNames` without a link; groups child nodes. |
| **Content Types Admin Node** | `OrchardCore.Contents` | A menu item per selected content type, linking to its content list. |
| **Lists Admin Node** | `OrchardCore.Lists` | A menu item per content item that has a `ListPart` (e.g. each Blog). |

### Custom Admin Node Class

```csharp
using System.ComponentModel.DataAnnotations;
using OrchardCore.AdminMenu.Models;

namespace MyModule.AdminNodes;

public sealed class CustomAdminNode : AdminNode
{
    [Required]
    public string LinkText { get; set; }

    [Required]
    public string LinkUrl { get; set; }

    public string IconClass { get; set; }
}
```

`AdminNode` derives from `MenuItem` and adds `UniqueId` and `Enabled`. `AddAdminNode` registers the type for polymorphic JSON (`$type`) serialization.

### Custom Admin Node Navigation Builder

```csharp
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.AdminMenu.Services;
using OrchardCore.Navigation;

namespace MyModule.AdminNodes;

public sealed class CustomAdminNodeNavigationBuilder : IAdminNodeNavigationBuilder
{
    private readonly ILogger _logger;

    public CustomAdminNodeNavigationBuilder(ILogger<CustomAdminNodeNavigationBuilder> logger)
    {
        _logger = logger;
    }

    public string Name => nameof(CustomAdminNode);

    public Task BuildNavigationAsync(MenuItem menuItem, NavigationBuilder builder, IEnumerable<IAdminNodeNavigationBuilder> treeNodeBuilders)
    {
        var node = menuItem as CustomAdminNode;

        if (node == null || string.IsNullOrEmpty(node.LinkText) || !node.Enabled)
        {
            return Task.CompletedTask;
        }

        return builder.AddAsync(new LocalizedString(node.LinkText, node.LinkText), async itemBuilder =>
        {
            // MenuName selects the data localization context of the text in TheAdmin.
            itemBuilder.MenuName(node.MenuName);
            itemBuilder.Url(node.LinkUrl);
            itemBuilder.Priority(node.Priority);
            itemBuilder.Position(node.Position);

            node.IconClass?.Split(' ').ToList()
                .ForEach(c => itemBuilder.AddClass(NavigationConstants.CssClassPrefix + c));

            foreach (var childNode in menuItem.Items)
            {
                try
                {
                    var childBuilder = treeNodeBuilders.FirstOrDefault(x => x.Name == childNode.GetType().Name);

                    if (childBuilder != null)
                    {
                        await childBuilder.BuildNavigationAsync(childNode, itemBuilder, treeNodeBuilders);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "An exception occurred while building the '{MenuItem}' child menu item.", childNode.GetType().Name);
                }
            }
        });
    }
}
```

To restrict a node by permissions, store permission names on the node and resolve them with `IPermissionService` (`OrchardCore.Security.Permissions`) before calling `itemBuilder.Permissions(...)`, as `LinkAdminNodeNavigationBuilder` does.

### Custom Admin Node Display Driver

```csharp
using OrchardCore.AdminMenu.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Navigation;

namespace MyModule.AdminNodes;

public sealed class CustomAdminNodeDriver : DisplayDriver<MenuItem, CustomAdminNode>
{
    public override IDisplayResult Display(CustomAdminNode model, BuildDisplayContext context)
    {
        return Combine(
            View("CustomAdminNode_Fields_TreeSummary", model)
                .Location("TreeSummary", "Content"),
            View("CustomAdminNode_Fields_TreeThumbnail", model)
                .Location("TreeThumbnail", "Content")
        );
    }

    public override IDisplayResult Edit(CustomAdminNode model, BuildEditorContext context)
    {
        return Initialize<CustomAdminNodeViewModel>("CustomAdminNode_Fields_TreeEdit", viewModel =>
        {
            viewModel.LinkText = model.LinkText;
            viewModel.LinkUrl = model.LinkUrl;
            viewModel.IconClass = model.IconClass;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(CustomAdminNode model, UpdateEditorContext context)
    {
        var viewModel = new CustomAdminNodeViewModel();
        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        model.LinkText = viewModel.LinkText;
        model.LinkUrl = viewModel.LinkUrl;
        model.IconClass = viewModel.IconClass;

        return Edit(model, context);
    }
}
```

Views: `Views/Items/CustomAdminNode.Fields.TreeSummary.cshtml`, `CustomAdminNode.Fields.TreeThumbnail.cshtml`, and `CustomAdminNode.Fields.TreeEdit.cshtml`. Node editors use full width; do not apply `ocat-*` classes to them.

### Custom Admin Node View Model

```csharp
namespace MyModule.AdminNodes;

public class CustomAdminNodeViewModel
{
    public string LinkText { get; set; }
    public string LinkUrl { get; set; }
    public string IconClass { get; set; }
}
```

### Registering Custom Admin Node in Startup

```csharp
using OrchardCore.AdminMenu;
using OrchardCore.Modules;

namespace MyModule;

[Feature("MyModule.AdminMenu")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAdminNode<CustomAdminNode, CustomAdminNodeNavigationBuilder, CustomAdminNodeDriver>();
    }
}
```

### Translating Node Text

With `OrchardCore.DataLocalization` enabled, admin menu names and the texts of Link and Placeholder nodes are listed in **Data Localization** under the context returned by `DataLocalizationContext.AdminMenu(menuName)` (`Admin Menus` plus the menu name). For a custom node, derive from `AdminNodeDataLocalizationProvider` and register it as an `ILocalizationDataProvider`:

```csharp
using OrchardCore.AdminMenu;
using OrchardCore.AdminMenu.Services;
using OrchardCore.Localization.Data;

public sealed class CustomAdminNodeDataLocalizationProvider : AdminNodeDataLocalizationProvider
{
    public CustomAdminNodeDataLocalizationProvider(IAdminMenuAccessor adminMenuAccessor)
        : base(adminMenuAccessor)
    {
    }

    public override async Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var menus = await GetAdminMenusAsync();

        return menus.SelectMany(menu => menu.MenuItems.OfType<CustomAdminNode>()
            .Select(node => new DataLocalizedString(DataLocalizationContext.AdminMenu(menu.Name), node.LinkText, string.Empty)));
    }
}
```

```csharp
[RequireFeatures("OrchardCore.DataLocalization")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, CustomAdminNodeDataLocalizationProvider>();
    }
}
```

### Permissions

| Permission | Member | Description |
|---|---|---|
| `ManageAdminMenu` | `AdminMenuPermissions.ManageAdminMenu` | Create, edit, and delete admin menus. Granted to Administrator and Editor by default. |
| `ViewAdminMenuAll` | `AdminMenuPermissions.ViewAdminMenuAll` | See every admin menu. Implied by `ManageAdminMenu`. |
| `ViewAdminMenu_{MenuName}` | `Permissions.CreatePermissionForAdminMenu(name)` | See one admin menu; assign per role on the Edit Role page. Implied by both permissions above. |

### Admin Menu Rendering Flow

1. `NavigationManager` collects all `INavigationProvider` implementations for the `admin` menu.
2. The module's `AdminMenu` provider adds **Tools → Admin Menus** and calls `AdminMenuNavigationProvidersCoordinator`.
3. The coordinator loads the stored admin menus the user may view and calls each node's `IAdminNodeNavigationBuilder` recursively.
4. The resulting items are merged into the standard admin navigation and rendered by TheAdmin's `Navigation-admin`, `NavigationItem-admin`, and `NavigationItemLink-admin` templates. Each permission is checked once, and children of an unauthorized item are removed with it.

The standard navigation is organized into **Content**, **Design**, **Search**, **Settings**, **Tools**, and **Access Control** groups, opened according to the **Navigation menu behavior** admin setting (`Focused` or `Persistent`, see `orchardcore-admin`).

### Upgrading from 3.x

- Delete `AppContext.SetSwitch("LegacyAdminMenuNavigation", true)`; the legacy layout was removed and the switch has no effect. `NavigationHelper.UseLegacyFormat()` and the `AdminMenuSecurityPosition`, `AdminMenuAuditTrailPosition`, `AdminMenuWorkflowsPosition`, and `AdminMenuConfigurationPosition` constants were removed.
- `OrchardCore.AdminMenu.Permissions.ManageAdminMenu` and `.ViewAdminMenuAll` were removed. In 3.x these obsolete members were **swapped** (each returned the other permission). Replace each with the member of the same name, `AdminMenuPermissions.ManageAdminMenu` and `AdminMenuPermissions.ViewAdminMenuAll`, not with what it used to return. Stored role claims are unchanged.
- Overrides of TheAdmin menu templates must follow the new markup (groups, `nav-group`, `nav-group-items`, `current`, `button.nav-toggle`).
