---
name: orchardcore-permission-providers
description: Skill for implementing Orchard Core permission providers. Covers static Permissions classes in Core projects, the IPermissionProvider pattern, AddPermissionProvider registration, LocalizationSource permission descriptions and PO-file translation, IStringLocalizerFactory.Localize, implied and security-critical permissions, PermissionTemplate dynamic permissions and ContentTypePermissionsHelper, default stereotypes, OrchardCoreConstants role names, and the 4.0 removal of obsolete permission members such as CommonPermissions and StandardPermissions.SiteOwner. Use this skill when requests mention Orchard Core permission providers, declaring permissions, localized permission descriptions, dynamic or per-item permissions, removed permission members, or closely related Orchard Core implementation, upgrade, or troubleshooting work. Strong matches include OrchardCore.Security.Permissions, IPermissionProvider, PermissionTemplate, PermissionStereotype, Permission.Description, UsersPermissions, and MediaPermissions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Permission Providers - Prompt Templates

## Create a Permission Provider

You are an Orchard Core expert. Generate permission-provider code that follows Orchard Core conventions and CrestApps project structure.

### Guidelines

- Declare reusable permissions as `static readonly` fields of a `static` `*Permissions` class (for example `TeamPermissions`) in the feature's `*.Core` or `*.Abstractions` project, so other modules can reference them without depending on the module.
- Keep the provider separate: an `internal sealed class PermissionProvider : IPermissionProvider` in the module project that only lists the permissions and stereotypes.
- Create descriptions with `LocalizationSource.Create("...", typeof(DeclaringType))` (namespace `OrchardCore.Localization`). The type is the PO translation context.
- Register the provider with `services.AddPermissionProvider<PermissionProvider>()` (it uses `TryAddEnumerable`, so double registration is harmless).
- Use `OrchardCoreConstants.Roles.Administrator` instead of hard-coded `"Administrator"`.
- Mark permissions that grant broad or dangerous access with `isSecurityCritical: true`.
- Use implied permissions (`impliedBy`) so a broader permission grants the narrower one.
- Place reusable content-part models shared by services, drivers, and other features in the `*.Core` project too; keep the module focused on `Startup`, drivers, controllers, and views.

### Recommended Project Placement

- `src/Core/{{FeatureName}}.Core/Permissions/{{FeatureName}}Permissions.cs`
- `src/Core/{{FeatureName}}.Core/Models/{{ContentPart}}.cs`
- `src/Modules/{{FeatureName}}/PermissionProvider.cs`
- `src/Modules/{{FeatureName}}/Startup.cs`

### Static Permission Definitions in a Core Project

```csharp
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.Sports.Teams.Core.Permissions;

public static class TeamPermissions
{
    public static readonly Permission ManageTeams = new(
        "ManageTeams",
        LocalizationSource.Create("Manage teams", typeof(TeamPermissions)),
        isSecurityCritical: true);

    public static readonly Permission ViewTeams = new(
        "ViewTeams",
        LocalizationSource.Create("View teams", typeof(TeamPermissions)),
        [ManageTeams]);
}
```

Use `typeof()` here: a `static` class cannot be the `T` of `LocalizationSource.Create<T>()`. The constructors taking a plain `string` description (`new Permission("ManageTeams", "Manage teams")`) are `[Obsolete]` (CS0618) since Orchard Core 4.0; they create a context-free source that is translated only through Data Localization, not PO files.

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
        TeamPermissions.ViewTeams,
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
        new PermissionStereotype
        {
            Name = "Coach",
            Permissions =
            [
                TeamPermissions.ViewTeams,
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
        services.AddPermissionProvider<PermissionProvider>();
    }
}
```

### Displaying and Translating Descriptions

`Permission.Description` is a `LocalizationSource`, not a `string`. Show it with `stringLocalizerFactory.Localize(permission.Description)?.Value ?? permission.Name`, and read `permission.Description?.Value` for the original text. The roles editor and the admin menu and menu item permission pickers translate typed descriptions with PO files, using the type's full name as `msgctxt`:

```po
msgctxt "CrestApps.Sports.Teams.Core.Permissions.TeamPermissions"
msgid "Manage teams"
msgstr "Gérer les équipes"
```

Typed descriptions are not listed in Data Localization; context-free descriptions, permissions built from templates (for example "Edit Article"), and categories still are. `orchardcore-localization-source` covers the API, the Data Localization interaction, and migration in detail.

### Dynamic Permission Templates

Declare the template of a family of dynamic permissions (one per team, role, content type...) as a `PermissionTemplate`, not as a `Permission`. A template is never listed by a provider or granted to a role; it only creates permissions:

```csharp
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.Sports.Teams.Core.Permissions;

public static class TeamPermissionTemplates
{
    private static readonly PermissionTemplate s_manageTeam = new(
        "ManageTeam_{0}",
        LocalizationSource.Create("Manage team {0}", typeof(TeamPermissionTemplates)),
        TeamPermissions.ManageTeams);

