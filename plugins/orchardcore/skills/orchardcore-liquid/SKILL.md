---
name: orchardcore-liquid
description: Skill for using Liquid templates in Orchard Core. Covers Liquid syntax, global objects, zones, render_section, shape rendering, alias handles, content access, Orchard Core tags and filters, LiquidPart and the new LiquidField, the ManageLiquidTemplates permission, Site property redaction with PermittedSiteProperties, Liquid parser options, ILiquidTemplateManager, Liquid shape templates, and Liquid best practices. Use this skill when requests mention Orchard Core Liquid, Write Liquid Templates, Global Objects, Content Item Access, LiquidPart, LiquidField, ManageLiquidTemplates, SettingsLiquidOptions, Site.Properties, RenderLiquid migration, slugify, transliterate, Orchard Core Liquid Tags, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Liquid

Liquid templates use the `.liquid` extension and receive Orchard Core globals,
shapes, filters, and tags. Liquid is powered by Fluid and all output is HTML
encoded unless the `raw` filter is used.

```liquid
{{ Site.SiteName }}
{{ Culture.Name }}
{{ User.Identity.Name }}
{{ Request.Path }}
```

Use `Culture.Name` for the current request culture; do not treat
`Site.Culture` as the current UI culture.

## Where Liquid Executes

Liquid runs only in shape templates (theme or admin **Templates**), shortcode
templates, `LiquidPart`, `LiquidField`, the Facebook plugin part, Liquid
settings such as patterns, and explicit APIs (`ILiquidTemplateManager`, the
`liquid` filter, `Orchard.LiquidToHtmlAsync()`). `HtmlBodyPart`, `HtmlField`,
`MarkdownBodyPart`, and `MarkdownField` treat Liquid syntax as plain source
text; they have no `RenderLiquid` setting anymore.

### LiquidPart and LiquidField

Both store the authored source in a `Liquid` property exactly as submitted,
validate it on save, and render it when the shape, body aspect, or GraphQL
`liquid` field is requested. They have no **Sanitize HTML** setting because
Liquid can produce HTML, JSON, XML, or plain text. `LiquidField` has no
field-specific settings and only the standard editor (a CodeMirror Liquid
editor). Both are registered by the `OrchardCore.Liquid` feature when
`OrchardCore.Contents` is enabled.

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("LandingPage", part => part
    .WithField("Intro", field => field
        .OfType("LiquidField")
        .WithDisplayName("Intro")
        .WithPosition("1")));

await _contentDefinitionManager.AlterTypeDefinitionAsync("Snippet", type => type
    .WithPart("TitlePart", part => part.WithPosition("0"))
    .WithPart("LiquidPart", part => part.WithPosition("1")));
```

The `LiquidField` shape (`LiquidField`, `LiquidFieldViewModel`) exposes
`Liquid` (source), `Html` (rendered output), `Field`, `Part`,
`PartFieldDefinition`, and `ContentItem`. Inside the source, `ContentItem` is
the containing content item:

```liquid
<h2>{{ ContentItem.DisplayText }}</h2>
<p>{{ "now" | local | date: "%Y" }} {{ Site.SiteName }}</p>
```

Recipe content stores the source under the field or part name:

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js:uuid()]",
          "ContentType": "LandingPage",
          "DisplayText": "Home",
          "Latest": true,
          "Published": true,
          "LandingPage": {
            "Intro": { "Liquid": "<p>Welcome to {{ Site.SiteName }}</p>" }
          }
        }
      ]
    }
  ]
}
```

### ManageLiquidTemplates Permission

`OrchardCore.Liquid.Permissions.ManageLiquidTemplates` is security critical and
is granted to **Administrator** and **Editor** by default (the migration keeps
existing Editor access). It is required, in addition to the normal content or
template permission, to:

- Edit `LiquidPart`, `LiquidField`, or `FacebookPluginPart` bodies.
- Edit, create, or publish any content item that contains one of them, directly
  or recursively (for example a widget inside `FlowPart`, `BagPart`, or custom
  JSON). This applies to the admin editors, the Content API, and remote
  publishing; an unauthorized mutation is rejected in full.
- Use **Templates**, **Admin Templates**, and template previews. Template
  navigation and content-definition template shortcuts are hidden without it.

```csharp
if (!await _authorizationService.AuthorizeAsync(User, OrchardCore.Liquid.Permissions.ManageLiquidTemplates))
{
    return Forbid();
}
```

## Site Properties

`{{ Site.SiteSalt }}` was removed for security reasons and only returns
`[REDACTED]`. `{{ Site.Properties }}` is redacted by
default because site settings can hold secrets. Allow specific property
sections with `SettingsLiquidOptions.PermittedSiteProperties`:

```json
{
  "OrchardCore": {
    "Liquid": {
      "PermittedSiteProperties": [
        "LayerSettings",
        "CurrentThemeName"
      ]
    }
  }
}
```

```liquid
{{ Site.Properties.LayerSettings.Zones | join: ", " }}
```

## Parser Configuration

The parser options live in the `OrchardCore:Liquid` section (the old
`OrchardCore_Liquid` section is deprecated but still read). All default to
`false`:

```json
{
  "OrchardCore": {
    "Liquid": {
      "AllowLiquidTag": true,
      "AllowParentheses": false,
      "AllowTrailingQuestionMark": false
    }
  }
}
```

Enable `AllowLiquidTag` only when the tenant needs the `{% liquid %}` tag.

## Slugs and Transliteration

`slugify` lowercases text, strips diacritics, and replaces separators with `-`;
it keeps non-Latin letters and no longer accepts a `transliterate` argument.
Chain `transliterate` (provided by `OrchardCore.Localization`) when an ASCII
slug is required:

```liquid
{{ "Crème brûlée" | slugify }}            {% comment %} creme-brulee {% endcomment %}
{{ "Ελληνικά" | transliterate | slugify }} {% comment %} ellinika {% endcomment %}
```

## Zones and Sections

`zone` is a block tag that adds its child content to a named layout zone. Use
`render_section` to render a zone:

```liquid
{% zone "Header" %}
  <span class="announcement">Welcome</span>
{% endzone %}

{% render_section "Header", required: false %}
{% render_section "Content", required: true %}
```

## Shapes and Content Handles

```liquid
{% shape "Menu", alias: "alias:main-menu" %}
{% contentitem alias: "alias:featured-article", display_type: "Summary" %}
{% contentitem id: "4j42fxryjcpqkkacmg6cwz3pr5" %}
```

An alias argument receives an Orchard content handle, including the `alias:`
prefix. Render an already built shape with `shape_render`:

```liquid
{{ Model.Content.HtmlBodyPart | shape_render }}
```

Render the part or field shape rather than its stored source. The stored HTML
or Markdown is the authored, unsanitized source; the shape output has
shortcodes processed and is sanitized when `SanitizeHtml` is enabled. If you
must output stored HTML directly, sanitize it explicitly:

```liquid
{{ Model.ContentItem.Content.Article.Summary.Html | sanitize_html | raw }}
```

## Content and Localized Text

```liquid
{{ Model.ContentItem.DisplayText }}
{{ Model.ContentItem.Content.AutoroutePart.Path }}
{{ "Welcome to Orchard Core" | t }}
{{ "Hello {0}!" | t: User.Identity.Name }}
```

Use the `t` filter for localized UI text; it also works in workflow Liquid
expressions. Use normal Fluid control flow and Orchard filters for content:

```liquid
{% if User | has_permission: "EditContent", Model.ContentItem %}
  <a href="{{ Model.ContentItem | edit_url }}">Edit</a>
{% endif %}

{% assign item = featuredContentItemId | content_item_id %}
{{ item.DisplayText }}
```

## Data Localization Filters

The `d` filter (from `OrchardCore.DataLocalization`) translates dynamic data,
such as content type or field display names, using a context:

```liquid
{{ "Blog" | d: "Content Types" }}
{{ "New {0}" | d: "Content Types", "Article" }}
```

## Liquid Shape Templates

`.liquid` shape templates are rendered by `LiquidShapeTemplateViewEngine`,
directly with Fluid, instead of as Razor partials of a `LiquidPage`. The
`LiquidViewExtensionProvider` is no longer registered and the Razor shape
engine ignores `.liquid` files. Liquid files used as controller views still use
`LiquidPage`. PO localization contexts are unchanged.

## Rendering Liquid from Code

Use `ILiquidTemplateManager` for explicit, trusted templates:

```csharp
public sealed class SnippetRenderer
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly HtmlEncoder _htmlEncoder;

    public SnippetRenderer(ILiquidTemplateManager liquidTemplateManager, HtmlEncoder htmlEncoder)
    {
        _liquidTemplateManager = liquidTemplateManager;
        _htmlEncoder = htmlEncoder;
    }

    public async Task<string> RenderAsync(string template, ContentItem contentItem)
    {
        if (!_liquidTemplateManager.Validate(template, out var errors))
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        return await _liquidTemplateManager.RenderStringAsync(
            template,
            _htmlEncoder,
            contentItem,
            new Dictionary<string, FluidValue> { ["ContentItem"] = new ObjectValue(contentItem) });
    }
}
```

`LiquidViewTemplateExtensions.RenderAsync()` renders a parsed template in a
Liquid view scope; `InvokeInScopeAsync()` runs a callback with `Model` and
`ViewLocalizer` set from an explicit `ViewContext` (pass `null` outside a
request). Only run the `liquid` filter or `Orchard.LiquidToHtmlAsync()` on
trusted source; running them on `HtmlField` or `HtmlBodyPart` content
re-enables Liquid for editors who do not have `ManageLiquidTemplates`.

## Custom Filters

```csharp
using Fluid;
using Fluid.Values;

namespace MyModule;

public sealed class ReadingTimeFilter : ILiquidFilter
{
    public ValueTask<FluidValue> ProcessAsync(
        FluidValue input,
        FilterArguments arguments,
        LiquidTemplateContext context)
    {
        var wordCount = input.ToStringValue()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Length;

        return ValueTask.FromResult<FluidValue>(new StringValue($"{Math.Max(1, wordCount / 200)} min read"));
    }
}
```

```csharp
services.AddLiquidFilter<ReadingTimeFilter>("reading_time");
```

## Upgrading from 3.x

- `RenderLiquid` on `HtmlBodyPart`, `HtmlField`, `MarkdownBodyPart`, and
  `MarkdownField` is gone; Liquid in that source now renders as literal text.
  Tenant startup logs a warning per affected type, part, and field. Move the
  source to `LiquidPart` or a `LiquidField` manually.
- Grant `ManageLiquidTemplates` to custom roles that edit Liquid parts or
  fields, content containing them, or templates.
- Stop using `{{ Site.SiteSalt }}` (now `[REDACTED]`) and list any `Site.Properties`
  sections you read in `OrchardCore:Liquid:PermittedSiteProperties`.
- `slugify: transliterate: true` is no longer supported; use
  `transliterate | slugify`.
- Rename the `OrchardCore_Liquid` configuration section to `OrchardCore:Liquid`.

See [references/liquid-examples.md](references/liquid-examples.md) for layout,
content, and zone examples.
