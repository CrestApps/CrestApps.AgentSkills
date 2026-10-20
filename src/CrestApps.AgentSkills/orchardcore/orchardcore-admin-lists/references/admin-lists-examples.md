# Admin List Examples

A complete module list page for `Announcement` records, modeled on the built-in Shortcodes and Indexes pages.

## Models and View Model

```csharp
public sealed class Announcement
{
    public long Id { get; set; }

    public string Title { get; set; }

    public bool IsActive { get; set; }

    public DateTime ModifiedUtc { get; set; }
}
```

```csharp
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

public class AnnouncementIndexViewModel
{
    public AnnouncementIndexOptions Options { get; set; } = new();

    // Rendered by the <admin-list> tag helper.
    public AdminListContext List { get; set; }
}

public sealed class AnnouncementIndexOptions
{
    public string Search { get; set; }

    public AnnouncementBulkAction BulkAction { get; set; }

    [BindNever]
    public List<SelectListItem> BulkActions { get; set; }
}

public enum AnnouncementBulkAction
{
    None,
    Remove,
}
```

## Controller

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Navigation;
using OrchardCore.Routing;

public sealed class AnnouncementController : Controller
{
    private const string OptionsSearch = "Options.Search";

    private readonly IAuthorizationService _authorizationService;
    private readonly IAnnouncementStore _store;
    private readonly IShapeFactory _shapeFactory;
    private readonly PagerOptions _pagerOptions;

    internal readonly IStringLocalizer S;

    public AnnouncementController(
        IAuthorizationService authorizationService,
        IAnnouncementStore store,
        IShapeFactory shapeFactory,
        IOptions<PagerOptions> pagerOptions,
        IStringLocalizer<AnnouncementController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _store = store;
        _shapeFactory = shapeFactory;
        _pagerOptions = pagerOptions.Value;
        S = stringLocalizer;
    }

    [Admin("Announcements", "Announcements.Index")]
    public async Task<IActionResult> Index(AnnouncementIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AnnouncementPermissions.ManageAnnouncements))
        {
            return Forbid();
        }

        // Honors the ?pageSize= selector when page size selection is enabled in the General settings.
        var pager = new Pager(pagerParameters, _pagerOptions);

        var (items, count) = await _store.ListAsync(options.Search, pager.GetStartIndex(), pager.PageSize);

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(OptionsSearch, options.Search);
        }

        options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(AnnouncementBulkAction.Remove)),
        ];

        var model = new AnnouncementIndexViewModel
        {
            Options = options,
            List = new AdminListContext<Announcement>
            {
                Source = items,
                Pager = await _shapeFactory.PagerAsync(pager, count, routeData),
            },
        };

        return View(model);
    }

    [HttpPost, ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    public ActionResult IndexFilterPost(AnnouncementIndexViewModel model)
        => RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { OptionsSearch, model.Options.Search },
        });

    [HttpPost, ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    public async Task<ActionResult> IndexBulkActionPost(AnnouncementIndexOptions options, IEnumerable<long> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AnnouncementPermissions.ManageAnnouncements))
        {
            return Forbid();
        }

        if (options.BulkAction == AnnouncementBulkAction.Remove)
        {
            await _store.DeleteAsync(itemIds);
        }

        return RedirectToAction(nameof(Index));
    }
}
```

## Row Display Driver and Registration

```csharp
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

