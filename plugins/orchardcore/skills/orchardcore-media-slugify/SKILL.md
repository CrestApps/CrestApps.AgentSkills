---
name: orchardcore-media-slugify
description: Skill for enabling SEO-friendly slugified media folder and file names in Orchard Core. Covers the Media Slugify feature, transliteration options, name normalization behavior, and configuration. Use this skill when requests mention Orchard Core Media Slugify, SEO-friendly media URLs, slugified asset names, media folder normalization, transliteration of file names, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Media.Slugify feature, SlugifyMediaNameNormalizerService, MediaSlugifyOptions, MediaSlugifyOptionsConfiguration, IMediaNameNormalizerService, and the OrchardCore_Media_Slugify configuration section. It also helps with cleaning up asset URLs, transliteration control, and the configuration patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Media Slugify - Prompt Templates

## Slugify Media Names

You are an Orchard Core expert. Enable and configure the Media Slugify feature so newly created media folders and files get SEO-friendly, URL-safe names.

### Guidelines

- Enable the `OrchardCore.Media.Slugify` feature. Its dependency is `OrchardCore.Media`.
- The feature registers `SlugifyMediaNameNormalizerService` as the media name normalizer, so folders and files created after it is enabled are slugified automatically.
- Example transformation: file `The team (2020).jpg` in folder `Images & docs` becomes `the-team-2020.jpg` in folder `images-docs`, so the URL changes from `/media/Images%20&%20docs/The%20team%20(2020).jpg` to `/media/images-docs/the-team-2020.jpg`.
- Enabling the feature does NOT rename existing folders and files; only new folders and files are slugified. Re-upload or re-create assets to normalize existing names.
- Because different original names can produce the same slug (for example `The team (2020).jpg` and `The Team 2020.jpg`), uploading both without renaming one may collide.
- Transliteration (converting accented/non-Latin characters to their closest ASCII form) is ON by default and can be toggled with the `Transliterate` option under the `OrchardCore_Media_Slugify` configuration section.
- Configuration is bound per tenant through `IShellConfiguration` in `MediaSlugifyOptionsConfiguration`.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

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

### Configuration

```json
{
  "OrchardCore": {
    "OrchardCore_Media_Slugify": {
      "Transliterate": true
    }
  }
}
```

- `Transliterate` (default `true`): when enabled, characters such as `é`, `ü`, or Cyrillic letters are transliterated to their closest ASCII equivalents before slugifying. Set to `false` to strip disallowed characters without transliteration.

### How It Works

- The feature supplies an `IMediaNameNormalizerService` implementation, `SlugifyMediaNameNormalizerService`, that the Media module calls whenever a folder or file is created or uploaded.
- `MediaSlugifyOptionsConfiguration` reads the `OrchardCore_Media_Slugify` section from the tenant shell configuration to populate `MediaSlugifyOptions.Transliterate`.
- Normalization runs at creation time, which is why pre-existing assets are unaffected.

### Notes

- Use this feature on public-facing sites where clean, predictable media URLs matter for SEO and sharing.
- Plan a one-time cleanup (re-upload or a scripted rename) if you enable slugify on a site that already has many mixed-case or space-containing asset names.
- Combine with `OrchardCore.Media` cache and CDN configuration for a complete media delivery setup.
