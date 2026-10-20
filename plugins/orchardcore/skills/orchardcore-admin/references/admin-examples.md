# Admin Panel Practical Examples

This reference provides additional hands-on examples for working with the Orchard Core admin panel.

## Complete Admin Controller with CRUD Operations

This example shows a full admin controller for managing a custom entity with list, create, edit, and delete actions:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using YesSql;

[Admin("Announcements/{action}/{id?}", "Announcements")]
public sealed class AnnouncementController : Controller
{
    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IHtmlLocalizer H;

    public AnnouncementController(
        ISession session,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<AnnouncementController> htmlLocalizer)
    {
        _session = session;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AnnouncementPermissions.ManageAnnouncements))
        {
            return Forbid();
        }

        var announcements = await _session.Query<Announcement>().ListAsync();

        var model = new AnnouncementIndexViewModel
        {
            // Rendered by the <admin-list> tag helper in the configured List or Grid layout.
            List = new AdminListContext<Announcement>
            {
                Source = announcements,
            },
        };

        return View(model);
    }

    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AnnouncementPermissions.ManageAnnouncements))
        {
            return Forbid();
        }

        return View(new AnnouncementEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AnnouncementEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AnnouncementPermissions.ManageAnnouncements))
        {
            return Forbid();
        }

        if (ModelState.IsValid)
        {
            var announcement = new Announcement
            {
                Title = model.Title,
                Message = model.Message,
                IsActive = model.IsActive,
            };

            await _session.SaveAsync(announcement);
            await _notifier.SuccessAsync(H["Announcement created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AnnouncementPermissions.ManageAnnouncements))
        {
            return Forbid();
        }

        var announcement = await _session.GetAsync<Announcement>(long.Parse(id));

        if (announcement == null)
        {
            return NotFound();
        }

        var model = new AnnouncementEditViewModel
        {
            Title = announcement.Title,
            Message = announcement.Message,
            IsActive = announcement.IsActive,
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AnnouncementPermissions.ManageAnnouncements))
        {
            return Forbid();
        }

        var announcement = await _session.GetAsync<Announcement>(long.Parse(id));

        if (announcement == null)
        {
            return NotFound();
        }

        _session.Delete(announcement);
        await _notifier.SuccessAsync(H["Announcement deleted successfully."]);

        return RedirectToAction(nameof(Index));
    }
}
```

## Permissions

```csharp
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

public static class AnnouncementPermissions
{
    public static readonly Permission ManageAnnouncements = new("ManageAnnouncements", LocalizationSource.Create("Manage announcements", typeof(AnnouncementPermissions)));

    public static readonly Permission ManageAnnouncementSettings = new("ManageAnnouncementSettings", LocalizationSource.Create("Manage announcement settings", typeof(AnnouncementPermissions)));
}

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        AnnouncementPermissions.ManageAnnouncements,
        AnnouncementPermissions.ManageAnnouncementSettings,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
```

## Admin Index View

List pages render their items with the `AdminList` shape so the site's List/Grid layout applies. The controller assigns `AdminListContext<Announcement>` to the view model's `List` property, and `Views/Announcement/Index.cshtml` renders it:

```cshtml
@model AnnouncementIndexViewModel

<zone Name="Title">
    <breadcrumb name="Announcements" title="@T["Announcements"]" />
</zone>

<form asp-action="Index" method="post">
    <admin-list name="Announcements" context="Model.List" show-select-all="false"
                empty-message="@T["No announcements found."]">
        <admin-list-list item-css-class="list-group-item" />
        <admin-list-grid>
            <admin-list-column name="Title" title="@T["Title"]" position="20" />
            <admin-list-column name="Status" title="@T["Status"]" position="30" width="auto" />
            <admin-list-column type="Actions" title="@T["Actions"]" />
        </admin-list-grid>
    </admin-list>
</form>
```

See `orchardcore-admin-lists` for the row display driver, row templates, search, and bulk actions.

## Admin Edit View with a Breadcrumb

`Views/Announcement/Edit.cshtml`:

```cshtml
@model AnnouncementEditViewModel

