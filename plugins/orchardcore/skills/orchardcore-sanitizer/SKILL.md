---
name: orchardcore-sanitizer
description: Skill for configuring HTML sanitization in Orchard Core. Covers the HtmlSanitizer service for XSS prevention, final-output sanitization of HTML and Markdown, source persisted as authored, the sanitized fragment cache, IHtmlSanitizerService, the sanitize_html Liquid filter, Razor sanitize helpers, configuring allowed tags and attributes, scheme allowlists, and custom sanitization configuration. Use this skill when requests mention Orchard Core Sanitizer, Configure HTML Sanitization, Parts and Fields Using the Sanitizer, Orchard Core Default Sanitizer Customizations, Sanitizing HTML in Razor Views, Configuring the Sanitizer in Program.cs, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with HtmlBodyPart, HtmlField, HtmlMenuItemPart, MarkdownBodyPart, MarkdownField, SanitizeHtml, @Orchard.SanitizeHtml(), ConfigureHtmlSanitizer, HtmlSanitizerOptions, AllowedTags.Add('iframe'), AllowedSchemes.Add('ftp').
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Sanitizer - Prompt Templates

## Configure HTML Sanitization

You are an Orchard Core expert. Generate HTML sanitization configurations and custom sanitizer settings for Orchard Core.

### Guidelines

