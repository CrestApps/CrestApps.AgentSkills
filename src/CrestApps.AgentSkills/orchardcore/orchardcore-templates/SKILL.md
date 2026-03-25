---
name: orchardcore-templates
description: Skill for customizing templates in Orchard Core via the admin UI. Covers shape template overrides, content type templates, content part templates, content field templates, differentiated display types, Liquid template syntax, admin template editing, and template precedence rules.
---

# Orchard Core Templates - Prompt Templates

## Customizing Templates in Orchard Core

You are an Orchard Core expert. Generate template overrides and explain template naming conventions for Orchard Core shapes.

### Guidelines

- The `OrchardCore.Templates` module allows creating custom Liquid templates via the admin web editor.
- Orchard Core renders **Shapes**, not HTML directly. Each shape has a type and a list of **alternates** — potential template names.
- Templates can be defined in the admin UI or in theme files. Admin templates take precedence over theme files.
- Template names use `__` (double underscore) as a separator for specificity levels.
- In file-based themes, `__` in template names maps to `-` in filenames, and `_` before display type maps to `.` (e.g., `Content_Summary__BlogPost` → `Content-BlogPost.Summary.cshtml`).
- Display types include `Detail` (default for full page), `Summary` (lists), `SummaryAdmin` (admin lists), etc.
- For custom model display drivers, Orchard also resolves root shapes like `CampaignAction_Edit` and `CampaignAction_SummaryAdmin`, which map to `CampaignAction.Edit.cshtml` and `CampaignAction.SummaryAdmin.cshtml`.
- If child shapes are placed into zones such as `Content`, `Actions`, `Meta`, or `Description`, the root template must render those zones explicitly.
- Use `ConsoleLog` (Razor) or `console_log` (Liquid) to inspect available alternates for any shape.
- All C# classes must use the `sealed` modifier except View Models.
- Use file-scoped namespaces in C# examples.

### Content Type Templates

#### `Content__[ContentType]`

Renders a content item when displayed at its own URL (Detail display type).

| Template Name       | Filename                  |
|---------------------|---------------------------|
| `Content__BlogPost` | `Content-BlogPost.cshtml` |
| `Content__Article`  | `Content-Article.cshtml`  |

**Available properties:**

| Property                    | Description                                                    |
|-----------------------------|----------------------------------------------------------------|
| `Model.Content`             | Zone shape containing all shapes from parts and fields         |
| `Model.ContentItem`         | The current content item being rendered                        |
| `Model.ContentItem.Content` | JSON object with all content item data                         |

#### `Content_[DisplayType]__[ContentType]`

Renders a content item with a specific display type (e.g., Summary in lists).

| Template Name               | Filename                          |
|-----------------------------|-----------------------------------|
| `Content_Summary__BlogPost` | `Content-BlogPost.Summary.cshtml` |
| `Content_Summary__Article`  | `Content-Article.Summary.cshtml`  |

#### `Content__Alias__[Alias]`

Renders a content item by its alias (requires `AliasPart`).

| Template Name              | Filename                       |
|----------------------------|--------------------------------|
| `Content__Alias__example`  | `Content-Alias-example.cshtml` |

#### `Content__Slug__[Slug]`

Renders a content item by its slug (requires `AutoroutePart`). Path separators `/` become `__` in template names and `-` in filenames.

| Template Name                   | Filename                           |
|---------------------------------|------------------------------------|
| `Content__Slug__example`        | `Content-Slug-example.cshtml`      |
| `Content__Slug__blog__my__post` | `Content-Slug-blog-my-post.cshtml` |

### Widget Templates

#### `Widget__[ContentType]`

Renders a widget on a page.

| Template Name        | Filename                   |
|----------------------|----------------------------|
| `Widget__Paragraph`  | `Widget-Paragraph.cshtml`  |
| `Widget__Blockquote` | `Widget-Blockquote.cshtml` |

**Available properties:**

