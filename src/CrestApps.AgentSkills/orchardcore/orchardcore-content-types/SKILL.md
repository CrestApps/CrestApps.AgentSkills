---
name: orchardcore-content-types
description: Skill for creating, managing, and configuring Orchard Core Content Types. Covers content definition services, content part definitions, content field definitions, stereotypes, block picker categories and thumbnails, the ContentTypes configuration section, content type permissions, and content type indexing. Use this skill when requests mention Orchard Core Content Types, Create a Content Type, Migration Pattern, Content Field Configuration or closely related Orchard Core work. Strong matches include work with TitlePart, AutoroutePart, CommonPart, ListPart, DataMigration, IContentDefinitionManager, IContentDefinitionService, AlterFieldContext, AlterTypePartContext, WithPart, WithSettings, AutoroutePartSettings, WithField, TextField, HtmlField, LiquidField, WithCategory, WithThumbnailPath, ContentTypesOptions, ContentTypesPermissions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Content Types - Prompt Templates

## Create a Content Type

You are an Orchard Core expert. Generate code and configuration for creating a content type.

### Guidelines

- Content type technical names must be PascalCase with no spaces.
- Always include a `TitlePart` unless the content type uses a custom title strategy.
- Add `AutoroutePart` for routable content types with a URL pattern.
- Use `CommonPart` conventions (owner, created/modified dates) where appropriate.
- Attach `ListPart` if the content type should act as a container.
- Use content part and field settings to configure editors and display modes.
- Use the asynchronous `IContentDefinitionManager` APIs such as `AlterTypeDefinitionAsync` and `AlterPartDefinitionAsync`; the synchronous `AlterTypeDefinition`/`AlterPartDefinition` methods no longer exist.
- Define fields on a content part, then attach that part to a content type. Fields cannot be attached directly to a type definition.
- To inject a part into types programmatically at build time, or to make a type, part, or field system-defined (undeletable through the UI or recipes), use `IContentDefinitionHandler` — see the `orchardcore-content-definition-handlers` skill.

### Migration Pattern

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
        await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentTypeName}}", type => type
            .DisplayedAs("{{DisplayName}}")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
            )
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    Pattern = "{{ ContentItem | display_text | slugify }}"
                })
            )
        );

        return 1;
    }
}
```

### Content Definition Service

`IContentDefinitionService` is in the `OrchardCore.ContentTypes`
namespace and is provided by `OrchardCore.ContentTypes.Abstractions`. It
contains reusable content definition operations and accepts
`AlterFieldContext` and `AlterTypePartContext` for targeted settings changes.
It does not expose public loading helpers. Use `IContentDefinitionManager`
when a definition must be loaded or listed.

```csharp
await contentDefinitionService.AlterFieldAsync(new AlterFieldContext
{
    PartName = "ArticlePart",
    FieldName = "Summary",
    DisplayName = "Summary",
    Editor = "Wysiwyg",
    DisplayMode = "Detail"
});

await contentDefinitionService.AlterTypePartAsync(new AlterTypePartContext
{
    TypeName = "Article",
    PartDefinition = await contentDefinitionManager.GetPartDefinitionAsync("ArticlePart"),
    PartName = "ArticlePart",
    DisplayName = "Article details",
    Description = "Article-specific settings",
    Editor = "Default",
    DisplayMode = "Detail"
});
```

### Content Field Configuration

When adding fields to a content part:

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("{{PartName}}", part => part
    .WithField("{{FieldName}}", field => field
        .OfType("{{FieldType}}")
        .WithDisplayName("{{FieldDisplayName}}")
        .WithPosition("{{Position}}")
    )
);
```

Common field types include:
- `TextField` - simple text input
- `HtmlField` - rich HTML editor
- `NumericField` - numeric values
- `BooleanField` - true/false
- `DateField` / `DateTimeField` - date pickers
- `ContentPickerField` - reference to other content items
- `MediaField` - media library attachment
- `LinkField` - URL with optional text
- `TaxonomyField` - taxonomy term selection
- `MarkdownField` - Markdown source rendered to sanitized HTML
- `LiquidField` - Liquid source executed on display (requires `ManageLiquidTemplates` to edit)

`HtmlField` and `MarkdownField` no longer execute Liquid. Attach a `LiquidPart`
or `LiquidField` when authored content needs templating.

### Block Picker Category and Thumbnail

Flow and Bag **Blocks** editors group content types by category and show a
thumbnail:

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("HeroBanner", type => type
    .Stereotype("Widget")
    .WithCategory("Media")
    .WithThumbnailPath("/media/thumbnails/hero-banner.png")
    .WithDescription("Full-width banner with a call to action"));
```

Defaults for types without a category or thumbnail come from
`ContentTypesOptions`, bound to the `OrchardCore:ContentTypes` section (the old
`OrchardCore_ContentTypes` section is deprecated but still read):

```json
{
  "OrchardCore": {
    "ContentTypes": {
      "DefaultCategory": "Widgets",
      "DefaultThumbnailPath": "/media/thumbnails/default.png"
    }
  }
}
```

### Permissions and Admin Shortcuts

- Editing definitions requires `OrchardCore.Contents.ContentTypesPermissions.EditContentTypes`
  (viewing: `ViewContentTypes`). The old `OrchardCore.ContentTypes.Permissions.*`
  members were removed in 4.0.
- The **Templates** shortcuts on the type, part, and type-part editors are only
  shown to users with both `ManageTemplates` and `ManageLiquidTemplates`.
- Adding a `LiquidPart`, `LiquidField`, or `FacebookPluginPart` to a type makes
  editing or publishing its items require `ManageLiquidTemplates` as well.
