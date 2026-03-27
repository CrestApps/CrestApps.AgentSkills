---
name: orchardcore-shortcodes
description: Skill for creating and using shortcodes in Orchard Core. Covers shortcode template creation via admin UI, code-based shortcode providers using delegate and class patterns, built-in shortcodes (image, asset_url, locale), and shortcode integration with content fields. Use this skill when requests mention Orchard Core Shortcodes, Create and Use Shortcodes, Enabling Shortcodes, Shortcode Templates (Admin UI), Template Arguments, Example [display_text] Template, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Shortcodes, OrchardCore.Shortcodes.Templates, OrchardCore.Localization, IShortcodeProvider, HtmlBodyPart, HtmlField, MarkdownBodyPart, MarkdownField, IServiceCollection, AlertShortcodeProvider. It also helps with Template Arguments, Example [display_text] Template, Example [site_name] Template, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Shortcodes - Prompt Templates

## Create and Use Shortcodes

You are an Orchard Core expert. Generate code, templates, and configuration for working with shortcodes — small bracketed tokens that add dynamic behavior to content editors.

### Guidelines

- Shortcodes are tokens wrapped in `[brackets]` that inject dynamic content into HTML or Markdown fields.
- Enable the `OrchardCore.Shortcodes` feature to use shortcodes; enable `OrchardCore.Shortcodes.Templates` for admin-managed templates.
- Shortcode templates created in the admin UI (_Design → Shortcodes_) use Liquid syntax.
- Admin-created shortcode templates can override code-based shortcodes with the same name.
- Code-based shortcodes are registered using `ShortcodeDelegate` (inline lambda) or `IShortcodeProvider` (class-based).
- Built-in shortcodes include `[image]`, `[asset_url]`, and `[locale]`.
- Shortcodes are automatically rendered by display drivers for `HtmlBodyPart`, `HtmlField`, `MarkdownBodyPart`, and `MarkdownField`.
- For manual rendering, use the `shortcode` Liquid filter or `Orchard.ShortcodesToHtmlAsync()` Razor helper.
- All C# classes must use the `sealed` modifier.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.

### Enabling Shortcodes

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Shortcodes",
        "OrchardCore.Shortcodes.Templates"
      ],
      "disable": []
    }
  ]
}
```

### Shortcode Templates (Admin UI)

Shortcode templates are managed via _Design → Shortcodes_ in the admin dashboard. Each template has the following fields:

| Parameter          | Description                                                                  |
|--------------------|------------------------------------------------------------------------------|
| `Name`             | The shortcode name, without brackets.                                        |
| `Hint`             | Tooltip text displayed in the shortcode picker.                              |
| `Usage`            | HTML string describing usage and arguments.                                  |
| `Categories`       | Categories the shortcode belongs to.                                         |
| `Return Shortcode` | The shortcode value returned from the picker when selected. Defaults to Name.|
| `Content`          | The Liquid template that renders the shortcode output.                       |

### Template Arguments

The following variables are available inside a shortcode Liquid template:

| Variable  | Description                                                              |
|-----------|--------------------------------------------------------------------------|
| `Args`    | Named arguments provided by the user (e.g., `[shortcode key="value"]`). |
| `Content` | Inner content between opening and closing tags.                          |
| `Context` | Contextual data from the caller (e.g., the parent `ContentItem`).        |

### Example: `[display_text]` Template

| Field     | Value                                   |
|-----------|-----------------------------------------|
| `Name`    | `display_text`                          |
| `Hint`    | Returns the display text of the content item. |
| `Usage`   | `[display_text]`                        |
| `Content` | `{{ Context.ContentItem.DisplayText }}` |

> The `ContentItem` context is only available when the caller (e.g., `HtmlBodyPart`) passes the `ContentItem` to the shortcode context.

### Example: `[site_name]` Template

| Field     | Value                  |
|-----------|------------------------|
| `Name`    | `site_name`            |
| `Hint`    | Returns the site name. |
| `Usage`   | `[site_name]`          |
| `Content` | `{{ Site.SiteName }}`  |

### Example: `[primary]` Template with Arguments

| Field     | Value |
|-----------|-------|
| `Name`    | `primary` |
| `Hint`    | Formats text in the theme's primary color. |
| `Usage`   | `[primary text]` or `[primary]text[/primary]` |
| `Content` | See Liquid below |

```liquid
{% capture output %}
{% if Args.Text != nil %}
<span class="text-primary">{{ Args.Text }}</span>
{% else %}
<span class="text-primary">{{ Content }}</span>
{% endif %}
{% endcapture %}
{{ output | sanitize | raw }}
```

### Registering a Shortcode via Delegate

Use `ShortcodeDelegate` for simple inline shortcodes registered in `Startup.ConfigureServices`:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShortcode("bold", (args, content, ctx) =>
        {
            var text = args.NamedOrDefault("text");

            if (!string.IsNullOrEmpty(text))
            {
                content = text;
            }

            return ValueTask.FromResult($"<b>{content}</b>");
        }, describe =>
        {
            describe.DefaultValue = "[bold text-here]";
            describe.Hint = "Add bold formatting with a shortcode.";
            describe.Usage = "[bold 'your bold content here']";
            describe.Categories = ["HTML Content"];
        });
    }
}
```

