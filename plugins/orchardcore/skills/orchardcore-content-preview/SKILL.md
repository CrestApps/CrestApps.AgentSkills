---
name: orchardcore-content-preview
description: Skill for configuring live draft preview in Orchard Core content editing. Covers ContentPreview, PreviewPart, PreviewPartSettings patterns, PreviewAspect URLs and custom aspect handlers, the preview draft controller, BroadcastChannel live updates, content-preview-text and content-preview-select hooks, the contentpreview render event for custom editors, preview detection, multi-node distributed cache, and frontend preview pipeline handling. Use this skill when requests mention Orchard Core Content Preview, live preview, PreviewPart, draft rendering, preview URL patterns, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.ContentPreview, PreviewPart, PreviewPartSettings, PreviewAspect, ContentPreviewFeature, PreviewController, PreviewStartupFilter, and IContentItemDisplayManager. It also helps with preview migrations, recipes, Liquid patterns, and the code patterns captured in this skill.
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
- The feature adds a **Preview** button (`ContentPreview_Button` shape, hidden by placement for menu items and dashboard widgets) to content editors. The button uses resource authorization for `Preview content` (`OrchardCore.Contents.CommonPermissions.PreviewContent`, including owner and per-type variants); the draft and display endpoints also require it.
- Live preview (unsaved form values) is different from **Preview Draft** in the content list, which renders the latest stored version.
- Attach `PreviewPart` only when a decoupled frontend needs a custom URL that should render the draft. The core preview UI works without it.
- Set `PreviewPartSettings.Pattern` to a Liquid path. The pattern is rendered with `Model` and `ContentItem`.
- Patterns should return a tenant-relative path starting with `/`, such as `{{ ContentItem | display_url }}` or `/articles/{{ ContentItem.ContentItemId }}`. Do not include a host or scheme; line breaks are stripped. External headless frontends cannot be preview targets.
- Preview drafts are stored in `IDistributedCache` for five minutes with sliding expiration and are keyed by an opaque preview token (not a credential). In multi-node deployments configure a shared distributed cache (e.g., Redis), or display requests on another node return `404`.
- The editor and preview window talk over a same-origin `BroadcastChannel`. Updates are debounced for 500 ms, in-flight requests are aborted by newer ones, and the preview swaps between two iframes to keep scroll position.
- `ContentPreviewFeature.Instance` (with `Previewing == true`) is placed in the current request features during a preview request. Drivers and handlers should check it to skip side effects (Orchard Core uses it to skip indexing and media moves).
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

During both `Draft` and `Display`, the controller places `ContentPreviewFeature.Instance` in the current `HttpContext.Features`. The editor update pipeline and custom update handlers still run against the temporary item, so avoid writes, queued work, or external calls while previewing:

```csharp
using OrchardCore.ContentPreview;

var isPreview = httpContext.Features.Get<ContentPreviewFeature>()?.Previewing == true;

if (isPreview)
{
    return;
}
```

Keep this logic narrowly scoped, and do not create a second preview cache or bypass the content manager session.

### Making a Custom Editor Live-Preview Aware

Live updates are driven by editor hooks, not a fixed list of field types:

| Editor behavior | Integration |
|---|---|
| Text-like control | Add the `content-preview-text` CSS class (input events trigger an update). |
| Select, checkbox, toggle | Add the `content-preview-select` CSS class (change events trigger an update). |
| Rich or JavaScript editor | Dispatch the `contentpreview:render` event on `document` after writing the value to a form control. |

```html
<input asp-for="Value" class="form-control content-preview-text" />
```

```javascript
// The preview script listens with addEventListener, so dispatch a native DOM event.
document.dispatchEvent(new CustomEvent('contentpreview:render'));
```

The module serializes the whole editor form with `FormData`, so values held only in JavaScript, disabled controls, and file inputs are not sent. BagPart and FlowPart editors trigger a render after every drag-and-drop operation.

### Computing the Preview URL in Code

When a Liquid pattern is not enough, provide `PreviewAspect` from a content handler instead of using `PreviewPart`:

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace MyModule.Handlers;

public sealed class ProductPreviewHandler : ContentHandlerBase
{
    public override Task GetContentItemAspectAsync(ContentItemAspectContext context)
    {
        if (context.ContentItem.ContentType != "Product")
        {
            return Task.CompletedTask;
        }

        return context.ForAsync<PreviewAspect>(aspect =>
        {
            aspect.PreviewUrl = $"/products/{context.ContentItem.ContentItemId}";

            return Task.CompletedTask;
        });
    }
}
```

Register it with `services.AddScoped<IContentHandler, ProductPreviewHandler>();`.

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
| Window opens but doesn't update | Keep the editor open, check `BroadcastChannel` support, and make sure the custom editor uses a hook above. A jQuery `$(document).trigger(...)` does not reach the native listener. |
| Preview is unstyled | The minimal fallback layout is used; attach `PreviewPart` with a route that renders through the active theme. |
| Preview returns `404` on a web farm | Configure a shared `IDistributedCache`. |
| Preview shows an older value | The latest values failed validation (the draft endpoint returns `422`); the window keeps the last successful render. |

### Upgrading from 3.x

- Admin scripts no longer use jQuery. Custom editors must raise the render event with `document.dispatchEvent(new CustomEvent('contentpreview:render'))` instead of `$(document).trigger('contentpreview:render')`.
