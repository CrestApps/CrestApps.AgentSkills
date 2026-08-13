---
name: orchardcore-users-roles
description: Skill for managing users, roles, and permissions in Orchard Core. Covers user registration, role creation, permission definitions, custom user settings, and authentication configuration. Use this skill when requests mention Orchard Core Users & Roles, Manage Users, Roles, and Permissions, Enabling User and Role Features, Defining Custom Permissions, Registering Permission Provider, Checking Permissions in Code, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Users, OrchardCore.Roles, OrchardCore.Users.Registration, OrchardCore.Users.ResetPassword, OrchardCore.Users.CustomUserSettings, OrchardCore.Security.Permissions, IPermissionProvider, IAuthorizationService. It also helps with users roles examples, Registering Permission Provider, Checking Permissions in Code, Checking Permissions in Liquid, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Users & Roles - Prompt Templates

## Manage Users, Roles, and Permissions

You are an Orchard Core expert. Generate code and configuration for user management, roles, and permissions.

### Guidelines

- Enable `OrchardCore.Users` and `OrchardCore.Roles` for user and role management.
- Custom permissions should extend `IPermissionProvider`.
- Roles group permissions together for easier management.
- Use `[Authorize]` attributes or `IAuthorizationService` for permission checks.
- Custom user settings allow extending user profiles with additional fields.
- Registration and login can be customized through the registration and login event interfaces.
- External authentication providers (Google, Microsoft, etc.) can be added.

### Enabling User and Role Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Users",
        "OrchardCore.Users.Registration",
        "OrchardCore.Users.ResetPassword",
        "OrchardCore.Users.CustomUserSettings",
        "OrchardCore.Roles"
      ],
      "disable": []
    }
  ]
}
```

### Defining Custom Permissions

```csharp
using OrchardCore.Security.Permissions;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission Manage{{Feature}} =
        new("Manage{{Feature}}", "Manage {{Feature}}");

    public static readonly Permission View{{Feature}} =
        new("View{{Feature}}", "View {{Feature}}");

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        return Task.FromResult<IEnumerable<Permission>>(new[]
        {
            Manage{{Feature}},
            View{{Feature}}
        });
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return new[]
        {
            new PermissionStereotype
            {
                Name = "Administrator",
                Permissions = new[] { Manage{{Feature}} }
            },
            new PermissionStereotype
            {
                Name = "Editor",
                Permissions = new[] { View{{Feature}} }
            }
        };
    }
}
```

### Registering Permission Provider

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPermissionProvider, Permissions>();
    }
}
```

### Checking Permissions in Code

```csharp
using Microsoft.AspNetCore.Authorization;

public sealed class MyController : Controller
{
    private readonly IAuthorizationService _authorizationService;

    public MyController(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, Permissions.View{{Feature}}))
        {
            return Forbid();
        }

        return View();
    }
}
```

### Checking Permissions in Liquid

```liquid
{% if User | has_permission: "ViewMyFeature" %}
    <p>You have access to this feature.</p>
{% endif %}
```

The `Administrator` role no longer receives permission claims by default during
login. Use `has_permission` for permission checks instead of checking a
permission claim with `has_claim`.

### Registration and Login Extensibility in 3.0

Registration and login customization now use dedicated event contracts:

```csharp
public sealed class RegistrationEvents : RegistrationFormEventsBase
{
    public override Task RegisteringAsync(UserRegisteringContext context)
    {
        // Validate or change the registration context.
        return Task.CompletedTask;
    }
}
```

`IRegistrationFormEvents` includes
`Task RegisteringAsync(UserRegisteringContext context)`, and
`RegistrationFormEventsBase` can be used when only selected handlers are
needed. `ILoginFormEvent` includes
`Task<IActionResult> ValidatingLoginAsync(IUser user)`.

The `ExternalLogin` action is no longer on the `Account` controller. Custom
login views must post external-login forms to the corresponding action on
`ExternalAuthenticationsController`.

### Registration Settings via Recipe

Use the `Settings` recipe step for the registration settings that remain in
Orchard Core 3.0:

```json
{
  "steps": [
    {
      "name": "Settings",
      "RegistrationSettings": {
        "UsersMustValidateEmail": true,
        "UsersAreModerated": false,
        "UseSiteTheme": false
      },
      "ExternalRegistrationSettings": {
        "DisableNewRegistrations": false,
        "NoPassword": false,
        "NoUsername": false,
        "NoEmail": false,
        "UseScriptToGenerateUsername": false,
        "GenerateUsernameScript": ""
      }
    }
  ]
}
```

`ExternalRegistrationSettings` controls users created through external
authentication. Do not use the removed `UsersCanRegister`,
`NoPasswordForExternalUsers`, `NoUsernameForExternalUsers`,
`NoEmailForExternalUsers`, or `UseScriptToGenerateUsername` properties on
`RegistrationSettings`.

### Creating Roles via Recipe

```json
{
  "steps": [
    {
      "name": "Roles",
      "Roles": [
        {
          "Name": "{{RoleName}}",
          "Description": "{{RoleDescription}}",
          "Permissions": [
            "View{{Feature}}",
            "AccessAdminPanel"
          ]
        }
      ]
    }
  ]
}
```

`AssignRoleToUsers` is no longer implicitly granted by `EditUsers`. Add
`OrchardCore.Users.UsersPermissions.AssignRoleToUsers` to existing roles that
must assign roles, or use
`OrchardCore.Users.UsersPermissions.CreateAssignRoleToUsersPermission(roleName)`
for a role-specific permission.

### Enabling and Disabling Users

User activation is managed from the Users list in 3.0. Use the user service
methods when changing status programmatically:

```csharp
await _userService.EnableAsync(user);
await _userService.DisableAsync(user);
```

### Custom User Settings

Extend user profiles with custom settings by enabling `OrchardCore.Users.CustomUserSettings`:

```csharp
// Define a custom user settings content type via migration
await _contentDefinitionManager.AlterTypeDefinitionAsync("UserProfile", type => type
    .DisplayedAs("User Profile")
    .Stereotype("CustomUserSettings")
    .WithPart("UserProfile", part => part
        .WithPosition("0")
    )
);

await _contentDefinitionManager.AlterPartDefinitionAsync("UserProfile", part => part
    .WithField("Bio", field => field
        .OfType("TextField")
        .WithDisplayName("Bio")
        .WithEditor("TextArea")
        .WithPosition("0")
    )
    .WithField("Avatar", field => field
        .OfType("MediaField")
        .WithDisplayName("Avatar")
        .WithPosition("1")
    )
);
```

### External Authentication (e.g., Microsoft)

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Microsoft.Authentication.AzureAD"
      ],
      "disable": []
    }
  ]
}
```

Configuration in `appsettings.json`:

```json
{
  "OrchardCore": {
    "OrchardCore_Microsoft_Authentication_AzureAD": {
      "AppId": "{{ClientId}}",
      "TenantId": "{{TenantId}}",
      "CallbackPath": "/signin-oidc"
    }
  }
}
```
