---
name: orchardcore-markdown
description: Skill for using MarkdownBodyPart and MarkdownField in Orchard Core. Covers Markdown content editing, shape templates for Liquid and Razor, custom editor configurations, final-output Markdown sanitization, shortcodes, the Markdig extension pipeline, the OrchardCore Markdown configuration section, IMarkdownService, IMarkdownDisplayService, the markdownify filter, and migrating away from RenderLiquid. Use this skill when requests mention Orchard Core Markdown, Use MarkdownBodyPart for Content Editing, Enabling Markdown, Attaching MarkdownBodyPart to a Content Type, Shapes, MarkdownBodyPartViewModel Properties, MarkdownToHtmlAsync, MarkdownPipelineOptions, ConfigureMarkdownPipeline, DefaultMarkdownService or closely related Orchard Core work. Strong matches include work with OrchardCore.Markdown, OrchardCore.Markdown.ViewModels, MarkdownBodyPart, MarkdownField, MarkdownBodyPartSettings, MarkdownFieldSettings, TypePartSettings, PartFieldDefinition.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Markdown - Prompt Templates

## Use MarkdownBodyPart for Content Editing

You are an Orchard Core expert. Generate code, templates, and configuration for the Markdown module — enabling Markdown-based content editing with Markdig pipeline support.

### Guidelines

- Enable the `OrchardCore.Markdown` feature to use `MarkdownBodyPart` and `MarkdownField`.
- The authored Markdown is persisted exactly as entered. On display it is converted to HTML, shortcodes are processed, and the final HTML is sanitized when `SanitizeHtml` is enabled (the default).
- Liquid in Markdown is not executed; use `LiquidPart` or `LiquidField` when content needs Liquid.
- Shape templates use `MarkdownBodyPartViewModel` / `MarkdownFieldViewModel`, whose `Html` is the final rendered output and whose `Markdown` is the authored source.
- The Markdig pipeline is configured with the `OrchardCore:Markdown:Extensions` setting or `ConfigureMarkdownPipeline` in code, and built once per tenant.
- Two built-in editors are available: `Default` (plain text area) and `Wysiwyg`.
- Custom editors are added by creating `Markdown_Option__{Name}` and `Markdown_Edit__{Name}` shape templates.
- All C# classes must use the `sealed` modifier, except View Models.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.

### Enabling Markdown

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Markdown"
      ],
      "disable": []
    }
  ]
}
```

### Attaching MarkdownBodyPart to a Content Type

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Article", type => type
            .DisplayedAs("Article")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("MarkdownBodyPart", part => part
                .WithPosition("1")
                .WithEditor("Wysiwyg")
                .WithSettings(new MarkdownBodyPartSettings
                {
                    SanitizeHtml = true,
                })
            )
        );

        return 1;
    }
}
```

`MarkdownBodyPartSettings` and `MarkdownFieldSettings` only expose
`SanitizeHtml`. The Wysiwyg editors accept `Options` through
`MarkdownBodyPartWysiwygEditorSettings` and `MarkdownFieldWysiwygEditorSettings`.

### Shapes

| Shape Name         | Display Type | Default Location | Model Type                  |
|--------------------|--------------|------------------|-----------------------------|
| `MarkdownBodyPart` | `Detail`     | `Content:5`      | `MarkdownBodyPartViewModel` |
| `MarkdownBodyPart` | `Summary`    | `Content:10`     | `MarkdownBodyPartViewModel` |

### MarkdownBodyPartViewModel Properties

| Property           | Type                       | Description                                                       |
|--------------------|----------------------------|-------------------------------------------------------------------|
| `Markdown`         | `string`                   | The exact authored Markdown source.                               |
| `Html`             | `string`                   | The HTML after Markdown conversion, shortcodes, and sanitization. |
| `ContentItem`      | `ContentItem`              | The content item of the part.                                     |
| `MarkdownBodyPart` | `MarkdownBodyPart`         | The `MarkdownBodyPart` instance.                                  |
| `TypePartSettings` | `MarkdownBodyPartSettings` | The settings of the part.                                         |

`MarkdownBodyPart.Markdown` is the authored source. Liquid syntax in it is
plain text.

### MarkdownField Properties

The `MarkdownFieldViewModel` is used when a `MarkdownField` is attached to a content part:

| Property              | Type                         | Description                                                       |
|-----------------------|------------------------------|-------------------------------------------------------------------|
| `Markdown`            | `string`                     | The exact authored Markdown source.                               |
| `Html`                | `string`                     | The HTML after Markdown conversion, shortcodes, and sanitization. |
| `Field`               | `MarkdownField`              | The `MarkdownField` instance.                                     |
| `Part`                | `ContentPart`                | The part this field is attached to.                               |
| `PartFieldDefinition` | `ContentPartFieldDefinition` | The part field definition.                                        |

The same pipeline applies to Detail, Summary, preview, `BodyAspect`, and the
GraphQL `html` field.

### Shape Template (Liquid)

Override the detail display at `Views/MarkdownBodyPart.liquid`:

```liquid
<article class="markdown-body">
    {{ Model.Html | raw }}
</article>
```

Override the summary display at `Views/MarkdownBodyPart.Summary.liquid`:

```liquid
<p>{{ Model.Html | strip_html | truncate: 200 }}</p>
```

Convert an arbitrary Markdown string in Liquid with `markdownify`, which
always sanitizes its output:

```liquid
{{ "### Services" | markdownify | raw }}
```

### Shape Template (Razor)

Override the detail display at `Views/MarkdownBodyPart.cshtml`:

```cshtml
@model OrchardCore.Markdown.ViewModels.MarkdownBodyPartViewModel

<article class="markdown-body">
    @Html.Raw(Model.Html)
</article>
```

### Razor Helper for Manual Rendering

`MarkdownToHtmlAsync(string markdown, bool sanitize = true)` converts Markdown,
processes shortcodes, and sanitizes the final HTML unless `sanitize` is `false`:

```cshtml
@await Orchard.MarkdownToHtmlAsync((string)Model.ContentItem.Content.MarkdownParagraph.Content.Markdown)

@* Trusted output only *@
@await Orchard.MarkdownToHtmlAsync((string)Model.ContentItem.Content.MarkdownParagraph.Content.Markdown, sanitize: false)
```

### Rendering from Code

| Service | Lifetime | Purpose |
|---|---|---|
| `IMarkdownService` (`DefaultMarkdownService`) | Singleton | `ToHtml(markdown)` with the tenant Markdig pipeline only. |
| `IMarkdownDisplayService` | Scoped | `ToHtmlAsync(markdown, shortcodeContext, sanitizeHtml)` — Markdown, then shortcodes, then sanitization. |

```csharp
var html = await _markdownDisplayService.ToHtmlAsync(
    markdown,
    new Shortcodes.Context { ["ContentItem"] = contentItem },
    sanitizeHtml: true);
```

`DefaultMarkdownService` builds its pipeline once, in its constructor, from
`IOptions<MarkdownPipelineOptions>`. A replacement `IMarkdownService` should
also be thread-safe and registered as a singleton.

### Sanitization

Disable sanitization per content type by unchecking `Sanitize HTML` in the
MarkdownBody Part (or MarkdownField) settings, or allow extra attributes in the
HTML Sanitizer. For example, `autoidentifiers` needs the `id` attribute:

```csharp
builder.Services
    .AddOrchardCms()
    .ConfigureServices(tenantServices =>
        tenantServices.ConfigureHtmlSanitizer(sanitizer => sanitizer.AllowedAttributes.Add("id")));
```

### Editors

| Editor Name | Description                                        |
|-------------|----------------------------------------------------|
| `Default`   | Plain text area for raw Markdown editing.          |
| `Wysiwyg`   | Rich editing experience with toolbar and preview.  |

Select the editor in the MarkdownBody Part settings for each content type.

### Custom Editor Declaration

Create a file `Markdown-{Name}.Option.cshtml` to declare a new editor option:

```cshtml
@{
    string currentEditor = Model.Editor;
}
<option value="MyCustomEditor" selected="@(currentEditor == "MyCustomEditor")">@T["My Custom Editor"]</option>
```

Create a file `Markdown-{Name}.Edit.cshtml` to render the editor:

```cshtml
@using OrchardCore.Markdown.ViewModels
@model MarkdownBodyPartViewModel

<fieldset class="ocat-wrapper">
    <label asp-for="Markdown" class="ocat-label">@T["Markdown"]</label>
    <div class="ocat-end">
        <textarea asp-for="Markdown" rows="10" class="form-control"></textarea>
        <span class="hint">@T["The markdown content of the item."]</span>
    </div>
</fieldset>
```

