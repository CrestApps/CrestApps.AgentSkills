---
name: orchardcore-content-fields
description: Skill for adding and configuring common Orchard Core content fields. Covers field settings, TextField minimum and maximum length, the new LiquidField, HTML and Markdown field rendering and sanitization, supported editor options, display modes, custom field editors as ES modules, and migration patterns. Use this skill when requests mention Orchard Core Content Fields, Add and Configure Content Fields, General Pattern for Adding a Field, TextField, TextField Settings, TextField Editors or closely related Orchard Core work. Strong matches include work with OrchardCore.Taxonomies, OrchardCore.ContentLocalization, OrchardCore.Application.Cms.Targets, FieldSettings, HtmlFieldSettings, MarkdownFieldSettings, LiquidField, TextFieldSettings, MinLength, MaxLength, NumericFieldSettings, ContentPartFieldSettings, WithField, WithSettings, TextField, TextFieldPredefinedListEditorSettings.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Content Fields - Prompt Templates

## Add and Configure Content Fields

You are an Orchard Core expert. Generate migration code and recipes for common content fields and verify feature-specific options against the enabled module.

### Guidelines

- Fields are added to content parts using `AlterPartDefinitionAsync` in a migration.
- Each field has a `FieldSettings` base with `Hint` (string) and `Required` (bool).
- Each field type has its own specific settings class (e.g., `TextFieldSettings`, `NumericFieldSettings`).
- Fields also have `ContentPartFieldSettings` controlling `DisplayName`, `Description`, `Editor`, `DisplayMode`, and `Position`.
- Use `.WithEditor("EditorName")` to select an editor variant.
- Use `.WithDisplayMode("DisplayModeName")` to select a display variant.
- In templates, render field shapes (for example `{{ Model.Content["Article-Summary"] | shape_render }}`) rather than writing stored HTML or Markdown with `raw`; stored source is unsanitized.
- Third-party modules providing fields (CrestApps, Lombiq, community modules, etc.) must be installed as NuGet packages in the web project (the startup project of the solution), not just in the module project.
- Always seal classes.

### General Pattern for Adding a Field

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("{{FieldType}}")
        .WithDisplayName("{{DisplayName}}")
        .WithDescription("{{Description}}")
        .WithPosition("{{Position}}")
        .WithSettings(new {{FieldType}}Settings
        {
            // Field-specific settings
        })
    )
);
```

---

## TextField

Stores a single text value. Supports multiple editor modes.

### TextField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |
| `DefaultValue` | string | `null` | Default value for new content items. |
| `Type` | FieldBehaviorType | `Unset` | Field behavior type. |
| `Pattern` | string | `null` | The pattern used to build the value. |
| `Placeholder` | string | `""` | Placeholder text. |
| `MinLength` | int? | `null` | Minimum number of characters; validated on save when greater than `0` and rendered as `minlength`. |
| `MaxLength` | int? | `null` | Maximum number of characters; validated on save when greater than `0` and rendered as `maxlength`. |

`MinLength`/`MaxLength` (**Minimum length**/**Maximum length** in the UI) apply
to the standard and `TextArea` editors. Leaving them empty means no limit, so
existing definitions are unaffected.

### TextField Editors

| Editor | Description |
|---|---|
| *(default)* | Standard single-line text input. |
| `TextArea` | Multi-line text area. |
| `CodeMirror` | Code editor with syntax highlighting. |
| `PredefinedList` | Dropdown or radio buttons from a predefined list. |
| `Monaco` | Monaco code editor. |
| `Color` | HTML color editor with an optional alpha value. |
| `Email`, `Tel`, `Url` | Typed HTML inputs. |
| `IconPicker` | Font Awesome icon picker. |

`Header` is a TextField **display mode** (`TextField-Header.Display.cshtml`),
not an editor.

### TextField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("TextField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
        .WithSettings(new TextFieldSettings
        {
            Required = true,
            Hint = "{{Hint}}",
            DefaultValue = "{{DefaultValue}}",
            Placeholder = "{{Placeholder}}",
            MinLength = 3,
            MaxLength = 120
        })
    )
);
```

