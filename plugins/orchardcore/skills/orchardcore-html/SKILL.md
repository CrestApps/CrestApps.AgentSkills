---
name: orchardcore-html
description: Skill for using HtmlBodyPart in Orchard Core. Covers HTML content editing, Liquid and Razor shape templates, HtmlBodyPartViewModel, TypePartDefinition, HTML sanitization, Trumbowyg and Monaco editors, and content type configuration. Use this skill when requests mention Orchard Core HTML Body, Use HtmlBodyPart for Rich Content Editing, Enabling HTML Body, Attaching HtmlBodyPart to a Content Type, Shapes, HtmlBodyPartViewModel Properties, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
---

# Orchard Core HTML Body

Enable `OrchardCore.Html` and attach `HtmlBodyPart` to a content type. The
part stores its source in `HtmlBodyPart.Html`; it has no `Body` property.

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("Article", type => type
    .Creatable()
    .WithPart("TitlePart", part => part.WithPosition("0"))
    .WithPart("HtmlBodyPart", part => part
        .WithPosition("1")
        .WithEditor("Wysiwyg")));
```

`HtmlBodyPartViewModel` exposes `Html`, `ContentItem`, `HtmlBodyPart`, and
`TypePartDefinition`. It does not expose `Body` or `TypePartSettings`.
Display rendering starts with the part HTML, optionally renders Liquid, and
then processes shortcodes.

```liquid
<article>
  {{ Model.Html | raw }}
</article>
```

```cshtml
@model OrchardCore.Html.ViewModels.HtmlBodyPartViewModel
<article>@Html.Raw(Model.Html)</article>
```

The part settings control sanitization, Liquid rendering, and editor choices.
Use the built-in default, Trumbowyg WYSIWYG, or Monaco editor according to the
enabled editor features and settings. Avoid relying on undocumented custom
editor shape names; inspect the available editor shapes in the target version.

All HTML is sanitized when `SanitizeHtml` is enabled. If Liquid is also
rendered, validate it after choosing the sanitization policy because
sanitization can change Liquid markup.
