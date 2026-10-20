# Site Settings Examples

These examples use code-defined settings sections (plain classes stored in `ISite.Properties`). For admin-defined sections built from content types, see `orchardcore-custom-settings`.

## Example 1: Complete Settings Group with Its Own Permission

### Settings, Permissions, and View Model

```csharp
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace Contoso.SocialMedia;

public sealed class SocialMediaSettings
{
    public string FacebookUrl { get; set; }

    public string XHandle { get; set; }

    public string LinkedInUrl { get; set; }

    public bool ShowSocialLinks { get; set; }
}

public static class SocialMediaPermissions
{
    public static readonly Permission ManageSocialMediaSettings = new(
        "ManageSocialMediaSettings",
        LocalizationSource.Create("Manage social media settings", typeof(SocialMediaPermissions)));
}

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        SocialMediaPermissions.ManageSocialMediaSettings,
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

public class SocialMediaSettingsViewModel
{
    public string FacebookUrl { get; set; }

    public string XHandle { get; set; }

    public string LinkedInUrl { get; set; }

    public bool ShowSocialLinks { get; set; }
}
```

### Display Driver

```csharp
public sealed class SocialMediaSettingsDisplayDriver : SiteDisplayDriver<SocialMediaSettings>
{
    public const string GroupId = "social-media";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public SocialMediaSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, SocialMediaSettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, SocialMediaPermissions.ManageSocialMediaSettings))
        {
            return null;
        }

        return Initialize<SocialMediaSettingsViewModel, SocialMediaSettings>("SocialMediaSettings_Edit", static (model, settings) =>
        {
            model.FacebookUrl = settings.FacebookUrl;
            model.XHandle = settings.XHandle;
            model.LinkedInUrl = settings.LinkedInUrl;
            model.ShowSocialLinks = settings.ShowSocialLinks;
        }, settings).Location("Content:5")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, SocialMediaSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, SocialMediaPermissions.ManageSocialMediaSettings))
        {
            return null;
        }

        var model = new SocialMediaSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.FacebookUrl = model.FacebookUrl?.Trim();
        settings.XHandle = model.XHandle?.Trim();
        settings.LinkedInUrl = model.LinkedInUrl?.Trim();
        settings.ShowSocialLinks = model.ShowSocialLinks;

        return await EditAsync(site, settings, context);
    }
}
```

### Admin Menu

```csharp
public sealed class AdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary s_routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", SocialMediaSettingsDisplayDriver.GroupId },
    };

    internal readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Social Media"], S["Social Media"].PrefixPosition(), entry => entry
                    .Id("socialMediaSettings")
                    .Action("Index", "Admin", s_routeValues)
                    .Permission(SocialMediaPermissions.ManageSocialMediaSettings)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
```

### Startup

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<SocialMediaSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(SocialMediaSettingsDisplayDriver.GroupId, SocialMediaPermissions.ManageSocialMediaSettings);
        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();
    }
}

[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<SocialMediaSettings, DeploymentStartup>(
            S => S["Social media settings"],
            S => S["Exports the social media settings."]);
    }
}
```

### Razor View (`Views/SocialMediaSettings.Edit.cshtml`)

```html
@model SocialMediaSettingsViewModel

<div class="mb-3" asp-validation-class-for="FacebookUrl">
    <label asp-for="FacebookUrl" class="form-label">@T["Facebook URL"]</label>
    <input asp-for="FacebookUrl" class="form-control" placeholder="https://facebook.com/yourpage" />
    <span asp-validation-for="FacebookUrl"></span>
</div>

<div class="mb-3" asp-validation-class-for="XHandle">
    <label asp-for="XHandle" class="form-label">@T["X handle"]</label>
    <div class="input-group">
        <span class="input-group-text">@@</span>
        <input asp-for="XHandle" class="form-control" />
    </div>
    <span asp-validation-for="XHandle"></span>
</div>

<div class="mb-3" asp-validation-class-for="LinkedInUrl">
    <label asp-for="LinkedInUrl" class="form-label">@T["LinkedIn URL"]</label>
    <input asp-for="LinkedInUrl" class="form-control" />
    <span asp-validation-for="LinkedInUrl"></span>
</div>

<div class="mb-3">
    <div class="form-check">
        <input asp-for="ShowSocialLinks" class="form-check-input" />
        <label asp-for="ShowSocialLinks" class="form-check-label">@T["Display social media links on the site"]</label>
    </div>
</div>
```

The view has no inline script. If an editor needs one, use `<script at="Foot" type="module">` with native DOM APIs; the admin no longer loads jQuery.

### Reading in a View Component

```csharp
public sealed class SocialLinksViewComponent : ViewComponent
{
    private readonly ISiteService _siteService;

    public SocialLinksViewComponent(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        if (!site.TryGet<SocialMediaSettings>(out var settings) || !settings.ShowSocialLinks)
        {
            return Content(string.Empty);
        }

        return View(settings);
    }
}
```

---

## Example 2: Settings Validation

Add model errors in `UpdateAsync`; the settings controller does not save the site document when the model state is invalid.

```csharp
public sealed class ApiGatewaySettingsDisplayDriver : SiteDisplayDriver<ApiGatewaySettings>
{
    public const string GroupId = "api-gateway";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;

    internal readonly IStringLocalizer S;

