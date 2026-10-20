---
name: orchardcore-module-creator
description: Creates new Orchard Core modules inside the Orchard Core source repository with proper structure, manifest, startup, and Orchard Core 4.0 patterns. Use when the user needs to create a module, add content parts, fields, drivers, handlers, settings, admin pages with breadcrumbs and admin lists, minimal API endpoints with Problem Details errors, permissions with LocalizationSource descriptions, RequiredStartup components, temporary files with ITempDirectoryProvider, secure uploads with FileCreationService, or ES module editor scripts without jQuery. Use this skill when requests mention Orchard Core Module Creator, Module Creation Workflow, Create Module Directory, Create Required Files, or closely related Orchard Core work. Strong matches include OrchardCore.Modules, OrchardCore.Module.Targets, StartupBase, RequireFeatures, RequiredStartup, FeatureAttribute, OCSG002, DisplayDriver, Initialize, Factory, AdminNavigationProvider, IPermissionProvider.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Module Creator

This skill is for contributing a module to the Orchard Core source repository.
For a module owned by an application that consumes Orchard Core packages, use
the `orchardcore-modules` skill instead.

## Prerequisites

- OrchardCore repository (working directory)
- .NET SDK 10.0+ installed

## Module Creation Workflow

### Step 1: Determine Module Type

| Type | Description | Key Components |
|------|-------------|----------------|
| **Content Part** | Adds data/behavior to content items | Part, Driver, Views |
| **Content Field** | Custom field type | Field, Driver, Views |
| **Settings** | Site-wide configuration | Settings class, `SiteDisplayDriver`, options |
| **Admin Feature** | Admin pages/tools | Controller, Views with `<breadcrumb>` and `<admin-list>`, Menu |
| **API** | JSON endpoints | Minimal API endpoint class + Startup route registration |
| **Background Task** | Scheduled jobs | `IBackgroundTask` |

### Step 2: Create Module Directory

```bash
mkdir src/OrchardCore.Modules/OrchardCore.YourModule
cd src/OrchardCore.Modules/OrchardCore.YourModule
```

### Step 3: Create Required Files

Every module needs:

1. **Manifest.cs** - Module metadata and features
2. **Startup.cs** - Service registration
3. **OrchardCore.YourModule.csproj** - Project file

Add a `ConfigurationSchema.json` (and the `JsonSchemaSegment` build item) when the module reads a configuration section; see `orchardcore-configuration`. See `references/module-structure.md` for templates.

### Step 4: Add Components Based on Type

**Content Part modules:**
```
Models/YourPart.cs
ViewModels/YourPartViewModel.cs
Drivers/YourPartDisplayDriver.cs
Views/YourPart.cshtml
Views/YourPart_Edit.cshtml
```

**Admin modules:**
```
Controllers/AdminController.cs
Views/Admin/Index.cshtml
AdminMenu.cs
Permissions.cs            (IPermissionProvider)
YourModulePermissions.cs  (static permission definitions, usually in a Core/Abstractions project)
```

**Data-storing modules:**
```
Migrations.cs
Indexes/YourIndex.cs
```

See `references/patterns.md` for code templates and `references/examples.md` for complete examples.

### Step 5: Register in Startup.cs

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<YourPart>()
            .UseDisplayDriver<YourPartDisplayDriver>();

        services.AddScoped<IYourService, YourService>();
        services.AddDataMigration<Migrations>();
        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();
    }
}

// Extends another module only when it is enabled; no extra feature to manage.
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<YourDeploymentSource, YourDeploymentStep, YourDeploymentStepDriver>();
    }
}
```

### Startup Attributes

| Attribute | Effect |
|-----------|--------|
| `[Feature("Id")]` | Binds the startup or component to one of the features **declared by this module's manifest**. |
| `[RequireFeatures("A", "B")]` | Composes the type only when all listed features are enabled. |
| `[RequiredStartup]` (4.0) | Composes the public type for **every tenant**, even when the feature that defines it is disabled. Its services are attributed to `Application.DefaultFeatureId`. Combine with `[RequireFeatures]` to still require other features. |

```csharp
// Always registered, even when 'OrchardCore.YourModule' is disabled,
// as long as OrchardCore.Contents is enabled.
[RequiredStartup]
[RequireFeatures("OrchardCore.Contents")]
public sealed class AlwaysOnStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentHandler, YourCleanupHandler>();
    }
}
```

- The `OCSG002` analyzer warns when `[Feature("...")]` names a feature that is not declared by the current module; such a target is silently ignored at runtime. To depend on another module's feature, use `[RequireFeatures("...")]` instead of `[Feature("...")]`.
- Shape table providers gated with `[RequireFeatures]` are now honored per shell, so their alternates and wrappers no longer leak into tenants that lack the required feature.

### JSON API Endpoints

When a module exposes an endpoint that primarily returns JSON, prefer Minimal APIs over MVC `ApiController` classes.

```csharp
public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
{
    routes.AddGetWeatherEndpoint();
}
```

```csharp
internal static class GetWeatherEndpoint
{
    public static IEndpointRouteBuilder AddGetWeatherEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("api/weather", HandleAsync)
            .WithName("YourModuleGetWeather")
            .AllowAnonymous()
            .DisableAntiforgery();

