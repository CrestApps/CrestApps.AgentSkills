---
name: orchardcore-shape-discovery
description: Skill for discovering rendered Orchard Core shapes and selecting safe frontend or admin theme overrides. Covers shape debug comments, bindings, alternates, differentiators, display types, field display modes, OrchardCore.Placements, OrchardCore.Templates, OrchardCore.AdminTemplates, OrchardCore.Liquid, and runtime visual theming workflows. Use when an AI must identify the exact shape behind existing HTML, create a targeted template or placement override, or redesign a running Orchard Core tenant without guessing shape names.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Shape Discovery

Use this skill before changing the visual output of an existing Orchard Core site. The rendered page is the source of truth: discover the shape type and binding first, then select the least invasive override that can produce the requested design.

## Core Rules

- Orchard Core renders a tree of shapes, not a single MVC view or a block of HTML.
- Treat `type`, `bindings`, nesting, URL, content type, display type, and the active theme as evidence. Do not invent a shape name from a CSS class or a part name.
- A shape type, a binding, a template name, and a placement key are related but are not always identical.
- A binding identifies the template or code that rendered the shape. The logical shape type identifies the shape used by placement and binding resolution.
- A repeated shape type can represent different fields or parts. Use the differentiator, surrounding shape tree, content definition, and request context to distinguish instances.
- Prefer a specific alternate or wrapper over replacing a generic template. Preserve child zones by rendering them when the design only changes the container.
- Use placement to move or hide output. Do not create an empty template to hide a shape.
- Keep shape debugging enabled only during development or controlled staging work. Debug comments expose implementation details in the response.

## Runtime Feature Map

| Feature | Use it for | Scope and behavior |
|---|---|---|
| `OrchardCore.Liquid` | Secure Liquid execution and Liquid filters/tags | Required by admin-authored Liquid templates. |
| `OrchardCore.Templates` | Create frontend Liquid shape templates from the admin UI | Overrides frontend shapes for the active frontend theme. |
| `OrchardCore.AdminTemplates` | Create admin Liquid shape templates from the admin UI | Overrides shapes rendered by the active admin theme. Treat its management permission as sensitive. |
| `OrchardCore.Placements` | Move, hide, substitute, alternate, or wrap shapes | Admin-defined rules are stored per tenant and take precedence over theme and module placement rules. |
| `OrchardCore.Placements.FileStorage` | Store placement rules in a file | Use only when the tenant is configured for file-based placement storage. |
| `DebugSettings.WriteShapeDebugInformation` | Add shape start/end comments to rendered HTML | Configure in Settings → Debugging → Write shape debug information. |

Feature names and menu labels can vary with the Orchard Core version and enabled features. Confirm the feature state and use the tenant's Design menu instead of assuming a route.

## Discovery Workflow

### 1. Define the rendering surface

Record all of the following before changing anything:

- Frontend or admin request. These use different active themes and may need `Templates` or `AdminTemplates`.
- Exact URL, query string, tenant, and user state.
- Content item, content type, stereotype such as `Widget`, and attached or named parts.
- Requested display context such as `Detail`, `Summary`, `SummaryAdmin`, or `Edit`.
- Whether a field or part uses a display mode such as `Header`, `Card`, or another configured mode.

If the request is not specific, inspect the live page that shows the undesired output rather than starting from the content type definition alone.

### 2. Enable shape debug information

Enable the tenant setting **Settings → Debugging → Write shape debug information**. In code, the equivalent setting is:

```csharp
public sealed class DebugSettings
{
    public bool WriteShapeDebugInformation { get; set; }
}
```

Refresh the exact page and capture its HTML. A marker has this form:

```html
<!--shape-start type:Content bindings:Content__BlogPost => Views/Content-BlogPost.cshtml (razor) -->
...
<!--shape-end type:Content -->
```

Use the marker as follows:

| Marker data | Meaning |
|---|---|
| `type:Content` | Logical shape type used by the rendering pipeline. Use this as the first placement key candidate. |
| `bindings:...` | Binding selected after alternate resolution. It shows the winning template or code binding. |
| Nested markers | Parent and child shape relationships. They show which zone or wrapper contains the target. |
| `(razor)`, `(liquid)`, or `(code)` | Binding implementation language or source. |

The marker does not reliably identify a unique field instance. For repeated types such as `TextField`, correlate the target DOM fragment with the surrounding content shape and use the documented differentiator pattern.

### 3. Build a shape inventory

For each target fragment, record:

```text
request: /some-page
surface: frontend | admin
active theme: confirmed theme name
content type: BlogPost
display type: Detail
shape type: Content
binding: Content__BlogPost => Views/Content-BlogPost.cshtml (razor)
candidate alternate: Content__BlogPost
differentiator: only when a repeated part or field requires it
parent zone: Content
```

Deduplicate the inventory by shape type and binding, but keep separate rows for different display types, content types, routes, and field or part instances.

### 4. Choose the override mechanism

| Desired change | First choice |
|---|---|
| Change order or zone | Placement rule with `place`. |
| Remove a shape | Placement rule with `place: "-"`. |
| Change one field or part instance | Placement rule filtered by the exact differentiator. |
| Replace markup for one content type or display type | Specific Liquid template alternate. |
| Add a common outer element around many shapes | Wrapper, preferably targeted by placement or a narrow alternate. |
| Change a whole page shell | Layout or layout-zone template in the correct theme. |
| Change admin UI markup | `AdminTemplates` or an admin theme template, never a frontend template. |
| Change values or conditional content | Template using the shape's actual model properties; inspect the content definition before coding. |
| Introduce a new reusable rendered component | A new shape template and, when code creates it, a display driver or shape factory. |

Use the existing `orchardcore-shapes`, `orchardcore-placement`, `orchardcore-templates`, and `orchardcore-theming` skills for the detailed implementation of the selected mechanism.

### 5. Apply and verify

1. Apply the smallest targeted change in the tenant UI when a runtime change is requested.
2. Refresh the same URL and confirm the expected binding, placement, and visual result.
3. Test every display type used by the site. A `Detail` override does not imply a `Summary` or `SummaryAdmin` override.
4. Test both a populated and an empty state for zones, fields, media, and lists.
5. Check responsive markup and accessibility after visual changes.
6. Remove temporary debug snippets and disable shape debug information when discovery is complete.

## Shape Names, Alternates, and Template Files

Use canonical binding names in placement rules, template editor names, metadata, and documentation. Theme filenames use punctuation that is converted to canonical names:

- `-` is a breaking separator and becomes `__`.
- `.` is a non-breaking separator and becomes `_`.
- A display suffix after the final breaking separator is placed before the first `__`.

Examples:

| Canonical name | Theme filename |
|---|---|
| `Content__BlogPost` | `Content-BlogPost.cshtml` or `Content-BlogPost.liquid` |
| `Content_Summary__BlogPost` | `Content-BlogPost.Summary.cshtml` or `Content-BlogPost.Summary.liquid` |
| `Widget__Hero` | `Widget-Hero.cshtml` or `Widget-Hero.liquid` |
| `Zone__Footer` | `Zone-Footer.cshtml` or `Zone-Footer.liquid` |

The most specific available alternate wins. A common content shape can have alternates for content type, display type, alias, slug, part, field, stereotype, and display mode. Add a new template only after confirming the alternate in the debug marker, content definition, or the Templates documentation.

## Display Types and Display Modes

Do not confuse these two concepts:

- **Display type** is the rendering context, commonly `Detail`, `Summary`, `SummaryAdmin`, and `Edit`. It is used by display drivers and placement rules and contributes to alternate names.
- **Display mode** is a configured presentation mode for a part or field, such as a custom `Header` mode. It can add a display-mode alternate and can change the shape type used for placement.

For a field display mode, use the `_Display` shape type and the complete differentiator. For a `Subtitle` text field on `Blog`, displayed with the `Header` mode, the placement key is:

```json
{
  "TextField_Display": [
    {
      "place": "Content:1",
      "differentiator": "Blog-Subtitle-TextField_Display__Header"
    }
  ]
}
```

For a part display mode, inspect the emitted marker and use the exact canonical alternate. A common pattern is:

```text
TitlePart_Summary__CustomMode_Display
```

Do not target `TitlePart` or `TextField` only because those are the underlying part or field names. The emitted shape type and differentiator decide the correct placement rule.

## Placement and Runtime Templates

### Placement rule shape

```json
{
  "TextField": [
    {
      "displayType": "Detail",
      "differentiator": "Blog-Subtitle",
      "contentType": "Blog",
      "place": "Content:2",
      "alternates": ["TextField__BlogSubtitle"],
      "wrappers": ["TextField_Wrapper"]
    }
  ]
}
```

Use `place: "-"` to hide a shape and a location beginning with `/` to move it to a layout zone. `shape` substitutes the logical shape type; it does not convert the CLR object. Placement also supports `contentType`, `contentPart`, and `path` filters. Placement group syntax for editor UI is `#` for tabs, `%` for cards, and `|` for columns.

Placement precedence is:

1. Startup application, which can act as a super-theme.
2. Active theme for the current request, frontend or admin.
3. Modules in dependency order.
4. Tenant placement rules from `OrchardCore.Placements`, which override theme and module placement rules.

Always target the original shape type shown by the marker. A rule for a part name will not match a `ContentPart` shape created for a part without a display driver.

### Liquid template pattern

Use a specific admin template name or theme filename that matches the discovered canonical binding:

```liquid
<article class="blog-post">
    <h1>{{ Model.ContentItem.DisplayText }}</h1>

    {% if Model.Content %}
        {{ Model.Content | shape_render }}
    {% endif %}
</article>
```

When the target is one child in a content zone, render the remaining children or explicitly remove the known differentiator:

```liquid
{% shape_remove_item Model.Content "Blog-Subtitle" %}
{{ Model.Content | shape_render }}
```

Do not use `shape_remove_item` as a replacement for a placement rule when the shape must be hidden consistently across templates and contexts.

## Common Failure Modes

| Symptom | Likely cause | Correction |
|---|---|---|
| Template never runs | Wrong surface, inactive feature, wrong canonical name, or wrong active theme | Confirm frontend/admin scope, feature state, debug binding, and filename conversion. |
| Whole content item changed unexpectedly | Generic `Content` or `Widget` template was overridden | Use the narrowest content type and display type alternate. |
| One of several fields changed | Placement targeted the shape type without its differentiator | Use the exact part/field differentiator. |
| Display mode rule has no effect | Used the base field type or short differentiator | Use `<FieldType>_Display` and the full `<Part>-<Field>-<FieldType>_Display__<Mode>` differentiator. |
| Child shapes disappeared | Replacement template did not render `Model.Content`, `Model.Header`, or another populated zone | Preserve and render the relevant child zones. |
| Empty Liquid template hides a shape | Template editor was used for placement | Create a placement rule with `place: "-"`. |
| Frontend change affects admin, or the reverse | Wrong template feature or active theme | Use `Templates` for frontend and `AdminTemplates` for admin output. |
| New theme file is not selected | Existing admin-defined template has higher precedence | Inspect and update or remove the admin template, or select a more specific alternate. |

## Related Skills and References

- Use `orchardcore-shapes` for shape metadata, wrappers, shape factory, shape table providers, and template rendering.
- Use `orchardcore-placement` for placement syntax, differentiators, editor grouping, and fluent locations.
- Use `orchardcore-templates` for the complete template alternate catalog and admin template behavior.
- Use `orchardcore-theming` and `orchardcore-theme-creator` for theme structure, layouts, assets, and resource manifests.
- See `references/runtime-discovery.md` for a compact discovery worksheet, marker parsing patterns, and end-to-end examples.
