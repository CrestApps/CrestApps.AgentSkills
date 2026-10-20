---
name: orchardcore-users-roles
description: Skill for managing users, roles, and permissions in Orchard Core. Covers login, registration, and external authentication settings recipes, persistent authentication cookies, email confirmation resend, ILoginFormEvent and ILogoutFormEvent, the User Loggedout workflow event, RegistrationOptions and ExternalLoginOptions through IOptionsMonitor, external login scripts, rate-limited user endpoints, Users and Roles configuration sections, role recipes with PermissionBehavior, ViewRoles, UsersPermissions, and permission checks in code and Liquid. Use this skill when requests mention Orchard Core Users & Roles, Login Settings, Remember me, Email Confirmation, Logout events, Defining Custom Permissions, Checking Permissions, or closely related Orchard Core implementation, setup, extension, upgrade, or troubleshooting work. Strong matches include OrchardCore.Users, OrchardCore.Roles, OrchardCore.Users.Registration, OrchardCore.Users.ExternalAuthentication, IAuthorizationService, UsersPermissions, and LoginSettings.
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
- Declare permissions in a static `*Permissions` class and expose them through an `IPermissionProvider` (see `orchardcore-permission-providers`).
- Create permission descriptions with `LocalizationSource.Create("...", typeof(MyPermissions))`.
- Use `IAuthorizationService.AuthorizeAsync(User, permission)` for permission checks and `has_permission` in Liquid.
- Reference built-in user permissions through `OrchardCore.Users.UsersPermissions` and role permissions through `OrchardCore.Roles.RolesPermissions`.
- Customize login, logout, registration, and password recovery with `ILoginFormEvent`, `ILogoutFormEvent`, `IRegistrationFormEvents`, and `IPasswordRecoveryFormEvents`.
- Read `RegistrationOptions` and `ExternalLoginOptions` through `IOptionsMonitor<T>.CurrentValue`; they refresh when site settings change.
- Enable `OrchardCore.RateLimits` to throttle login, registration, password recovery, and confirmation-email endpoints (see `orchardcore-rate-limits`).
- External authentication providers live in their own skills (`orchardcore-google`, `orchardcore-microsoft-auth`, `orchardcore-facebook`, `orchardcore-github-authentication`, `orchardcore-twitter`, `orchardcore-openid`).

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

Other Users features include `OrchardCore.Users.ExternalAuthentication`, Users Change Email, User Time Zone, User Localization, Users Authentication Ticket Store (server-side tickets in memory or distributed cache), Users Audit Trail, and the Two-Factor Email, Authenticator App, and SMS methods.

### Defining Custom Permissions

```csharp
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace MyModule;

public static class MyFeaturePermissions
{
    public static readonly Permission ManageMyFeature = new(
        "ManageMyFeature",
        LocalizationSource.Create("Manage my feature", typeof(MyFeaturePermissions)));

    public static readonly Permission ViewMyFeature = new(
        "ViewMyFeature",
        LocalizationSource.Create("View my feature", typeof(MyFeaturePermissions)),
        [ManageMyFeature]);
}
```

```csharp
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace MyModule;

internal sealed class PermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        MyFeaturePermissions.ManageMyFeature,
        MyFeaturePermissions.ViewMyFeature,
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
            Name = "Editor",
            Permissions = [MyFeaturePermissions.ViewMyFeature],
        },
    ];
}
```

### Registering Permission Provider

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<PermissionProvider>();
    }
}
```

### Checking Permissions in Code

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public sealed class MyController : Controller
{
    private readonly IAuthorizationService _authorizationService;

    public MyController(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, MyFeaturePermissions.ViewMyFeature))
        {
            return Forbid();
        }

        return View();
    }
}
```

Role-specific user permissions are created from `UsersPermissions`, for example `UsersPermissions.CreateAssignRoleToUsersPermission("Editor")`, `CreateEditUsersInRolePermission`, `CreateDeleteUsersInRolePermission`, `CreateListUsersInRolePermission`, and `CreatePermissionForManageUsersInRole`.

### Checking Permissions in Liquid

```liquid
{% if User | has_permission: "ViewMyFeature" %}
    <p>You have access to this feature.</p>
{% endif %}
```

The `Administrator` role does not receive permission claims during login. Use `has_permission` instead of checking a permission claim with `has_claim`.

### Login Settings via Recipe

```json
{
  "steps": [
    {
      "name": "Settings",
      "LoginSettings": {
        "UseSiteTheme": false,
        "DisableLocalLogin": false,
        "AllowRememberMe": true,
        "UsePersistentAuthenticationCookie": false,
        "AllowChangingUsername": false,
        "AllowChangingEmail": false,
        "AllowChangingPhoneNumber": true
      }
    }
  ]
}
```

`UsePersistentAuthenticationCookie` (**Use persistent authentication cookies by default**) controls whether local and external sign-ins survive browser restarts. With `AllowRememberMe` enabled it is the default value of the **Remember me** checkbox; with `AllowRememberMe` disabled it decides persistence for every sign-in.

