---
name: orchardcore-sanitizer
description: Skill for configuring HTML sanitization in Orchard Core. Covers the HtmlSanitizer service for XSS prevention, Razor sanitize helpers, configuring allowed tags and attributes, scheme allowlists, and custom sanitization configuration. Use this skill when requests mention Orchard Core Sanitizer, Configure HTML Sanitization, Parts and Fields Using the Sanitizer, Orchard Core Default Sanitizer Customizations, Sanitizing HTML in Razor Views, Configuring the Sanitizer in Program.cs, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with HtmlBodyPart, HtmlField, HtmlMenuItemPart, MarkdownBodyPart, MarkdownField, @Orchard.SanitizeHtml(), ConfigureHtmlSanitizer, AllowedTags.Add('iframe'), AllowedSchemes.Add('ftp'). It also helps with Sanitizing HTML in Razor Views, Configuring the Sanitizer in Program.cs, Allowing Additional Tags, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Sanitizer - Prompt Templates

## Configure HTML Sanitization

You are an Orchard Core expert. Generate HTML sanitization configurations and custom sanitizer settings for Orchard Core.

### Guidelines

- The HTML Sanitizer is part of Orchard Core's infrastructure — no additional feature needs to be enabled.
- The sanitizer prevents XSS attacks by cleaning user-provided HTML content.
- It is used by default for `HtmlBodyPart`, `HtmlField`, `HtmlMenuItemPart`, `MarkdownBodyPart`, and `MarkdownField`.
- Sanitization can be disabled per field/part via the `Sanitize Html` option in field or part settings.
- Use the `@Orchard.SanitizeHtml()` Razor helper to sanitize HTML in views.
- Orchard Core customizes the default HtmlSanitizer by allowing the `class` attribute, allowing `mailto` and `tel` schemes, and removing the `form` tag.
- Configure the sanitizer using `ConfigureHtmlSanitizer` during service registration.
- Multiple `ConfigureHtmlSanitizer` calls can be chained in the startup pipeline.
- The sanitizer is based on the [HtmlSanitizer](https://github.com/mganss/HtmlSanitizer) library.
- Always seal classes.

### Parts and Fields Using the Sanitizer

| Part / Field | Description |
|-------------|-------------|
| `HtmlBodyPart` | Rich HTML content body. |
| `HtmlField` | HTML content field attached to any content type. |
| `HtmlMenuItemPart` | HTML content in menu items. |
| `MarkdownBodyPart` | Markdown rendered to HTML, then sanitized. |
| `MarkdownField` | Markdown field rendered to HTML, then sanitized. |

### Orchard Core Default Sanitizer Customizations

Orchard Core modifies the base HtmlSanitizer defaults:

| Change | Description |
|--------|-------------|
| Allow `class` attribute | The `class` attribute is added to the allowed attributes list. |
| Allow `mailto` scheme | The `mailto:` URI scheme is allowed in links. |
| Allow `tel` scheme | The `tel:` URI scheme is allowed in links. |
| Remove `form` tag | The `<form>` tag is removed from the allowed tags list. |

### Sanitizing HTML in Razor Views

```html
@Orchard.SanitizeHtml((string)Model.ContentItem.HtmlBodyPart.Html)
```

This renders the sanitized HTML, stripping any potentially dangerous tags, attributes, or scripts.

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
            sanitizer.AllowedAttributes.Add("data-*");
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
            // Remove <script> and <style> tags (removed by default, but shown for illustration).
            sanitizer.AllowedTags.Remove("script");
            sanitizer.AllowedTags.Remove("style");
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

            // Allow data attributes for JavaScript frameworks.
            sanitizer.AllowedAttributes.Add("data-*");

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

This is useful when trusted editors need to use HTML that would otherwise be stripped (e.g., embedded scripts, custom forms).

### Common Sanitizer Configuration Scenarios

| Scenario | Configuration |
|----------|---------------|
| Allow YouTube/Vimeo embeds | `AllowedTags.Add("iframe")` |
| Allow FTP links | `AllowedSchemes.Add("ftp")` |
| Allow data URIs for inline images | `AllowedSchemes.Add("data")` |
| Allow custom data attributes | `AllowedAttributes.Add("data-*")` |
| Allow target attribute on links | `AllowedAttributes.Add("target")` |
| Block all inline styles | `AllowedAttributes.Remove("style")` |
