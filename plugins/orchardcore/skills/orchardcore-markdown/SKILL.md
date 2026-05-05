---
name: orchardcore-markdown
description: Skill for using MarkdownBodyPart in Orchard Core. Covers Markdown content editing, shape templates for Liquid and Razor, custom editor configurations, Markdown sanitization, Markdig extension pipeline, and available editor options. Use this skill when requests mention Orchard Core Markdown, Use MarkdownBodyPart for Content Editing, Enabling Markdown, Attaching MarkdownBodyPart to a Content Type, Shapes, MarkdownBodyPartViewModel Properties, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Markdown, OrchardCore.Markdown.ViewModels, MarkdownBodyPart, MarkdownField, DataMigration, IContentDefinitionManager, WithPart, TitlePart, TypePartSettings, MarkdownBodyPartSettings, ContentPart, PartFieldDefinition. It also helps with Shapes, MarkdownBodyPartViewModel Properties, MarkdownBodyPart Properties, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Markdown - Prompt Templates

## Use MarkdownBodyPart for Content Editing

You are an Orchard Core expert. Generate code, templates, and configuration for the Markdown module — enabling Markdown-based content editing with Markdig pipeline support.

### Guidelines

- Enable the `OrchardCore.Markdown` feature to use `MarkdownBodyPart` and `MarkdownField`.
- `MarkdownBodyPart` stores Markdown content and converts it to HTML for rendering.
- Shape templates use the `MarkdownBodyPartViewModel` model with `Markdown` and `Html` properties.
- Markdown content can include Liquid tags that are processed before Markdown conversion.
- HTML output is sanitized by default; disable via the `Sanitize HTML` part setting.
- The Markdig pipeline is configurable via `appsettings.json` extensions or `ConfigureMarkdownPipeline` in code.
- Two built-in editors are available: `Default` (plain text area) and `Wysiwyg` (rich editing experience).
- Custom editors can be added by creating `Markdown_Option__{Name}` and `Markdown_Edit__{Name}` shape templates.
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
            )
        );

        return 1;
    }
}
```

### Shapes

The following shapes are rendered when `MarkdownBodyPart` is attached to a content type:

| Shape Name         | Display Type | Default Location | Model Type                  |
|--------------------|--------------|------------------|-----------------------------|
| `MarkdownBodyPart` | `Detail`     | `Content:5`      | `MarkdownBodyPartViewModel` |
| `MarkdownBodyPart` | `Summary`    | `Content:10`     | `MarkdownBodyPartViewModel` |

### MarkdownBodyPartViewModel Properties

| Property           | Type                       | Description                                              |
|--------------------|----------------------------|----------------------------------------------------------|
| `Markdown`         | `string`                   | The Markdown value after all tokens have been processed. |
| `Html`             | `string`                   | The HTML content resulting from the Markdown source.     |
| `ContentItem`      | `ContentItem`              | The content item of the part.                            |
| `MarkdownBodyPart` | `MarkdownBodyPart`         | The `MarkdownBodyPart` instance.                         |
| `TypePartSettings` | `MarkdownBodyPartSettings` | The settings of the part.                                |

### MarkdownBodyPart Properties

| Property      | Type          | Description                                                                |
|---------------|---------------|----------------------------------------------------------------------------|
| `Markdown`    | `string`      | Raw Markdown content. May contain Liquid tags; prefer rendering the shape. |
| `Content`     | `JObject`     | The raw content of the part.                                               |
| `ContentItem` | `ContentItem` | The content item containing this part.                                     |

### MarkdownField Properties

The `MarkdownFieldViewModel` is used when a `MarkdownField` is attached to a content part:

| Property              | Type                         | Description                                             |
|-----------------------|------------------------------|---------------------------------------------------------|
| `Markdown`            | `string`                     | The Markdown value once all tokens have been processed. |
| `Html`                | `string`                     | The HTML content resulting from the Markdown source.    |
| `Field`               | `MarkdownField`              | The `MarkdownField` instance.                           |
| `Part`                | `ContentPart`                | The part this field is attached to.                     |
| `PartFieldDefinition` | `ContentPartFieldDefinition` | The part field definition.                              |

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

### Shape Template (Razor)

Override the detail display at `Views/MarkdownBodyPart.cshtml`:

```cshtml
@model OrchardCore.Markdown.ViewModels.MarkdownBodyPartViewModel

<article class="markdown-body">
    @Html.Raw(Model.Html)
</article>
```

### Razor Helper for Manual Rendering

Use `MarkdownToHtmlAsync` to convert Markdown strings in Razor views:

```cshtml
@* Basic rendering (sanitized by default) *@
@await Orchard.MarkdownToHtmlAsync((string)Model.ContentItem.Content.MarkdownParagraph.Content.Markdown)

@* Disable sanitization *@
@await Orchard.MarkdownToHtmlAsync((string)Model.ContentItem.Content.MarkdownParagraph.Content.Markdown, sanitize: false)

@* Render embedded Liquid before converting Markdown *@
@await Orchard.MarkdownToHtmlAsync((string)Model.ContentItem.Content.MarkdownParagraph.Content.Markdown, renderLiquid: true)
```

### Sanitization

Markdown output is sanitized by default during content rendering via Display Management. Disable sanitization per content type by unchecking `Sanitize HTML` in the MarkdownBody Part settings, or configure the HTML Sanitizer globally.

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

<fieldset class="mb-3">
    <label asp-for="Markdown">@T["Markdown"]</label>
    <textarea asp-for="Markdown" rows="10" class="form-control"></textarea>
    <span class="hint">@T["The markdown content of the item."]</span>
</fieldset>
```

### Markdown Configuration (appsettings.json)

Configure Markdig extensions via `appsettings.json`:

```json
{
  "OrchardCore_Markdown": {
    "Extensions": "nohtml+advanced"
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
| `smartypants`       | Smart typography (curly quotes, dashes).                            |
| `autoidentifiers`   | Auto-generate heading IDs.                                         |
| `tasklists`         | Task list checkboxes.                                              |
| `diagrams`          | Diagram rendering.                                                 |
| `nofollowlinks`     | Add `rel=nofollow` to all links.                                   |
| `nohtml`            | Disable raw HTML in Markdown.                                      |
| `autolinks`         | Auto-link URLs (`http://`, `https://`, `mailto:`).                 |
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
