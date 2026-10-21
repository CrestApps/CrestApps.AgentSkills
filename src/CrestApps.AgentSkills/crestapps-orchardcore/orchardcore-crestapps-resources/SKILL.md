---
name: orchardcore-crestapps-resources
description: Skill for the CrestApps Resources module in Orchard Core. Covers the shared script and stylesheet manifest with local assets and CDN fallbacks, the AI chat UI resources such as chat-markers, chat-image-carousel and realtime-audio, the DateRangePicker and ItemSelector view components, the date-range-picker markup contract and presets, the Vite front-end asset build with Assets.json groups and npm workspace commands, and the 3.0 removal of crestapps-bootstrap-select. Use this skill when requests mention CrestApps shared resources, a date range picker, an item selector, building CrestApps front-end assets, Assets.json, npm run build, CDN fallbacks, intl-tel-input, chart.js, flatpickr or DOMPurify. Strong matches include CrestApps.OrchardCore.Resources, ResourceManagementOptionsConfiguration, DateRangePickerViewComponent, ItemSelectorViewComponent, flatpickr-culture, date-range-picker, item-selector, chat-image-carousel and bootstrap-select.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Resources

## Consume CrestApps shared frontend resources

You are an Orchard Core expert. `CrestApps.OrchardCore.Resources` is the shared resource manifest for CrestApps modules. It registers named scripts and stylesheets with Orchard Core Resource Management, and ships two view components (`DateRangePicker` and `ItemSelector`). Consumers request a resource by name and let Orchard Core choose the local or CDN URL.

### Guidelines

- Install `CrestApps.OrchardCore.Resources` in the web or startup project when an application directly depends on its assets.
- Enable the `CrestApps.OrchardCore.Resources` feature. Other CrestApps features usually bring it in through their manifest dependencies.
- Request resources by their registered names with `<script asp-name>` / `<style asp-name>` (or the Liquid `{% script %}` / `{% style %}` tags). Do not hardcode the module's virtual asset paths.
- Scripts and styles are separate registrations. Request both when a name has both (`flatpickr`, `item-selector`, `intl-tel-input`, `chat-image-carousel`).
- CDN-capable resources declare local URLs, CDN URLs, a version, and integrity hashes. Keep CSP and SRI policies compatible with them.
- `bootstrap-select` is registered by Orchard Core's `OrchardCore.Resources` feature, not by this module. The old `crestapps-bootstrap-select` names were removed in 3.0.
- Do not define another manifest entry with an existing name, and do not copy vendor files into a consuming module to reuse a registered name.
- Resource registration does not sanitize HTML. Sanitize rendered user content (for example with `dompurify`) before assigning it to the DOM.
- See the framework `orchardcore-resources` skill for resource manifest and tag-helper mechanics.

### Feature overview

| Feature ID | Purpose |
|---|---|
| `CrestApps.OrchardCore.Resources` | Shared resource manifest, `DateRangePicker` and `ItemSelector` view components |

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

All names are registered in `ResourceManagementOptionsConfiguration`, added through `services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>()`.

| Name | Kind | Depends on | Notes |
|---|---|---|---|
| `list-management-ui` | Script | - | Select-all and row selection for admin list tables, local only |
| `item-selector` | Script and style | - | Searchable selector behind the `ItemSelector` view component, local only |
| `collapsible-panel` | Script | - | Collapsible admin panel behavior, local only |
| `tool-instance-parameters` | Script | - | AI tool instance parameter editor, local only |
| `easymde` | Script and style | - | EasyMDE 2.21.0, local only |
| `chart.js` | Script | - | Chart.js 4.5.1, jsDelivr fallback |
| `medium-zoom` | Script | - | medium-zoom 1.1.0, enlarges chat figures, jsDelivr fallback |
| `marked` | Script | - | marked 18, bundled to `window.marked`, jsDelivr fallback |
| `flatpickr` | Script and style | - | Flatpickr 4.6.13, jsDelivr fallback |
| `flatpickr-culture` | Script | - | `flatpickrCulture.createLocalizedDateConfig` / `createLocalizedDateTimeConfig` helpers that map .NET culture patterns to Flatpickr |
| `date-range-picker` | Script | `flatpickr`, `flatpickr-culture` | Date-range dropdown, see below |
| `dompurify` | Script | - | DOMPurify 3.4, jsDelivr fallback |
| `highlightjs` | Script and style | - | Highlight.js 11.12.0; the script is CDN only (cdnjs), the GitHub style has a local copy |
| `technical-name-generator` | Script | - | From `@crestapps/ai-chat-ui` |
| `document-drop-zone` | Script and style | - | From `@crestapps/ai-chat-ui` |
| `chat-markers` | Script | `chart.js` | `window.CoreAIChatMarkers`, turns `[doc:N]`, `[fig:N]`, `[chart:{...}]`, `[tbl:N]` markers into citations, pictures, charts and tables |
| `chat-image-carousel` | Script and style | - | `window.CoreAIChatImageCarousel`, stacks two or more pictures of one answer into a swipeable carousel |
| `realtime-audio` | Script | `signalr`, `dompurify` | `window.CoreAIRealtime`, speech-to-speech audio controller |
| `intl-tel-input` | Script and style | - | intl-tel-input 29.5.3 with utils, jsDelivr fallback |
| `sip.js` | Script | - | sip.js 0.21.2 bundled to `window.SIP`, local only |
| `telnyx-webrtc` | Script | - | Telnyx WebRTC SDK bundled to `window.TelnyxWebRTC`, local only |

