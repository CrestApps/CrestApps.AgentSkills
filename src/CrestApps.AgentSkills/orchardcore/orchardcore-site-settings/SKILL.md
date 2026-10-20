---
name: orchardcore-site-settings
description: Skill for accessing and extending Orchard Core site settings. Covers ISiteService, ISite built-in properties, General settings with page size selection, code-defined settings groups with SiteDisplayDriver, per-group permissions with AddSiteSettingsPermission and SettingsPermissions, options built from site settings that refresh without a shell release through IOptionsMonitor and IOptionsUpdateNotifier, tenant reload with IShellReleaseManager, Liquid Site accessor and PermittedSiteProperties, the Settings recipe step, and deployment steps. Use this skill when requests mention Orchard Core Site Settings, Reading or Updating Site Settings, Creating a Settings Group, settings-backed options, Page Size Selection, or closely related Orchard Core implementation, upgrade, or troubleshooting work. Strong matches include OrchardCore.Settings, ISiteService, SiteDisplayDriver, ManageGeneralSettings, AddSiteSettingsPermission, AddSignalOptionsChangeTokenSource, PagerOptions, SettingsLiquidOptions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Site Settings - Prompt Templates

## Access and Extend Site Settings

You are an Orchard Core expert. Generate code and configuration for reading site settings, adding code-defined settings groups to the admin, and feeding options from site settings.

### Guidelines

- `OrchardCore.Settings` is always enabled. Site settings are a single per-tenant document (`ISite`); modules add their own sections to its `Properties` bag.
- Use `ISiteService.GetSiteSettingsAsync()` (cached, read-only) to read; use `LoadSiteSettingsAsync()` before modifying and saving with `UpdateSiteSettingsAsync()`.
- Read a section with `site.TryGet<TSettings>(out var settings)` or `site.GetOrCreate<TSettings>()`; `site.As<T>()` is obsolete. `ISiteService.GetSettingsAsync<T>()` is a shortcut.
- A code-defined settings group is a plain POCO class (not a `ContentPart`) edited by a `SiteDisplayDriver<TSettings>` with a unique `SettingsGroupId`.
- Register a permission for the group with `AddSiteSettingsPermission(groupId, permission)` **and** check that permission in both `EditAsync` and `UpdateAsync` of the driver.
- When services consume the settings through options, prefer the 4.0 `IOptionsMonitor<TOptions>` refresh pipeline over a shell release: register `AddSignalOptionsChangeTokenSource<TOptions>()`, call `IOptionsUpdateNotifier.RequestUpdate<TOptions>()` from the driver, and read `IOptionsMonitor<TOptions>.CurrentValue`.
- Use `IShellReleaseManager.RequestRelease()` plus `context.AddTenantReloadWarningWrapper()` only when the change affects service registration, middleware, or other state that is built once per shell. Do not gate `RequestRelease()` on `ModelState.IsValid`; the settings controller only releases after a successful save.
- Admin-defined (no code) settings sections use the `CustomSettings` stereotype; see `orchardcore-custom-settings`.
- Settings stored in `Site.Properties` are hidden from Liquid unless listed in `OrchardCore:Liquid:PermittedSiteProperties`.
- Permission descriptions are `LocalizationSource` values; create them with `LocalizationSource.Create(value, typeof(DeclaringType))`.
- All C# classes must use the `sealed` modifier, except view models.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.

### Core Services

| Service | Purpose |
|---------|---------|
| `ISiteService` | Read (`GetSiteSettingsAsync`) and write (`LoadSiteSettingsAsync`, `UpdateSiteSettingsAsync`) the site settings document. |
| `ISite` | Site settings object. `TryGet<T>(out ...)` / `GetOrCreate<T>()` read sections from `Properties`. |
| `SiteDisplayDriver<TSettings>` | Renders and updates one settings section in a settings group. |
| `AddSiteSettingsPermission(groupId, permission)` | Lets the shared settings controller authorize a group with its own permission. |
| `IOptionsUpdateNotifier` | Requests an `IOptionsMonitor<TOptions>` refresh after the settings commit. |
| `IShellReleaseManager` | Requests a tenant reload after a shell-affecting settings change. |
| `INavigationProvider` / `AdminNavigationProvider` | Adds the settings page to the admin menu. |

### Built-In Site Properties