### TextField with TextArea Editor

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("TextField")
        .WithDisplayName("{{DisplayName}}")
        .WithEditor("TextArea")
        .WithPosition("{{Position}}")
        .WithSettings(new TextFieldSettings
        {
            Hint = "Enter a detailed description"
        })
    )
);
```

### TextField with PredefinedList Editor

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("TextField")
        .WithDisplayName("{{DisplayName}}")
        .WithEditor("PredefinedList")
        .WithPosition("{{Position}}")
        .WithSettings(new TextFieldPredefinedListEditorSettings
        {
            Editor = EditorOption.Dropdown,
            DefaultValue = "option1",
            Options = new[]
            {
                new ListValueOption("Option 1", "option1"),
                new ListValueOption("Option 2", "option2"),
                new ListValueOption("Option 3", "option3")
            }
        })
    )
);
```

In the admin options editor, a new option's value is prefilled from its label
until the value is edited, and saving rejects two options with the same label
and value pair.

---

## HtmlField

Stores the authored HTML in `Html`, unchanged. On display the HTML is
processed for shortcodes and then sanitized when `SanitizeHtml` is enabled.
Liquid syntax is not executed; use `LiquidField` for templating.

### HtmlField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |
| `SanitizeHtml` | bool | `true` | Whether to sanitize the final rendered HTML. |

### HtmlField Editors

| Editor | Description |
|---|---|
| *(default)* | CodeMirror HTML source editor. |
| `Multiline` | Plain multi-line text area. |
| `Wysiwyg` | WYSIWYG HTML editor. |
| `Trumbowyg` | Trumbowyg editor (`HtmlFieldTrumbowygEditorSettings`). |
| `Monaco` | Monaco code editor for raw HTML (`HtmlFieldMonacoEditorSettings`). |

`DisplayHtmlFieldViewModel.Html` is the rendered output; `Model.Field.Html` is
the authored source.

### HtmlField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("HtmlField")
        .WithDisplayName("{{DisplayName}}")
        .WithEditor("Wysiwyg")
        .WithPosition("{{Position}}")
        .WithSettings(new HtmlFieldSettings
        {
            Required = true,
            SanitizeHtml = true,
            Hint = "Enter HTML content"
        })
    )
);
```

---

## LiquidField

Stores authored Liquid source in `Liquid` and renders it on display, in the
body aspect, and through the GraphQL `liquid` field. Provided by
`OrchardCore.Liquid`. It has no field-specific settings, no sanitize option,
and a single standard (CodeMirror) editor. The source is validated on save.
Editing it, or any content item containing it, requires the
`ManageLiquidTemplates` permission (see `orchardcore-liquid`).

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("LiquidField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
    )
);
```

In the field source, `ContentItem` is the containing content item. The display
shape model `LiquidFieldViewModel` exposes `Liquid` (source) and `Html`
(rendered output).

---

## NumericField

Stores a numeric (decimal) value.

### NumericField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |
| `Scale` | int | `0` | Number of decimal places. |
| `Minimum` | decimal? | `null` | Minimum allowed value. |
| `Maximum` | decimal? | `null` | Maximum allowed value. |
| `Placeholder` | string | `null` | Placeholder text. |
| `DefaultValue` | string | `null` | Default value. |

### NumericField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("NumericField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
        .WithSettings(new NumericFieldSettings
        {
            Required = true,
            Minimum = 0,
            Maximum = 999999,
            Scale = 2,
            Placeholder = "0.00",
            Hint = "Enter the price"
        })
    )
);
```

---

## BooleanField

Stores a true/false value.

### BooleanField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |
| `Label` | string | `null` | Label text for the checkbox. |
| `DefaultValue` | bool | `false` | Default checked state. |

### BooleanField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("BooleanField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
        .WithSettings(new BooleanFieldSettings
        {
            Label = "Is Featured",
            DefaultValue = false,
            Hint = "Check to mark as featured"
        })
    )
);
```