The AI chat resources come from the `@crestapps/ai-chat-ui` package (`2.0.0-preview.226` at the time of writing); local copies, CDN URLs, versions and SRI hashes are updated together. The `AIChatApp` and `ChatInteractionApp` scripts declare dependencies on `chat-markers`, `chat-image-carousel`, `realtime-audio` and `medium-zoom`, so pages that require those apps need no change.

## Register resources in Razor

```razor
<style asp-name="flatpickr"></style>
<script asp-name="flatpickr" at="Foot"></script>
<script asp-name="flatpickr-culture" at="Foot"></script>

<style asp-name="intl-tel-input"></style>
<script asp-name="intl-tel-input" at="Foot"></script>

<script asp-name="marked" at="Foot"></script>
<script asp-name="dompurify" at="Foot"></script>
```

Request `chart.js` by name rather than a `cdn.jsdelivr.net` URL so environments that prefer local assets keep working:

```razor
@model MyModule.ViewModels.ChartViewModel

<script asp-name="chart.js" at="Foot"></script>

<canvas id="@Model.CanvasId"></canvas>
<script at="Foot">
    new Chart(document.getElementById("@Model.CanvasId"), {
        type: "bar",
        data: @Html.Raw(Model.ChartDataJson)
    });
</script>
```

## Date range picker

`date-range-picker` enhances two machine-formatted inputs (from and to) with one Bootstrap dropdown offering presets grouped into Relative days (Today, Yesterday, Last 7/30/90 Days), Calendar periods (This/Last Week, Month, Quarter, Year), Rolling months (Last 3/6/12 Months), a Custom Range, and single **On or before** / **On or after** bounds. The toggle always shows the current selection as text. It is purely client-side and general purpose (the Reports module uses it).

Defaults: when a user opens an empty custom or single-bound panel, the picker seeds it with today's local date. Lower-bound inputs default to `00:00`, upper-bound inputs to `23:59`. Presets are computed in the browser with the culture's first day of week.

### Render with the view component (recommended)

`DateRangePickerViewComponent` renders the full markup and requires its own resources.

```cshtml
@await Component.InvokeAsync("DateRangePicker", new
{
    fromName = Html.NameFor(m => m.From),
    toName = Html.NameFor(m => m.To),
    fromId = Html.IdFor(m => m.From),
    toId = Html.IdFor(m => m.To),
    from = Model.From,
    to = Model.To,
    selectedRangeName = Html.NameFor(m => m.Range),
    selectedRange = Model.Range,
    label = T["Date range"].Value,
})
```

| Parameter | Purpose |
|---|---|
| `fromName` / `toName` | Required. Posted field names for the bounds. |
| `fromId` / `toId` | Optional ids; derived from the names by default. |
| `from` / `to` | Optional `DateTime?` initial values, written as `yyyy-MM-ddTHH:mm`. |
| `selectedRangeName` / `selectedRange` | Optional hidden field name and value that persist the preset key (for example `last30` or `custom`). |
| `label`, `labelCssClass` | Optional label; class defaults to `form-label`. |
| `placeholder` | Toggle text when nothing is selected; defaults to "Select range". |
| `wrapperCssClass` | Root classes; defaults to `col p-1`. |
| `toggleCssClass` | Toggle classes; defaults to `form-select`. Use `form-select form-select-sm` in dense filter forms. |

Bind the bounds to `DateTime?` and the preset key to a `string`. Without `selectedRangeName` the picker falls back to Custom Range whenever initial values exist. For paginated list filters, also add the key to the driver's route values next to the bounds. Named presets keep the stored range and are not recomputed on reload.

### Markup contract (manual rendering)

Require `flatpickr` (style and script), `flatpickr-culture` and `date-range-picker`. The script auto-initializes each `[data-date-range-picker]` element:

| Selector | Purpose |
|---|---|
| `[data-date-range-picker]` | Root; reads `data-week-start` (0 = Sunday), `data-date-pattern`, `data-time-pattern`, `data-drp-initial`, and localized `data-prior-label`, `data-after-label`, `data-from-word`, `data-to-word` |
| `[data-drp-toggle]` / `[data-drp-label]` | Dropdown toggle and its text; `data-placeholder` shows when empty |
| `[data-drp-selected]` | Optional hidden input receiving the preset key |
| `input[type=radio][data-drp-range]` | One per option; values `today`, `yesterday`, `last7`, `last30`, `last90`, `thisWeek`, `lastWeek`, `thisMonth`, `lastMonth`, `last3Months`, `last6Months`, `last12Months`, `thisQuarter`, `lastQuarter`, `thisYear`, `lastYear`, `custom`, `prior`, `after` |
| `[data-drp-panel="custom"]` | Holds the real submit inputs `[data-drp-from]` and `[data-drp-to]` |
| `[data-drp-panel="prior"]` | `[data-drp-prior-date]` sets only the to bound |
| `[data-drp-panel="after"]` | `[data-drp-after-date]` sets only the from bound |

The module's `Views/Shared/Components/DateRangePicker/Default.cshtml` is the complete, localized reference.

## Item selector

`ItemSelectorViewComponent` renders a searchable `<select>` built on Orchard Core's `bootstrap-select` resource and the `item-selector` script. It requires `bootstrap-select` (which depends on `bootstrap`), so the page needs Orchard Core's `OrchardCore.Resources` feature.

```cshtml
@await Component.InvokeAsync("ItemSelector", new
{
    id = "agent-user-selector",
    inputName = Html.NameFor(m => m.UserName),
    endpoint = Model.UserSearchEndpoint,
    initialItemsJson = "[]",
    multiple = false,
    enableSearch = true,
    buttonText = T["Browse users"].Value,
    searchPlaceholder = T["Search users"].Value,
    emptyResultsText = T["No users found."].Value,
})
```

- `id`, `inputName` and `endpoint` are required. Other options include `multiple`, `enableSelectAll`, `searchDelay` (default 300 ms) and `smallButton`.
- The endpoint answers `GET ?query=<text>` with a JSON array of `{ "value", "text", "secondaryText", "isEnabled" }`.
- `initialItemsJson` lists the saved values in the same shape; each item is selected unless it sets `"selected": false`.
- Selected items are real `<option>` elements, so they post with the form under `inputName`.

## Building the module's front-end assets

CrestApps.OrchardCore 3.0 replaced the Gulp pipeline with a Vite-based build in one npm workspace. Built `wwwroot` files are committed and included in the NuGet packages, so package consumers build nothing. Read [references/building-assets.md](references/building-assets.md) when changing a module's scripts or styles, adding a library, or converting an `Assets.json`.

Quick reference (run at the repository root with Node.js 24 and npm 11):

```bash
npm install
npm run rebuild
npm run build -- -n date-range-picker.js
npm run watch -- -t Resources
npm run dry-run -- -t vendor
```

## Add a shared resource to CrestApps Resources

When extending the module itself, add the source under `Assets/`, an `Assets.json` group, and the manifest entry together. A `vite` group writes both the readable and `.min` files; register both.

```csharp
_manifest
    .DefineScript("example-widget")
    .SetUrl(
        "~/CrestApps.OrchardCore.Resources/scripts/example-widget.min.js",
        "~/CrestApps.OrchardCore.Resources/scripts/example-widget.js")
    .SetVersion("1.0.0");
```

Add `SetCdn(...)` and `SetCdnIntegrity(...)` only when a published artifact with a known hash exists. When bumping a vendored library, update the package version, local files, CDN URLs, version and hashes in one change.

## Upgrading from 2.x

- `crestapps-bootstrap-select` (style and script) was removed. Use Orchard Core's `bootstrap-select` resources and depend on `OrchardCore.Resources`. Enhanced Roles, Content Transfer and Omnichannel Management now declare that dependency. Contact Center views also use `bootstrap-select` but its manifest does not declare the dependency, so enable `OrchardCore.Resources` yourself.
- The Gulp build is gone. Convert any custom `Assets.json` to the `action` / `name` / `source` / `dest` format and run `npm install` at the repository root.
- Built scripts are bundled per source file. A top-level `var` no longer becomes a page global; assign page APIs to `window` explicitly.
- The AI chat resources moved to `@crestapps/ai-chat-ui` 2.0 previews and gained `chat-markers`, `chat-image-carousel`, `realtime-audio` and `medium-zoom`.

## Troubleshooting

- An `asp-name` resource does not render: verify the feature is enabled for the tenant and the name matches the table exactly.
- The item selector renders as a plain select: make sure `OrchardCore.Resources` is enabled so `bootstrap-select` resolves.
- A script works in the readable file but a page global is missing: publish the API with `window.name = ...` instead of a top-level `var`.
- A CDN resource is blocked: check CSP, SRI and the local or CDN preference before hardcoding a URL.
- `PhoneField` loses its country selector: verify the Content Fields feature and its Resources dependency rather than adding a second `intl-tel-input` bundle.
