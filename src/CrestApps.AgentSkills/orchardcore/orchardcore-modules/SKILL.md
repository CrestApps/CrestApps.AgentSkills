---
name: orchardcore-modules
description: Skill for creating and structuring Orchard Core modules in an application that consumes Orchard Core packages. Covers module scaffolding, manifest and feature registration, feature dependencies, Startup classes with Feature, RequireFeatures and the Orchard Core 4.0 RequiredStartup attribute, the OCSG002 analyzer, installing third-party modules in the web project, configuration section naming, static assets, and 4.0 authoring rules. Use this skill when requests mention Orchard Core Modules, Create a Module, Installing Third-Party Modules, Manifest Pattern, Startup Pattern, Project File Pattern, always-composed startup, or closely related Orchard Core implementation, upgrade, or troubleshooting work. Strong matches include OrchardCore.Application.Cms.Targets, OrchardCore.Modules.Manifest, OrchardCore.Module.Targets, Manifest.cs, StartupBase, [Feature], [RequireFeatures], [RequiredStartup], OCSG002.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Modules - Prompt Templates

## Create a Module

You are an Orchard Core expert. Generate the scaffolding for a module owned by
an application that consumes Orchard Core packages. For the full code
templates (parts, fields, drivers, handlers, admin pages, the `ocmodulecms`
project template), use the `orchardcore-module-creator` skill alongside this
one.

### Guidelines

- Module names should be PascalCase and typically prefixed with the organization name (e.g., `MyOrg.MyModule`).
- Every module must have a `Manifest.cs` file declaring its features.
- Each feature must have a unique ID and should declare its dependencies (feature IDs such as `OrchardCore.Contents`, not package names).
- Use `Startup` classes to register services, routes, and navigation.
- Register migrations with `services.AddDataMigration<Migrations>()`.
- Declare a new feature with `[assembly: Feature(...)]` only when the user should manage it; to extend or override behavior when other features are present, gate a `Startup` with `[RequireFeatures(...)]` instead of adding another feature the user must enable. See the `orchardcore-features` skill for the full decision.
- `[Feature("Id")]` on a startup or component may only name a feature declared by **the same module**; otherwise the `OCSG002` analyzer warns and the target is ignored at runtime. Use `[RequireFeatures]` for other modules' features.
- Use `[RequiredStartup]` (4.0) for a component that must be composed for every tenant, even when its own feature is disabled.
- Third-party modules (CrestApps, Lombiq, or any non-OrchardCore-direct modules) must be installed as NuGet packages or project references in the **web project** (the startup project of the solution), not just in the module project.
- Name configuration sections `OrchardCore:MyService[:Provider]`, without an `OrchardCore_` prefix (see `orchardcore-configuration`).
- Always seal classes, except view models.

### Installing Third-Party Modules

Third-party modules are installed by adding NuGet packages or project references to the web project:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <!-- Orchard Core base -->
    <PackageReference Include="OrchardCore.Application.Cms.Targets" Version="3.*" />

    <!-- Third-party modules must be in the web project -->
    <PackageReference Include="Lombiq.HelpfulExtensions.OrchardCore" Version="1.*" />
  </ItemGroup>
</Project>
```

For local module projects:

```xml
<ItemGroup>
  <ProjectReference Include="../ThirdParty.Module/ThirdParty.Module.csproj" />
</ItemGroup>
```

### Manifest Pattern

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "{{ModuleName}}",
    Author = "{{Author}}",
    Website = "{{Website}}",
    Version = "1.0.0",
    Description = "{{Description}}",
    Category = "{{Category}}"
)]

[assembly: Feature(
    Id = "{{ModuleName}}",
    Name = "{{FeatureName}}",
    Description = "{{FeatureDescription}}",
    Dependencies =
    [
        "OrchardCore.Contents",
    ],
    Category = "{{Category}}"
)]
```

### Startup Pattern

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace {{ModuleName}};

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register services here
    }
}

// Only composed when the Workflows feature is enabled.
[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register workflow activities here
    }
}
```

### Feature Targeting Attributes

| Attribute | Use it to |
|-----------|-----------|
| `[Feature("MyModule.Extra")]` | Attach a startup/component to a feature declared in this module's `Manifest.cs`. |
| `[RequireFeatures("OrchardCore.Media")]` | Compose the type only when all listed features (from any module) are enabled. |
| `[RequiredStartup]` | Compose the type for every tenant, even if its feature is disabled; its services belong to `Application.DefaultFeatureId`. Can be combined with `[RequireFeatures]`. |

```csharp
// Composed in every tenant where OrchardCore.Users is enabled,
// whether or not the module's own feature is enabled.
[RequiredStartup]
[RequireFeatures("OrchardCore.Users")]
public sealed class UserCleanupStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IUserEventHandler, UserCleanupHandler>();
    }
}
```

Wrong (OCSG002, silently ignored at runtime):

```csharp
[Feature("OrchardCore.Liquid")] // Not declared by this module.
public sealed class LiquidStartup : StartupBase { }
```

Right:

```csharp
[RequireFeatures("OrchardCore.Liquid")]
public sealed class LiquidStartup : StartupBase { }
```

### Project File Pattern

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="OrchardCore.Module.Targets" Version="3.*" />
  </ItemGroup>

</Project>
```

### Module Folder Structure

```
MyModule/
├── Manifest.cs
├── Startup.cs
├── MyModule.csproj
├── Controllers/
├── Drivers/
├── Handlers/
├── Migrations/
├── Models/
├── Services/
├── ViewModels/
├── Views/
└── wwwroot/
```

Files in `wwwroot/` are served at `/{ModuleId}/...` (e.g. `/MyModule/js/app.js`), from the project folder during development and from the embedded assembly resources otherwise.

### Orchard Core 4.0 Authoring Checklist

- Display drivers: `Initialize<T>()` / `Factory()`; `Shape()` is obsolete.
- Permissions: static `*Permissions` class, `LocalizationSource.Create(...)` descriptions, `services.AddPermissionProvider<T>()`.
- Admin pages: `<breadcrumb>` in the `Title` zone, `<admin-list>` for lists, `AdminNavigationProvider` for menus.
- Admin scripts: no jQuery; editor scripts as `<script at="Foot" type="module">`.
- Temporary files: `ITempDirectoryProvider`; user uploads: `FileCreationService`.
- Options built from site settings: `IOptionsMonitor<T>.CurrentValue`.

See `orchardcore-module-creator` for code templates of each item, and `references/module-examples.md` for a complete module.