---

## DateField

Stores a date value (no time component).

### DateField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |

### DateField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("DateField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
        .WithSettings(new DateFieldSettings
        {
            Required = true,
            Hint = "Select a date"
        })
    )
);
```

---

## DateTimeField

Stores a date and time value.

### DateTimeField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |

### DateTimeField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("DateTimeField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
        .WithSettings(new DateTimeFieldSettings
        {
            Required = true,
            Hint = "Select date and time"
        })
    )
);
```

---

## TimeField

Stores a time value.

### TimeField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |
| `Step` | string | `null` | Step interval for the time picker (e.g., `"00:15:00"` for 15-minute steps). |

### TimeField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("TimeField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
        .WithSettings(new TimeFieldSettings
        {
            Required = true,
            Step = "00:15:00",
            Hint = "Select a time"
        })
    )
);
```

---

## ContentPickerField

References other content items.

### ContentPickerField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |
| `Multiple` | bool | `false` | Whether to allow selecting multiple content items. |
| `DisplayAllContentTypes` | bool | `false` | Whether to show all content types in the picker. |
| `DisplayedContentTypes` | string[] | `[]` | Content types to display in the picker. |
| `DisplayedStereotypes` | string[] | `[]` | Stereotypes to filter displayed content types. |
| `Placeholder` | string | `""` | Placeholder text. |
| `TitlePattern` | string | `"{{ Model.ContentItem \| display_text }}"` | Liquid pattern for the displayed title. |

### ContentPickerField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("ContentPickerField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
        .WithSettings(new ContentPickerFieldSettings
        {
            Required = true,
            Multiple = false,
            DisplayedContentTypes = new[] { "BlogPost", "Article" },
            Hint = "Select a related article"
        })
    )
);
```

---

## MediaField

Attaches media files from the media library.

### MediaField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |
| `Multiple` | bool | `true` | Whether to allow multiple files. |
| `AllowMediaText` | bool | `true` | Whether to allow alt text for media. |
| `AllowAnchors` | bool | `false` | Whether to allow anchor points on images. |
| `AllowedExtensions` | string[] | `[]` | Allowed file extensions (empty = all allowed). |

### MediaField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("MediaField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
        .WithSettings(new MediaFieldSettings
        {
            Required = true,
            Multiple = false,
            AllowMediaText = true,
            AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" },
            Hint = "Upload an image"
        })
    )
);
```

---

## LinkField

Stores a URL with optional link text and target.

### LinkField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |
| `HintLinkText` | string | `null` | Hint text for the link text input. |
| `LinkTextMode` | LinkTextMode | `Optional` | Link text mode (`Optional`, `Required`, `Static`, `Url`). |
| `UrlPlaceholder` | string | `null` | Placeholder for the URL input. |
| `TextPlaceholder` | string | `null` | Placeholder for the text input. |
| `DefaultUrl` | string | `null` | Default URL value. |
| `DefaultText` | string | `null` | Default link text. |
| `DefaultTarget` | string | `null` | Default target (e.g., `_blank`). |

### LinkField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("LinkField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
        .WithSettings(new LinkFieldSettings
        {
            Required = true,
            LinkTextMode = LinkTextMode.Required,
            UrlPlaceholder = "https://example.com",
            TextPlaceholder = "Click here",
            Hint = "Enter an external link"
        })
    )
);
```

---

## MultiTextField

Stores multiple text values from predefined options.

### MultiTextField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |
| `Options` | MultiTextFieldValueOption[] | `[]` | Available options with Name, Value, and Default. |