    public static Permission CreateManageTeamPermission(string teamName, string teamDisplayName) =>
        s_manageTeam.CreateDynamicPermission(teamName, teamDisplayName);
}
```

- Constructors: `PermissionTemplate(name, description, category = null, params Permission[] impliedBy)` and `PermissionTemplate(name, description, params Permission[] impliedBy)`. `Name`, the description value and `Category` use `{0}`; `Category` can also use `{1}` for the description value.
- `CreateDynamicPermission(nameValue)` uses the same value for both; `CreateDynamicPermission(nameValue, descriptionValue)` formats `Name` with the first value and `Description.Value` with the second.
- `ImpliedBy` lists static permissions; the init-only `ImpliedByTemplates` lists other templates, created with the same values, for example "Edit own {0}" implied by "Edit {0} for others". Set `IsSecurityCritical` with an object initializer.
- The created permission's description is `LocalizationSource.Create(formattedText)` without a type, so it is translated through Data Localization, not PO files. Data Localization skips values that still contain `{0}`.
- Cache created permissions (for example in a `ConcurrentDictionary`) when they are requested often, as `IndexingPermissions.CreateDynamicPermission(indexProfile)` does.

Content type permissions use the same model: `ContentTypePermissionsHelper.PermissionTemplates` (namespace `OrchardCore.Contents.Security`) is a `Dictionary<string, PermissionTemplate>` keyed by the global permission name. Use `ContentTypePermissionsHelper.CreateDynamicPermissionOf(CommonPermissions.EditContent, typeDefinition)`, or its `(string basePermissionName, ContentTypeDefinition)` and `(string basePermissionName, string contentType)` overloads, and `GetPermissionTemplate(name)`.

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

### Upgrading from 3.x

- `Permission.Description` changed from `string` to `LocalizationSource`. Replace `permission.Description` string usage with `permission.Description.Value` (raw text) or `IStringLocalizerFactory.Localize(permission.Description)` (translated), and create descriptions with `LocalizationSource.Create("...", typeof(MyPermissions))`. Modules compiled against 3.x must be recompiled. See `orchardcore-localization-source`.
- The `[Obsolete]` permission members were removed. Build on the latest 3.x, fix every `CS0618` warning, then upgrade. Stored role claims use the same permission names, so no data migration is needed. Common replacements:
  - `OrchardCore.Users.CommonPermissions.*` becomes `OrchardCore.Users.UsersPermissions.*`
  - `OrchardCore.Deployment.CommonPermissions.*` becomes `OrchardCore.Deployment.DeploymentPermissions.*`
  - `OrchardCore.Apis.GraphQL.CommonPermissions.*` becomes `OrchardCore.Apis.GraphQL.GraphQLPermissions.*`
  - `OrchardCore.Media.Permissions.*` and `SecureMediaPermissions.*` become `OrchardCore.Media.MediaPermissions.*`
  - `OrchardCore.Workflows.Permissions.*` becomes `OrchardCore.Workflows.WorkflowsPermissions.*`
  - `OrchardCore.Settings.Permissions.*` becomes `OrchardCore.Settings.SettingsPermissions.*`
  - `OrchardCore.ReCaptcha.Permissions.ManageReCaptchaSettings` becomes `OrchardCore.ReCaptcha.ReCaptchaPermissions.ManageReCaptchaSettings`
  - `OrchardCore.Security.StandardPermissions.SiteOwner` is gone; assign users to the `Administrator` system role instead.
  - See `references/permission-provider-examples.md` for the full table.
- Beware of 3.x members that returned the wrong permission. Replace them with the member of the same name, not the one in the obsolete message:
  - `AdminMenu.Permissions.ManageAdminMenu` and `ViewAdminMenuAll` were swapped; use `AdminMenuPermissions.ManageAdminMenu` and `AdminMenuPermissions.ViewAdminMenuAll` and re-check what each call site intends.
  - `CommonPermissions.CreateAssignRoleToUsersPermission(role)` actually returned the edit-users-in-role permission. `UsersPermissions.CreateAssignRoleToUsersPermission(role)` now returns the real assign-role permission, so code relying on the old behavior must use `UsersPermissions.CreateEditUsersInRolePermission(role)`.
- Provider classes such as `OrchardCore.Workflows.Permissions` are still registered; only their static members were removed.
- The `Permission(string name, string description, ...)` constructors are `[Obsolete]`. Pass `LocalizationSource.Create("...", typeof(MyPermissions))`.
- Templates declared as a `Permission` (for example `new("ManageTeam_{0}", "Manage team {0}", [...])`) and formatted by hand become `PermissionTemplate` fields and `CreateDynamicPermission(...)` calls.
- `ContentTypePermissionsHelper.PermissionTemplates` values are `PermissionTemplate` instead of `Permission`. `CreateDynamicPermission(Permission template, ContentTypeDefinition)`, `CreateDynamicPermission(Permission template, string contentType)` and `ConvertToDynamicPermission(Permission)` are `[Obsolete]`; use `CreateDynamicPermissionOf(...)` and `GetPermissionTemplate(...)`.
- `DeploymentStep.Category` and `Title` are also `LocalizationSource` now, exposed as read-only virtual properties that a step overrides (see `orchardcore-deployments`).
