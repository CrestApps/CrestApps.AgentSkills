---
name: orchardcore-content-types
description: Skill for creating, managing, and configuring Orchard Core Content Types. Covers content part definitions, content field definitions, stereotypes, and content type indexing. Use this skill when requests mention Orchard Core Content Types, Create a Content Type, Migration Pattern, Content Field Configuration, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with TitlePart, AutoroutePart, CommonPart, ListPart, DataMigration, IContentDefinitionManager, WithPart, WithSettings, AutoroutePartSettings, WithField, TextField, HtmlField. It also helps with content type examples, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
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
