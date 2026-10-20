---
name: orchardcore-content-parts
description: Skill for adding and configuring common built-in Orchard Core content parts. Covers selected settings, LiquidPart, HTML and Markdown body sanitization, TitlePart patterns on publish, migration patterns, and recipe configuration. Use this skill when requests mention Orchard Core Content Parts, Add and Configure Content Parts, General Pattern for Attaching a Part, TitlePart, TitlePart Settings, TitlePart Migration or closely related Orchard Core work. Strong matches include work with OrchardCore.Title, OrchardCore.Liquid, LiquidPart, OrchardCore.Autoroute, OrchardCore.Html, HtmlBodyPartSettings, OrchardCore.Markdown, MarkdownBodyPartSettings, OrchardCore.Lists, OrchardCore.Flows, OrchardCore.Alias, OrchardCore.PublishLater, OrchardCore.ContentLocalization, OrchardCore.Taxonomies, OrchardCore.Seo, OrchardCore.ContentPreview.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Content Parts - Prompt Templates

## Add and Configure Content Parts

You are an Orchard Core expert. Generate migration code and recipes for attaching common built-in content parts.

### Guidelines

- Parts are attached to content types using `AlterTypeDefinitionAsync` in a migration.
- Each part has specific settings that control its behavior.
- Part settings are applied via `.WithSettings(new XxxPartSettings { ... })`.
- Use `.WithPosition("N")` to control the order of parts in the editor.
- Content part editor wrapper placement uses `ContentPart_Edit` with differentiator `{ContentType}-{PartName}` when you need to move or hide the whole editor row.
- Part display shapes and inner `XxxPart_Edit` shapes from part drivers typically use `{PartName}` as the differentiator.
- Third-party modules providing parts (CrestApps, Lombiq, etc.) must be installed as NuGet packages in the web project (the startup project of the solution), not just in the module project.
- Always seal classes.

### General Pattern for Attaching a Part

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("{{PartName}}", part => part
        .WithPosition("{{Position}}")
        .WithSettings(new {{PartName}}Settings
        {
            // Part-specific settings
        })
    )
);
```

---

## TitlePart

Provides a title/display text for a content item. Feature: `OrchardCore.Title`.

### TitlePart Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Options` | TitlePartOptions | `Editable` | Title behavior: `Editable`, `GeneratedDisabled`, `GeneratedHidden`, `EditableRequired`. |
| `Pattern` | string | `null` | Liquid pattern to generate the title (used with `GeneratedDisabled` or `GeneratedHidden`). |
| `RenderTitle` | bool | `true` | Whether to render the title in the content shape. |
| `Placeholder` | string | `""` | Placeholder for the title editor. |

### TitlePart Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("TitlePart", part => part
        .WithPosition("0")
        .WithSettings(new TitlePartSettings
        {
            Options = TitlePartOptions.EditableRequired,
            RenderTitle = true
        })
    )
);
```

### TitlePart with Generated Title

A generated title is evaluated when the item is created or updated and also
when a version is published (for example when republishing an unpublished
item), at most once per content item per request.

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("TitlePart", part => part
        .WithPosition("0")
        .WithSettings(new TitlePartSettings
        {
            Options = TitlePartOptions.GeneratedDisabled,
            Pattern = "{{ ContentItem.Content.{{ContentType}}.Name.Text }} - {{ ContentItem.Content.{{ContentType}}.Date.Value | date: '%Y-%m-%d' }}"
        })
    )
);
```

---

## AutoroutePart

Generates a URL (slug) for a content item. Feature: `OrchardCore.Autoroute`.

### AutoroutePart Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `AllowCustomPath` | bool | `false` | Whether users can define a custom path. |
| `Pattern` | string | `"{{ ContentItem.DisplayText \| slugify }}"` | Liquid pattern to generate the slug. |
| `ShowHomepageOption` | bool | `false` | Whether to show the "Set as homepage" option. |
| `AllowUpdatePath` | bool | `false` | Whether to allow re-generating the path on data change. |
| `AllowDisabled` | bool | `false` | Whether to allow disabling autoroute on individual items. |
| `AllowRouteContainedItems` | bool | `false` | Whether to route contained items. |
| `ManageContainedItemRoutes` | bool | `false` | Whether this part manages contained item routes. |
| `AllowAbsolutePath` | bool | `false` | Whether to allow absolute paths for contained items. |

