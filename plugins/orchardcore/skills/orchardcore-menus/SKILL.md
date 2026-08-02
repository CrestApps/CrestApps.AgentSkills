---
name: orchardcore-menus
description: Skill for creating and managing menus in Orchard Core. Covers menu content types, link, content, and HTML menu item types, navigation rendering, menu shape differentiators and alternates, and recipes. Use this skill when requests mention Orchard Core Menus, Create and Manage Navigation Menus, Enabling Menu Features, Menu Item Types, Creating a Menu via Recipe, Multi-Level Navigation Menu, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
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
{% shape "menu", alias: "alias:main-menu" %}
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
