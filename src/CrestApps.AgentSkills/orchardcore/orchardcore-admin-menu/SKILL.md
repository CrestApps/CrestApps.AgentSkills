---
name: orchardcore-admin-menu
description: Skill for creating admin menus in Orchard Core. Covers admin menu content types, link, content type list, and placeholder admin node types, admin menu deployment, permissions, custom admin node development with TreeNode base class, and admin menu rendering.
---

# Orchard Core Admin Menu - Prompt Templates

## Create and Manage Admin Menus

You are an Orchard Core expert. Generate admin menus, custom admin node types, and admin menu configurations for Orchard Core.

### Guidelines

- Enable `OrchardCore.AdminMenu` for custom admin menu support.
- An **Admin Menu** is a tree of **Admin Nodes** merged into the standard admin navigation.
- Admin Nodes can be nested via drag-and-drop in the admin UI.
- Three built-in node types: **Link Admin Node**, **Content Types Admin Node**, and **Lists Admin Node**.
- Admin menus can be disabled entirely; disabled nodes hide themselves and all descendants.
- Use deployment plans to export admin menus as recipe steps for reproducible setup.
- Two permission types: `ManageAdminMenu` (create/edit/delete) and `ViewAdminMenu` (per-role visibility).
- Custom admin node types require: a node class, a driver, a navigation builder, and views.
- Register custom admin node services with `services.AddAdminNode<TNode, TBuilder, TDriver>()`.
- Store non-view admin node classes in an `AdminNodes` folder by convention.
- Store admin node views in `Views/Items/` (required convention).
- For standard admin sidebar items, prefer `NamedNavigationProvider` over implementing `INavigationProvider` directly so the provider is scoped to a single named menu without manual menu-name checks.
- Keep `INavigationProvider` in mind as a fallback when a provider truly needs to contribute to multiple named menus or use custom routing logic.
- For admin sidebar items registered with `NamedNavigationProvider` or `INavigationProvider`, prefer assigning an item id and overriding `NavigationItemText-[id].cshtml` when you need a custom icon or text wrapper.
- Do not use `AddClass(...)` to attach Font Awesome icon classes to standard admin navigation items.
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

Export admin menus via a deployment plan, then use the generated JSON as a recipe step:

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
              "$type": "LinkAdminNode",
              "LinkText": "{{LinkText}}",
              "LinkUrl": "{{LinkUrl}}",
              "IconClass": "fa-solid fa-{{icon}}",
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
```

### Built-In Admin Node Types

| Node Type | Module | Description |
|-----------|--------|-------------|
| **Link Admin Node** | `OrchardCore.AdminMenu` | Simple menu item with text, URL, and Font Awesome icon class. |
| **Content Types Admin Node** | `OrchardCore.Contents` | Generates a menu item for each content type linking to its list view. |
| **Lists Admin Node** | `OrchardCore.Lists` | Generates menu items for content items that include a list part (e.g., each Blog). |

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

    public string[] PermissionNames { get; set; } = [];
}
```

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
            itemBuilder.Url(node.LinkUrl);
            itemBuilder.Priority(node.Priority);
            itemBuilder.Position(node.Position);

            node.IconClass?.Split(' ').ToList()
                .ForEach(c => itemBuilder.AddClass("icon-class-" + c));

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

### Custom Admin Node Display Driver

```csharp
using OrchardCore.AdminMenu.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

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
using OrchardCore.AdminMenu.Services;
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

### Permissions

| Permission | Description |
|------------|-------------|
| `ManageAdminMenu` | Create, edit, and delete admin menus. |
| `ViewAdminMenu` | Control per-role visibility of admin menus via the Edit Roles page. |

### Admin Menu Rendering Flow

1. `NavigationManager` collects all `INavigationProvider` implementations.
2. When `OrchardCore.AdminMenu` is enabled, `AdminMenuNavigationProvidersCoordinator` is registered as a navigation provider.
3. The coordinator loads all admin menus from the database and calls `BuildTreeAsync` on each.
4. Each admin node recursively adds menu items to the navigation builder.
5. The resulting menu items are merged into the standard admin navigation.

### Customizing Standard Admin Navigation Item Icons

When you are customizing the standard admin sidebar through `INavigationProvider` rather than `OrchardCore.AdminMenu` content items, use an item id plus a `NavigationItemText-[id].cshtml` template.

Example:

```csharp
public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
{
    if (!NavigationHelper.IsAdminMenu(name))
    {
        return ValueTask.CompletedTask;
    }

    builder.Add(S["Sports"], sports => sports
        .Id("sports")
        .Add(S["Calendar"], calendar => calendar
            .Action("Index", "Admin", new { area = "MyModule" })
            .LocalNav()
        )
    );

    return ValueTask.CompletedTask;
}
```

Then create `Views/NavigationItemText-sports.Id.cshtml`:

```cshtml
<span class="icon">
    <i class="fa-regular fa-futbol"></i>
</span>
<span class="title">@Model.Text</span>
```

This is the preferred way to add icons to standard TheAdmin navigation items supplied by code.
