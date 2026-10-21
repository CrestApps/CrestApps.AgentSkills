---
name: orchardcore-admin-lists
description: Skill for Orchard Core 4.0 admin index views (list pages) rendered with the AdminList shape. Covers the List and Grid layouts, AdminListContext of TModel, the admin-list and admin-list-column tag helpers, column width and alignment, row display drivers placing shapes for SummaryAdmin and SummaryAdminGrid, row templates and zones, AdminListActions Buttons and Menu layouts, AdminListToolbar, AdminListSearch, the list-management script for select-all and bulk actions, IAdminListColumnsProvider, AdminListOptions, user layout selection, per-list alternates, sortable lists, and upgrading overridden list views. Use this skill when requests mention Orchard Core admin index views, an admin index page, admin lists, converting a hand-written admin listing, grid layout, list columns, row actions menu, or adding a column to the content items list. Strong matches include OrchardCore.Admin.Abstractions, AdminListContext, AdminListColumn, IAdminListColumnsProvider, AdminListOptions, AdminListLayout, ContentsAdminList.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Admin Lists

Every Orchard Core 4.0 admin page that lists items (content items, users, features, queries, workflows, layers, ...) renders them through the shared `AdminList` shape from `OrchardCore.Admin`. The shape renders the search bar, page actions, toolbar (item count, select-all, bulk actions), rows, empty message, pager, and page size selector in the layout the site (or the user) picks. Never hand-write a `<ul class="list-group">`, `<table>`, toolbar, or pager for an admin listing.

## Layouts

| Enum | Values | Description |
|---|---|---|
| `AdminListLayout` | `List` (default), `Grid` | `List` renders each item's `SummaryAdmin` shape as a whole row. `Grid` renders the item's `SummaryAdminGrid` shape, one cell per declared `AdminListColumn`, on a CSS grid whose header always lines up; below 48rem of list width (container query) it stacks like a `List` row instead of scrolling. |
| `AdminListActionsLayout` | `Buttons` (default), `Menu` | `Buttons` renders the `Actions` zone as buttons plus an "Actions" dropdown for `ActionsMenu`. `Menu` renders everything in one ellipsis dropdown. |

Exactly these values exist; templates can restyle them but cannot register more layouts.

## Ownership

| Layer | Responsibility |
|---|---|
| Controller | Query raw items and assign an `AdminListContext<TModel>` with `Source`, `Pager`, and optional region shapes. Never build the list or row shapes. |
| View | Render `<admin-list name="..." context="Model.List">` and declare the grid columns and presentation. |
| Row display driver | A `DisplayDriver<TModel>` returning shapes with a `SummaryAdmin` location (List) **and** a `SummaryAdminGrid` location (Grid). |
| Column provider | Optional `IAdminListColumnsProvider` adding or changing another list's columns. |
| `AdminList` shape | Resolves the layout at display time, builds each row once with that layout's display type, and renders the whole listing. |

Do not add column or cell drivers, column metadata classes, or build both representations of a row.

## When to Use

| Situation | Do this |
|---|---|
| New admin page that lists items | Follow "Building a List Page" below. |
| Controller already builds rows with `BuildDisplayAsync(item, updater, "SummaryAdmin")` and the view loops over them | Move the raw items into `AdminListContext<TModel>.Source`, delete the row building, add `SummaryAdminGrid` locations to the driver, render `<admin-list>`. |
| Rows are hard-coded `<li>` markup in Razor | Write a `DisplayDriver<TModel>` for the row model first, so each row is a zone-holding shape, then build the page. |
| Adding a column to another module's list | Column provider plus a `SummaryAdminGrid` location in your driver (see "Extending Another Module's Columns"). |
| The page edits one entity | Not a list; see `orchardcore-admin-edit-views`. |
| The page arranges items rather than listing them (a tree, a canvas, a media library, widgets by zone) | Keep its own presentation. |

## Building a List Page

### 1. Controller

```csharp
model.List = new AdminListContext<Announcement>
{
    Source = announcements,
    Pager = pagerShape,
};
```

The view model exposes `public AdminListContext List { get; set; }`. The generic argument selects the display model type (also for empty collections, and for a base class or interface display manager). Rows are built only while the view renders, after the layout is resolved, so driver queries and errors happen at render time.

### 2. View

Import the tag helpers once (for example in `Views/_ViewImports.cshtml`):

```cshtml
@using OrchardCore.Admin
@addTagHelper *, OrchardCore.Admin.Abstractions
```

The view keeps the form, its hidden inputs, and the action bar, and renders the list inside the form:

```cshtml
<form asp-action="Index" method="post" class="no-multisubmit" data-list-management data-selected-label="@T["selected"]">
    @* Keep the hidden filter submit first: Enter in the search box triggers it. *@
    <input type="submit" name="submit.Filter" id="submitFilter" class="visually-hidden" />
    <input asp-for="Options.BulkAction" type="hidden" />
    <input type="submit" name="submit.BulkAction" class="visually-hidden" />

    @* Search input group and page buttons in a sticky action bar, see references/admin-lists-examples.md *@

    <admin-list name="Announcements" context="Model.List"
                bulk-actions="Model.Options.BulkActions"
                empty-message="@T["<strong>Nothing here!</strong> There are no announcements yet."]">
        <admin-list-list item-css-class="list-group-item" />
        <admin-list-grid>
            <admin-list-column type="Select" />
            <admin-list-column name="Title" title="@T["Title"]" position="20" />
            <admin-list-column name="Status" title="@T["Status"]" position="30" width="auto" no-wrap="true" />
            <admin-list-column type="Actions" title="@T["Actions"]" />
        </admin-list-grid>
    </admin-list>
</form>

<script asp-name="list-management" at="Foot"></script>
```

- `name` identifies the list for column providers, the user's layout choice, and alternates. Use a string literal when only the view uses it; keep a module constant (like `ContentsAdminList.Name`) only when code reuses it.
- The search is an input group ending with a magnifying-glass submit button (`name="submit.Filter"`), never an icon inside the input. The `AdminListSearch` shape renders exactly that.
- The `list-management` script resource (from `OrchardCore.Resources`) wires every `[data-list-management]` root: the toolbar's `#select-all`, `#items`, `#selected-items`, and `#actions` elements, `itemIds` row checkboxes, bulk action `.dropdown-item[data-action]` links (confirmed through TheAdmin's `confirmDialog` when they carry `data-message`, as the generated toolbar's do), the hidden `Options.BulkAction` input, and `submit.BulkAction`. Override names with `data-item-input-name`, `data-bulk-action-input-name`, `data-submit-bulk-action-name`, and `data-min-selected-items`; add `data-client-side-search="true"` to filter rows by their `data-filter-value` attribute as the user types in `#search-box`, with an optional `#list-alert` element shown when nothing matches.
- Keep those ids as the toolbar renders them; scripts and alternates rely on them.

### 3. Row Display Driver

Pair every location with a display type. The shape type and template are shared; only the zone differs:

```csharp
public sealed class AnnouncementDisplayDriver : DisplayDriver<Announcement>
{
    public override Task<IDisplayResult> DisplayAsync(Announcement announcement, BuildDisplayContext context)
    {
        // Row classes and attributes render on the row element in both layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape row)
        {
            row.Attributes["data-filter-value"] = announcement.Title?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("Announcement_Checkbox_SummaryAdmin", announcement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("Announcement_Fields_SummaryAdmin", announcement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Title:1"),
            View("Announcement_Status_SummaryAdmin", announcement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("Announcement_Buttons_SummaryAdmin", announcement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Actions:5"));
    }
}
```

Register it with `services.AddDisplayDriver<Announcement, AnnouncementDisplayDriver>()` in the feature that owns the list; a driver registered by a neighbouring feature leaves every row empty on sites where that feature is off. The row shape type is `typeof(TModel).Name` plus the display type (`Announcement_SummaryAdmin`), so drive a domain model or a dedicated entry record named what the shape should be called, not a `*ViewModel`.

- Every visible piece of a row (checkbox, title, badges, dates, buttons, menu items) is its own shape in a zone. Anything hard-coded in the row template is missing from the grid.
- A location without a display type applies to both, so `.Location("Tags:5")` lands in a `Tags` zone no column renders. Pair every `SummaryAdmin` location with a `SummaryAdminGrid` one, unless the zone name is also a column name (such as `Actions`).
- A grid-only shape declares only a `SummaryAdminGrid` location. Display types do not inherit placement, whether it comes from a driver or `placement.json`.
- Never use editor `|Column` groupings for cells; columns come from `<admin-list-column>`.

The full controller, views, and templates are in `references/admin-lists-examples.md`.

## Tag Helper Reference

`<admin-list>` requires `name` and `context`:

| Attribute | Purpose |
|---|---|
| `name` | Stable list identity used by column providers, the user's layout cookie, and alternates. Not part of the context. |
| `context` | The `AdminListContext` from the view model. |
| `bulk-actions` | `IList<SelectListItem>` for the generic toolbar; each `Value` is the posted action name. |
| `empty-message` | `IHtmlContent` shown when there are no rows (defaults to "No results found."). |
| `show-toolbar`, `show-select-all`, `show-layout-selector` | Override the matching context flags. |
| `layout` | Forces `List` or `Grid`. |
| `item-css-class` | Row classes for the `List` layout (default `list-group-item list-group-item-action`). |
| `rows-*` / `rows-attributes` | Attributes on the element wrapping the rows, e.g. `rows-id="my-sortable-list"`. |

`<admin-list-list>` and `<admin-list-grid>` accept `rows-class` and `item-css-class`, may each be declared once, configure their layout without forcing it, and emit no HTML. For Grid, `item-css-class` adds to the required grid row class.

`<admin-list-column>` (inside `admin-list` or `admin-list-grid`):

| Attribute | Purpose |
|---|---|
| `type` | `Select` (the `Select` zone at position `10`) or `Actions` (`Actions` and `ActionsMenu` zones at position `end`, end-aligned). Omit for a regular column. |
| `name` | Required for regular columns, unique ignoring case. Default zone and `admin-list-column-{name}` CSS class. |
| `title` | Localized header text; also labels stacked cells. |
| `position` | Placement syntax (`10`, `25`, `35.5`, `end`). Columns sort after all providers ran; unpositioned columns go last. |
| `width` | Any CSS width (`20%`, `12rem`) or `auto` to hug the content. Columns without a width share the remaining space. |
| `alignment` | `Start` (default), `Center`, `End`. |
| `no-wrap` | Prevents wrapping, e.g. dates and badges. |
| `css-class` | Extra classes on the header and cells. |
| `zones` | Comma- or space-separated row zones to render instead of the zone named after the column. |

## `AdminListContext` Properties

| Property | Description |
|---|---|
| `Source`, `ItemType` | Raw items; `AdminListContext<TModel>` sets `ItemType` and types `Source`. |
| `Pager`, `PageSize` | Pager shape; a separately placed page size selector (set `ShowPageSizeSelector = false` on the pager to avoid rendering it twice). |
| `Header` | Options editor shape whose `Summary` and `Actions` zones render above the rows (content items, users). |
| `Toolbar`, `ShowToolbar`, `ShowSelectAll`, `BulkActions`, `ToolbarActions` | Supply a custom toolbar, or let the list build `AdminListToolbar` from the rows, pager, and bulk actions. |
| `Search`, `Actions` | Search-bar shape (e.g. `AdminListSearch` with `Name`, `Value`, `Placeholder`, `Id`, `SubmitName`, `Autofocus`, and a `Filters` zone) and page buttons. |
| `Layout`, `ShowLayoutSelector` | Force a layout (e.g. while rows are drag-sorted) and hide the selector. |
| `Data` | Values for column providers, e.g. `ContentsAdminList.ContentTypesKey`. |
| `RowsAttributes`, `ItemCssClass`, `ListOptions`, `GridOptions`, `EmptyMessage` | Presentation. |

The default `IAdminListItemDisplayManager<TModel>` delegates to `IDisplayManager<TModel>`; register a closed implementation for a specialized pipeline (Contents uses one that calls `IContentItemDisplayManager`).

## Row Templates and Placement

`{Model}.SummaryAdmin.cshtml` renders the zones for the `List` layout only (the grid renders column zones directly). Every shipped row uses the same structure so lists look alike:

```cshtml
<div class="row g-0 align-items-center">
    <div class="col">
        <div class="title d-flex align-items-center">
            @* Checkbox zone, then <div class="summary d-flex flex-column flex-md-row list-item-search-text"> with Content, Tags, Meta *@
        </div>
        @if (Model.Description != null)
        {
            @await DisplayAsync(Model.Description)
        }
    </div>
    <div class="col-auto d-flex justify-content-end ps-2">
        @await DisplayAsync(await Factory.CreateAdminListActionsAsync((IShape)Model))
    </div>
</div>
```

| Zone | Renders as | Never |
|---|---|---|
| `Content` | The title, a plain link to the edit page | A heading (`<h5>`) |
| `Tags`, `Meta` | One `<span class="badge ta-badge fw-normal">` per fact, with a leading icon and a `title` tooltip | Plain text or `<span class="hint">` |
| `Description` | `<div class="admin-list-secondary">`, its own line under the title | A full-width row running under the actions |
| `Actions`, `ActionsMenu` | Through `CreateAdminListActionsAsync`, so the Buttons/Menu setting applies; menu items are bare `<a>`/`<button class="dropdown-item">` | A hand-written button group, or menu items wrapped in `<li>` |