| Property | Type | Description |
|----------|------|-------------|
| `SiteName` | `string` | Display name of the site. |
| `PageTitleFormat` | `string` | Liquid format of the page `<title>`. |
| `BaseUrl` | `string` | Absolute base URL used when an absolute URL must be generated. |
| `TimeZoneId` | `string` | Default time zone identifier (e.g. `America/New_York`). |
| `Calendar` | `string` | Default calendar system. |
| `PageSize` | `int` | Default number of items per page. |
| `MaxPageSize` | `int` | Maximum page size a client can request. |
| `MaxPagedCount` | `int` | Maximum number of items that can be paged through. |
| `AllowPageSizeSelection` | `bool` | Shows an **Items per page** selector next to pagers (4.0). |
| `PageSizeOptions` | `int[]` | Allowed page sizes, default `10, 25, 50, 100` (4.0). |
| `UseCdn` / `CdnBaseUrl` | `bool` / `string` | Serve framework assets from a CDN. |
| `ResourceDebugMode` | `ResourceDebugMode` | Serve non-minified resources. |
| `AppendVersion` | `bool` | Append a version token to static asset URLs. |
| `CacheMode` | `CacheMode` | Default resource caching behavior. |
| `HomeRoute` | `RouteValueDictionary` | Route of the home page. |
| `SuperUser`, `SiteSalt` | `string` | Internal; never expose them. |

There is no `Culture` property on `ISite`; cultures are managed by the Localization module.

### Reading Site Settings

```csharp
public sealed class MyService
{
    private readonly ISiteService _siteService;

    public MyService(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public async Task<string> GetSiteNameAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        return site.SiteName;
    }

    public async Task<SocialMediaSettings> GetSocialMediaSettingsAsync()
        => await _siteService.GetSettingsAsync<SocialMediaSettings>();
}
```

`ISiteService.GetSiteSettings()` and `GetSettings<T>()` are synchronous variants for code paths that cannot be async, such as `IConfigureOptions<T>.Configure()`; site settings are preloaded when the tenant activates.

### Updating Site Settings

```csharp
public async Task UpdateBaseUrlAsync(string newBaseUrl)
{
    var site = await _siteService.LoadSiteSettingsAsync();
    site.BaseUrl = newBaseUrl;
    await _siteService.UpdateSiteSettingsAsync(site);
}
```

### General Settings and Page Size Selection

The **General** group (`/Admin/Settings/general`) requires `ManageGeneralSettings`; the **Debugging** group requires `ManageDebuggingSettings`. `ManageSettings` implies both.

With **Page size selection** enabled, the shared `Pager` and `PagerSlim` shapes render an **Items per page** selector (`Pager_PageSizeSelector` shape, receives `PageSizes` and `CurrentPageSize`) on admin and front-end lists. The value travels as the `pageSize` query string and is honored only when it is one of `PageSizeOptions`. Custom listings opt into the same validation through `PagerOptions`:

```csharp
public sealed class ProductsController : Controller
{
    private readonly PagerOptions _pagerOptions;

    public ProductsController(IOptions<PagerOptions> pagerOptions)
    {
        _pagerOptions = pagerOptions.Value;
    }

    public IActionResult Index(PagerParameters pagerParameters)
    {
        // Applies AllowPageSizeSelection and PageSizeOptions, otherwise falls back to the site page size.
        var pager = new Pager(pagerParameters, _pagerOptions);

        // ... query with pager.GetStartIndex() and pager.PageSize
        return View();
    }
}
```

`PagerOptions.GetPageSize(int? selectedPageSize, int defaultPageSize)` validates a requested size against a list's own default, and `PagerSlimParameters` carries an optional `PageSize`.

### Creating a Settings Group

#### Step 1: Settings Class and Permissions

```csharp
public sealed class SocialMediaSettings
{
    public string FacebookUrl { get; set; }

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
```

#### Step 2: Display Driver

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
        settings.ShowSocialLinks = model.ShowSocialLinks;

        return await EditAsync(site, settings, context);
    }
}

public class SocialMediaSettingsViewModel
{
    public string FacebookUrl { get; set; }

    public bool ShowSocialLinks { get; set; }
}
```

The view model must not be `sealed` (proxy-based shapes, see analyzer `OCSG001` in `orchardcore-source-generators`).

#### Step 3: Razor View (`Views/SocialMediaSettings.Edit.cshtml`)

```html
@model SocialMediaSettingsViewModel

