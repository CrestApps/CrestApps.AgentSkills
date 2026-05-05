# Permission Provider Examples

## Orchard Core Style Example

```csharp
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace OrchardCore.Media;

public sealed class PermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        MediaPermissions.ManageMedia,
        MediaPermissions.ManageMediaFolder,
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
                MediaPermissions.ManageMediaFolder,
            ],
        },
    ];
}
```

## CrestApps Placement Example

```text
src/Core/CrestApps.Sports.Teams.Core/
  Models/TeamPart.cs
  Permissions/TeamPermissions.cs

src/Modules/CrestApps.Sports.Teams/
  PermissionProvider.cs
  Startup.cs
  Drivers/TeamPartDisplayDriver.cs
```

## Best Practices

- Keep `PermissionProvider` internal and sealed.
- Keep reusable static permission instances in the matching `*.Core` project.
- Use `OrchardCoreConstants.Roles.Administrator` for the administrator stereotype.
- Keep role-specific stereotypes focused on the minimum permissions each role needs.
