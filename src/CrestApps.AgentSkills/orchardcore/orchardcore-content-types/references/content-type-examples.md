# Content Type Examples

## Example 1: Blog Post Content Type

### Migration

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
        await _contentDefinitionManager.AlterTypeDefinitionAsync("BlogPost", type => type
            .DisplayedAs("Blog Post")
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
            .WithPart("HtmlBodyPart", part => part
                .WithPosition("2")
                .WithEditor("Wysiwyg")
            )
            .WithPart("BlogPost", part => part
                .WithPosition("3")
            )
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync("BlogPost", part => part
            .WithField("Subtitle", field => field
                .OfType("TextField")
                .WithDisplayName("Subtitle")
                .WithPosition("0")
            )
            .WithField("Image", field => field
                .OfType("MediaField")
                .WithDisplayName("Featured Image")
                .WithPosition("1")
            )
            .WithField("Tags", field => field
                .OfType("TaxonomyField")
                .WithDisplayName("Tags")
                .WithPosition("2")
            )
        );

        return 1;
    }
}
```

## Example 2: Widget Content Type

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("HeroBanner", type => type
    .DisplayedAs("Hero Banner")
    .Stereotype("Widget")
    .WithCategory("Marketing")
    .WithPart("HeroBanner", part => part
        .WithPosition("0")
    )
);

await _contentDefinitionManager.AlterPartDefinitionAsync("HeroBanner", part => part
    .WithField("Heading", field => field
        .OfType("TextField")
        .WithDisplayName("Heading")
        .WithPosition("0")
    )
    .WithField("Description", field => field
        .OfType("HtmlField")
        .WithDisplayName("Description")
        .WithPosition("1")
    )
    .WithField("BackgroundImage", field => field
        .OfType("MediaField")
        .WithDisplayName("Background Image")
        .WithPosition("2")
    )
    .WithField("CallToAction", field => field
        .OfType("LinkField")
        .WithDisplayName("Call to Action")
        .WithPosition("3")
    )
);
```