<zone Name="Title">
    <breadcrumb name="AnnouncementsEdit" title="@T["Edit {0}", Model.Title]">
        <breadcrumb-item id="Announcements" action="Index" controller="Announcement" area="MyModule"
                         permission="@AnnouncementPermissions.ManageAnnouncements.Name">@T["Announcements"]</breadcrumb-item>
    </breadcrumb>
</zone>

<form asp-action="Edit" method="post">
    <div class="ocat-wrapper" asp-validation-class-for="Title">
        <label asp-for="Title" class="ocat-label ocat-label-required">@T["Title"]</label>
        <div class="ocat-end">
            <input asp-for="Title" class="form-control" />
            <span asp-validation-for="Title"></span>
        </div>
    </div>

    <div class="ocat-wrapper">
        <div class="ocat-end-offset">
            <div class="form-check">
                <input type="checkbox" class="form-check-input" asp-for="IsActive" />
                <label class="form-check-label" asp-for="IsActive">@T["Active"]</label>
            </div>
        </div>
    </div>

    <div class="ocat-wrapper">
        <div class="ocat-end-offset">
            <button type="submit" class="btn btn-primary">@T["Save"]</button>
            <a asp-action="Index" class="btn btn-secondary">@T["Cancel"]</a>
        </div>
    </div>
</form>
```

## Hierarchical Admin Menu with Multiple Sections

This example demonstrates a navigation provider that registers items across multiple top-level groups with explicit ordering:

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

public sealed class AdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> localizer)
    {
        S = localizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Content"], NavigationConstants.AdminMenuContentPosition, content => content
                .Add(S["Announcements"], S["Announcements"].PrefixPosition(), announcements => announcements
                    .Permission(AnnouncementPermissions.ManageAnnouncements)
                    .Action("Index", "Announcement", new { area = "MyModule" })
                    .LocalNav()
                )
            )
            .Add(S["Settings"], settings => settings
                .Add(S["Announcement Defaults"], S["Announcement Defaults"].PrefixPosition(), defaults => defaults
                    .Permission(AnnouncementPermissions.ManageAnnouncementSettings)
                    .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = "announcements" })
                    .LocalNav()
                )
            )
            .Add(S["Tools"], tools => tools
                .Add(S["Announcement Import"], S["Announcement Import"].PrefixPosition(), import => import
                    .Permission(AnnouncementPermissions.ManageAnnouncements)
                    .Action("Import", "Announcement", new { area = "MyModule" })
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
```

## Multi-Section Settings Page with Display Driver

This example shows how to build a tabbed settings page using the `Location` syntax with tab notation:

```csharp
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

public sealed class EmailDeliverySettingsDisplayDriver : SiteDisplayDriver<EmailDeliverySettings>
{
    public const string GroupId = "email-delivery";

    protected override string SettingsGroupId => GroupId;

    public override IDisplayResult Edit(ISite site, EmailDeliverySettings settings, BuildEditorContext context)
    {
        return Combine(
            Initialize<EmailDeliverySettingsViewModel>("EmailDeliverySettings_Edit__General", model =>
            {
                model.SenderAddress = settings.SenderAddress;
                model.SenderName = settings.SenderName;
            }).Location("Content:1#General")
              .OnGroup(SettingsGroupId),

            Initialize<EmailDeliverySettingsViewModel>("EmailDeliverySettings_Edit__Retry", model =>
            {
                model.MaxRetries = settings.MaxRetries;
                model.RetryDelaySeconds = settings.RetryDelaySeconds;
            }).Location("Content:2#Retry Policy")
              .OnGroup(SettingsGroupId)
        );
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, EmailDeliverySettings settings, UpdateEditorContext context)
    {
        var model = new EmailDeliverySettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.SenderAddress = model.SenderAddress;
        settings.SenderName = model.SenderName;
        settings.MaxRetries = model.MaxRetries;
        settings.RetryDelaySeconds = model.RetryDelaySeconds;

        return Edit(site, settings, context);
    }
}
```

## Custom Dashboard Widget with Data

This example shows a dashboard widget that queries data and displays it in a card on the admin dashboard:

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

public sealed class ContentStatsPart : ContentPart
{
}

public sealed class ContentStatsPartDisplayDriver : ContentPartDisplayDriver<ContentStatsPart>
{
    private readonly IContentStatisticsService _statisticsService;