    public ApiGatewaySettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IOptionsUpdateNotifier optionsUpdateNotifier,
        IStringLocalizer<ApiGatewaySettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _optionsUpdateNotifier = optionsUpdateNotifier;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, ApiGatewaySettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ApiGatewayPermissions.ManageApiGatewaySettings))
        {
            return null;
        }

        return Initialize<ApiGatewaySettingsViewModel, ApiGatewaySettings>("ApiGatewaySettings_Edit", static (model, settings) =>
        {
            model.Endpoint = settings.Endpoint;
            model.TimeoutSeconds = settings.TimeoutSeconds;
        }, settings).Location("Content:5")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ApiGatewaySettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ApiGatewayPermissions.ManageApiGatewaySettings))
        {
            return null;
        }

        var model = new ApiGatewaySettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["A valid absolute endpoint URL is required."]);
        }

        if (model.TimeoutSeconds is < 1 or > 300)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TimeoutSeconds), S["The timeout must be between 1 and 300 seconds."]);
        }

        if (context.Updater.ModelState.IsValid)
        {
            if (settings.Endpoint != model.Endpoint || settings.TimeoutSeconds != model.TimeoutSeconds)
            {
                _optionsUpdateNotifier.RequestUpdate<ApiGatewayOptions>();
            }

            settings.Endpoint = model.Endpoint;
            settings.TimeoutSeconds = model.TimeoutSeconds;
        }

        return await EditAsync(site, settings, context);
    }
}

public sealed class ApiGatewaySettings
{
    public string Endpoint { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}

public class ApiGatewaySettingsViewModel
{
    public string Endpoint { get; set; }

    public int TimeoutSeconds { get; set; }
}

public static class ApiGatewayPermissions
{
    public static readonly Permission ManageApiGatewaySettings = new(
        "ManageApiGatewaySettings",
        LocalizationSource.Create("Manage API gateway settings", typeof(ApiGatewayPermissions)));
}
```

`ApiGatewayOptions` is registered with `AddSignalOptionsChangeTokenSource<ApiGatewayOptions>()` and an `IConfigureOptions<ApiGatewayOptions>` that reads `ISiteService.GetSettings<ApiGatewaySettings>()`, as shown in `SKILL.md`.

---

## Example 3: Reading Settings in a Middleware

```csharp
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISiteService siteService)
    {
        var site = await siteService.GetSiteSettingsAsync();

        if (site.TryGet<SecurityHeaderSettings>(out var settings) &&
            !string.IsNullOrEmpty(settings.ContentSecurityPolicy))
        {
            context.Response.Headers.ContentSecurityPolicy = settings.ContentSecurityPolicy;
        }

        await _next(context);
    }
}

public sealed class SecurityHeaderSettings
{
    public string ContentSecurityPolicy { get; set; }
}
```

`GetSiteSettingsAsync()` is correct for read-only access; switch to `LoadSiteSettingsAsync()` only when you mutate and save the document.

---

## Example 4: Recipes

### Built-In Properties and a Code-Defined Section

```json
{
  "steps": [
    {
      "name": "Settings",
      "SiteName": "Contoso Corporate Portal",
      "BaseUrl": "https://www.contoso.com",
      "TimeZoneId": "Europe/London",
      "PageSize": 15,
      "MaxPageSize": 100,
      "AllowPageSizeSelection": true,
      "PageSizeOptions": [ 15, 30, 60 ],
      "UseCdn": false,
      "SocialMediaSettings": {
        "FacebookUrl": "https://facebook.com/contoso",
        "XHandle": "contoso",
        "LinkedInUrl": "https://linkedin.com/company/contoso",
        "ShowSocialLinks": true
      }
    }
  ]
}
```

Keys that are not built-in `ISite` properties are copied as-is into `ISite.Properties`, so the section name must match the settings class name read with `TryGet<T>()`.

### Allowing a Section in Liquid

```json
{
  "OrchardCore": {
    "Liquid": {
      "PermittedSiteProperties": [ "SocialMediaSettings" ]
    }
  }
}
```

```liquid
{% assign social = Site.Properties.SocialMediaSettings %}
{% if social.ShowSocialLinks %}
  <a href="https://x.com/{{ social.XHandle }}">X</a>
{% endif %}
```

---

## Example 5: Razor View Injection

```html
@using OrchardCore.Settings
@inject ISiteService SiteService

@{
    var site = await SiteService.GetSiteSettingsAsync();
    var hasSocialSettings = site.TryGet<SocialMediaSettings>(out var socialSettings);
}

<footer>
    <p>&copy; @DateTime.UtcNow.Year @site.SiteName</p>

    @if (hasSocialSettings && socialSettings.ShowSocialLinks)
    {
        <nav aria-label="@T["Social media links"]">
            <ul class="list-inline">
                @if (!string.IsNullOrEmpty(socialSettings.FacebookUrl))
                {
                    <li class="list-inline-item"><a href="@socialSettings.FacebookUrl" target="_blank" rel="noopener">Facebook</a></li>
                }
                @if (!string.IsNullOrEmpty(socialSettings.LinkedInUrl))
                {
                    <li class="list-inline-item"><a href="@socialSettings.LinkedInUrl" target="_blank" rel="noopener">LinkedIn</a></li>
                }
            </ul>
        </nav>
    }
</footer>
```