| Property                    | Description                                              |
|-----------------------------|----------------------------------------------------------|
| `Model.Content`             | Zone shape with all shapes from the widget's parts/fields |
| `Model.ContentItem`         | The widget content item                                  |
| `Model.ContentItem.Content` | JSON object with widget data                             |
| `Model.Classes`             | Array of CSS classes attached to the widget              |

### Content Part Templates

#### `[ShapeType]`

Default template for a content part shape. The shape type usually matches the part name.

| Template Name  | Filename              |
|----------------|-----------------------|
| `HtmlBodyPart` | `HtmlBodyPart.cshtml` |

#### `[ShapeType]_[DisplayType]`

Part shape with a specific display type.

| Template Name          | Filename                      |
|------------------------|-------------------------------|
| `HtmlBodyPart_Summary` | `HtmlBodyPart.Summary.cshtml` |

#### `[ContentType]__[PartType]`

Part shape scoped to a specific content type.

| Template Name            | Filename                     |
|--------------------------|------------------------------|
| `Blog__HtmlBodyPart`     | `Blog-HtmlBodyPart.cshtml`   |
| `LandingPage__BagPart`   | `LandingPage-BagPart.cshtml` |

#### `[ContentType]_[DisplayType]__[PartType]`

Part shape scoped to content type and display type.

| Template Name                     | Filename                             |
|-----------------------------------|--------------------------------------|
| `Blog_Summary__HtmlBodyPart`      | `Blog-HtmlBodyPart.Summary.cshtml`   |

#### `[ContentType]__[PartName]`

Named part scoped to a content type (for reusable named parts like BagPart).

| Template Name           | Filename                      |
|-------------------------|-------------------------------|
| `LandingPage__Services` | `LandingPage-Services.cshtml` |

### Custom Model Shape Templates

Display drivers for non-content-item models follow the same shape naming rules, but they often need both a root wrapper template and child-zone templates.

Example shape names and filenames:

| Shape Name | Filename |
|---|---|
| `CampaignAction_Edit` | `CampaignAction.Edit.cshtml` |
| `CampaignAction_SummaryAdmin` | `CampaignAction.SummaryAdmin.cshtml` |
| `CampaignActionFields_Edit` | `CampaignActionFields.Edit.cshtml` |
| `CampaignAction_Fields_SummaryAdmin` | `CampaignAction.Fields.SummaryAdmin.cshtml` |
| `CampaignAction_Buttons_SummaryAdmin` | `CampaignAction.Buttons.SummaryAdmin.cshtml` |

When a driver uses placement such as `.Location("SummaryAdmin", "Content:1")` and `.Location("SummaryAdmin", "Actions:5")`, `CampaignAction.SummaryAdmin.cshtml` must render `Model.Content` and `Model.Actions` for those child shapes to appear.

### Content Field Templates

#### `[ShapeType]_[DisplayType]`

Field shape with a display type.

| Template Name       | Filename                   |
|---------------------|----------------------------|
| `TextField_Summary` | `TextField.Summary.cshtml` |

#### `[PartType]__[FieldName]`

Field scoped to a part type.

| Template Name                | Filename                          |
|------------------------------|-----------------------------------|
| `HtmlBodyPart__Description`  | `HtmlBodyPart-Description.cshtml` |

#### `[ContentType]__[PartName]__[FieldName]`

Field scoped to content type and part name.

| Template Name                     | Filename                                  |
|-----------------------------------|-------------------------------------------|
| `Blog__HtmlBodyPart__Description` | `Blog-HtmlBodyPart-Description.cshtml`    |
| `LandingPage__Services__Image`    | `LandingPage-Services-Image.cshtml`       |

### Widget / Stereotype Part Templates

Parts attached to stereotyped content types (e.g., `Widget`) get additional alternates:

| Template Name               | Filename                         |
|-----------------------------|----------------------------------|
| `Widget__HtmlBodyPart`      | `Widget-HtmlBodyPart.cshtml`     |

### Zone Templates

#### `Zone__[ZoneName]`

Renders a layout zone.

| Template Name   | Filename              |
|-----------------|-----------------------|
| `Zone__Footer`  | `Zone-Footer.cshtml`  |
| `Zone__Content` | `Zone-Content.cshtml` |