        return builder;
    }

    [Authorize(AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api)]
    private static async Task<IResult> HandleAsync(
        IAuthorizationService authorizationService,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, YourModulePermissions.ViewWeather))
        {
            return httpContext.ChallengeOrForbid(OrchardCoreConstants.AuthenticationSchemes.Api);
        }

        return TypedResults.Ok(new { ok = true });
    }
}
```

- 401/403 responses from the shared `Api` authentication scheme, and responses of endpoints marked `[SkipStatusCodePages]`, now carry an RFC 9457 Problem Details body (`application/problem+json`). Return `TypedResults.Problem(...)` / `ValidationProblem(...)` for your own errors so clients get one format.
- Give each endpoint a unique, stable name with `.WithName(...)` (one per verb, it becomes the OpenAPI `operationId`) so the `OrchardCore.OpenApi` module documents it cleanly; see `orchardcore-openapi`.
- Use MVC controllers for HTML/admin pages; use Minimal APIs for JSON endpoints.

### Orchard Core 4.0 Authoring Rules

- **Display drivers**: build shapes with `Initialize<TModel>()` (prefer the typed-state overloads with `static` lambdas) or `Factory()`; `Shape()` is obsolete. Initializer delegates are `ValueTask`-based. Overrides of the 3-parameter `Factory()` must move to `Factory<TBuilderState, TInitState>()`.
- **Permissions**: declare them in a static `*Permissions` class with `LocalizationSource.Create("Description", typeof(DeclaringType))` descriptions, and expose them through an `IPermissionProvider` registered with `AddPermissionProvider<T>()`. Use the current permission classes (`MediaPermissions`, `SettingsPermissions`, `UsersPermissions`, `DeploymentPermissions`, `GraphQLPermissions`, ...); the old `*.Permissions.*` members and `CommonPermissions` were removed, and `StandardPermissions.SiteOwner` is gone (use the Administrator role).
- **Deployment steps**: assign `Category` and `Title` as static `LocalizationSource` instances in the parameterless constructor.
- **Admin views**: declare the page title with `<breadcrumb name="..." title="@T["..."]">` inside the `Title` zone instead of `<h1>`; render lists with `<admin-list>` so List/Grid layouts work (see `orchardcore-breadcrumbs` and `orchardcore-admin-lists`).
- **Admin menu**: derive from `AdminNavigationProvider`; there is no legacy admin menu layout any more.
- **Front-end scripts in admin**: admin pages no longer load jQuery or jQuery UI. Use native DOM APIs and Bootstrap's JS API. Field and part editor scripts that can be injected via Flow, Bag, or Widgets "Add" must use `<script at="Foot" type="module">` and must not wait for `DOMContentLoaded`. Admin Vue apps use Vue 3.
- **Temporary files**: inject `ITempDirectoryProvider` instead of `Path.GetTempPath()`.
- **User uploads** stored outside `IMediaFileStore`: run them through `FileCreationService.CreateAsync()` first and abort when `Succeeded` is `false`.
- **Settings-backed options**: read `IOptionsMonitor<T>.CurrentValue`; see `orchardcore-site-settings`.
- **Configuration sections**: name them `OrchardCore:YourService[:Provider]` without an `OrchardCore_` prefix.
- **Static files**: module `wwwroot` files are served from `/{ModuleId}/...`; the application's own `wwwroot` is resolved through the web root, so do not build paths from `Application.Root`.

### Step 6: Build and Test

```bash
dotnet build src/OrchardCore.Modules/OrchardCore.YourModule

cd src/OrchardCore.Cms.Web
dotnet run -f net10.0

# Enable the feature in Admin → Tools → Features
```

Use the `orchardcore-tester` skill to verify the feature in a browser.

## Quick Reference

### Naming Conventions

| Item | Convention | Example |
|------|------------|---------|
| Module folder | `OrchardCore.ModuleName` | `OrchardCore.Rating` |
| Namespace | `OrchardCore.ModuleName` | `OrchardCore.Rating` |
| Feature ID | `OrchardCore.ModuleName` | `OrchardCore.Rating` |
| Content Part | `NamePart` | `RatingPart` |
| Driver | `NamePartDisplayDriver` | `RatingPartDisplayDriver` |
| Permissions | `NamePermissions` (static) | `RatingPermissions` |
| View | `PartName.cshtml` | `RatingPart.cshtml` |
| Edit View | `PartName_Edit.cshtml` | `RatingPart_Edit.cshtml` |

### Common Dependencies

```xml
<!-- Core module support -->
<ProjectReference Include="..\..\OrchardCore\OrchardCore.Module.Targets\OrchardCore.Module.Targets.csproj" />

<!-- Content management -->
<ProjectReference Include="..\..\OrchardCore\OrchardCore.ContentManagement\OrchardCore.ContentManagement.csproj" />

<!-- Admin UI -->
<ProjectReference Include="..\..\OrchardCore\OrchardCore.Admin.Abstractions\OrchardCore.Admin.Abstractions.csproj" />
```

### Feature Categories

Use in `Manifest.cs`: `Content Management`, `Content`, `Navigation`, `Security`, `Infrastructure`, `Theming`, `Developer`.

## References

- `references/module-structure.md` - Directory layout and file templates
- `references/patterns.md` - Code patterns (parts, drivers, handlers, permissions, uploads, temp files)
- `references/examples.md` - Complete module examples
- `AGENTS.md` (repo root) - Coding conventions and build commands
