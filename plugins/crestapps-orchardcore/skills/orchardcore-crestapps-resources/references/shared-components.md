# Shared pickers, shapes and confirm buttons

The Resources feature ships these besides the resource manifest. All are used by name from any module that depends on `CrestApps.OrchardCore.Resources`.

## File drop input (`FileDropInput` shape)

A file picker that takes dropped, pasted or browsed files. Files add up across picks, duplicates are kept once, and each file is listed with its size and a remove button. Files stay in the form's own `<input type="file">`, so they post under `InputName` like a plain input; without script the plain input shows. The shape requires the `file-drop` script and style itself.

```cshtml
@await DisplayAsync(await New.FileDropInput(InputName: "files", InputId: "ticket-files", Accept: ".pdf,image/*", Target: "form"))
```

| Property | Purpose |
|---|---|
| `InputName` | Required. Name the files post under |
| `InputId` | Input id for `<label for>`; defaults to the name with dots replaced by underscores |
| `Multiple` | Default `true`; `false` replaces the chosen file |
| `Accept` | `accept` attribute; dropped or pasted files that do not match are left out with a message. Validate on the server too |
| `Compact` | One-line zone, for reply boxes |
| `Target` | CSS selector (resolved with `closest()`) of a surrounding area that also takes drops and pastes, for example `form` |
| `Text`, `Hint`, `CssClass`, `Disabled` | Display options |

The script raises `filedrop:change` on the input with `detail.files`; call `CrestAppsFileDrop.initializeAll(root)` for pickers added later.

## Party picker (`PartyPicker` view component)

Picks people or organizations of one or more party kinds (users, contacts, customers, accounts, staff, any kind with a registered `IPartySource`; see `orchardcore-teams` for parties). Values are party references `Kind:Id`.

```cshtml
@await Component.InvokeAsync("PartyPicker", new
{
    name = Html.NameFor(m => m.Requester).ToString(),
    selectedValues = new[] { Model.Requester },
    kinds = new[] { "User", "Contact" },
    multiple = false,
    required = true,
})
```

Parameters: `name` (required), `selectedValues`, `kinds` (none means every kind the user may search), `multiple`, `buttonText`, `smallButton`, `required`. It searches `GET Admin/api/crestapps/parties/search?kinds=&query=` (route `CrestApps.Parties.Search`), which keeps only kinds whose registered search permission the user holds (403 when none), returns up to 30 items split across kinds, and adds the kind name as `secondaryText` when several kinds are searched.

## Record picker (`RecordPicker` view component)

Picks records of one kind (orders, assets, tickets, content items, any kind with a registered `IRecordSource`). Renders nothing when no source has the kind.

```cshtml
@await Component.InvokeAsync("RecordPicker", new
{
    name = Html.NameFor(m => m.RelatedRecord).ToString(),
    kind = "Content",
    selectedValues = new[] { Model.RelatedRecord },
    valueType = "reference",
})
```

- `valueType`: `id` (default) stores the record id; `reference` stores `Kind:Id` (`RecordRef`) for a field that holds records of any kind.
- Endpoint `GET Admin/api/crestapps/records/search?kind=&query=&valueType=` (route `CrestApps.Records.Search`): 404 for an unknown kind, 403 when `IRecordSource.CanSearchAsync` refuses, up to 30 `{ value, text, secondaryText, isEnabled }` items.
- With `OrchardCore.Contents` enabled, the module registers `ContentItemRecordSource` for kind `Content` (`RecordKinds.ContentItem`), searchable with `ListContent`.

Add a record kind (contracts in `CrestApps.OrchardCore.Records`, assembly `CrestApps.OrchardCore.Abstractions`):

