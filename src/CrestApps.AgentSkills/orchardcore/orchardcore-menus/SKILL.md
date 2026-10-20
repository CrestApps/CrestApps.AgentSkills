---
name: orchardcore-menus
description: Skill for creating and managing menus in Orchard Core. Covers menu content types, link, content, and HTML menu item types, the menu Razor tag helper and Liquid shape with caching, menu permissions (ManageMenu, MenuItemPermissionPart), render-time URL validation and HTML sanitization of menu items, navigation rendering, menu shape differentiators and alternates, and recipes. Use this skill when requests mention Orchard Core Menus, Create and Manage Navigation Menus, Enabling Menu Features, Menu Item Types, Creating a Menu via Recipe, Multi-Level Navigation Menu, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Menus

Enable `OrchardCore.Menu`. A menu is a `Menu` content item with
`MenuItemsListPart`; its entries are nested content items such as
`LinkMenuItem`, `ContentMenuItem`, or `HtmlMenuItem`.

## Menu Item Types

| Type | Purpose | URL source |
|---|---|---|
| `LinkMenuItem` | Static navigation link | Hardcoded `LinkMenuItemPart.Url` |
| `ContentMenuItem` | Link derived from a referenced content item | Resolved from the referenced item's route |
| `HtmlMenuItem` | Custom markup inside a menu | HTML stored on `HtmlMenuItemPart` |

See `orchardcore-navigation` for `ContentMenuItem` recipe examples and for registering menus from a code-first `INavigationProvider`.

Menus are managed under **Content → Menus**, which is visible to users who can list `Menu` content items. The content-type permission `Edit_Menu` also grants `ManageMenu`, so users who can edit menus can manage their items.

### Render-Time Safety (4.0)

- Link and HTML menu items validate their URLs when rendered, including items created through APIs or imported from older content. Unsafe schemes such as `javascript:` are replaced with an empty URL on a rendering-only copy; stored content is not changed.
- `HtmlMenuItemPart` stores its HTML exactly as authored and sanitizes it on a cloned item at render time. Like `HtmlBodyPart`, it does not execute Liquid.

## Recipe

Wrap every menu item in a complete root recipe document:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Menu",
        "OrchardCore.Widgets",
        "OrchardCore.Layers"
      ]
    },
    {
      "name": "content",
      "data": [
        {
          "ContentItemId": "4j42fxryjcpqkkacmg6cwz3pr5",
          "ContentType": "Menu",
          "DisplayText": "Main Menu",
          "Latest": true,
          "Published": true,
          "MenuPart": {},
          "TitlePart": {
            "Title": "Main Menu"
          },
          "AliasPart": {
            "Alias": "main-menu"
          },
          "MenuItemsListPart": {
            "MenuItems": [
              {
                "ContentItemId": "4j42fxryjcpqkkacmg6cwz3pr6",
                "ContentType": "LinkMenuItem",
                "DisplayText": "Home",
                "LinkMenuItemPart": {
                  "Url": "~/"
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

Render a menu directly from a Liquid theme layout with its alias handle:

```liquid
{% shape "menu", alias: "alias:main-menu", cache_id: "main-menu", cache_fixed_duration: "00:05:00", cache_tag: "alias:main-menu", cache_context: "user.roles" %}
```

Menus can be permission-based. Configure permissions on menu items when a
link must be visible only to users who can perform the related action. When
the menu is cached, include `user.roles` in `cache_context` so the output is
varied by the current user's roles.

For a custom `MenuItem` content type, attach `MenuItemPermissionPart` to
enable permission-based visibility. The **Content Menu Item** also provides
a **Check content permissions** option; when enabled, the item is shown only
if the current user has `View Content` permission for the selected item.
Permissions are enabled automatically for new tenants. Run the
`MenuAddPermissions` recipe for an existing tenant that needs the permission
part:

```json
{
  "steps": [
    {
      "name": "recipes",
      "Values": [
        {
          "executionid": "MenuAddPermissions",
          "name": "MenuAddPermissions"
        }
      ]
    }
  ]
}
```

## Shape Alternates

The menu's display text is normalized to its differentiator. For a display
text of `Main Menu`, the menu gets `Menu__MainMenu` and its child shapes use
the same differentiator. The generated alternates include:

| Shape | Examples |
|---|---|
| `Menu` | `Menu__MainMenu` |
| `MenuItem` | `MenuItem__MainMenu`, `MenuItem__LinkMenuItem`, `MenuItem__MainMenu__LinkMenuItem`, plus `__level__{n}` variants |
| `MenuItemLink` | Equivalent `MenuItemLink__...` alternates |

Use filenames such as `Menu-MainMenu.liquid` and
`MenuItem-MainMenu-LinkMenuItem.cshtml`. The differentiator comes from the
menu display text, not its alias.

Within a custom menu shape, render the existing menu-item shapes directly:

```liquid
<nav aria-label="{{ Model.MenuName }}">
  <ul>
    {% for item in Model.Items %}
      {{ item | shape_render }}
    {% endfor %}
  </ul>
</nav>
```

Render menus directly with the `<menu>` Razor tag helper or the `Menu` Liquid
shape. Menu content items are not built-in widget types.

## Razor Tag Helper

Import it with `@addTagHelper *, OrchardCore.Menu`, then:

```cshtml
<menu alias="alias:main-menu" cache-id="main-menu" cache-fixed-duration="00:05:00" cache-tag="alias:main-menu" cache-context="user.roles" />
```

| Attribute | Description |
|---|---|
| `alias` | Alias of the menu to render, e.g. `alias:main-menu`. |
| `cache-id` | Unique identifier for caching the output. |
| `cache-context` | Cache contexts, e.g. `user.roles` for permission-based items. |
| `cache-tag` | Tags used to invalidate the cached entry. |
| `cache-fixed-duration`, `cache-sliding-duration` | Cache expiration, e.g. `00:05:00`. |

The Liquid `shape` tag accepts the same values with underscores (`cache_id`, `cache_context`, `cache_tag`, `cache_fixed_duration`, `cache_sliding_duration`).

## Marking the Active Item

Menus are usually cached, so mark the current item client-side with the module's script, which adds a class (default `active`) to the parent of the link matching the current URL. In 4.0 it is a plain function, `window.activateLinks(container, options, callback)`, not a jQuery plugin:

```cshtml
<script asp-src="~/OrchardCore.Menu/Scripts/activate-links.min.js" debug-src="~/OrchardCore.Menu/Scripts/activate-links.js" at="Foot"></script>
<script at="Foot">
    document.addEventListener('DOMContentLoaded', function () {
        activateLinks(document.getElementById('mainNav'), { selector: 'a', traverse: 2 }, function (items) {
            items.forEach(function (item) { item.closest('.has-treeview')?.classList.add('menu-open'); });
        });
    });
</script>
```

`traverse` removes up to that many URL segments to activate a less specific link (e.g. `/todo-items` while on `/todo-items/Create`); `selector` targets a child of the link's parent.
