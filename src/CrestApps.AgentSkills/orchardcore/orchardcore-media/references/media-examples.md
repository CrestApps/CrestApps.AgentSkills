# Media Examples

## Example 1: Image Gallery Content Type with Media Fields

### Migration

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Media.Settings;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("ImageGallery", type => type
            .DisplayedAs("Image Gallery")
            .Creatable()
            .Listable()
            .Draftable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("ImageGallery", part => part.WithPosition("1")));

        await _contentDefinitionManager.AlterPartDefinitionAsync("ImageGallery", part => part
            .WithField("Images", field => field
                .OfType("MediaField")
                .WithDisplayName("Gallery Images")
                .WithEditor("Gallery")
                .WithSettings(new MediaFieldSettings
                {
                    Multiple = true,
                    AllowMediaText = true,
                    AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"],
                })
                .WithPosition("0"))
            .WithField("CoverImage", field => field
                .OfType("MediaField")
                .WithDisplayName("Cover Image")
                .WithSettings(new MediaFieldSettings
                {
                    Multiple = false,
                    AllowAnchors = true,
                })
                .WithPosition("1")));

        return 1;
    }
}
```

## Example 2: Media Profiles Recipe

```json
{
  "steps": [
    {
      "name": "MediaProfiles",
      "MediaProfiles": {
        "thumbnail": {
          "Hint": "Small thumbnail for listings",
          "Width": 150,
          "Height": 150,
          "Mode": "Crop",
          "Format": "WebP",
          "Quality": 75
        },
        "banner": {
          "Hint": "Full-width banner image",
          "Width": 1920,
          "Height": 600,
          "Mode": "Crop",
          "Format": "WebP",
          "Quality": 85
        },
        "avatar": {
          "Hint": "User avatar on a white background",
          "Width": 200,
          "Height": 200,
          "Mode": "Pad",
          "Format": "Png",
          "BackgroundColor": "ffffff"
        }
      }
    }
  ]
}
```

## Example 3: Displaying a Gallery in Liquid

```liquid
{% assign images = Model.ContentItem.Content.ImageGallery.Images %}
{% for path in images.Paths %}
    <a class="gallery-item" href="{{ path | asset_url }}">
        <img src="{{ path | asset_url | resize_url: profile: 'thumbnail' }}"
             srcset="{{ path | asset_url | resize_url: width: 400, format: 'webp' }} 400w,
                     {{ path | asset_url | resize_url: width: 800, format: 'webp' }} 800w"
             alt="{{ images.MediaTexts[forloop.index0] }}" />
    </a>
{% endfor %}
```

## Example 4: Cropping With the Field Anchor in Razor

```razor
@using OrchardCore.Media.Fields
@model OrchardCore.Media.ViewModels.DisplayMediaFieldViewModel

@for (var i = 0; i < Model.Field.Paths.Length; i++)
{
    <img asset-src="@Model.Field.Paths[i]"
         img-width="600"
         img-height="400"
         img-resize-mode="Crop"
         img-anchor="@Model.Field.GetAnchors()[i]"
         alt="@Model.Field.MediaTexts[i]" />
}
```