```csharp
using System.Security.Claims;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Records;
using OrchardCore.Localization;

namespace MyModule.Services;

public sealed class WarrantyRecordSource : IRecordSource
{
    private readonly IWarrantyStore _store;

    public WarrantyRecordSource(IWarrantyStore store)
    {
        _store = store;
    }

    public string Kind => "Warranty";

    [JsonIgnore]
    public LocalizationSource DisplayName => LocalizationSource.Create<WarrantyRecordSource>("Warranty");

    public Task<bool> CanSearchAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => _store.CanListAsync(user, cancellationToken);

    public Task<IReadOnlyList<RecordSummary>> SearchAsync(RecordSearch search, CancellationToken cancellationToken = default)
        => _store.SearchAsync(search.Text, search.Count, cancellationToken);

    public Task<IReadOnlyDictionary<string, RecordSummary>> GetSummariesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
        => _store.GetSummariesAsync(ids, cancellationToken);
}
```

Register with `services.AddRecordSource<WarrantyRecordSource>()` (it also registers `IRecordDirectory`). `RecordSummary` has `Id`, `Title`, `Description` and `AdminUrl`.

## Page sections (`PageSections` shape)

Renders the shapes of a record page's `Content` zone grouped into sections by their placement tab, as a side list on large screens and a scrolling row on small ones; the selected section is kept in the URL hash. The staff profile (see `orchardcore-staff`) and the account page use it.

- Register a section: `services.AddPageSection(pageName, name, LocalizationSource title, icon, position)` (namespace `CrestApps.OrchardCore.PageSections`). `PageSection.Title` is a `[JsonIgnore]` `LocalizationSource`.
- Place a shape in a section with `PageSectionPlacement.Location(section, position)` (`Content:{position}#{section}`).
- Shape properties: `Zone`, `Page`, `DefaultSection` (default `Overview`), `KeyPrefix` (default `section-`) and `PageId` (default `page-sections`).

## Templated email editor (`TemplatedEmailTemplatesEditor` shape)

The view for `TemplatedEmailTemplatesEditorViewModel` (`CrestApps.OrchardCore.Core.Emails`): an optional `Layout` (HTML with `{{ Content }}` where each email goes) and one fieldset per templated email. A module's settings display driver fills and reads the model; field names are relative to the driver's prefix.

## List management `minimumSelection`

`list-management-ui` shows bulk actions once `data-minimum-selection` items are checked (default `2`, since row actions cover one item). Set `data-minimum-selection="1"` on the list root when a bulk action has no row action, such as printing one check.

## Confirm buttons in the admin

The confirm dialog is Orchard Core TheAdmin's `window.confirmDialog`, not a CrestApps resource. Admin buttons that only had a confirm message now use it (CrestApps.OrchardCore #1340): render an anchor with `data-url-af="RemoveUrl UnsafeUrl"` and the dialog texts. After confirmation, `UnsafeUrl` POSTs the `href` (query string values become form fields) with the page's antiforgery token, so the target must be an `[HttpPost]` action and the page must contain a form with `__RequestVerificationToken`.

```cshtml
<div class="action-bar-bottom">
    <a class="btn btn-secondary cancel" role="button" asp-action="Index">@T["Cancel"]</a>
    <a class="btn btn-danger action-bar-secondary"
       href="@Url.Action("Void", new { id = Model.Id })"
       data-url-af="RemoveUrl UnsafeUrl"
       data-title="@T["Void the invoice"]"
       data-message="@T["Void this invoice? It can't be undone."]"
       data-ok-text="@T["Void"]"
       data-cancel-text="@T["Keep it"]">@T["Void"]</a>
    <button class="btn btn-primary" type="submit">@T["Save"]</button>
</div>
```

| Attribute | Purpose |
|---|---|
| `data-title`, `data-message` | Dialog heading and text (rendered as HTML; do not put unencoded user input here) |
| `data-ok-text`, `data-cancel-text` | Button labels |
| `data-ok-class`, `data-cancel-class` | Button classes, for example `btn-primary` for a non-destructive action |

`data-url-af="RemoveUrl"` alone confirms and then navigates to the `href` (GET), or, without an `href`, submits the closest form with the element's `name`/`value`. Order actions **Cancel** first, secondary actions (`action-bar-secondary`, collapsed into a **More actions** menu on small screens) next, and the main `btn-primary` action last.