### AutoroutePart Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("AutoroutePart", part => part
        .WithPosition("1")
        .WithSettings(new AutoroutePartSettings
        {
            AllowCustomPath = true,
            ShowHomepageOption = false,
            Pattern = "{{ ContentItem.DisplayText | slugify }}",
            AllowUpdatePath = true
        })
    )
);
```

---

## HtmlBodyPart

Provides a rich HTML body editor. Feature: `OrchardCore.Html`.

The authored HTML is stored unchanged. On display it is processed for
shortcodes and then sanitized when `SanitizeHtml` is enabled. Liquid syntax is
not executed; use `LiquidPart` for templated bodies. See `orchardcore-html`.

### HtmlBodyPart Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `SanitizeHtml` | bool | `true` | Whether to sanitize the final rendered HTML. |

### HtmlBodyPart Editors

| Editor | Description |
|---|---|
| `Default` | Plain text area. |
| `Wysiwyg` | WYSIWYG HTML editor. |
| `Trumbowyg` | Trumbowyg WYSIWYG editor. |
| `Monaco` | Monaco code editor. |

### HtmlBodyPart Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("HtmlBodyPart", part => part
        .WithPosition("2")
        .WithEditor("Wysiwyg")
        .WithSettings(new HtmlBodyPartSettings
        {
            SanitizeHtml = true
        })
    )
);
```

---

## MarkdownBodyPart

Provides a Markdown body editor. Feature: `OrchardCore.Markdown`.

The authored Markdown is stored unchanged, converted to HTML on display,
processed for shortcodes, and then sanitized when `SanitizeHtml` is enabled.
Liquid in it is not executed. See `orchardcore-markdown`.

### MarkdownBodyPart Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `SanitizeHtml` | bool | `true` | Whether to sanitize the final HTML output. |

### MarkdownBodyPart Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("MarkdownBodyPart", part => part
        .WithPosition("2")
        .WithSettings(new MarkdownBodyPartSettings
        {
            SanitizeHtml = true
        })
    )
);
```

---

## LiquidPart

Stores a Liquid body in `LiquidPart.Liquid` and renders it on display, in the
body aspect, and in GraphQL. Feature: `OrchardCore.Liquid`. It has no settings
and no sanitize option. Editing it, or any item that contains it (including
widgets nested in `FlowPart` or `BagPart`), requires the security-critical
`ManageLiquidTemplates` permission. See `orchardcore-liquid`.

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("LiquidPart", part => part
        .WithPosition("2")
    )
);
```

---

## ListPart

Makes a content type a container for other content items. Feature: `OrchardCore.Lists`.

### ListPart Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `PageSize` | int | `10` | Number of items to display per page. |
| `EnableOrdering` | bool | `false` | Whether to allow manual ordering. |
| `ContainedContentTypes` | string[] | `null` | Content types that can be contained. |
| `ShowHeader` | bool | `false` | Whether to show the list header. |
| `ShowFullPager` | bool | `false` | Whether to render the full pager. |

### ListPart Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("Blog", type => type
    .WithPart("ListPart", part => part
        .WithPosition("3")
        .WithSettings(new ListPartSettings
        {
            PageSize = 10,
            EnableOrdering = false,
            ContainedContentTypes = new[] { "BlogPost" },
            ShowHeader = true
        })
    )
);
```

---

## FlowPart

Enables a content type to contain a flow of widget content items. Feature: `OrchardCore.Flows`.

### FlowPart Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `ContainedContentTypes` | string[] | `[]` | Content types that can be added to the flow. |
| `CollapseContainedItems` | bool | `false` | Whether contained editors start collapsed. |
| `DefaultAlignment` | FlowAlignment? | `null` | Default alignment for new flow items. |

### FlowPart Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("FlowPart", part => part
        .WithPosition("3")
        .WithSettings(new FlowPartSettings
        {
            ContainedContentTypes = new[] { "HtmlWidget", "ImageWidget", "BlockQuote" }
        })
    )
);
```

---

## BagPart

A container part allowing nested content items within a content item. Feature: `OrchardCore.Flows`.

### BagPart Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `ContainedContentTypes` | string[] | `[]` | Content types that can be added to the bag. |
| `DisplayType` | string | `null` | Display type for rendering bag items (`Detail` when not set). |
| `ContainedStereotypes` | string[] | `[]` | Stereotypes allowed in the bag. |
| `CollapseContainedItems` | bool | `false` | Whether contained editors start collapsed. |