### Registering a Shortcode via IShortcodeProvider

For complex shortcodes, implement `IShortcodeProvider` and register with `AddShortcode<T>`:

```csharp
public sealed class AlertShortcodeProvider : IShortcodeProvider
{
    public ValueTask<string> EvaluateAsync(string identifier, Arguments args, string content, Context context)
    {
        var alertType = args.NamedOrDefault("type") ?? "info";

        return ValueTask.FromResult(
            $"<div class=\"alert alert-{alertType}\" role=\"alert\">{content}</div>"
        );
    }
}
```

Register in `Startup.ConfigureServices`:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShortcode<AlertShortcodeProvider>("alert", describe =>
        {
            describe.DefaultValue = "[alert] [/alert]";
            describe.Hint = "Display a Bootstrap alert box.";
            describe.Usage =
                "[alert type=\"info\"]Your message here[/alert]<br>" +
                "Supported types: info, warning, danger, success";
            describe.Categories = ["HTML Content", "UI Components"];
        });
    }
}
```

### Built-In Shortcode: `[image]`

Renders an image from the media library.

```
[image alt="My lovely image"]my-image.jpg[/image]
```

| Parameter        | Description                                                        |
|------------------|--------------------------------------------------------------------|
| `alt`            | Alternative text for the image (accessibility and SEO).            |
| `class`          | CSS class attribute for the `<img>` tag.                           |
| `append_version` | Set to `true` to add a cache-busting query string.                 |
| `format`         | Output format: `jpeg`, `png`, `gif`, or `bmp`.                    |
| `quality`        | JPEG quality (0–100, default 75).                                  |
| `width`, `height`| Resize dimensions. Allowed: 16, 32, 50, 100, 160, 240, 480, 600, 1024, 2048. |
| `mode`           | Resize mode: `pad`, `boxpad`, `max` (default), `min`, `stretch`, `crop`. |

### Built-In Shortcode: `[asset_url]`

Returns a relative URL from the media library. Useful for backgrounds or links.

```
[asset_url]my-image.jpg[/asset_url]
```

Returns a path like `/my-tenant/media/my-image.jpg`. Supports the same `format`, `quality`, `width`, `height`, and `mode` parameters as `[image]`.

### Built-In Shortcode: `[locale]`

Conditionally renders content based on the current culture. Requires `OrchardCore.Localization`.

```
[locale en]English Text[/locale][locale fr]French Text[/locale]
```

By default, parent cultures match (e.g., `en-CA` matches `[locale en]`). Pass `false` as the second argument for exact matching:

```
[locale en false]Exact English only[/locale]
```

### Rendering Shortcodes in Templates

Shortcodes are automatically rendered for `HtmlBodyPart`, `HtmlField`, `MarkdownBodyPart`, and `MarkdownField` shapes. For manual rendering:

#### Liquid

```liquid
{{ Model.ContentItem.Content.RawHtml.Content.Html | shortcode | raw }}
```

#### Razor

```csharp
@Html.Raw(await Orchard.ShortcodesToHtmlAsync((string)Model.ContentItem.Content.RawHtml.Content.Html))
```