Override the built-in editors with `Markdown.Edit.cshtml` (Default) or
`Markdown-Wysiwyg.Edit.cshtml`. Editor scripts should be ES modules that also
initialize editors injected later by Flow and Bag editors; admin pages no
longer load jQuery implicitly.

### Markdown Configuration (appsettings.json)

Configure Markdig extensions under `OrchardCore:Markdown` (the default is
`nohtml+advanced`; the old `OrchardCore_Markdown` section is deprecated but
still read). Per-tenant values go under `OrchardCore:{TenantName}:Markdown`.

```json
{
  "OrchardCore": {
    "Markdown": {
      "Extensions": "nohtml+advanced"
    }
  }
}
```

### Available Markdig Extensions

| Extension           | Description                                                        |
|---------------------|--------------------------------------------------------------------|
| `advanced`          | Enable advanced Markdown extensions (bundle of common extensions). |
| `pipetables`        | Pipe-delimited tables.                                             |
| `gfm-pipetables`    | GitHub Flavored Markdown pipe tables using header for column count.|
| `hardlinebreak`     | Treat soft line breaks as hard line breaks.                        |
| `footnotes`         | Footnote support.                                                  |
| `footers`           | Footer blocks.                                                     |
| `citations`         | Citation syntax.                                                   |
| `attributes`        | Attach HTML attributes to elements.                                |
| `gridtables`        | Grid-style tables.                                                 |
| `abbreviations`     | Abbreviation definitions.                                          |
| `emojis`            | Emoji and smiley support.                                          |
| `definitionlists`   | Definition list syntax.                                            |
| `customcontainers`  | Custom block containers.                                           |
| `figures`           | Figure elements.                                                   |
| `mathematics`       | Mathematical expressions.                                          |
| `bootstrap`         | Bootstrap CSS class support.                                       |
| `medialinks`        | Extended image links for video/audio.                              |
| `smartypants`       | Smart typography (curly quotes, dashes).                           |
| `autoidentifiers`   | Auto-generate heading IDs.                                         |
| `tasklists`         | Task list checkboxes.                                              |
| `diagrams`          | Diagram rendering.                                                 |
| `nofollowlinks`     | Add `rel=nofollow` to all links.                                   |
| `noopenerlinks`     | Add `rel=noopener` to all links.                                   |
| `noreferrerlinks`   | Add `rel=noreferrer` to all links.                                 |
| `nohtml`            | Disable raw HTML in Markdown.                                      |
| `yaml`              | Parse YAML front matter.                                           |
| `nonascii-noescape` | Do not %-escape non-ASCII characters in URIs.                      |
| `autolinks`         | Auto-link URLs (`http://`, `https://`, `ftp://`, `mailto:`, `www.`). |
| `globalization`     | Right-to-left content support.                                     |

### Pipeline Configuration in Code

Configure the Markdig pipeline programmatically in `Program.cs` or `Startup`:

```csharp
services
    .AddOrchardCms()
    .ConfigureServices(tenantServices =>
        tenantServices.ConfigureMarkdownPipeline(pipeline =>
        {
            pipeline.UseEmojiAndSmiley();
        }));
```

To clear all default pipeline configuration:

```csharp
services
    .AddOrchardCms()
    .ConfigureServices(tenantServices =>
        tenantServices.PostConfigure<MarkdownPipelineOptions>(o =>
        {
            o.Configure.Clear();
        }));
```

Pipeline options are read once when the tenant shell is built; restart or
release the tenant after changing them.

### Upgrading from 3.x

- `MarkdownBodyPartSettings.RenderLiquid` and `MarkdownFieldSettings.RenderLiquid`
  were removed. Liquid in existing Markdown stays stored but renders as text;
  tenant startup logs a warning per affected part and field. Move that content
  to `LiquidPart` or `LiquidField`.
- `MarkdownToHtmlAsync(markdown, sanitize, renderLiquid)` lost its
  `renderLiquid` parameter.
- Sanitization runs on the final HTML only; stored Markdown is no longer
  rewritten on save.
- `DefaultMarkdownService` is a singleton whose constructor only takes
  `IOptions<MarkdownPipelineOptions>`; update subclasses or custom registrations.
- Rename the `OrchardCore_Markdown` section to `OrchardCore:Markdown`.
