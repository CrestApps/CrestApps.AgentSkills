---
name: orchardcore-crestapps-resources
description: Skill for CrestApps resource-management extensions in Orchard Core. Covers the shared registered scripts and stylesheets, local asset and CDN fallback behavior, resource names, dependency consumption, and safe Razor registration. Use this skill when requests mention CrestApps shared resources, Orchard Core resource manifests, CDN fallbacks, intl-tel-input, chart.js, EasyMDE, Flatpickr, DOMPurify, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.Resources, ResourceManagementOptionsConfiguration, ResourceManifest, item-selector, intl-tel-input, chart.js, flatpickr, and document-drop-zone.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Resources

## Consume CrestApps shared frontend resources

You are an Orchard Core expert. Use `CrestApps.OrchardCore.Resources` as the shared resource manifest for CrestApps modules. It registers known resource names with Orchard Core Resource Management. Consumers request a resource by name and let Orchard Core select local URLs or configured CDN behavior.

### Guidelines

- Install `CrestApps.OrchardCore.Resources` in the web or startup project when an application directly depends on its assets.
- Enable the exact `CrestApps.OrchardCore.Resources` feature. Other CrestApps features can bring it in through their manifest dependencies.
- Request resources by their registered names. Do not hardcode the module's virtual asset paths in a theme or module.
- Use Orchard Core resource tag helpers in Razor so resource management controls rendering and placement.
- Prefer the existing `intl-tel-input` script and style for `PhoneField`; the Content Fields module already depends on this feature.
- CDN-capable resources define local URLs, CDN URLs, versions, and for many resources integrity values. Keep browser CSP and SRI policies compatible with those declarations.
- Resources without a CDN declaration such as `item-selector` and `list-management-ui` are served from the module's local static assets.
- Do not define another manifest entry with an existing name. That makes selection and ordering ambiguous.
- Do not copy vendor files into a consuming module merely to use one of the registered names.
- Resource registration is not a content-feature enablement substitute. Enable the module that owns application behavior separately.
- When adding a new shared resource to this module, add the manifest declaration and local static assets together, then use a unique stable name.

### Feature overview

| Feature ID | Purpose |
|---|---|
| `CrestApps.OrchardCore.Resources` | Shared Orchard Core script and stylesheet resource manifest |

### Enable shared resources

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Resources"
      ],
      "disable": []
    }
  ]
}
```

## Registered resource names

Use the names in the following table. The implementation registers them in `ResourceManagementOptionsConfiguration`.

| Name | Kind | Notes |
|---|---|---|
| `list-management-ui` | Script | Local CrestApps list management UI |
| `item-selector` | Script and style | Local CrestApps item-selector UI |
| `easymde` | Script and style | EasyMDE editor assets |
| `chart.js` | Script | Local asset with jsDelivr CDN fallback |
| `marked` | Script | Local asset with cdnjs fallback |
| `flatpickr` | Script and style | Local assets with jsDelivr fallback |
| `flatpickr-culture` | Script | Local culture integration script |
| `dompurify` | Script | Local asset with cdnjs fallback |
| `highlightjs` | Script and style | CDN-backed Highlight.js and GitHub-style sheet |
| `technical-name-generator` | Script | CrestApps AI chat UI asset |
| `document-drop-zone` | Script and style | CrestApps AI chat UI asset |
| `intl-tel-input` | Script and style | International phone input assets |

## Register resources in Razor

Use resource tag helpers with the type and registered name. Add a script at the page foot when it does not need to run before the body is parsed.

```razor
<style asp-name="flatpickr"></style>
<script asp-name="flatpickr" at="Foot"></script>
<script asp-name="flatpickr-culture" at="Foot"></script>
```

For a phone editor, request both portions of the shared library:

```razor
<style asp-name="intl-tel-input"></style>
<script asp-name="intl-tel-input" at="Foot"></script>
```

For a client-side preview that renders Markdown and sanitizes it, request each concern explicitly:

```razor
<script asp-name="marked" at="Foot"></script>
<script asp-name="dompurify" at="Foot"></script>
```

Resource registration ensures the assets are available; it does not sanitize arbitrary HTML automatically. When displaying rendered user content, sanitize the output before assigning it to the DOM.

## Use a resource from a display driver view

Keep view behavior local to the shape. The driver should select the shape, while the view requests only the assets needed by that shape.

```razor
@model MyModule.ViewModels.ChartViewModel

<script asp-name="chart.js" at="Foot"></script>

<canvas id="@Model.CanvasId"></canvas>
<script at="Foot">
    const canvas = document.getElementById("@Model.CanvasId");
    new Chart(canvas, {
        type: "bar",
        data: @Html.Raw(Model.ChartDataJson)
    });
</script>
```

Serialize chart data safely for JavaScript and avoid using untrusted values as raw script text. Request `chart.js` by name rather than a `cdn.jsdelivr.net` URL so environments configured to prefer local assets continue to work.

## Add a shared resource to CrestApps Resources

When extending the source module itself, define a stable resource name and both development and minified local URLs. Add a CDN fallback only when there is a compatible published artifact and known integrity hash.

```csharp
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Resources;

internal sealed class ExampleResourceRegistration
{
    public static void Add(ResourceManifest manifest)
    {
        manifest
            .DefineScript("example-widget")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/scripts/example-widget.min.js",
                "~/CrestApps.OrchardCore.Resources/scripts/example-widget.js")
            .SetVersion("1.0.0");
    }
}
```

The actual module configures `ResourceManagementOptions` through `ResourceManagementOptionsConfiguration` and adds its `ResourceManifest` there. Do not register an independent manifest from a consuming module merely to duplicate a resource already owned by CrestApps Resources.

## CDN and production guidance

- Test the local path as well as the CDN path. A CDN outage must not hide an incorrect local asset path.
- Respect each registered version and integrity value. Do not declare a newer library version while retaining the old hash.
- `highlightjs` is registered from its CDN path, while several other resources provide both local and CDN alternatives.
- Ensure your CSP permits the CDN hosts only if production is configured to use CDN resources.
- Keep styles before dependent widget initialization scripts, especially for `flatpickr` and `intl-tel-input`.
- Ask the owning module to declare a resource dependency when a resource is intrinsic to that module's UI. Avoid requiring every consuming view to know the asset name.

## Troubleshooting

- If an `asp-name` resource does not render, verify the Resources feature is enabled for the tenant and the name matches the table exactly.
- If a vendor script exists but its matching style does not, check that the resource has a style declaration. Scripts and styles are independent registrations.
- If a CDN resource is blocked, inspect CSP, SRI, and the configured local or CDN preference before replacing the tag with a hardcoded URL.
- If `PhoneField` loses its interactive country selector, verify the Content Fields feature and its Resources dependency instead of adding a second `intl-tel-input` bundle.
