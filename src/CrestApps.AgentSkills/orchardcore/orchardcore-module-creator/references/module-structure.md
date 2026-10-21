# Module Structure Reference

## Directory Layout

```
src/Modules/Contoso.YourModule/
├── Manifest.cs                 # Module metadata and features
├── Startup.cs                  # Service registration
├── Migrations.cs               # Database migrations
├── Permissions.cs              # IPermissionProvider (static definitions in *Permissions class)
├── AdminMenu.cs                # Admin navigation (AdminNavigationProvider)
├── placement.json              # Optional default placement
├── ConfigurationSchema.json    # Optional appsettings.json schema for the module's sections
├── Controllers/                # MVC controllers
├── Drivers/                    # Content part/field drivers
├── Endpoints/                  # Minimal API endpoints
├── Fields/                     # Custom content fields
├── Handlers/                   # Event handlers
├── Indexes/                    # YesSql indexes
├── Models/                     # Data models
├── Services/                   # Business logic services
├── Settings/                   # Part/field settings
├── ViewModels/                 # View models (not sealed)
├── Views/                      # Razor/Liquid templates
├── Assets/                     # Frontend sources (JS, CSS, SCSS)
│   ├── js/                     # ES modules, no jQuery
│   ├── scss/
│   └── package.json
├── Assets.json                 # Optional asset build actions
├── wwwroot/                    # Static files, served at ~/Contoso.YourModule/...
└── Contoso.YourModule.csproj
```

The web (startup) project references the module project; modules that are not referenced by the startup project are not discovered.

## Required Files

### Manifest.cs

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Your Module",
    Author = "Contoso",
    Website = "https://contoso.com",
    Version = "1.0.0"
)]

[assembly: Feature(
    Id = "Contoso.YourModule",
    Name = "Your Module",
    Description = "Description of your module.",
    Dependencies = ["OrchardCore.ContentTypes"],
    Category = "Content Management"
)]
```

A module with a single feature can put `Description`, `Dependencies`, and `Category` on `[Module]` instead; the module ID (assembly name) is then the feature ID.

### Startup.cs

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace Contoso.YourModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IYourService, YourService>();
        services.AddDataMigration<Migrations>();
        // Register navigation, permissions, etc.
    }
}
```

### Project File (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="OrchardCore.Module.Targets" Version="3.*" />
    <PackageReference Include="OrchardCore.ContentManagement" Version="3.*" />
    <PackageReference Include="OrchardCore.ContentTypes.Abstractions" Version="3.*" />
    <PackageReference Include="OrchardCore.DisplayManagement" Version="3.*" />
  </ItemGroup>

</Project>
```

Reference only the abstractions and libraries the code uses (for example `OrchardCore.Admin.Abstractions`, `OrchardCore.Navigation.Core`, `OrchardCore.Data.YesSql.Abstractions`). Third-party modules your module depends on must also be installed in the web project.

## Optional Files

| File | Purpose | When Needed |
|------|---------|-------------|
| `Migrations.cs` | Database schema | When storing data |
| `Permissions.cs` | `IPermissionProvider` exposing static permissions | When securing features |
| `ConfigurationSchema.json` | IntelliSense/validation of the module's `OrchardCore:*` section | When reading configuration |
| `AdminMenu.cs` | Admin navigation | When adding admin UI |
| `placement.json` | Default shape placement | When the module's shapes need default zones |
| `Controllers/` | HTTP endpoints (HTML) | When handling requests |
| `Drivers/` | Content display/edit | When creating content parts |
| `Handlers/` | Event handling | When responding to events |
| `Services/` | Business logic | When encapsulating logic |
| `Views/` | Razor templates | When rendering HTML |

## Feature Dependencies

Common dependencies to consider:

| Dependency | Use Case |
|------------|----------|
| `OrchardCore.ContentTypes` | Working with content types |
| `OrchardCore.Contents` | Content item CRUD |
| `OrchardCore.Media` | Media/file handling |
| `OrchardCore.Users` | User management |
| `OrchardCore.Workflows` | Workflow integration |
| `OrchardCore.Liquid` | Liquid template support |

Feature dependencies are feature IDs, not package names. Depending on a feature also makes your `Startup` run after its startup, which is required when you change its registrations (for example `ForEditor` on a built-in field).