public sealed class AnnouncementDisplayDriver : DisplayDriver<Announcement>
{
    public override Task<IDisplayResult> DisplayAsync(Announcement announcement, BuildDisplayContext context)
    {
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
            View("Announcement_Modified_SummaryAdmin", announcement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5"),
            View("Announcement_Buttons_SummaryAdmin", announcement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Actions:5"));
    }
}
```

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<Announcement, AnnouncementDisplayDriver>();
        services.AddNavigationProvider<AdminMenu>();
    }
}
```

## Views

`Views/_ViewImports.cshtml` (in addition to the usual Orchard Core imports):

```cshtml
@using OrchardCore.Admin
@addTagHelper *, OrchardCore.Admin.Abstractions
@addTagHelper *, OrchardCore.Navigation.Core
```

`Views/Announcement/Index.cshtml`:

```cshtml
@model AnnouncementIndexViewModel

<zone Name="Title">
    <breadcrumb name="Announcements" title="@T["Announcements"]" />
</zone>

<form asp-action="Index" method="post" class="no-multisubmit bulk-select-list"
      data-checkbox-name="itemIds" data-bulk-action-name="Options.BulkAction" data-selected-text="@T["selected"]">
    @* Keep the hidden filter submit first: Enter in the search box triggers it. *@
    <input type="submit" name="submit.Filter" id="submitFilter" class="visually-hidden" />
    <input asp-for="Options.BulkAction" type="hidden" />
    <input type="submit" name="submit.BulkAction" class="visually-hidden" />

    <div class="card text-bg-theme mb-3 position-sticky action-bar">
        <div class="card-body">
            <div class="row gx-2">
                <div class="col">
                    <div class="input-group">
                        <input asp-for="Options.Search" class="form-control" placeholder="@T["Search"]" type="search" autofocus autocomplete="off" />
                        <button type="submit" name="submit.Filter" class="btn btn-outline-secondary" title="@T["Search"]" aria-label="@T["Search"]">
                            <i class="fa-solid fa-magnifying-glass" aria-hidden="true"></i>
                        </button>
                    </div>
                </div>
                <div class="col-auto">
                    <a asp-action="Create" class="btn btn-secondary" role="button">@T["Add Announcement"]</a>
                </div>
            </div>
        </div>
    </div>

    <admin-list name="Announcements" context="Model.List"
                bulk-actions="Model.Options.BulkActions"
                empty-message="@T["<strong>Nothing here!</strong> There are no announcements yet."]">
        <admin-list-list item-css-class="list-group-item" />
        <admin-list-grid>
            <admin-list-column type="Select" />
            <admin-list-column name="Title" title="@T["Title"]" position="20" />
            <admin-list-column name="Status" title="@T["Status"]" position="30" width="auto" />
            <admin-list-column name="Modified" title="@T["Last modified"]" position="40" width="auto" no-wrap="true" />
            <admin-list-column type="Actions" title="@T["Actions"]" />
        </admin-list-grid>
    </admin-list>
</form>
```

`Views/Announcement.SummaryAdmin.cshtml` (List layout row):

```cshtml
@using OrchardCore.Admin

<div class="row g-0 align-items-center">
    <div class="col">
        <div class="title d-flex align-items-center">
            @if (Model.Checkbox != null)
            {
                <div class="selectors-container d-flex me-2">@await DisplayAsync(Model.Checkbox)</div>
            }
            <div class="summary d-flex flex-column flex-md-row list-item-search-text">
                <div class="me-2">@await DisplayAsync(Model.Content)</div>
                @if (Model.Tags != null)
                {
                    <div class="tags me-1">@await DisplayAsync(Model.Tags)</div>
                }
                @if (Model.Meta != null)
                {
                    <div class="metadata me-1">@await DisplayAsync(Model.Meta)</div>
                }
            </div>
        </div>
    </div>
    <div class="col-auto d-flex justify-content-end ps-2">
        @await DisplayAsync(await Factory.CreateAdminListActionsAsync((IShape)Model))
    </div>
</div>
```

`Views/Announcement.Checkbox.SummaryAdmin.cshtml`:

```cshtml
@model OrchardCore.DisplayManagement.Views.ShapeViewModel<Announcement>

<div class="form-check">
    <input type="checkbox" class="form-check-input" value="@Model.Value.Id" name="itemIds" id="itemIds-@Model.Value.Id">
    <label class="form-check-label" for="itemIds-@Model.Value.Id">&nbsp;</label>
</div>
```

`Views/Announcement.Fields.SummaryAdmin.cshtml`:

```cshtml
@model OrchardCore.DisplayManagement.Views.ShapeViewModel<Announcement>

<a asp-action="Edit" asp-controller="Announcement" asp-route-id="@Model.Value.Id">@Model.Value.Title</a>
```

`Views/Announcement.Status.SummaryAdmin.cshtml`:

```cshtml
@model OrchardCore.DisplayManagement.Views.ShapeViewModel<Announcement>

<span class="badge ta-badge fw-normal" title="@T["Status"]">
    <i class="fa-solid @(Model.Value.IsActive ? "fa-check text-success" : "fa-pause text-secondary")" aria-hidden="true"></i>
    @(Model.Value.IsActive ? T["Active"] : T["Inactive"])
</span>
```

`Views/Announcement.Buttons.SummaryAdmin.cshtml`:

```cshtml
@model OrchardCore.DisplayManagement.Views.ShapeViewModel<Announcement>

<a asp-action="Edit" asp-controller="Announcement" asp-route-id="@Model.Value.Id" class="btn btn-primary btn-sm">@T["Edit"]</a>
<a asp-action="Delete" asp-controller="Announcement" asp-route-id="@Model.Value.Id" class="btn btn-danger btn-sm" data-url-af="RemoveUrl UnsafeUrl">@T["Delete"]</a>
```

## Adding a Column to the Content Items List

Column provider plus a grid-only shape from a content display driver:

```csharp
public sealed class ReviewColumnsProvider : IAdminListColumnsProvider
{
    private readonly IStringLocalizer S;

    public ReviewColumnsProvider(IStringLocalizer<ReviewColumnsProvider> localizer)
    {
        S = localizer;
    }

    public Task PopulateAsync(AdminListColumnsContext context)
    {
        // A string[], empty when every type is listed.
        var types = context.GetData<string[]>(ContentsAdminList.ContentTypesKey, []);

        // Only when the list is filtered to articles.
        if (types.Contains("Article"))
        {
            context.Columns.Add(new AdminListColumn
            {
                Name = "Reviewer",
                Position = "35",
                Title = S["Reviewer"],
                Width = AdminListColumn.AutoWidth,
                NoWrap = true,
            });
        }

        return Task.CompletedTask;
    }
}

public sealed class ReviewerContentDisplayDriver : ContentDisplayDriver
{
    public override IDisplayResult Display(ContentItem contentItem, BuildDisplayContext context)
    {
        return Initialize<ReviewerViewModel>("ContentReviewer_SummaryAdmin", model =>
            model.Reviewer = contentItem.As<ReviewPart>()?.Reviewer)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Reviewer:10");
    }
}
```

```csharp
services.AddAdminListColumnsProvider<ReviewColumnsProvider>(ContentsAdminList.Name);
services.AddScoped<IContentDisplayDriver, ReviewerContentDisplayDriver>();
```

## Theme Override for One List

`Views/AdminListCell-Announcements-Status.cshtml` replaces only the Status cell of the grid. The shape carries the row as `Row` and the `AdminListColumn` as `Column`:

```cshtml
@using OrchardCore.DisplayManagement.Zones
@{
    var row = Model.Row as IZoneHolding;
}
@if (row != null && row.Zones.IsNotEmpty("Status"))
{
    <div class="text-center">
        @await DisplayAsync(row.Zones["Status"])
    </div>
}
```
