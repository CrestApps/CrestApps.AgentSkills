---
name: orchardcore-content-preview
description: Skill for configuring live draft preview in Orchard Core content editing. Covers ContentPreview, PreviewPart, PreviewPartSettings patterns, PreviewAspect URLs, the preview draft controller, and frontend preview pipeline handling. Use this skill when requests mention Orchard Core Content Preview, live preview, PreviewPart, draft rendering, preview URL patterns, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.ContentPreview, PreviewPart, PreviewPartSettings, PreviewAspect, ContentPreviewFeature, PreviewController, PreviewStartupFilter, and IContentItemDisplayManager. It also helps with preview migrations, recipes, Liquid patterns, and the code patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Content Preview - Prompt Templates

## Preview Unpublished Content

You are an Orchard Core expert. Generate configuration and content definitions for live preview of an editor's in-memory draft without publishing it.

### Guidelines

- Enable the `OrchardCore.ContentPreview` feature. Its module dependency is `OrchardCore.Contents`.
- The feature adds a preview button to content editors and requires the standard `PreviewContent` permission.
- Attach `PreviewPart` only when a decoupled frontend needs a custom URL that should render the draft. The core preview UI works without it.
- Set `PreviewPartSettings.Pattern` to a Liquid path. The pattern is rendered with `Model` and `ContentItem`.
- Patterns should return a relative path such as `/articles/{{ ContentItem.ContentItemId }}`. Do not include a host, scheme, or line breaks.
- Preview drafts are stored in distributed cache for five minutes with sliding expiration and are keyed by a preview token.
- `ContentPreviewFeature.Instance` is placed in the current request features during a preview request. Drivers and handlers can inspect it to change preview-specific behavior.
- A configured `PreviewPart` causes `PreviewStartupFilter` to re-execute the frontend pipeline with the preview path. This lets the real frontend route, theme, and scripts render the draft.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling Content Preview

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.ContentPreview"
      ],
      "disable": []
    }
  ]
}
```

### Attaching PreviewPart with a Migration

`PreviewPart` has no persisted fields. Its type-part setting supplies the frontend path pattern.

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentPreview.Models;
using OrchardCore.Data.Migration;

namespace MyModule;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("LandingPage", type => type
            .Draftable()
            .Versionable()
            .WithPart("TitlePart")
            .WithPart(nameof(PreviewPart), part => part
                .WithSettings(new PreviewPartSettings
                {
                    Pattern = "/landing-pages/{{ ContentItem.ContentItemId }}",
                })));

        return 1;
    }
}
```

### PreviewPart Settings

| Setting | Type | Description |
|---|---|---|
| `Pattern` | `string` | Liquid template that builds the frontend path for the draft. |

The handler populates `PreviewAspect.PreviewUrl` only when the pattern is non-empty. It strips line endings from the rendered result.

### Content Definition Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "LandingPage",
          "DisplayName": "Landing Page",
          "Settings": {
            "ContentTypeSettings": {
              "Draftable": true,
              "Versionable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart"
            },
            {
              "PartName": "PreviewPart",
              "Name": "PreviewPart",
              "Settings": {
                "PreviewPartSettings": {
                  "Pattern": "/landing-pages/{{ ContentItem.ContentItemId }}"
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

### How the Preview Request Works

1. The editor posts form values to `PreviewController.Draft`.
2. The controller creates an in-memory `ContentItem`, applies editor updates, validates it, and populates `PreviewAspect`.
3. The draft and its frontend preview URL are saved to distributed cache under `contentpreview:<token>`.
4. The browser loads `PreviewController.Display` with the token.
5. If the type has a preview path, `PreviewStartupFilter` reruns the request pipeline using that path. Otherwise the controller renders the detail shape through its MVC view.

Do not persist a preview draft from custom code. The feature deliberately keeps it transient and authorization-protected.

### Detecting Preview Mode in a Driver

During both `Draft` and `Display`, the controller places `ContentPreviewFeature.Instance` in the current `HttpContext.Features`. A driver, handler, or underlying service that needs preview-specific behavior can inspect that feature from its `IHttpContextAccessor` request. Keep this logic narrowly scoped, and do not create a second preview cache or bypass the content manager session. If no alternate behavior is required, do not add preview-specific driver logic.

### Frontend Considerations

- The configured route must load content through `IContentManager` or the normal display pipeline so the `IContentManagerSession` can return the cached draft.
- Keep the configured preview URL within the current tenant and protected by the preview token flow. Do not expose it as a public draft route.
- Preview is authorized with `CommonPermissions.PreviewContent`; a missing or expired cache entry returns `404`.
- Content types without `PreviewPart` still render their preview in the module's MVC display view with the detail display type.

### Troubleshooting

| Symptom | Check |
|---|---|
| Preview button is absent | Enable `OrchardCore.ContentPreview` and verify the user can preview content. |
| Preview opens the fallback renderer | Attach `PreviewPart` and configure a non-empty `Pattern`. |
| Frontend route shows published data | Ensure the route resolves content through the normal request-scoped content manager session. |
| Preview expires unexpectedly | The cache entry uses a five-minute sliding expiration; save or reload the preview to refresh it. |
