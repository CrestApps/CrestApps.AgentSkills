---
name: orchardcore-admin-edit-views
description: Skill for writing Orchard Core admin editor views with the ocat CSS classes. Covers Edit views for content parts and fields, field editor variants, part and field settings editors, site settings editors, workflow activity and deployment step Fields.Edit views, and full admin edit pages, plus the ocat-wrapper, ocat-label, ocat-label-required, ocat-end, ocat-end-offset, ocat-start, ocat-limited-wrapper and ocat-limited classes and their layout rules, content-part-wrapper and field-wrapper classes, asp-validation-class-for and validation messages, hints and the Hint_Admin shape, editor tabs, cards and columns, and horizontal admin form layouts. Use this skill when requests mention Orchard Core admin edit views, admin forms, Edit.cshtml, Fields.Edit.cshtml, Settings.Edit.cshtml, ocat classes, replacing mb-3 or form-group in admin views, or field hints. Strong matches include TheAdmin, ocat-wrapper, Hint_Admin, ContentPartDisplayDriver, ContentTypePartDefinitionDisplayDriver, SiteDisplayDriver.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Admin Edit Views

Every admin view that edits something (a content part or field, a settings group, a workflow activity, a deployment step, or a module's own entity) lays out its rows with the static `ocat-` (Orchard Core Admin Theme) CSS classes. TheAdmin styles them as a stacked Bootstrap form; a custom admin theme can restyle every editor (for example as a horizontal form) by overriding the classes, without touching any view.

- Use `ocat-*` only in admin views. **Never** in front-end views (Login, Register, ForgotPassword, or anything rendered by the site theme).
- Do not use Bootstrap-only row markup (`mb-3`, `form-group`, `form-label`) for admin field rows, and do not use the `@Orchard.GetWrapperClasses()`-style helpers (removed in Orchard Core 3.0).
- Admin pages that list items are not edit views; they render `<admin-list>` (see `orchardcore-admin-lists`).

## Which Views This Covers

| Editor | Produced by | View file |
|---|---|---|
| Content part editor | `ContentPartDisplayDriver<TPart>.Edit` with `GetEditorShapeType(context)` | `Views/{Part}.Edit.cshtml` |
| Content field editor | `ContentFieldDisplayDriver<TField>.Edit` with `GetEditorShapeType(context)` | `Views/{Field}.Edit.cshtml` |
| Field editor variant | The field's **Editor** setting | `Views/{Field}-{Editor}.Edit.cshtml` plus `Views/{Field}-{Editor}.Option.cshtml` (the `<option>` shown in the editor dropdown) |
| Part settings (per content type) | `ContentTypePartDefinitionDisplayDriver<TPart>` returning `"{Part}Settings_Edit"` at `Content` | `Views/{Part}Settings.Edit.cshtml` |
| Field settings | `ContentPartFieldDefinitionDisplayDriver<TField>` returning `"{Field}Settings_Edit"` at `Content` | `Views/{Field}Settings.Edit.cshtml` |
| Site settings group | `SiteDisplayDriver<TSettings>` with `.OnGroup(...)` | `Views/{Name}Settings.Edit.cshtml` |
| Workflow activity, deployment step | `ActivityDisplayDriver` / `DeploymentStepDisplayDriver` (`{Name}_Fields_Edit`) | `Views/Items/{Name}.Fields.Edit.cshtml` |
| Module entity page | Your admin controller action | `Views/{Controller}/Edit.cshtml`, `Create.cshtml` |

Shape-based editors (all rows except the last) render **only their rows**: the hosting page already owns the `<form>`, the validation summary, the title, and the Save button. Site settings get their Save button from the shared `SiteSettings.SaveButton` shape, so never add a form or a submit button to a settings editor.

## The `ocat-*` Classes

| Class | Element | Purpose | Default TheAdmin style |
|---|---|---|---|
| `ocat-wrapper` | `<div>` | Outer container of one field row. Carries `asp-validation-class-for`. | `margin-bottom: 1rem` |
| `ocat-label` | `<label>` (or `<div>` for a group caption) | Field label in the leading column. | Like `.form-label` |
| `ocat-label-required` | `<label>` | Added next to `ocat-label` on required fields. | Appends a red ` *` |
| `ocat-end` | `<div>` | Holds the control, its validation message, and its hint, after a label. | None (stacked) |
| `ocat-end-offset` | `<div>` | Same column with **no** label: checkboxes, switches, alerts, headings, buttons. | None (stacked) |
| `ocat-start` | `<div>` | Leading column container for content other than a label (alignment/push). | None (stacked) |
| `ocat-limited-wrapper` | `<div>` | Row (or inner wrapper) for a control that should stay narrow. | `margin-bottom: 1rem` |
| `ocat-limited` | `<div>` | Constrains the control width inside `ocat-limited-wrapper`. | `100%`, `50%` from `md`, `33.333%` from `lg`, `25%` from `xxl` |

### Choosing the Row Pattern

| Row | Pattern |
|---|---|
| Labelled full-width field | `ocat-wrapper` > `ocat-label` + `ocat-end` |
| Required field | Same, with `class="ocat-label ocat-label-required"` |
| Checkbox or switch (its label is inline) | `ocat-wrapper` > `ocat-end-offset` > `form-check` |
| Alert, notice, `<legend>`, section heading, or buttons in the form flow | `ocat-wrapper` > `ocat-end-offset` |
| Short control (number, port, page size, select, short code or path) | `ocat-limited-wrapper` > `ocat-label` + `ocat-limited` |
| Short control in a part or field editor that needs `content-part-wrapper-*` / `field-wrapper-*` classes on the row | Keep the outer `ocat-wrapper field-wrapper ...`, nest `ocat-limited-wrapper` > `ocat-limited` inside `ocat-end` |
| Read-only value | `ocat-wrapper` > `ocat-label` + `ocat-end` holding text |

### Layout Rules

1. Every field row is an `ocat-wrapper` (or a top-level `ocat-limited-wrapper`). Never put an input, validation span, or hint directly in the wrapper.
2. A row uses **either** `ocat-end` (it has a label) **or** `ocat-end-offset` (it has none), never both.
3. Validation spans and hints go inside `ocat-end` / `ocat-end-offset` / `ocat-limited` (or inside the `form-check` of a checkbox).
4. Standalone alerts, legends, headings, and action buttons get their own `ocat-wrapper` > `ocat-end-offset` row.
5. To show or hide a row dynamically, toggle `d-none` on the `ocat-wrapper` itself, not on an inner element.
6. Do not replace an outer row that carries `field-wrapper-*` or `content-part-wrapper-*` classes with `ocat-limited-wrapper`; nest the limited wrapper instead.
7. Container editors (the inner editors of BagPart, FlowPart, WidgetsListPart, and Admin Menu node trees) keep their own card markup and do not use `ocat-*` inside; their outer content-part wrapper still does.

## Row Templates

### Text, Required, Code, Textarea, Select

```cshtml
<div class="ocat-wrapper" asp-validation-class-for="Title">
    <label asp-for="Title" class="ocat-label ocat-label-required">@T["Title"]</label>
    <div class="ocat-end">
        <input asp-for="Title" class="form-control" autofocus />
        <span asp-validation-for="Title"></span>
        <span class="hint">@T["The title of the item."]</span>
    </div>
</div>

<div class="ocat-wrapper" asp-validation-class-for="Expression">
    <label asp-for="Expression" class="ocat-label">@T["Expression"]</label>
    <div class="ocat-end">
        <input asp-for="Expression" class="form-control code" />
        <span asp-validation-for="Expression"></span>
        <span class="hint">@T["A Liquid expression. Example {0}", "{{ Model.Value }}"]</span>
    </div>
</div>

<div class="ocat-wrapper" asp-validation-class-for="Description">
    <label asp-for="Description" class="ocat-label">@T["Description"]</label>
    <div class="ocat-end">
        <textarea asp-for="Description" class="form-control" rows="4"></textarea>
        <span asp-validation-for="Description"></span>
    </div>
</div>

<div class="ocat-wrapper" asp-validation-class-for="Category">
    <label asp-for="Category" class="ocat-label">@T["Category"]</label>
    <div class="ocat-end">
        <select asp-for="Category" class="form-select" asp-items="Model.Categories"></select>
        <span asp-validation-for="Category"></span>
    </div>
</div>
```

`form-control code` renders a monospace input for scripts, patterns, and expressions.

### Checkbox, Alert, Heading, Buttons (No Label Column)

```cshtml
<div class="ocat-wrapper" asp-validation-class-for="IsEnabled">
    <div class="ocat-end-offset">
        <div class="form-check">
            <input asp-for="IsEnabled" type="checkbox" class="form-check-input" />
            <label asp-for="IsEnabled" class="form-check-label">@T["Enable feature"]</label>
            <span class="hint dashed">@T["Check to enable this feature."]</span>
        </div>
    </div>
</div>

<div class="ocat-wrapper">
    <div class="ocat-end-offset">
        <h5>@T["Advanced"]</h5>
        <div class="alert alert-warning" role="alert">@T["Changing these values affects every tenant."]</div>
    </div>
</div>
```

Use `form-check form-switch` for a toggle. A settings editor that has nothing to edit can render its explanation in an `ocat-end-offset` alert row and `return;`.

### Input Group

```cshtml
<div class="ocat-wrapper" asp-validation-class-for="Url">
    <label asp-for="Url" class="ocat-label">@T["URL"]</label>
    <div class="ocat-end">
        <div class="input-group">
            <span class="input-group-text">https://</span>
            <input asp-for="Url" class="form-control" />
        </div>
        <span asp-validation-for="Url"></span>
    </div>
</div>
```

### Limited Width

```cshtml
<div class="ocat-limited-wrapper" asp-validation-class-for="PageSize">
    <label asp-for="PageSize" class="ocat-label">@T["Page size"]</label>
    <div class="ocat-limited">
        <input asp-for="PageSize" type="number" min="1" class="form-control" />
        <span asp-validation-for="PageSize"></span>
        <span class="hint">@T["The default page size."]</span>
    </div>
</div>
```

### Read-Only Value

```cshtml
<div class="ocat-wrapper">
    <label class="ocat-label">@T["Created"]</label>
    <div class="ocat-end">
        <span>@Model.CreatedUtc.ToString("g")</span>
    </div>
</div>
```

### Conditional Rows

```cshtml
<div class="ocat-limited-wrapper">
    <label asp-for="Mode" class="ocat-label">@T["Mode"]</label>
    <div class="ocat-limited">
        <select asp-for="Mode" class="form-select" asp-items="Html.GetEnumSelectList<ExportMode>()"></select>
    </div>
</div>

<div id="@Html.IdFor(m => m.Schedule)_group" class="ocat-wrapper @(Model.Mode == ExportMode.Scheduled ? "" : "d-none")" asp-validation-class-for="Schedule">
    <label asp-for="Schedule" class="ocat-label">@T["Schedule"]</label>
    <div class="ocat-end">
        <input asp-for="Schedule" class="form-control code" />
        <span asp-validation-for="Schedule"></span>
    </div>
</div>

<script at="Foot">
    document.addEventListener('DOMContentLoaded', () => {
        const mode = document.getElementById('@Html.IdFor(m => m.Mode)');
        const group = document.getElementById('@(Html.IdFor(m => m.Schedule))_group');
        const toggle = () => group.classList.toggle('d-none', mode.value !== '@nameof(ExportMode.Scheduled)');

        mode.addEventListener('change', toggle);
        toggle();
    });
</script>
```

Admin scripts are plain DOM code: TheAdmin no longer loads jQuery. Larger scripts belong in a module script (`<script asp-src="~/My.Module/Scripts/my-editor.min.js" at="Foot" type="module"></script>`).

## Validation

- `asp-validation-class-for="Prop"` on the row adds `has-validation-error is-invalid` when `ModelState` has an error for that property (`OrchardCore.DisplayManagement` `ValidationMessageTagHelper`). Inputs with errors get `input-validation-error`, and TheAdmin draws the error border and icon.
- `<span asp-validation-for="Prop"></span>` renders the message; place it right after the control.
- In shape-based editors the property names are prefixed, so add errors from the driver with the prefix-aware extension in `OrchardCore.Mvc.ModelBinding`:

```csharp
context.Updater.ModelState.AddModelError(Prefix, nameof(model.Pattern), S["A pattern is required when using the selected behavior."]);
```

- `context.Updater.ModelState.AddModelError(Prefix, S["..."])` reports a model-level error shown in the page's validation summary.
- Full pages render the summary once, above the form: `<div asp-validation-summary="ModelOnly"></div>` (or `@Html.ValidationSummary()` as the settings page does). Editors never render one.
- Return the editor from `UpdateAsync` (`return Edit(model, context);`) so the posted values and errors re-render.

## Hints

- `<span class="hint">` is secondary help text under the control; `hint dashed` prefixes it with an em dash and is used inside `form-check` rows.
- Content field editors render the user-configured hint (the field's `Hint` setting) through the `Hint_Admin` shape so admin themes can restyle every field hint at once:

```cshtml
@if (!string.IsNullOrEmpty(settings.Hint))
{
    @await DisplayAsync(await New.Hint_Admin(Hint: settings.Hint))
}
```

- TheAdmin's `Hint.Admin.cshtml` renders `<span class="hint dashed">@Model.Hint</span>`. A custom admin theme can override `Views/Hint.Admin.cshtml`, for example with a Bootstrap tooltip button (TheAdmin initializes `data-bs-toggle="tooltip"` elements). A theme that does not depend on TheAdmin must provide this template.
- For a checkbox field (like `BooleanField`), render the hint inside the `form-check-label`.

## Content Part and Field Wrapper Classes

Part and field editors put these classes on the outer row so themes can target one content type's editor:

- Parts: `ocat-wrapper content-part-wrapper content-part-wrapper-{part-name}`, plus `content-part-wrapper-{named-part-name}` for a named part.
- Fields: `ocat-wrapper field-wrapper field-wrapper-{part-name}-{field-name}`, plus `field-wrapper-{named-part-name}-{field-name}` for a field of a named part.

A field editor (`Views/RatingField.Edit.cshtml`) built like the shipped `NumericField`:

```cshtml
@model EditRatingFieldViewModel
@using OrchardCore.ContentManagement.Metadata.Models
@using OrchardCore.Mvc.Utilities
@{
    var settings = Model.PartFieldDefinition.GetSettings<RatingFieldSettings>();
    var partName = Model.PartFieldDefinition.PartDefinition.Name.HtmlClassify();
    var fieldName = Model.PartFieldDefinition.Name.HtmlClassify();
    var wrapperClasses = $"ocat-wrapper field-wrapper field-wrapper-{partName}-{fieldName}";

    if (Model.PartFieldDefinition.IsNamedPart())
    {
        wrapperClasses += $" field-wrapper-{Model.PartFieldDefinition.ContentTypePartDefinition.Name.HtmlClassify()}-{fieldName}";
    }
}

<div class="@wrapperClasses" id="@Html.IdFor(m => m.Value)_FieldWrapper" asp-validation-class-for="Value">
    <label asp-for="Value" class="ocat-label @(settings.Required ? "ocat-label-required" : "")">@Model.PartFieldDefinition.DisplayName()</label>
    <div class="ocat-end">
        <div class="ocat-limited-wrapper">
            <div class="ocat-limited">
                <input asp-for="Value" type="number" min="0" max="@settings.Maximum" class="form-control content-preview-select" />
                <span asp-validation-for="Value"></span>
            </div>
        </div>
        @if (!string.IsNullOrEmpty(settings.Hint))
        {
            @await DisplayAsync(await New.Hint_Admin(Hint: settings.Hint))
        }
    </div>
</div>
```

Shipped field editors localize the display name with `IDataLocalizer` (`D[Model.PartFieldDefinition.DisplayName(), DataLocalizationContext.ContentField(...)]`) when Data Localization is used. `content-preview-text` / `content-preview-select` classes let the Content Preview feature refresh on input.

## Editor Groupings (Tabs, Cards, Columns)

Editor shapes are grouped by their placement location, not by markup in the view. Append modifiers to the zone and position:

| Modifier | Grouping | Example |
|---|---|---|
| `#Name;order` | Tab | `Parts:1#Settings;2` |
| `%Name;order` | Card | `Parts:1%Details;1` |
| `\|Name_width;order` | Column (Bootstrap `col-md-{width}`, or `_lg-9` for another breakpoint) | `Parts:1\|Main_9;1` |

```csharp
return Initialize<MyPartViewModel>(GetEditorShapeType(context), model => model.Value = part.Value)
    .Location(l => l.Zone("Parts", "2").Tab("Settings", "1"));
```

- Settings editors use the same syntax, e.g. `.Location("Content:1#Providers")` puts a site settings group in a **Providers** tab.
- Shapes without a group fall into a default `Content` tab/card once others are grouped; give shapes distinct positions within a zone.
- `placement.json` can regroup editors of other modules (`"place": "Parts:0#Media;0"`). See `orchardcore-placement` for the full syntax.
- Do not use `|Column` editor groupings for admin list grid cells; lists declare columns with `<admin-list-column>`.

## Full Admin Edit Pages

A module's own edit page owns the title, form, summary, and actions:

```cshtml
@model RecordEditViewModel

<zone Name="Title">
    <breadcrumb name="RecordsEdit" title="@T["Edit {0}", Model.Name]">
        <breadcrumb-item id="Records" action="Index" controller="Record" area="My.Module">@T["Records"]</breadcrumb-item>
    </breadcrumb>
</zone>

<div asp-validation-summary="ModelOnly"></div>

<form asp-action="Edit" asp-route-returnUrl="@ViewData["ReturnUrl"]" method="post" class="no-multisubmit">
    <div class="ocat-wrapper" asp-validation-class-for="Name">
        <label asp-for="Name" class="ocat-label ocat-label-required">@T["Name"]</label>
        <div class="ocat-end">
            <input asp-for="Name" class="form-control" autofocus />
            <span asp-validation-for="Name"></span>
        </div>
    </div>

    <div class="ocat-wrapper">
        <div class="ocat-end-offset">
            <button type="submit" class="btn btn-primary save">@T["Save"]</button>
            <a asp-action="Index" class="btn btn-secondary cancel" role="button">@T["Cancel"]</a>
        </div>
    </div>
</form>
```

- `no-multisubmit` makes TheAdmin ignore a second submit while the form posts.
- Long editors can put the buttons in a sticky action bar at the top of the form instead: `<div class="card text-bg-theme mb-3 position-sticky action-bar"><div class="card-body p-3 text-end">...</div></div>`.
- A page that renders a shape-based editor (`@await DisplayAsync(Model.Editor)`) keeps the same structure; the editor contributes only rows.
- See `orchardcore-breadcrumbs` for the title trail and `orchardcore-admin` for controllers and routes.

## Restyling Admin Forms

The classes are the theming contract. A custom admin theme (see `orchardcore-theme-creator`) that depends on TheAdmin can turn every editor into a horizontal form with CSS only:

```css
.ocat-wrapper,
.ocat-limited-wrapper {
    --ocat-gutter-x: 1.5rem;
    display: flex;
    flex-wrap: wrap;
    margin-right: calc(-0.5 * var(--ocat-gutter-x));
    margin-left: calc(-0.5 * var(--ocat-gutter-x));
    margin-bottom: 1rem;
}

.ocat-wrapper > *,
.ocat-limited-wrapper > * {
    box-sizing: border-box;
    flex-shrink: 0;
    width: 100%;
    max-width: 100%;
    padding-right: calc(var(--ocat-gutter-x) * 0.5);
    padding-left: calc(var(--ocat-gutter-x) * 0.5);
}

.ocat-label { flex: 0 0 auto; width: 25%; padding-top: calc(0.375rem + var(--bs-border-width)); text-align: end; }
.ocat-start { flex: 0 0 auto; width: 25%; }
.ocat-end { flex: 0 0 auto; width: 75%; }
.ocat-end-offset { flex: 0 0 auto; width: 75%; margin-inline-start: 25%; }
.ocat-limited { flex: 0 0 auto; width: 33.333%; max-width: 33.333%; }
```

This works only because views follow the rules above: a row that skips `ocat-end` or mixes it with `ocat-end-offset` breaks the alignment.

## Migrating Legacy Views

| Old markup | New markup |
|---|---|
| `<div class="mb-3">`, `<div class="form-group">`, `@Orchard.GetWrapperClasses()` | `<div class="ocat-wrapper">` |
| `<label class="form-label">`, `@Orchard.GetLabelClasses()` | `<label class="ocat-label">` |
| `@Orchard.GetLabelClasses(inputRequired: true)`, `label.input-required` | `ocat-label ocat-label-required` |
| Control directly in the row, `@Orchard.GetEndClasses()` | Wrap in `<div class="ocat-end">` |
| `@Orchard.GetEndClasses(true)`, `@Orchard.GetOffsetClasses()`, checkbox rows | `<div class="ocat-end-offset">` |
| `@Orchard.GetStartClasses()` | `ocat-start` |
| `@Orchard.GetLimitedWidthWrapperClasses()`, `@Orchard.GetLimitedWidthClasses()` | `ocat-limited-wrapper`, `ocat-limited` |
| `@Orchard.GetPartWrapperClasses(...)`, `@Orchard.GetFieldWrapperClasses(...)` | Literal `ocat-wrapper content-part-wrapper ...` / `ocat-wrapper field-wrapper ...` |
| `TheAdminThemeOptions`, `TheAdminTheme:StyleSettings` | CSS overrides of the `ocat-*` classes in an admin theme |
| Hint directly in the row | Hint inside `ocat-end` / `ocat-end-offset` |

See `references/admin-edit-views-examples.md` for a complete part with its driver, part settings editor, site settings editor, and workflow activity editor.

## Related Skills

- `orchardcore-admin` for TheAdmin, admin controllers, routes, and settings pages.
- `orchardcore-admin-lists` for admin index (list) pages.
- `orchardcore-breadcrumbs` for the page title trail.
- `orchardcore-display-management` and `orchardcore-placement` for display drivers and editor placement.
- `orchardcore-content-parts`, `orchardcore-content-fields`, and `orchardcore-site-settings` for the models behind the editors.
