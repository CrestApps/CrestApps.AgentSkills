# Runtime Shape Discovery Reference

Use this reference when an existing page must be redesigned without guessing which Orchard Core view or shape produced the HTML.

## Discovery Worksheet

```text
Tenant:
URL:
Surface: frontend | admin
Active theme:
Feature state: Placements / Templates / AdminTemplates / Liquid
Content type:
Stereotype:
Display type:
Display mode:
Target DOM fragment:

Parent shape:
Shape type:
Binding:
Alternate or canonical template name:
Differentiator:
Placement zone and position:
Override location: theme file | Templates UI | AdminTemplates UI | placement UI
```

Fill one worksheet row for each visual fragment. Keep separate rows for the same shape type rendered in different contexts.

## Reading Shape Debug Markers

A page with shape debugging enabled contains nested marker pairs:

```html
<!--shape-start type:Content bindings:Content__BlogPost=>Views/Content-BlogPost.cshtml (razor) -->
  ...
  <!--shape-start type:TitlePart bindings:TitlePart=>Views/TitlePart.cshtml (razor) -->
  ...
  <!--shape-end type:TitlePart -->
<!--shape-end type:Content -->
```

Use a stack while scanning the document:

1. Push each `shape-start` marker.
2. Attach its `type` and `bindings` to the current parent.
3. Pop on the matching `shape-end`.
4. Associate the target DOM node with the smallest containing marker.
5. If the type repeats, use the containing content type, DOM context, field/part data, or a known differentiator.

The binding path is evidence of the winning binding, not necessarily the filename to enter in the Templates UI. Convert between canonical names and theme filenames deliberately.

## Choosing the Correct Target

| Observation | Target |
|---|---|
| `Content` binding is too broad for one content type | `Content__<ContentType>` or `Content_<DisplayType>__<ContentType>`. |
| `Widget` binding is too broad for one widget type | `Widget__<WidgetType>` or its display-type alternate. |
| Same `TextField` type appears several times | `TextField` plus the exact field differentiator. |
| A part has no display driver | `ContentPart` plus the part-name differentiator. |
| Whole admin part row must move or hide | `ContentPart_Edit` plus `<ContentType>-<PartName>`. |
| Field uses a display mode | `<FieldType>_Display` plus the full `<Part>-<Field>-<FieldType>_Display__<Mode>` differentiator. |
| A zone's container markup is wrong | `Zone__<ZoneName>` or the zone shape shown by the marker. |
| A wrapper is needed without changing inner markup | Add a wrapper through placement or a shape table provider. |

## Runtime Frontend Example

Goal: make only `BlogPost` detail pages use a new article shell.

1. Capture a frontend `BlogPost` detail page with shape debugging enabled.
2. Confirm a marker similar to `type:Content` with a `Content__BlogPost` binding.
3. Enable `OrchardCore.Templates` and `OrchardCore.Liquid` if a runtime edit is required.
4. Create the canonical template `Content__BlogPost` in the Templates UI, or add `Views/Content-BlogPost.liquid` to the active theme.
5. Render `Model.Content` so existing part and field shapes remain available.
6. Refresh the same detail URL and confirm the marker binding changed to the new template.
7. Repeat with a summary URL. Create `Content_Summary__BlogPost` only if the summary design differs.

```liquid
<article class="post post--{{ Model.ContentItem.ContentType | downcase }}">
    <header class="post__header">
        <h1>{{ Model.ContentItem.DisplayText }}</h1>
    </header>
    <div class="post__content">
        {{ Model.Content | shape_render }}
    </div>
</article>
```

## Runtime Admin Example

Goal: restyle the branding shape in the admin header.

1. Capture an admin page, not a frontend page, with shape debugging enabled.
2. Locate the `AdminBranding` marker and confirm the active admin theme binding.
3. Enable `OrchardCore.AdminTemplates` and use the Design → Admin Templates UI.
4. Override only the discovered `AdminBranding` shape. Do not create a generic `Layout` override unless the whole admin shell must change.
5. Refresh an admin page and verify the new binding and the required navigation/accessibility markup.

Keep frontend `Templates` and admin `AdminTemplates` changes separate. An admin template can affect every administrator page that renders the target shape.

## Placement Example

Goal: move one field in `BlogPost` detail output without replacing its markup.

```json
{
  "TextField": [
    {
      "displayType": "Detail",
      "differentiator": "BlogPost-Subtitle",
      "contentType": "BlogPost",
      "place": "Header:2"
    }
  ]
}
```

Goal: hide the whole `Services` part editor row for `LandingPage`:

```json
{
  "ContentPart_Edit": [
    {
      "differentiator": "LandingPage-Services",
      "place": "-"
    }
  ]
}
```

Use the shape type shown by the marker and the documented differentiator pattern. `BagPart_Edit` would target only the inner editor shape, not necessarily its full wrapper row.

## Verification Matrix

| Check | Detail | Summary | SummaryAdmin | Edit |
|---|---:|---:|---:|---:|
| Correct shape marker | yes | yes | yes | yes |
| Correct binding or alternate | yes | yes | yes | yes |
| Placement filter | if used | if used | if used | if used |
| Empty zone/field state | yes | yes | not always | yes |
| Responsive and accessible markup | yes | yes | yes | yes |
| Debug information removed after discovery | yes | yes | yes | yes |

## Sources

- [Orchard Core Shapes](https://docs.orchardcore.net/en/latest/topics/display/shapes/)
- [Orchard Core Placement](https://docs.orchardcore.net/en/latest/reference/modules/Placement/)
- [Orchard Core Placements module](https://docs.orchardcore.net/en/latest/reference/modules/Placements/)
- [Orchard Core Templates](https://docs.orchardcore.net/en/latest/reference/modules/Templates/)
- [Orchard Core Liquid](https://docs.orchardcore.net/en/latest/reference/modules/Liquid/)
