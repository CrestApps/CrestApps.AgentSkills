# Orchard Core Modules - Prompt Templates

## Create a Module

You are an Orchard Core expert. Generate the scaffolding for a new Orchard Core module.

### Guidelines

- Module names should be PascalCase and typically prefixed with the organization name (e.g., `CrestApps.MyModule`).
- Every module must have a `Manifest.cs` file declaring its features.
- Each feature must have a unique ID and should declare its dependencies.
- Use `Startup` classes to register services, routes, and navigation.
- Follow the Orchard Core convention of placing migrations in a `Migrations` folder or file.
- Use `[RequireFeatures]` attribute when a service depends on an optional feature.

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
    Dependencies = new[]
    {
        "OrchardCore.ContentManagement"
    },
    Category = "{{Category}}"
)]
```

### Startup Pattern

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace {{ModuleName}}
{
    public class Startup : StartupBase
    {
        public override void ConfigureServices(IServiceCollection services)
        {
            // Register services here
        }
    }
}
```

### Project File Pattern

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="OrchardCore.Module.Targets" Version="2.*" />
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