- The HTML Sanitizer is part of Orchard Core's infrastructure — no additional feature needs to be enabled.
- The sanitizer prevents XSS attacks by cleaning the **final rendered** HTML. Authored HTML and Markdown source is stored unchanged; nothing is sanitized on save.
- It is used by default for `HtmlBodyPart`, `HtmlField`, `HtmlMenuItemPart`, `MarkdownBodyPart`, and `MarkdownField`.
- Sanitization can be disabled per field/part via the `Sanitize Html` option (`SanitizeHtml = false`) for trusted output.
- Use the `@Orchard.SanitizeHtml()` Razor helper, the `sanitize_html` Liquid filter, or `IHtmlSanitizerService.Sanitize()` in code.
- Orchard Core customizes the default HtmlSanitizer by allowing the `class` attribute, allowing `mailto` and `tel` schemes, and removing the `form` tag.
- Configure the sanitizer using `ConfigureHtmlSanitizer` during service registration; multiple calls are combined.
- Sanitizer configuration actions must be deterministic, because results are cached per tenant (see below).
- The sanitizer is based on the [HtmlSanitizer](https://github.com/mganss/HtmlSanitizer) library.
- Always seal classes.

### Parts and Fields Using the Sanitizer

| Part / Field | Pipeline before sanitization |
|-------------|-------------|
| `HtmlBodyPart` | Authored HTML, then shortcodes. |
| `HtmlField` | Authored HTML, then shortcodes. |
| `HtmlMenuItemPart` | Sanitized on a cloned content item when the menu renders; unsafe URL schemes are rejected. |
| `MarkdownBodyPart` | Markdown converted to HTML, then shortcodes. |
| `MarkdownField` | Markdown converted to HTML, then shortcodes. |

This applies consistently to Detail, Summary, Preview, `BodyAspect`, GraphQL
rendered HTML, and the `MarkdownToHtmlAsync` Razor helper. `LiquidPart` and
`LiquidField` have no sanitize setting because their output may not be HTML.

Because the stored source is unsanitized, render the part or field shape (or
sanitize explicitly) instead of writing stored `Html` with `raw`:

```liquid
{{ Model.Content.HtmlBodyPart | shape_render }}
{{ Model.ContentItem.Content.Article.Summary.Html | sanitize_html | raw }}
```

### Orchard Core Default Sanitizer Customizations

Orchard Core modifies the base HtmlSanitizer defaults:

| Change | Description |
|--------|-------------|
| Allow `class` attribute | The `class` attribute is added to the allowed attributes list. |
| Allow `mailto` scheme | The `mailto:` URI scheme is allowed in links. |
| Allow `tel` scheme | The `tel:` URI scheme is allowed in links. |
| Remove `form` tag | The `<form>` tag is removed from the allowed tags list. |

### Sanitized Result Cache

Content is sanitized each time it is displayed. `HtmlSanitizerService` caches
the sanitized result of fragments up to 64K characters per tenant (bounded to a
few MB of text), so repeated displays skip parsing. Consequently, actions
registered with `ConfigureHtmlSanitizer()`, including sanitizer event handlers
such as `PostProcessNode`, must return the same result for the same input;
do not make them depend on the current user, request, or time.

### Sanitizing HTML in Razor Views

```html
@Orchard.SanitizeHtml((string)Model.ContentItem.Content.HtmlBodyPart.Html)
```

### Sanitizing HTML in Code

```csharp
public sealed class CommentFormatter
{
    private readonly IHtmlSanitizerService _htmlSanitizerService;

    public CommentFormatter(IHtmlSanitizerService htmlSanitizerService)
    {
        _htmlSanitizerService = htmlSanitizerService;
    }

    public string Format(string html)
        => _htmlSanitizerService.Sanitize(html);
}
```

`IHtmlSanitizerService` lives in `OrchardCore.Infrastructure.Html`.

### Configuring the Sanitizer in Program.cs

```csharp
builder.Services
    .AddOrchardCms()
    .ConfigureServices(tenantServices =>
        tenantServices.ConfigureHtmlSanitizer((sanitizer) =>
        {
            // Allow additional URI schemes.
            sanitizer.AllowedSchemes.Add("ftp");
            sanitizer.AllowedSchemes.Add("data");
        }));
```

### Allowing Additional Tags

```csharp
builder.Services
    .AddOrchardCms()
    .ConfigureServices(tenantServices =>
        tenantServices.ConfigureHtmlSanitizer((sanitizer) =>
        {
            // Allow <iframe> tags for embedded content.
            sanitizer.AllowedTags.Add("iframe");
        }));
```

### Allowing Additional Attributes

```csharp
builder.Services
    .AddOrchardCms()
    .ConfigureServices(tenantServices =>
        tenantServices.ConfigureHtmlSanitizer((sanitizer) =>
        {
            // Allow data attributes and srcset.
            sanitizer.AllowDataAttributes = true;
            sanitizer.AllowedAttributes.Add("srcset");
        }));
```

### Removing Tags from the Allow List

```csharp
builder.Services
    .AddOrchardCms()
    .ConfigureServices(tenantServices =>
        tenantServices.ConfigureHtmlSanitizer((sanitizer) =>
        {
            // Disallow inline styles and embedded media.
            sanitizer.AllowedAttributes.Remove("style");
            sanitizer.AllowedTags.Remove("video");
        }));
```

### Combining Multiple Sanitizer Configurations

```csharp
builder.Services
    .AddOrchardCms()
    .ConfigureServices(tenantServices =>
        tenantServices.ConfigureHtmlSanitizer((sanitizer) =>
        {
            // Allow iframes for video embeds.
            sanitizer.AllowedTags.Add("iframe");

            // Allow additional URI schemes.
            sanitizer.AllowedSchemes.Add("ftp");

            // Allow the target attribute on links.
            sanitizer.AllowedAttributes.Add("target");
        }));
```

### Disabling Sanitization on a Field or Part

To disable sanitization for a specific field or part:

1. Navigate to the content type definition in the admin UI.
2. Edit the field or part settings (e.g., `HtmlBodyPart`).
3. Uncheck the **Sanitize Html** option.

Or in a migration: `.WithSettings(new HtmlBodyPartSettings { SanitizeHtml = false })`.
Only do this when every editor of the content type is trusted.

### Common Sanitizer Configuration Scenarios

| Scenario | Configuration |
|----------|---------------|
| Allow YouTube/Vimeo embeds | `AllowedTags.Add("iframe")` |
| Allow FTP links | `AllowedSchemes.Add("ftp")` |
| Allow data URIs for inline images | `AllowedSchemes.Add("data")` |
| Allow custom data attributes | `AllowDataAttributes = true` |
| Allow heading ids from Markdown `autoidentifiers` | `AllowedAttributes.Add("id")` |
| Allow target attribute on links | `AllowedAttributes.Add("target")` |
| Block all inline styles | `AllowedAttributes.Remove("style")` |

### Upgrading from 3.x

- Before: HTML and Markdown parts and fields sanitized the source when saving.
  After: the source is stored as authored and only the final rendered HTML is
  sanitized. Templates or APIs that output the stored `Html` directly must now
  sanitize it themselves.
- Sanitizer configuration actions must be deterministic because of the new
  per-tenant result cache.