### MultiTextField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("MultiTextField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
        .WithSettings(new MultiTextFieldSettings
        {
            Options = new[]
            {
                new MultiTextFieldValueOption { Name = "Red", Value = "red", Default = false },
                new MultiTextFieldValueOption { Name = "Green", Value = "green", Default = true },
                new MultiTextFieldValueOption { Name = "Blue", Value = "blue", Default = false }
            },
            Hint = "Select one or more colors"
        })
    )
);
```

---

## UserPickerField

References users.

### UserPickerField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |
| `Multiple` | bool | `false` | Whether to allow multiple users. |
| `DisplayAllUsers` | bool | `true` | Whether to display all users. |
| `DisplayedRoles` | string[] | `[]` | Roles to filter displayed users. |
| `Placeholder` | string | `""` | Placeholder text. |

### UserPickerField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("UserPickerField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
        .WithSettings(new UserPickerFieldSettings
        {
            Required = true,
            Multiple = false,
            DisplayAllUsers = false,
            DisplayedRoles = new[] { "Author", "Editor" },
            Hint = "Select an author"
        })
    )
);
```

---

## YoutubeField

Stores a YouTube video URL and embeds the player.

### YoutubeField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |
| `Label` | string | `null` | Label text. |
| `Width` | int | `0` | Embed player width (0 = default). |
| `Height` | int | `0` | Embed player height (0 = default). |
| `Placeholder` | string | `""` | Placeholder text. |

### YoutubeField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("YoutubeField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
        .WithSettings(new YoutubeFieldSettings
        {
            Required = false,
            Width = 640,
            Height = 360,
            Placeholder = "https://www.youtube.com/watch?v=...",
            Hint = "Paste a YouTube video URL"
        })
    )
);
```

---

## TaxonomyField

References taxonomy terms. Provided by the `OrchardCore.Taxonomies` module.

### TaxonomyField Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Hint` | string | `null` | Help text displayed below the field. |
| `Required` | bool | `false` | Whether the field is required. |
| `TaxonomyContentItemId` | string | `null` | The content item ID of the taxonomy. |
| `Unique` | bool | `false` | Whether to enforce unique selection. |
| `LeavesOnly` | bool | `false` | Whether to allow only leaf terms (no parents). |
| `Open` | bool | `false` | Whether the taxonomy tree is open by default. |

### TaxonomyField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("TaxonomyField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
        .WithSettings(new TaxonomyFieldSettings
        {
            Required = true,
            TaxonomyContentItemId = "{{TaxonomyContentItemId}}",
            LeavesOnly = true,
            Hint = "Select a category"
        })
    )
);
```

---

## LocalizationSetContentPickerField

References content items by localization set (for multi-lingual content). Provided by `OrchardCore.ContentLocalization`.

| Setting | Type | Default | Description |
|---|---|---|---|
| `Multiple` | bool | `false` | Whether to allow more than one localization set. |
| `DisplayedContentTypes` | string[] | `[]` | Content types offered by the picker. |

### LocalizationSetContentPickerField Migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("LocalizationSetContentPickerField")
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("{{Position}}")
    )
);
```

---

## MarkdownField

Stores Markdown on a part field. Enable `OrchardCore.Markdown`.

| Setting | Type | Default | Description |
|---|---|---|---|
| `SanitizeHtml` | bool | `true` | Sanitizes the final HTML (after Markdown conversion and shortcodes). |

The Markdown source is stored as authored and Liquid in it is not executed.