- `placement.json` rules filtered by `"displayType": "SummaryAdminGrid"` move a shape to another column zone, hide it (`"place": "-"`), change its template, or add wrappers and alternates, independently of the List layout:

```json
{
  "ContentCulture_SummaryAdmin": [
    { "displayType": "SummaryAdminGrid", "place": "Status:10" }
  ]
}
```

- Whole-row templates such as `Content-BlogPost.SummaryAdmin.cshtml` affect only `List`; customize grid cells with `AdminListCell-{ListName}-{Column}.cshtml`, which receives `Row` and `Column`. The grid only creates `AdminListCell` shapes for overridden cells (see `CellRenderings`).
- The content items row now places the title link and type badge as `ContentsTitle_SummaryAdmin` and `ContentsType_SummaryAdmin` in the `Title` and `Type` zones.
- The Actions cell renders the row's `Actions` and `ActionsMenu` zones through `AdminListActions`; moving an action shape to another zone removes it from that cell.
- On the shape, `Rows` is output built during display (never an input) and `DisplayType` is the resolved row display type. Rows and regions inherit the list name and layout, so their alternates work without the page passing them.

## Extending Another Module's Columns

```csharp
public sealed class CultureColumnsProvider : IAdminListColumnsProvider
{
    private readonly IStringLocalizer S;

    public CultureColumnsProvider(IStringLocalizer<CultureColumnsProvider> localizer)
    {
        S = localizer;
    }

    public Task PopulateAsync(AdminListColumnsContext context)
    {
        context.Columns.Add(new AdminListColumn
        {
            Name = "Culture",
            Position = "25",
            Title = S["Culture"],
            Width = AdminListColumn.AutoWidth,
        });

        return Task.CompletedTask;
    }
}
```

```csharp
services.AddAdminListColumnsProvider<CultureColumnsProvider>(ContentsAdminList.Name);
services.AddAdminListColumnsProvider<SiteColumnsProvider>(); // global, every list
```

- Named providers are keyed scoped services (`AdminListColumnsProviderKey`); list names are case-sensitive and only matching providers are constructed. Duplicate registrations are ignored.
- Global providers run first, then named ones, each group in feature dependency order; columns are sorted afterwards. Duplicate column names throw; provider errors are logged.
- `context.ListName`, `context.Find(name)`, `context.Remove(name)`, `context.Data`, `TryGetData<T>()`, and `GetData<T>()` are available. Global providers must check `Find` before changing optional columns.
- A provider never renders cells: a display driver must place a shape in that column's zone for `SummaryAdminGrid`.
- `AdminListColumns.Select()` and `AdminListColumns.Actions(S["Actions"])` create the shared first and last columns in C#.

Published list names include `ContentsAdminList.Name` (`Contents`) and `UsersAdminList.Name` (`Users`); others are literal, e.g. `Features`, `Queries`, `Layers`, `Tenants`, `WorkflowTypes`, `AdminMenus`, `AuditTrail`, `Roles`, `Templates`. The Contents list passes `ContentsAdminList.ContentTypesKey` and `ContentsAdminList.StereotypesKey` in `Data`.

## Configuration and Settings

Shipped defaults come from `AdminListOptions`, bound per tenant from `OrchardCore:Admin:List`:

```json
{
  "OrchardCore": {
    "Admin": {
      "List": {
        "DefaultLayout": "Grid",
        "DefaultActionsLayout": "Menu",
        "AllowUserSelection": true
      }
    }
  }
}
```

**Configuration → Settings → Admin** overrides them with **List layout**, **List actions layout** (each with **Use configuration default**), and **Let users choose the layout of a list**, stored on `AdminSettings`:

```json
{
  "steps": [
    {
      "name": "settings",
      "AdminSettings": {
        "ListLayout": "Grid",
        "ListActionsLayout": "Menu",
        "AllowUserListLayoutSelection": true
      }
    }
  ]
}
```

`AdminListOptions` participates in the signal-backed refresh pipeline: read it through `IOptionsMonitor<AdminListOptions>.CurrentValue`; saving the settings applies without a shell release.

With user selection on, each list shows a List/Grid selector; a click reloads with `?layout=Grid` and stores the choice in a per-list cookie. Resolution order is `AdminListContext.Layout`, then `?layout=`, then the cookie, then the site setting, then `AdminListOptions.DefaultLayout`. Invalid values are ignored. A list without columns falls back to `List` and shows no selector. A page rendering several lists (grouped by category) sets `ShowLayoutSelector = false` on each and creates one selector shape:

```csharp
model.LayoutSelector = await shapeFactory.CreateAsync(AdminListConstants.LayoutSelectorShapeType, Arguments.From(new
{
    ListName = "Features",
}));
```

## Alternates

| Shape | Alternates (least to most specific) |
|---|---|
| `AdminList` | `AdminList__{Layout}`, `AdminList__{Name}`, `AdminList__{Name}__{Layout}` |
| `AdminListCell` | `AdminListCell__{Column}`, `AdminListCell__{Name}__{Column}` |
| `AdminListActions` | `AdminListActions__{Layout}`, `AdminListActions__{Name}`, `AdminListActions__{Name}__{Layout}` (layout is `Buttons` or `Menu`) |
| `AdminListToolbar` | `AdminListToolbar__{Layout}`, `AdminListToolbar__{Name}`, `AdminListToolbar__{Name}__{Layout}` |
| `AdminListSearch` | `AdminListSearch__{Layout}`, `AdminListSearch__{Name}`, `AdminListSearch__{Name}__{Layout}` |

Examples are `AdminList-Contents-Grid.cshtml` and `AdminListActions-Menu.cshtml`. A name-only alternate such as `AdminList-Users.cshtml` applies to every layout of that list (opting it out of the setting); name the layout to change one mode. The list stamps `ListName` and `ListLayout` on its toolbar, search, pager, page size selector, and rows, so alternates resolve without the page passing them.

## Sortable Lists

Mark the rows container with `rows-*` attributes (e.g. `rows-id="rewrite-rules-sortable-list"`) and drag the rows, not their container: in `Grid` the container is `display: contents`. Use SortableJS `oldDraggableIndex`/`newDraggableIndex`, which count only rows. Force `Layout = AdminListLayout.List` only when the page cannot render a grid while sorting.

## Gotchas

1. **Never name a view model's rows `Items`**: `Shape` already exposes `Items` (its child shapes).
2. **An empty grid column** means a shape has no `SummaryAdminGrid` location for that column's zone.
3. **Per-type row templates only affect `List`**: change the grid with `SummaryAdminGrid` placement or `AdminListCell-{List}-{Column}.cshtml`.
4. **Never wrap `ActionsMenu` items in `<li>`**: the dropdown sits inside the row's `<li>` in the List layout, the HTML parser hoists the nested `<li>` out, and the menu renders empty with stray links under the list.
5. **No viewport classes such as `d-md-none` in row Razor**: the grid stacks through a container query on the list itself, because the admin sidebar makes the viewport a poor proxy for the list width.
6. **Rows are built while the view renders**, from the `AdminList` displaying event, so driver queries and exceptions surface at render time, not in the controller.

## Checking a List

Check every list page in both layouts and both actions layouts (Buttons and Menu), with rows and with none (empty message), and in a narrow window where the grid stacks its cells. In the Grid layout every declared column should render something on every row. Enable **Let users choose the layout of a list** while developing to switch quickly.

## Upgrading from 3.x

- Overriding a module's list view (e.g. `Views/Admin/Index.cshtml`) is a breaking change: an override that writes its own `<ul class="list-group">` keeps the old look and ignores the selected layout. Render the view model's `List` context with `<admin-list>` and declare columns with `<admin-list-column>`.
- Before, controllers built row shapes (`BuildDisplayAsync(item, updater, "SummaryAdmin")`) and views looped over them. Now controllers assign raw records to `AdminListContext<TModel>.Source` and the list builds rows.
- Built-in listing collections (`ListContentsViewModel.ContentItems`, `Users`, `Queries`, ...) now hold raw records, not row shapes or model/shape pairs; custom templates reading them must adapt. `ContentsAdminList.cshtml` renders through `ListContentsViewModel.List`.
- Add `SummaryAdminGrid` locations to existing row drivers, or their content is missing from the grid.
- Audit trail rows now have `Event`, `Category`, `Details`, and `Actions` columns; `AuditTrailEventTags_SummaryAdmin` was replaced by `AuditTrailEventName_SummaryAdmin` (`EventName` zone) and `AuditTrailEventCategory_SummaryAdmin` (`EventCategory` zone).

## Related Skills

- `orchardcore-admin` for admin controllers, routes, and settings pages.
- `orchardcore-admin-edit-views` for the create and edit pages a list links to.
- `orchardcore-breadcrumbs` for the page title trail; reuse the list name as the trail name.
- `orchardcore-navigation` for pager shapes and page size selection.
- `orchardcore-contents-admin-list-filters` and `orchardcore-users-admin-list-filters` for list filters.
