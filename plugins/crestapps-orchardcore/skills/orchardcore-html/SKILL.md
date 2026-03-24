---
name: orchardcore-html
description: Skill for using HtmlBodyPart in Orchard Core. Covers HTML content editing, shape templates for Liquid and Razor, custom editor configurations, HTML sanitization, Trumbowyg and Monaco editors, and display/editor shape types.
---

# Orchard Core HTML Body - Prompt Templates

## Use HtmlBodyPart for Rich Content Editing

You are an Orchard Core expert. Generate code, templates, and configuration for the HTML Body module — enabling rich HTML content editing with multiple editor options.

### Guidelines

- Enable the `OrchardCore.Html` feature to use `HtmlBodyPart`.
- `HtmlBodyPart` stores HTML content for rich text editing scenarios.
- Shape templates use the `HtmlBodyPartViewModel` model with `Body` and `Html` properties.
- HTML content can include Liquid tags that are processed before rendering.
- HTML input is sanitized by default when saved; disable via the `Sanitize HTML` part setting.
- Three built-in editors: `Default` (basic textarea), `Wysiwyg` (Trumbowyg WYSIWYG), and `Monaco` (source code editor).
- Custom editors are created by providing `HtmlBodyPart_Option__{Name}` and `HtmlBodyPart_Edit__{Name}` shape templates.
- All C# classes must use the `sealed` modifier, except View Models.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.

### Enabling HTML Body

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Html"
      ],
      "disable": []
    }
  ]
}
```

### Attaching HtmlBodyPart to a Content Type

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
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Page", type => type
            .DisplayedAs("Page")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("HtmlBodyPart", part => part
                .WithPosition("1")
                .WithEditor("Wysiwyg")
            )
        );

        return 1;
    }
}
```

### Shapes

The following shapes are rendered when `HtmlBodyPart` is attached to a content type:

| Shape Name     | Display Type | Default Location | Model Type              |
|----------------|--------------|------------------|-------------------------|
| `HtmlBodyPart` | `Detail`     | `Content:5`      | `HtmlBodyPartViewModel` |
| `HtmlBodyPart` | `Summary`    | `Content:10`     | `HtmlBodyPartViewModel` |

### HtmlBodyPartViewModel Properties

| Property           | Type                   | Description                                           |
|--------------------|------------------------|-------------------------------------------------------|
| `Body`             | `string`               | The content that was edited. It might contain tokens.  |
| `Html`             | `string`               | The HTML content once all tokens have been processed.  |
| `ContentItem`      | `ContentItem`          | The content item of the part.                          |
| `HtmlBodyPart`     | `HtmlBodyPart`         | The `HtmlBodyPart` instance.                           |
| `TypePartSettings` | `HtmlBodyPartSettings` | The settings of the part.                              |

### HtmlBodyPart Properties

| Property      | Type          | Description                                                                 |
|---------------|---------------|-----------------------------------------------------------------------------|
| `Body`        | `string`      | Raw HTML content. May contain Liquid tags; prefer rendering the shape.      |
| `Content`     | `JObject`     | The raw content of the part.                                                |
| `ContentItem` | `ContentItem` | The content item containing this part.                                      |

### Shape Template (Liquid)

Override the detail display at `Views/HtmlBodyPart.liquid`:

```liquid
<article>
    {{ Model.Html | raw }}
</article>
```

Override the summary display at `Views/HtmlBodyPart.Summary.liquid`:

```liquid
<p>{{ Model.Html | strip_html | truncate: 200 }}</p>
```

### Shape Template (Razor)

Override the detail display at `Views/HtmlBodyPart.cshtml`:

```cshtml
@model OrchardCore.Html.ViewModels.HtmlBodyPartViewModel

<article>
    @Html.Raw(Model.Html)
</article>
```

### Editors

| Editor Name | Description                                              |
|-------------|----------------------------------------------------------|
| `Default`   | Basic HTML textarea for raw HTML editing.                |
| `Wysiwyg`   | Trumbowyg WYSIWYG editor with toolbar for rich editing.  |
| `Monaco`    | Monaco source code editor for HTML source editing.       |

Select the editor in the HtmlBody Part settings for each content type.

### Custom Editor Declaration

To declare a new editor option, create a file named `HtmlBodyPart-{Name}.Option.cshtml`:

```cshtml
@{
    string currentEditor = Model.Editor;
}
<option value="MyCustomEditor" selected="@(currentEditor == "MyCustomEditor")">@T["My Custom Editor"]</option>
```

To render the editor UI, create a file named `HtmlBodyPart-{Name}.Edit.cshtml`:

```cshtml
@using OrchardCore.Html.ViewModels
@model HtmlBodyPartViewModel

<fieldset class="mb-3">
    <label asp-for="Body">@T["Body"]</label>
    <textarea asp-for="Body" rows="10" class="form-control"></textarea>
    <span class="hint">@T["The body of the content item."]</span>
</fieldset>
```

### Overriding Predefined Editors

| File Name                          | Overrides                    |
|------------------------------------|------------------------------|
| `HtmlBodyPart.Edit.cshtml`         | The `Default` editor.        |
| `HtmlBodyPart-Wysiwyg.Edit.cshtml` | The `Wysiwyg` editor.        |
| `HtmlBodyPart-Monaco.Edit.cshtml`  | The `Monaco` editor.         |

### Sanitization

By default, all HTML input is sanitized when `HtmlBodyPart` is saved. To manage sanitization:

- **Per content type**: Uncheck `Sanitize HTML` in the HtmlBody Part settings.
- **Globally**: Configure the HTML Sanitizer module to adjust allowed tags and attributes.

### Creating a Content Type with HtmlBodyPart via Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "Article",
          "DisplayName": "Article",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true,
              "Draftable": true,
              "Versionable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "0"
                }
              }
            },
            {
              "PartName": "HtmlBodyPart",
              "Name": "HtmlBodyPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "1",
                  "Editor": "Wysiwyg"
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

### Creating Content with HtmlBodyPart via Recipe

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js:uuid()]",
          "ContentType": "Article",
          "DisplayText": "Welcome",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "Welcome"
          },
          "HtmlBodyPart": {
            "Html": "<p>Welcome to our site!</p>"
          }
        }
      ]
    }
  ]
}
```

### Rendering HtmlBodyPart in Templates

Shortcodes and Liquid tags within `HtmlBodyPart` are automatically processed when the shape is rendered through Display Management.

#### Liquid

```liquid
{{ Model.Content.HtmlBodyPart | shape_render }}
```

#### Razor

```cshtml
@await DisplayAsync(Model.Content.HtmlBodyPart)
```
