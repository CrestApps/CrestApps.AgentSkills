---
name: orchardcore-layers
description: Skill for configuring layers in Orchard Core. Covers layer creation, layer rules and conditions, zone-based widget visibility, JavaScript and URL rule expressions, and conditional content display. Use this skill when requests mention Orchard Core Layers, Configuring Layers for Conditional Widget Display, Enabling Layer Features, Available Conditions, Common Layer Patterns, Declaring Zones in Themes, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Layers, OrchardCore.Widgets, OrchardCore.Rules, OrchardCore.Scripting, LayerRule, HtmlWidget, HtmlBodyPart, BeforeContent, AfterContent, Widget. It also helps with Common Layer Patterns, Declaring Zones in Themes, Configuring Zones, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Layers - Prompt Templates

## Configuring Layers for Conditional Widget Display

You are an Orchard Core expert. Generate configuration and code for managing layers and conditional widget visibility in Orchard Core.

### Guidelines

- Layers are provided by the `OrchardCore.Layers` module and control when widgets are displayed in theme zones.
- A Layer has a name, description, and one or more **conditions** (rules) that determine when its widgets are visible.
- Widgets placed in a zone must be associated with a Layer; the widget renders only when the Layer's rules evaluate to `true`.
- Layers are managed from **Design > Widgets** in the admin panel.
- Available zones are configured in **Design > Settings > Zones** and must match theme layout sections.
- Conditions can be combined using `All` (AND) and `Any` (OR) condition groups.
- The `Always` layer uses a `Boolean` condition set to `true`, so widgets on this layer always render.
- Enable `OrchardCore.Layers` and `OrchardCore.Widgets` features for full widget/layer support.
- Always wrap recipe JSON in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier except View Models.
- Use file-scoped namespaces in C# examples.

### Enabling Layer Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Layers",
        "OrchardCore.Widgets"
      ],
      "disable": []
    }
  ]
}
```

### Available Conditions

| Condition          | Description                                                                   |
|--------------------|-------------------------------------------------------------------------------|
| `Homepage`         | True when the current page is the site homepage                               |
| `Is anonymous`     | True when the current user is not authenticated                               |
| `Is authenticated` | True when the current user is authenticated                                   |
| `Role`             | Evaluates the current user's roles against a value                            |
| `Url`              | Evaluates the current URL against a value                                     |
| `Culture`          | Evaluates the current UI culture against a value                              |
| `Content Type`     | Evaluates the currently displayed content type against a value                |
| `Javascript`       | A custom script condition written in JavaScript                               |
| `All`              | Condition group where all nested conditions must be true (AND logic)          |
| `Any`              | Condition group where at least one nested condition must be true (OR logic)   |
| `Boolean`          | A simple true/false condition                                                 |

### Common Layer Patterns

#### Always Layer

The `Always` layer uses a `Boolean` condition set to `true`. Widgets on this layer are always visible (e.g., site footer, header navigation).

#### Homepage Layer

Uses the `Homepage` condition so widgets only display on the homepage (e.g., hero banners, featured content).

#### Authenticated Layer

Uses the `Is authenticated` condition so widgets display only for logged-in users (e.g., dashboard links, user menus).

#### Anonymous Layer

Uses the `Is anonymous` condition so widgets display only for unauthenticated visitors (e.g., login prompts, signup banners).

### Declaring Zones in Themes

Zones must be declared as sections in the theme layout. Widgets are rendered in matching zones.

#### Liquid

```liquid
{% render_section "Header", required: false %}
{% render_section "Content", required: false %}
{% render_section "Footer", required: false %}
```

#### Razor

```html
@await RenderSectionAsync("Header", required: false)
@await RenderSectionAsync("Content", required: false)
@await RenderSectionAsync("Footer", required: false)
```

### Configuring Zones

Set available zone names in **Design > Settings > Zones** in the admin. Common zone names include:

- `Header`
- `Navigation`
- `BeforeContent`
- `Content`
- `AfterContent`
- `Sidebar`
- `Footer`

### Recipe: Creating Layers

```json
{
  "steps": [
    {
      "name": "Layers",
      "Layers": [
        {
          "Name": "Always",
          "Description": "Widgets on this layer are always displayed.",
          "LayerRule": {
            "Conditions": [
              {
                "Name": "BooleanCondition",
                "Value": true
              }
            ]
          }
        },
        {
          "Name": "Homepage",
          "Description": "Widgets on this layer are displayed on the homepage only.",
          "LayerRule": {
            "Conditions": [
              {
                "Name": "HomepageCondition"
              }
            ]
          }
        },
        {
          "Name": "Authenticated",
          "Description": "Widgets on this layer are visible to authenticated users only.",
          "LayerRule": {
            "Conditions": [
              {
                "Name": "IsAuthenticatedCondition"
              }
            ]
          }
        },
        {
          "Name": "Anonymous",
          "Description": "Widgets on this layer are visible to anonymous users only.",
          "LayerRule": {
            "Conditions": [
              {
                "Name": "IsAnonymousCondition"
              }
            ]
          }
        }
      ]
    }
  ]
}
```

### Recipe: Adding a Widget to a Layer and Zone

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js:uuid()]",
          "ContentType": "HtmlWidget",
          "DisplayText": "Footer Copyright",
          "Latest": true,
          "Published": true,
          "LayerMetadata": {
            "Title": "Footer Copyright",
            "Layer": "Always",
            "Zone": "Footer",
            "Position": 10
          },
          "HtmlBodyPart": {
            "Html": "<p>&copy; 2025 My Company. All rights reserved.</p>"
          }
        }
      ]
    }
  ]
}
```

### Using Condition Groups

Combine conditions with `All` (AND) or `Any` (OR) groups for complex rules:

#### Example: Show widget on homepage for authenticated users only

Use an `All` group with both `Homepage` and `Is authenticated` conditions:

1. Navigate to **Design > Widgets**.
2. Create or edit a Layer.
3. Add an **All** condition group.
4. Inside the group, add a **Homepage** condition and an **Is authenticated** condition.

#### Example: Show widget on homepage or for admin role

Use an `Any` group with a `Homepage` condition and a `Role` condition set to `Administrator`:

1. Navigate to **Design > Widgets**.
2. Create or edit a Layer.
3. Add an **Any** condition group.
4. Inside the group, add a **Homepage** condition and a **Role** condition set to `Administrator`.

### Creating Custom Conditions

Implement custom layer conditions by following the Rules module pattern. Refer to the `OrchardCore.Rules` module for guidance on creating custom condition evaluators.

### JavaScript Conditions

The `Javascript` condition allows writing custom rule logic in JavaScript. Use the scripting API provided by `OrchardCore.Scripting`:

```javascript
// Example: Show widget only on URLs starting with /blog
url().startsWith('/blog')
```

### Widget Management Workflow

1. Enable `OrchardCore.Layers` and `OrchardCore.Widgets` features.
2. Configure zones in **Design > Settings > Zones**.
3. Create layers with appropriate conditions in **Design > Widgets**.
4. Create widget content types with the `Widget` stereotype.
5. Add widgets to zones and associate them with layers.
6. Widgets display only when their associated layer's conditions evaluate to `true`.
