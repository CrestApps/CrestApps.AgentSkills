---
name: orchardcore-media-slugify
description: Skill for enabling SEO-friendly slugified media folder and file names in Orchard Core. Covers the OrchardCore.Media.Slugify feature, the OrchardCore Media Slugify configuration section and its Transliterate option, environment variable names, name normalization behavior, collisions, and custom IMediaNameNormalizerService implementations. Use this skill when requests mention Orchard Core Media Slugify, SEO-friendly media URLs, slugified asset names, media folder normalization, transliteration of uploaded file names, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include SlugifyMediaNameNormalizerService, MediaSlugifyOptions, MediaSlugifyOptionsConfiguration, IMediaNameNormalizerService, ISlugService, and upgrading from the OrchardCore_Media_Slugify section.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Media Slugify

## Slugify Media Names

Enable the Media Slugify feature so newly created media folders and files get
SEO-friendly, URL-safe names.

### Guidelines

- Enable `OrchardCore.Media.Slugify` (depends on `OrchardCore.Media`).
- The feature registers `SlugifyMediaNameNormalizerService` as the `IMediaNameNormalizerService`. The Media API (gallery uploads, folder creation) calls it for every new folder and file.
- Example: `The team (2020).jpg` in `Images & docs` becomes `the-team-2020.jpg` in `images-docs`, so `/media/Images%20&%20docs/The%20team%20(2020).jpg` becomes `/media/images-docs/the-team-2020.jpg`. The file extension is kept.
- Existing folders and files are NOT renamed; only new ones are slugified.
- Different names can produce the same slug (`The team (2020).jpg` and `The Team 2020.jpg`); the second upload then collides with the first.
- Transliteration (accented and non-Latin characters to ASCII, e.g. `Ελληνικά` to `ellinika`) is on by default and controlled by `Transliterate`.

### Enabling Media Slugify

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Media.Slugify"
      ],
      "disable": []
    }
  ]
}
```

### Configuration (`OrchardCore:Media:Slugify`)

```json
{
  "OrchardCore": {
    "Media": {
      "Slugify": {
        "Transliterate": true
      }
    }
  }
}
```

- `Transliterate` (default `true`): `true` calls `ISlugService.SlugifyAndTransliterate`; `false` calls `ISlugService.Slugify`, which strips characters it cannot keep.
- Environment variable: `OrchardCore__Media__Slugify__Transliterate=false`.
- The value is read per tenant from `IShellConfiguration` by `MediaSlugifyOptionsConfiguration` into `MediaSlugifyOptions`.

### Custom Normalizer

Replace the normalizer to apply your own naming rules (it is scoped):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Media;
using OrchardCore.Modules;

public sealed class LowercaseMediaNameNormalizer : IMediaNameNormalizerService
{
    public string NormalizeFolderName(string folderName)
        => folderName.Trim().ToLowerInvariant();

    public string NormalizeFileName(string fileName)
        => Path.GetFileNameWithoutExtension(fileName).Trim().ToLowerInvariant() + Path.GetExtension(fileName).ToLowerInvariant();
}

[RequireFeatures("OrchardCore.Media")]
public sealed class Startup : StartupBase
{
    // Run after the Media and Media Slugify startups so this registration wins.
    public override int Order => 10;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IMediaNameNormalizerService, LowercaseMediaNameNormalizer>());
    }
}
```

### Notes

- Use the feature on public sites where clean, predictable media URLs matter for SEO and sharing.
- Plan a one-time cleanup (re-upload or a scripted rename) when enabling it on a site that already has mixed-case or space-containing names.
- Combine with `orchardcore-media` CDN settings (`Media:CdnBaseUrl`) for media delivery.
- The Liquid `slugify` filter is separate and takes no `transliterate` argument (use the `transliterate` filter in templates); Media Slugify keeps its `Transliterate` option.

### Upgrading from 3.x

- Rename the `OrchardCore_Media_Slugify` section to `Media:Slugify` (env var `OrchardCore__OrchardCore_Media_Slugify__Transliterate` becomes `OrchardCore__Media__Slugify__Transliterate`). The old name is deprecated but still read.
