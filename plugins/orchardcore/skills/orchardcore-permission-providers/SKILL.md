---
name: orchardcore-permission-providers
description: Skill for implementing Orchard Core permission providers. Covers the PermissionProvider pattern, default stereotypes, OrchardCoreConstants role names, and where to place reusable static permission instances and reusable content-part models. Use this skill when requests mention Orchard Core Permission Providers, Create a Permission Provider, Recommended Project Placement, Static Permission Definitions in a Core Project, PermissionProvider Class, Registering the Provider, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.Sports.Teams.Core, OrchardCore.Security.Permissions, CrestApps.Sports.Teams.Core.Permissions, CrestApps.Sports.Teams, OrchardCore.Modules. It also helps with permission provider examples, PermissionProvider Class, Registering the Provider, Content Part Placement Guidance, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Permission Providers - Prompt Templates

## Create a Permission Provider

You are an Orchard Core expert. Generate permission-provider code that follows Orchard Core conventions and CrestApps project structure.

### Guidelines

- Name the provider class `PermissionProvider`.
- Keep the provider class `internal sealed`.
- Implement `IPermissionProvider`.
- Use `OrchardCoreConstants.Roles.Administrator` instead of hard-coded `"Administrator"`.
- Keep reusable static permission instances outside the provider class.
- Place reusable static permission instances in the corresponding `*.Core` project for the feature, for example `CrestApps.Sports.Teams.Core`.
- Place reusable content-part models that are shared between runtime services, display drivers, and feature code in the corresponding `*.Core` project.
- Keep the feature project focused on Orchard feature wiring such as `Startup`, display drivers, controllers, and views.

### Recommended Project Placement

- `src/Core/{{FeatureName}}.Core/Permissions/{{FeatureName}}Permissions.cs`
- `src/Core/{{FeatureName}}.Core/Models/{{ContentPart}}.cs`
- `src/Modules/{{FeatureName}}/PermissionProvider.cs`
- `src/Modules/{{FeatureName}}/Startup.cs`

### Static Permission Definitions in a Core Project

```csharp
using OrchardCore.Security.Permissions;

namespace CrestApps.Sports.Teams.Core.Permissions;

public static class TeamPermissions
{
    public static readonly Permission ManageTeams = new("ManageTeams", "Manage teams");
}
```

### PermissionProvider Class

```csharp
using CrestApps.Sports.Teams.Core.Permissions;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.Sports.Teams;

internal sealed class PermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        TeamPermissions.ManageTeams,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions =
            [
                TeamPermissions.ManageTeams,
            ],
        },
        new PermissionStereotype
        {
            Name = "Coach",
            Permissions =
            [
                TeamPermissions.ManageTeams,
            ],
        },
    ];
}
```

### Registering the Provider

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.Sports.Teams;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPermissionProvider, PermissionProvider>();
    }
}
```

### Content Part Placement Guidance

When a content part model is reused by services or other projects, move it into the matching `*.Core` project:

```csharp
using OrchardCore.ContentManagement;

namespace CrestApps.Sports.Teams.Core.Models;

public sealed class TeamPart : ContentPart
{
    public string AgeGroup { get; set; }

    public string Level { get; set; }
}
```

The feature project should reference the core project and use that shared model from drivers, services, and startup wiring.