<div class="mb-3" asp-validation-class-for="FacebookUrl">
    <label asp-for="FacebookUrl" class="form-label">@T["Facebook URL"]</label>
    <input asp-for="FacebookUrl" class="form-control" />
    <span asp-validation-for="FacebookUrl"></span>
</div>

<div class="mb-3">
    <div class="form-check">
        <input asp-for="ShowSocialLinks" class="form-check-input" />
        <label asp-for="ShowSocialLinks" class="form-check-label">@T["Display social media links"]</label>
    </div>
</div>
```

#### Step 4: Admin Menu

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

The `groupId` route value must match `SettingsGroupId`; the built-in settings controller serves `/Admin/Settings/{groupId}`.

#### Step 5: Startup

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

Without `AddSiteSettingsPermission`, users need the broad `ManageSettings` permission to open the group.

### Options Populated from Site Settings (Refresh Without a Shell Release)

Use this pattern when services consume settings through options. The options are rebuilt after the settings are committed, on every node when a distributed `ISignal` (Redis Bus) is enabled.

```csharp
public sealed class WeatherSettings
{
    public string ApiKey { get; set; }

    public string Units { get; set; } = "metric";
}

public sealed class WeatherOptions
{
    public string ApiKey { get; set; }

    public string Units { get; set; }
}

// Builds the options from the site settings each time the monitor cache is invalidated.
public sealed class WeatherOptionsConfiguration : IConfigureOptions<WeatherOptions>
{
    private readonly ISiteService _siteService;

    public WeatherOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(WeatherOptions options)
    {
        var settings = _siteService.GetSettings<WeatherSettings>();

        // '??=' keeps values already set from appsettings.json.
        options.ApiKey ??= settings.ApiKey;
        options.Units ??= settings.Units;
    }
}

public sealed class WeatherSettingsDisplayDriver : SiteDisplayDriver<WeatherSettings>
{
    public const string GroupId = "weather";

    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public WeatherSettingsDisplayDriver(
        IOptionsUpdateNotifier optionsUpdateNotifier,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _optionsUpdateNotifier = optionsUpdateNotifier;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, WeatherSettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, WeatherPermissions.ManageWeatherSettings))
        {
            return null;
        }

        return Initialize<WeatherSettingsViewModel, WeatherSettings>("WeatherSettings_Edit", static (model, settings) =>
        {
            model.ApiKey = settings.ApiKey;
            model.Units = settings.Units;
        }, settings).Location("Content:5")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, WeatherSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, WeatherPermissions.ManageWeatherSettings))
        {
            return null;
        }

        var model = new WeatherSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!context.Updater.ModelState.IsValid)
        {
            return await EditAsync(site, settings, context);
        }

        var apiKey = model.ApiKey?.Trim();

        if (settings.ApiKey != apiKey || settings.Units != model.Units)
        {
            // Dispatched only after the site settings document commits successfully.
            _optionsUpdateNotifier.RequestUpdate<WeatherOptions>();
        }

        settings.ApiKey = apiKey;
        settings.Units = model.Units;

        return await EditAsync(site, settings, context);
    }
}

public sealed class WeatherClient
{
    private readonly IOptionsMonitor<WeatherOptions> _options;

    public WeatherClient(IOptionsMonitor<WeatherOptions> options)
    {
        _options = options;
    }

    // Read CurrentValue at the point of use; never cache it in a field.
    public string GetUnits() => _options.CurrentValue.Units;
}

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<WeatherSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(WeatherSettingsDisplayDriver.GroupId, WeatherPermissions.ManageWeatherSettings);

        // Opts WeatherOptions into Orchard Core's signal-backed IOptionsMonitor refresh.
        services.AddSignalOptionsChangeTokenSource<WeatherOptions>();
        services.AddTransient<IConfigureOptions<WeatherOptions>, WeatherOptionsConfiguration>();

        services.AddSingleton<WeatherClient>();
    }
}
```

`WeatherPermissions` and `WeatherSettingsViewModel` follow the same shape as the social media example. For named options, register `new SignalOptionsChangeTokenSource<TOptions>(signal, name)` as `IOptionsChangeTokenSource<TOptions>` and call `RequestUpdate<TOptions>(name)`. `RequestUpdate` returns the notifier, so several types can be chained: `.RequestUpdate<A>().RequestUpdate<B>()`.

### Settings That Require a Tenant Reload

```csharp
public override async Task<IDisplayResult> EditAsync(ISite site, MySettings settings, BuildEditorContext context)
{
    // ... authorization
    context.AddTenantReloadWarningWrapper();

    return Initialize<MySettingsViewModel, MySettings>("MySettings_Edit", static (model, settings) =>
    {
        model.Enabled = settings.Enabled;
    }, settings).Location("Content:5")
    .OnGroup(SettingsGroupId);
}

