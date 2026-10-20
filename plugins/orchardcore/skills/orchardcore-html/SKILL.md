---
name: orchardcore-html
description: Skill for using HtmlBodyPart in Orchard Core. Covers HTML content editing, Liquid and Razor shape templates, HtmlBodyPartViewModel, HtmlBodyPartSettings, source persistence and final-output HTML sanitization, shortcodes, IHtmlDisplayService, Default, Wysiwyg, Trumbowyg and Monaco editors, custom editors, and migrating away from RenderLiquid. Use this skill when requests mention Orchard Core HTML Body, Use HtmlBodyPart for Rich Content Editing, Enabling HTML Body, Attaching HtmlBodyPart to a Content Type, Shapes, HtmlBodyPartViewModel Properties, SanitizeHtml, RenderLiquid removed, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core HTML Body

Enable `OrchardCore.Html` and attach `HtmlBodyPart` to a content type. The
part stores the authored source in `HtmlBodyPart.Html`; it has no `Body`
property.

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("Article", type => type
    .Creatable()
    .WithPart("TitlePart", part => part.WithPosition("0"))
    .WithPart("HtmlBodyPart", part => part
        .WithPosition("1")
        .WithEditor("Wysiwyg")
        .WithSettings(new HtmlBodyPartSettings
        {
            SanitizeHtml = true,
        })));
```

## Rendering Pipeline

The source is persisted exactly as authored; it is never sanitized or
rewritten on save. On display (Detail, Summary, preview, `BodyAspect`, and the
GraphQL `html` field) the part:

1. Takes the stored `Html`.
2. Processes shortcodes (with `ContentItem` in the shortcode context).
3. Sanitizes the final HTML when `HtmlBodyPartSettings.SanitizeHtml` is `true`
   (the default).

Liquid syntax is not executed; it renders as literal text. Use `LiquidPart` or
a `LiquidField` when authored content needs Liquid (see `orchardcore-liquid`).

## Shapes

| Shape | Display Type | Default Location | Model |
|---|---|---|---|
| `HtmlBodyPart` | `Detail` | `Content:5` | `HtmlBodyPartViewModel` |
| `HtmlBodyPart` | `Summary` | `Content:10` | `HtmlBodyPartViewModel` |

`HtmlBodyPartViewModel` exposes `Html` (the final rendered HTML),
`ContentItem`, `HtmlBodyPart` (whose `Html` is the authored source), and
`TypePartDefinition`.

```liquid
<article>
  {{ Model.Html | raw }}
</article>
```

```cshtml
@model OrchardCore.Html.ViewModels.HtmlBodyPartViewModel
<article>@Html.Raw(Model.Html)</article>
```

Outside the part's own shape, render the shape instead of the stored source.
`Model.ContentItem.Content.HtmlBodyPart.Html | raw` outputs the unsanitized
source:

```liquid
{{ Model.Content.HtmlBodyPart | shape_render }}
{{ Model.ContentItem.Content.HtmlBodyPart.Html | shortcode | sanitize_html | raw }}
```

## Editors

Select the editor per content type in the part settings, or with
`.WithEditor(...)`:

| Editor | Description |
|---|---|
| `Default` | Plain text area. |
| `Wysiwyg` | WYSIWYG editor. |
| `Trumbowyg` | Trumbowyg editor; `HtmlBodyPartTrumbowygEditorSettings` (`Options`, `InsertMediaWithUrl`, `AllowCustomScripts`). |
| `Monaco` | Source code editor; `HtmlBodyPartMonacoEditorSettings` (`Options`, `InsertMediaWithUrl`, `AllowCustomScripts`). |

Trumbowyg `Options` are a JavaScript object literal, validated with Acornima
(a direct dependency of `OrchardCore.Html`, no longer Jint) when the settings
are saved. Monaco `Options` must be valid JSON.

### Custom Editors

Declare an option shape `HtmlBodyPart_Option__{Name}`
(`HtmlBodyPart-{Name}.Option.cshtml`) and an editor shape
`HtmlBodyPart_Edit__{Name}` (`HtmlBodyPart-{Name}.Edit.cshtml`). The editor
binds the `Html` property:

```cshtml
@{
    string currentEditor = Model.Editor;
}
<option value="Plain" selected="@(currentEditor == "Plain")">@T["Plain text area"]</option>
```

```cshtml
@using OrchardCore.Html.ViewModels
@model HtmlBodyPartViewModel

<div class="ocat-wrapper">
    <label asp-for="Html" class="ocat-label">@T["Body"]</label>
    <div class="ocat-end">
        <textarea asp-for="Html" rows="10" class="form-control content-preview-text"></textarea>
    </div>
</div>
```

Editors can be injected later by Flow, Bag, and widget list editors, so load
editor scripts as ES modules (`<script asp-src="..." type="module"
at="Foot">`) and initialize every matching element, including elements added
after page load (for example with a `MutationObserver`), instead of relying on
`DOMContentLoaded` or fixed element ids. Admin scripts no longer load jQuery
implicitly.

## Rendering HTML in Custom Code

`IHtmlDisplayService` (`OrchardCore.Html.Services`, registered by
`services.AddHtmlServices()`) applies the same shortcode and sanitization
pipeline to any `HtmlViewModelBase` model:

```csharp
await _htmlDisplayService.UpdateModelHtmlAsync(
    model,
    new Shortcodes.Context { ["TypePartDefinition"] = typePartDefinition },
    sanitizeHtml: true);
```

## Upgrading from 3.x

- `HtmlBodyPartSettings.RenderLiquid` was removed. Liquid in existing HTML is
  kept but no longer executed, and tenant startup logs a warning for each type
  that had it enabled. Move that source to `LiquidPart` manually.
- Sanitization moved from save time to render time. Stored `Html` may now
  contain markup the sanitizer would remove, so templates that output
  `HtmlBodyPart.Html` with `raw` must render the shape or pipe through
  `sanitize_html` instead.
- `SanitizeHtml = false` remains the opt-out for trusted output.
