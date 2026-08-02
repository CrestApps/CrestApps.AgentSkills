---
name: orchardcore-liquid
description: Skill for using Liquid templates in Orchard Core. Covers Liquid syntax, global objects, zones, render_section, shape rendering, alias handles, content access, Orchard Core tags and filters, and Liquid best practices. Use this skill when requests mention Orchard Core Liquid, Write Liquid Templates, Global Objects, Content Item Access, Accessing Content Parts and Fields, Orchard Core Liquid Tags, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Liquid

Liquid templates use the `.liquid` extension and receive Orchard Core globals,
shapes, filters, and tags.

```liquid
{{ Site.SiteName }}
{{ Culture.Name }}
{{ User.Identity.Name }}
{{ Request.Path }}
```

Use `Culture.Name` for the current request culture; do not treat
`Site.Culture` as the current UI culture.

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

## Content and Localized Text

```liquid
{{ Model.ContentItem.DisplayText }}
{{ Model.ContentItem.Content.TitlePart.Title }}
{{ Model.ContentItem.Content.AutoroutePart.Path }}
{{ "Welcome to Orchard Core" | t }}
{{ "Hello {0}!" | t: User.Identity.Name }}
```

Use the `t` filter for localized UI text. Use normal Fluid control flow and
Orchard filters for content:

```liquid
{% if User | has_permission: "EditContent" %}
  <a href="{{ Model.ContentItem | edit_url }}">Edit</a>
{% endif %}

{% assign item = featuredContentItemId | content_item_id %}
{{ item.DisplayText }}
```

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