**Available properties:**

| Property         | Description                                   |
|------------------|-----------------------------------------------|
| `Model.Items`    | Child shapes to render from the zone          |
| `Model.Parent`   | Parent zone shape (Layout for root zones)     |
| `Model.ZoneName` | Name of the zone (e.g., "Footer", "Content")  |

#### Rendering Zone Children

**Liquid:**

```liquid
{% for shape in Model.Items %}
    {{ shape | shape_render }}
{% endfor %}
```

**Razor:**

```csharp
@foreach (var shape in Model)
{
    @await DisplayAsync(shape);
}
```

### Shape Differentiators

Differentiators uniquely identify a shape in a zone for placement and template access:

- **Part differentiator:** `[PartName]` if shape type equals part name, otherwise `[PartName]-[ShapeType]`.
- **Field differentiator:** `[PartName]-[FieldName]` if shape type equals field name, otherwise `[PartName]-[FieldName]-[ShapeType]`.

#### Accessing Shapes by Differentiator

**Razor:**

```csharp
// Access by name
Model.Content.HtmlBodyPart;
Model.Content.Named("ListPart-ListPartFeed");

// Remove a shape
Model.Content.Remove("HtmlBodyPart");
```

**Liquid:**

```liquid
{%- comment -%} Display a specific shape {%- endcomment -%}
{{ Model.Content.HtmlBodyPart | shape_render }}

{%- comment -%} Remove then render remaining {%- endcomment -%}
{% shape_remove_item Model.Content "HtmlBodyPart" %}
{{ Model.Content | shape_render }}

{%- comment -%} Access field shape for a field added directly to content type {%- endcomment -%}
{{ Model.Content["Article-Description"] | shape_render }}

{%- comment -%} Access field property directly {%- endcomment -%}
{{ Model.Content["Article-Description"].Field.Text }}
```

### Content Part Display Modes

Parts that support display modes get additional alternates:

| Template Name                                        | Filename                                                  |
|------------------------------------------------------|-----------------------------------------------------------|
| `TitlePart_Summary__CustomMode_Display`               | `TitlePart-CustomMode.Display.Summary.cshtml`             |
| `LandingPage_Display__TitlePart__CustomMode`          | `LandingPage-TitlePart-CustomMode.Display.cshtml`         |
| `LandingPage_Summary__TitlePart__CustomMode_Display`  | `LandingPage-TitlePart-CustomMode.Display.Summary.cshtml` |

### Template Precedence

Templates are resolved in this order (highest to lowest priority):

1. Admin-defined templates (via Templates module)
2. Active theme templates
3. Module-provided templates

Within theme/module templates, more specific alternates take priority over generic ones.

### Overriding Module Views

Some module views (not shapes) can be overridden in your theme by placing them at:

```
YourTheme/Views/[ModuleName]/[Controller]/[Action].cshtml
```

Example: Override the login page:

```
YourTheme/Views/OrchardCore.Users/Account/Login.cshtml
```

### Example: Custom Blog Post Detail Template

**Admin template name:** `Content__BlogPost`

```liquid
<article>
    <h1>{{ Model.ContentItem.DisplayText }}</h1>
    <div class="metadata">
        <span>Published: {{ Model.ContentItem.PublishedUtc | date: "%B %d, %Y" }}</span>
        <span>By: {{ Model.ContentItem.Author }}</span>
    </div>

    {% shape_remove_item Model.Content "TitlePart" %}
    {{ Model.Content | shape_render }}
</article>
```

### Example: Custom Summary Template for Lists

**Admin template name:** `Content_Summary__BlogPost`

```liquid
<div class="blog-post-summary">
    <h2><a href="{{ Model.ContentItem | href }}">{{ Model.ContentItem.DisplayText }}</a></h2>
    <p>{{ Model.ContentItem.Content.HtmlBodyPart.Html | strip_html | truncate: 200 }}</p>
    <small>{{ Model.ContentItem.PublishedUtc | date: "%B %d, %Y" }}</small>
</div>
```