Use the default editor or `.WithEditor("Wysiwyg")`; the WYSIWYG editor accepts `MarkdownFieldWysiwygEditorSettings.Options`.

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("MarkdownField")
        .WithEditor("Wysiwyg")
        .WithSettings(new MarkdownFieldSettings
        {
            SanitizeHtml = true,
        })
    )
);
```

---

## GeoPointField

Stores latitude and longitude. Enable `OrchardCore.Spatial`; its field settings are the common `Hint` and `Required` settings. Use `.WithEditor("Leaflet")` for the built-in map editor.

For related content-part and route configuration, see `orchardcore-content-parts` and `orchardcore-autoroute`.

---

## Other Built-In Editors

| Field | Editors besides the standard one |
|---|---|
| `BooleanField` | `Switch` |
| `DateField` | `Localized` |
| `NumericField` | `Range`, `Select`, `Slider`, `Spinner` |
| `MultiTextField` | `CheckboxList`, `Picker` |
| `MediaField` | `Attached` (stores under `mediafields/`, requires `ManageAttachedMediaFieldsFolder`) |
| `TaxonomyField` | `Tags` |
| `UserPickerField` | `UserNames` |

---

## Custom Field Editors

Create `{FieldType}-{Editor}.Option.cshtml` (an `<option>` for the editor list)
and `{FieldType}-{Editor}.Edit.cshtml`. Field editors can be injected after
page load by the Flow, Bag, and widget list editors, so:

- Put configuration in `data-*` attributes on the editor wrapper and load the
  script as an ES module: `<script asp-src="~/MyModule/Scripts/my-editor.js" type="module" at="Foot"></script>`.
- Initialize every matching element, including ones added later (for example
  with a `MutationObserver` on `document.body`), and scope lookups to the
  wrapper or to `Html.IdFor(...)` ids instead of global ids.
- Do not depend on `DOMContentLoaded` alone or on jQuery; admin pages no
  longer load jQuery or jQuery UI implicitly (require them explicitly if a
  legacy editor still needs them).
- Render the hint with `@await DisplayAsync(await New.Hint_Admin(Hint: settings.Hint))`
  so admin themes can customize it.

```cshtml
@model OrchardCore.ContentFields.ViewModels.EditTextFieldViewModel
<div class="ocat-wrapper my-counter-editor" data-max="140">
    <label asp-for="Text" class="ocat-label">@Model.PartFieldDefinition.DisplayName()</label>
    <div class="ocat-end">
        <input asp-for="Text" class="form-control content-preview-text" />
    </div>
</div>
<script asp-src="~/MyModule/Scripts/counter-editor.js" type="module" at="Foot"></script>
```

```javascript
const init = (wrapper) => {
    if (wrapper.dataset.initialized) return;
    wrapper.dataset.initialized = "true";
    wrapper.querySelector("input").maxLength = Number(wrapper.dataset.max);
};

const initAll = () => document.querySelectorAll(".my-counter-editor").forEach(init);
initAll();
new MutationObserver(initAll).observe(document.body, { childList: true, subtree: true });
```

Content field handler and display driver resolvers now resolve their lists
once per scope and return the same list; copy it before adding or removing
items.

---

## Upgrading from 3.x

- `HtmlFieldSettings.RenderLiquid` and `MarkdownFieldSettings.RenderLiquid`
  were removed. Liquid in those fields renders as text and startup logs a
  warning per affected field; move that content to a `LiquidField`.
- HTML and Markdown are no longer sanitized on save. Template code that output
  `Field.Html` with `raw` must render the field shape or use `sanitize_html`.
- AJAX-injected field editors are ES modules and the admin no longer ships
  jQuery implicitly; port custom editors as described above.

---

## Installing Third-Party Field Modules

Modules that provide custom fields from external sources (community or third-party modules) must be installed as NuGet packages in the **web project** (the startup project of the solution):

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <!-- Orchard Core base -->
    <PackageReference Include="OrchardCore.Application.Cms.Targets" Version="3.*" />

    <!-- Third-party modules must be added to the web project -->
    <PackageReference Include="Lombiq.HelpfulExtensions.OrchardCore" Version="1.*" />
  </ItemGroup>
</Project>
```

For local project references to third-party modules:

```xml
<ItemGroup>
  <!-- Local third-party module project reference in the web project -->
  <ProjectReference Include="../ThirdParty.Module/ThirdParty.Module.csproj" />
</ItemGroup>
```