    public ContentStatsPartDisplayDriver(IContentStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    public override IDisplayResult Display(ContentStatsPart part, BuildPartDisplayContext context)
    {
        return Initialize<ContentStatsViewModel>("ContentStatsDashboard", async viewModel =>
        {
            var stats = await _statisticsService.GetStatisticsAsync();
            viewModel.TotalPublished = stats.TotalPublished;
            viewModel.TotalDraft = stats.TotalDraft;
            viewModel.RecentlyModified = stats.RecentlyModified;
        }).Location("DetailAdmin", "Content:2.5");
    }
}
```

Dashboard widget view `Views/ContentStatsDashboard.cshtml`:

```html
@model ContentStatsViewModel

<div class="col-sm-6 col-lg-3">
    <div class="card text-bg-light mb-3">
        <div class="card-header">
            <h5 class="card-title mb-0">@T["Content Overview"]</h5>
        </div>
        <div class="card-body">
            <div class="row text-center">
                <div class="col">
                    <h3 class="mb-0">@Model.TotalPublished</h3>
                    <small class="text-muted">@T["Published"]</small>
                </div>
                <div class="col">
                    <h3 class="mb-0">@Model.TotalDraft</h3>
                    <small class="text-muted">@T["Drafts"]</small>
                </div>
                <div class="col">
                    <h3 class="mb-0">@Model.RecentlyModified</h3>
                    <small class="text-muted">@T["Modified Today"]</small>
                </div>
            </div>
        </div>
    </div>
</div>
```

## Admin Result Filter for Injecting Layout Content

This example injects a maintenance notification banner into the admin `Messages` zone when a flag is active:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Layout;
using OrchardCore.Settings;

public sealed class MaintenanceBannerAdminFilter : IAsyncResultFilter
{
    private readonly ILayoutAccessor _layoutAccessor;
    private readonly IShapeFactory _shapeFactory;
    private readonly ISiteService _siteService;

    public MaintenanceBannerAdminFilter(
        ILayoutAccessor layoutAccessor,
        IShapeFactory shapeFactory,
        ISiteService siteService)
    {
        _layoutAccessor = layoutAccessor;
        _shapeFactory = shapeFactory;
        _siteService = siteService;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is not ViewResult || !AdminAttribute.IsApplied(context.HttpContext))
        {
            await next();
            return;
        }

        var settings = await _siteService.GetSettingsAsync<MaintenanceSettings>();

        if (settings.IsMaintenanceScheduled)
        {
            var layout = await _layoutAccessor.GetLayoutAsync();
            var messagesZone = layout.Zones["Messages"];

            var banner = await _shapeFactory.CreateAsync("MaintenanceBanner", new
            {
                ScheduledDate = settings.ScheduledDate,
            });

            await messagesZone.AddAsync(banner, "0");
        }

        await next();
    }
}
```

## Complete Startup Registration Example

This ties all admin components together in a single module startup:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

[RequireFeatures("OrchardCore.Admin")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddDisplayDriver<Announcement, AnnouncementDisplayDriver>();
        services.AddSiteDisplayDriver<EmailDeliverySettingsDisplayDriver>();
        services.AddContentPart<ContentStatsPart>()
            .UseDisplayDriver<ContentStatsPartDisplayDriver>();
        services.Configure<MvcOptions>(options => options.Filters.Add<MaintenanceBannerAdminFilter>());
    }
}
```

## Recipe: Configure Admin Settings at Setup

Use the following recipe to configure admin panel preferences when provisioning a new tenant:

```json
{
    "steps": [
        {
            "name": "settings",
            "AdminSettings": {
                "DisplayMenuFilter": true,
                "DisplayThemeToggler": true,
                "DisplayQuickNavigation": true,
                "ShowBreadcrumb": true,
                "MenuBehavior": "Persistent",
                "ListLayout": "Grid"
            }
        }
    ]
}
```

## Recipe: Enable Admin Features

Enable admin features as part of site setup (`OrchardCore.Settings` and `OrchardCore.Navigation` are always enabled):

```json
{
    "steps": [
        {
            "name": "feature",
            "enable": [
                "OrchardCore.Admin",
                "OrchardCore.AdminDashboard",
                "OrchardCore.Contents.QuickNavigation"
            ]
        }
    ]
}
```