### Registration and External Authentication Settings via Recipe

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
      "ResetPasswordSettings": {
        "AllowResetPassword": true,
        "UseSiteTheme": false
      },
      "ExternalLoginSettings": {
        "UseExternalProviderIfOnlyOneDefined": false,
        "UseScriptToSyncProperties": false,
        "SyncPropertiesScript": ""
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

With `OrchardCore.Deployment` enabled, Login, Registration, Reset Password, Change Email, External login, and External registration settings each have a deployment step.

The **Generate username** script receives `context` with `userName`, `loginProvider`, and `externalClaims`; set `context.userName`. The **Sync properties** script receives `context` and fills `rolesToAdd`, `rolesToRemove`, claims, and properties. Orchard passes the per-login data through the scoped `externalLoginContext()` global, so write scripts against `context` and never build script text from user data.

### Email Confirmation

When `UsersMustValidateEmail` is enabled, an unconfirmed user who signs in with a valid password lands on a page with **Send confirmation email**. The action is offered for about 10 minutes in the same browser (a short-lived cookie, not a sign-in), at most once per minute per user, and, with Rate Limits enabled, 2 requests per minute per IP on the `ResendEmailConfirmation` route (`email-confirmation` policy).

### Login, Logout, and Registration Events

```csharp
using OrchardCore.Users;
using OrchardCore.Users.Events;

public sealed class AuditLogoutFormEvent : LogoutFormEventBase
{
    public override Task LoggingOutAsync(IUser user, CancellationToken cancellationToken = default)
    {
        // Runs before sign-out, while the principal is still available.
        return Task.CompletedTask;
    }

    public override Task LoggedOutAsync(IUser user, CancellationToken cancellationToken = default)
    {
        // Runs after sign-out.
        return Task.CompletedTask;
    }
}
```

```csharp
services.AddScoped<ILogoutFormEvent, AuditLogoutFormEvent>();
services.AddScoped<IRegistrationFormEvents, MyRegistrationEvents>();
```

- `ILoginFormEvent` includes `Task<IActionResult> ValidatingLoginAsync(IUser user)`.
- `IRegistrationFormEvents` includes `Task RegisteringAsync(UserRegisteringContext context)`; derive from `RegistrationFormEventsBase` to override only what you need.
- With `OrchardCore.Workflows`, the **User Loggedout Event** (`UserLoggedOutEvent`) complements **User Loggedin Event**.
- External-login forms post to `ExternalAuthenticationsController`, not `AccountController`.

### Reading Settings-Backed Options

```csharp
using Microsoft.Extensions.Options;
using OrchardCore.Users.Models;

public sealed class RegistrationGate
{
    private readonly IOptionsMonitor<RegistrationOptions> _registrationOptions;

    public RegistrationGate(IOptionsMonitor<RegistrationOptions> registrationOptions)
    {
        _registrationOptions = registrationOptions;
    }

    public bool RequiresEmailConfirmation()
        => _registrationOptions.CurrentValue.UsersMustValidateEmail;
}
```

### Rate-Limited User Endpoints

When `OrchardCore.RateLimits` is enabled, Users registers per-IP route limits: login (POST, 10, sliding window), registration and external registration (3), link external login (10), forgot and reset password (5), and resend confirmation (2 per minute). See `orchardcore-rate-limits` to tune them.

### Configuration Sections

```json
{
  "OrchardCore": {
    "Users": {
      "LoginPath": "Login",
      "LogoffPath": "Users/LogOff",
      "ChangePasswordUrl": "ChangePassword",
      "ChangePasswordConfirmationUrl": "ChangePasswordConfirmation",
      "ExternalLoginsUrl": "ExternalLogins",
      "TwoFactorAuthenticationPath": "TwoFactor"
    },
    "Roles": {
      "AdminRoleName": "Administrator"
    }
  }
}
```

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
          "PermissionBehavior": "Replace",
          "Permissions": [
            "ViewMyFeature",
            "AccessAdminPanel"
          ]
        }
      ]
    }
  ]
}
```

`PermissionBehavior` is `Replace` (default), `Add` (append missing permissions), or `Remove` (remove listed permissions).

`AssignRoleToUsers` is not implied by `EditUsers`. Grant `AssignRoleToUsers` explicitly, or a role-specific `AssignRoleToUsers_{RoleName}` permission.

### Roles Admin

- `ViewRoles` (implied by `ManageRoles`) gives read-only access to the roles list and a role's permissions.
- The role Create page has **Save & Configure** to go straight to the permission editor; the Edit page has **Save and continue**.
- System roles (`Administrator`, `Anonymous`, `Authenticated`) cannot be deleted, and `Administrator` cannot be edited.
- Permission descriptions are translated with PO files; only context-free descriptions, templated permissions such as "Edit Article", and categories use Data Localization.

### Enabling and Disabling Users

```csharp
await _userService.EnableAsync(user);
await _userService.DisableAsync(user);
```

### Creating Users from a Recipe

```json
{
  "steps": [
    {
      "name": "command",
      "Commands": [
        "createUser /UserName:admin /Password:{{Password}} /Email:admin@example.com /Roles:Administrator"
      ]
    }
  ]
}
```

### Custom User Settings

Use the `CustomUserSettings` stereotype for per-user fields. See `orchardcore-custom-user-settings`.

### Upgrading from 3.x

- `OrchardCore.Users.CommonPermissions` was removed; use `OrchardCore.Users.UsersPermissions` (same member names).
- `CommonPermissions.CreateAssignRoleToUsersPermission(role)` returned the edit-users-in-role permission in 3.x. `UsersPermissions.CreateAssignRoleToUsersPermission(role)` returns the real assign-role permission; code that relied on the old result must call `CreateEditUsersInRolePermission(role)`.
- `StandardPermissions.SiteOwner` is gone; use the `Administrator` role.
- `Permission.Description` is a `LocalizationSource`; use `.Value` or `IStringLocalizerFactory.Localize(permission.Description)`.
- `IOptions<RegistrationOptions>` and `IOptions<ExternalLoginOptions>` become `IOptionsMonitor<T>` with `CurrentValue`.
- Rename configuration sections `OrchardCore_Users` to `OrchardCore:Users` and `OrchardCore_Roles` to `OrchardCore:Roles` (old names still work but are deprecated).
