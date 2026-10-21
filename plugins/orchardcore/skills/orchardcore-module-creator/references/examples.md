# Module Examples Reference

## Example: Simple Settings Module

A module that adds site-wide settings.

### Manifest.cs
```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Site Settings",
    Author = "Contoso",
    Website = "https://contoso.com",
    Version = "1.0.0"
)]

[assembly: Feature(
    Id = "Contoso.SiteSettings",
    Name = "Site Settings",
    Description = "Provides custom site settings.",
    Category = "Configuration"
)]
```

### Models/SiteSettings.cs
```csharp
namespace Contoso.SiteSettings.Models;

public sealed class SiteSettingsModel
{
    public string ApiKey { get; set; }
    public bool EnableFeature { get; set; }
    public int MaxItems { get; set; } = 10;
}
```

### Startup.cs
```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using Contoso.SiteSettings.Drivers;
using Contoso.SiteSettings.Navigation;

namespace Contoso.SiteSettings;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<SiteSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(SiteSettingsDisplayDriver.GroupId, SiteSettingsPermissions.ManageSiteSettings);
        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();
    }
}
```

The driver derives from `SiteDisplayDriver<SiteSettingsModel>`, checks `ManageSiteSettings` in `EditAsync` and `UpdateAsync`, and calls `IOptionsUpdateNotifier.RequestUpdate<TOptions>()` when options are built from these settings. See `orchardcore-site-settings` for the full driver, view, menu and options code.

---

## Example: Content Part Module

A module that adds a "Rating" part to content items. `dotnet new ocmodulecms -n Contoso.Rating --AddPart true --PartName Rating` generates the same skeleton (part, driver, handler, settings, migration, views); the driver follows the pattern in `patterns.md`.

### Models/RatingPart.cs
```csharp
using OrchardCore.ContentManagement;

namespace Contoso.Rating.Models;

public sealed class RatingPart : ContentPart
{
    public int Stars { get; set; }
    public int ReviewCount { get; set; }
    public decimal AverageRating { get; set; }
}
```

### ViewModels/RatingPartViewModel.cs
```csharp
namespace Contoso.Rating.ViewModels;

public class RatingPartViewModel
{
    public int Stars { get; set; }
    public int ReviewCount { get; set; }
    public decimal AverageRating { get; set; }
}
```

### Views/RatingPart.cshtml
```html
@model Contoso.Rating.ViewModels.RatingPartViewModel

<div class="rating-display">
    <span class="stars">@Model.Stars / 5</span>
    <span class="review-count">(@Model.ReviewCount reviews)</span>
    <span class="average">Average: @Model.AverageRating.ToString("F1")</span>
</div>
```

### Views/RatingPart_Edit.cshtml
```html
@model Contoso.Rating.ViewModels.RatingPartViewModel

<div class="ocat-wrapper" asp-validation-class-for="Stars">
    <label asp-for="Stars" class="ocat-label">Stars (1-5)</label>
    <div class="ocat-end">
        <input asp-for="Stars" type="number" min="1" max="5" class="form-control" />
    </div>
</div>

<div class="ocat-wrapper" asp-validation-class-for="ReviewCount">
    <label asp-for="ReviewCount" class="ocat-label">Review Count</label>
    <div class="ocat-end">
        <input asp-for="ReviewCount" type="number" min="0" class="form-control" />
    </div>
</div>
```

---

## Example: Admin Controller Module

A module with admin pages.

### Controllers/AdminController.cs
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;

namespace Contoso.YourModule.Controllers;

[Admin("YourModule/{action}/{id?}", "YourModule.{action}")]
public sealed class AdminController : Controller
{
    private readonly IAuthorizationService _authorizationService;

    public AdminController(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, YourModulePermissions.ManageYourFeature))
        {
            return Forbid();
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(YourViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, YourModulePermissions.ManageYourFeature))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Create logic here
        
        return RedirectToAction(nameof(Index));
    }
}
```

### Views/Admin/Index.cshtml
```html
<zone Name="Title">
    <breadcrumb name="YourModule" title="@T["Your Module"]" />
</zone>

<div class="card">
    <div class="card-body">
        <p>@T["Your module content here."]</p>
        <a asp-action="Create" class="btn btn-primary">@T["Create New"]</a>
    </div>
</div>
```

### Views/Admin/Create.cshtml
```html
@model YourViewModel

<zone Name="Title">
    <breadcrumb name="YourModuleCreate" title="@T["New item"]">
        <breadcrumb-item id="YourModule" action="Index" controller="Admin" area="Contoso.YourModule" permission="@YourModulePermissions.ManageYourFeature.Name">@T["Your Module"]</breadcrumb-item>
    </breadcrumb>
</zone>

<form asp-action="Create" method="post" class="no-multisubmit">
    @Html.ValidationSummary()
    @* fields *@
    <button type="submit" class="btn btn-primary">@T["Save"]</button>
</form>
```

The `<breadcrumb>` tag helper renders the trail and the page `<h1>`, and adds the title to the browser `<title>`; do not add your own `<h1>`. See `orchardcore-breadcrumbs`.

---

## Example: API Controller Module

A module exposing REST API endpoints with an MVC controller. Prefer Minimal API endpoints (see `SKILL.md`) for new JSON endpoints. With the `Api` scheme, 401/403 responses carry an RFC 9457 Problem Details body.

### Controllers/ApiController.cs
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.ContentManagement;

namespace Contoso.YourModule.Controllers;

[Route("api/yourmodule")]
[ApiController]
[Authorize(AuthenticationSchemes = "Api")]
[IgnoreAntiforgeryToken]
public sealed class ApiController : ControllerBase
{
    private readonly IContentManager _contentManager;

    public ApiController(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var contentItem = await _contentManager.GetAsync(id);
        
        if (contentItem == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Content item not found.");
        }

        return Ok(contentItem);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request)
    {
        var contentItem = await _contentManager.NewAsync(request.ContentType);
        contentItem.DisplayText = request.Title;
        
        await _contentManager.CreateAsync(contentItem, VersionOptions.Published);
        
        return CreatedAtAction(nameof(Get), new { id = contentItem.ContentItemId }, contentItem);
    }
}

public sealed record CreateRequest(string ContentType, string Title);
```

---

## Frontend Assets

### Assets.json
```json
[
  {
    "action": "vite",
    "name": "your-module",
    "source": "Assets/",
    "tags": ["js", "css"]
  }
]
```

### Assets/package.json
```json
{
  "name": "@contoso/your-module",
  "version": "1.0.0",
  "private": true,
  "dependencies": {
    "vue": "3.5.13"
  }
}
```

### Assets/js/main.js
```javascript
// Loaded with <script asp-name="your-module" at="Foot" type="module">: runs once parsed,
// no jQuery and no DOMContentLoaded wrapper needed.
for (const element of document.querySelectorAll('[data-your-module]')) {
    element.addEventListener('click', () => element.classList.toggle('active'));
}
```

### Assets/scss/styles.scss
```scss
.your-module {
    &__container {
        padding: 1rem;
    }
    
    &__title {
        font-size: 1.5rem;
        font-weight: bold;
    }
}
```