### BagPart Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("{{BagPartName}}", "BagPart", part => part
        .WithDisplayName("{{DisplayName}}")
        .WithPosition("4")
        .WithSettings(new BagPartSettings
        {
            ContainedContentTypes = new[] { "Slide", "Testimonial" }
        })
    )
);
```

---

## AliasPart

Provides an alias identifier for a content item. Feature: `OrchardCore.Alias`.

### AliasPart Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Pattern` | string | `"{{ Model.ContentItem.DisplayText \| slugify }}"` | Liquid pattern to generate the alias. |
| `Options` | AliasPartOptions | `Editable` | `Editable` or `GeneratedDisabled`. |

### AliasPart Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("AliasPart", part => part
        .WithPosition("1")
        .WithSettings(new AliasPartSettings
        {
            Pattern = "{{ ContentItem.DisplayText | slugify }}"
        })
    )
);
```

---

## PublishLaterPart

Allows scheduling future publish dates. Feature: `OrchardCore.PublishLater`.

### PublishLaterPart Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("PublishLaterPart", part => part
        .WithPosition("5")
    )
);
```

---

## LocalizationPart

Enables content localization (translation). Feature: `OrchardCore.ContentLocalization`.

### LocalizationPart Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("LocalizationPart", part => part
        .WithPosition("6")
    )
);
```

---

## TaxonomyPart

Makes a content type act as a taxonomy container. Feature: `OrchardCore.Taxonomies`.

`TaxonomyPart` has no type-level settings. `TermContentType` and `Terms` are
stored on each taxonomy content item (`TaxonomyPart.TermContentType`), chosen
when the taxonomy is created or set in recipe content.

### TaxonomyPart Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{TaxonomyType}}", type => type
    .WithPart("TitlePart", part => part.WithPosition("0"))
    .WithPart("TaxonomyPart", part => part.WithPosition("1"))
);
```

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js:uuid()]",
          "ContentType": "Taxonomy",
          "DisplayText": "Categories",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "Categories" },
          "TaxonomyPart": { "TermContentType": "{{TermContentType}}", "Terms": [] }
        }
      ]
    }
  ]
}
```

---

## SeoMetaPart

Provides SEO metadata (meta title, description, etc.). Feature: `OrchardCore.Seo`.

### SeoMetaPart Settings

| Setting | Type | Default |
|---|---|---|
| `DisplayKeywords` | bool | `false` |
| `DisplayCustomMetaTags` | bool | `false` |
| `DisplayOpenGraph` | bool | `false` |
| `DisplayTwitter` | bool | `false` |
| `DisplayGoogleSchema` | bool | `false` |

### SeoMetaPart Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("SeoMetaPart", part => part
        .WithPosition("10")
    )
);
```

---

## PreviewPart

Enables content preview. Feature: `OrchardCore.ContentPreview`.

### PreviewPart Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `Pattern` | string | `""` | Liquid pattern for the preview URL. |

### PreviewPart Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .WithPart("PreviewPart", part => part
        .WithPosition("11")
        .WithSettings(new PreviewPartSettings
        {
            Pattern = "{{ ContentItem | display_url }}"
        })
    )
);
```

---

## Content Type Options

When defining a content type, these options control the content type behavior:

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentType}}", type => type
    .DisplayedAs("{{DisplayName}}")
    .Creatable()        // Can be created from the admin
    .Listable()         // Appears in content listings
    .Draftable()        // Supports draft versions
    .Versionable()      // Supports versioning
    .Securable()        // Supports per-content permissions
    .Stereotype("{{Stereotype}}") // Set a stereotype (e.g., "Widget", "MenuItem")
);
```

---

## Installing Third-Party Part Modules

Modules that provide custom parts from external sources (community or third-party modules) must be installed as NuGet packages in the **web project** (the startup project of the solution):

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

## Upgrading from 3.x

- `HtmlBodyPartSettings.RenderLiquid` and `MarkdownBodyPartSettings.RenderLiquid`
  were removed. Existing Liquid stays stored but renders as text; startup logs
  a warning per affected type. Move that content to `LiquidPart`.
- HTML and Markdown bodies are sanitized only when rendered. Render the part
  shape instead of writing `HtmlBodyPart.Html` with `raw`.
- Part editors injected by Flow and Bag editors are ES modules; custom part
  editor scripts must also initialize editors added after page load.
- Content part handler and display driver resolvers return the same list for
  the whole scope; copy it before adding or removing items.

For field settings and editor choices, see `orchardcore-content-fields`. For routing options, see `orchardcore-autoroute`.