public override async Task<IDisplayResult> UpdateAsync(ISite site, MySettings settings, UpdateEditorContext context)
{
    // ... authorization and TryUpdateModelAsync
    if (settings.Enabled != model.Enabled)
    {
        _shellReleaseManager.RequestRelease();
    }

    settings.Enabled = model.Enabled;

    return await EditAsync(site, settings, context);
}
```

### Accessing Settings in Liquid

`{{ Site.SiteName }}`, `{{ Site.PageSize }}`, `{{ Site.BaseUrl }}` and other `ISite` properties are available. The site salt was removed from Liquid output (`{{ Site.SiteSalt }}` only returns `[REDACTED]`), and `{{ Site.Properties }}` is empty unless the sections are allowed:

```json
{
  "OrchardCore": {
    "Liquid": {
      "PermittedSiteProperties": [
        "SocialMediaSettings",
        "CurrentThemeName",
        "LayerSettings"
      ]
    }
  }
}
```

```liquid
{% if Site.Properties.SocialMediaSettings.ShowSocialLinks %}
  <a href="{{ Site.Properties.SocialMediaSettings.FacebookUrl }}">Facebook</a>
{% endif %}
```

Never permit sections holding secrets (API keys, passwords).

### Reading Settings in Razor

```html
@inject OrchardCore.Settings.ISiteService SiteService
@{
    var site = await SiteService.GetSiteSettingsAsync();
}

@if (site.TryGet<SocialMediaSettings>(out var social) && social.ShowSocialLinks)
{
    <a href="@social.FacebookUrl">Facebook</a>
}
```

### Configuring Site Settings via Recipes

The `Settings` step sets built-in properties; any other key is stored as a section in `Properties`:

```json
{
  "steps": [
    {
      "name": "Settings",
      "SiteName": "My Orchard Core Site",
      "BaseUrl": "https://www.example.com",
      "TimeZoneId": "America/New_York",
      "PageSize": 10,
      "AllowPageSizeSelection": true,
      "PageSizeOptions": [ 10, 25, 50, 100 ],
      "UseCdn": false,
      "SocialMediaSettings": {
        "FacebookUrl": "https://facebook.com/contoso",
        "ShowSocialLinks": true
      }
    }
  ]
}
```

Use the **Site Settings** deployment step to export the built-in settings, and `AddSiteSettingsPropertyDeploymentStep<TSettings, TLocalizer>()` to export one section into the same `Settings` step.

### Upgrading from 3.x

- `OrchardCore.Settings.Permissions.ManageSettings` / `ManageGroupSettings` were removed; use `OrchardCore.Settings.SettingsPermissions.ManageSettings` / `ManageGroupSettings`. Built-in groups now use `ManageGeneralSettings` and `ManageDebuggingSettings`.
- Register each custom group with `AddSiteSettingsPermission(groupId, permission)` so it no longer requires `ManageSettings`.
- Options built from site settings: replace `IOptions<T>` with `IOptionsMonitor<T>.CurrentValue`, and replace `IShellReleaseManager.RequestRelease()` with `AddSignalOptionsChangeTokenSource<T>()` + `IOptionsUpdateNotifier.RequestUpdate<T>()` when only options depend on the settings.
- Permission descriptions: `new Permission("Name", "Description")` still compiles, but prefer `LocalizationSource.Create("Description", typeof(DeclaringType))` so the roles editor shows PO translations.
- Liquid: the site salt was removed from Liquid output, so `{{ Site.SiteSalt }}` now only returns `[REDACTED]`, and `{{ Site.Properties.X }}` is empty until `X` is listed in `OrchardCore:Liquid:PermittedSiteProperties`.
- `site.As<T>()` is obsolete; use `TryGet<T>()` or `GetOrCreate<T>()`.

See `references/site-settings-examples.md` for complete examples.
