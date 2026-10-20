# Users & Roles Examples

## Example 1: Custom Permissions and Provider

```csharp
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace MyShop.Core;

public static class ShopPermissions
{
    public static readonly Permission ManageProducts = new(
        "ManageProducts",
        LocalizationSource.Create("Manage product catalog", typeof(ShopPermissions)));

    public static readonly Permission ViewProducts = new(
        "ViewProducts",
        LocalizationSource.Create("View product catalog", typeof(ShopPermissions)),
        [ManageProducts]);

    public static readonly Permission ManageOrders = new(
        "ManageOrders",
        LocalizationSource.Create("Manage customer orders", typeof(ShopPermissions)),
        isSecurityCritical: true);

    public static readonly Permission ViewOrders = new(
        "ViewOrders",
        LocalizationSource.Create("View customer orders", typeof(ShopPermissions)),
        [ManageOrders]);
}
```

```csharp
using MyShop.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace MyShop;

internal sealed class PermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        ShopPermissions.ManageProducts,
        ShopPermissions.ViewProducts,
        ShopPermissions.ManageOrders,
        ShopPermissions.ViewOrders,
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
            Permissions = [ShopPermissions.ViewProducts, ShopPermissions.ViewOrders],
        },
        new PermissionStereotype
        {
            Name = "Contributor",
            Permissions = [ShopPermissions.ViewProducts],
        },
    ];
}
```

Register with `services.AddPermissionProvider<PermissionProvider>();`.

## Example 2: Roles Recipe

```json
{
  "steps": [
    {
      "name": "Roles",
      "Roles": [
        {
          "Name": "ProductManager",
          "Description": "Can manage products and view orders",
          "PermissionBehavior": "Replace",
          "Permissions": [
            "ManageProducts",
            "ViewOrders",
            "AccessAdminPanel"
          ]
        },
        {
          "Name": "OrderProcessor",
          "Description": "Can manage orders",
          "Permissions": [
            "ManageOrders",
            "ViewProducts",
            "AccessAdminPanel"
          ]
        },
        {
          "Name": "Authenticated",
          "PermissionBehavior": "Add",
          "Permissions": [
            "ViewProducts"
          ]
        }
      ]
    }
  ]
}
```

## Example 3: Role Manager With Read-Only Roles Access

`AssignRoleToUsers` is not implied by `EditUsers`, and `ViewRoles` grants read-only access to roles:

```json
{
  "steps": [
    {
      "name": "Roles",
      "Roles": [
        {
          "Name": "UserManager",
          "Permissions": [
            "ListUsers",
            "EditUsers",
            "AssignRoleToUsers_Editor",
            "ViewRoles",
            "AccessAdminPanel"
          ]
        }
      ]
    }
  ]
}
```

In code, build the same role-specific permission with `UsersPermissions.CreateAssignRoleToUsersPermission("Editor")`.

## Example 4: Login, Logout, and Registration Events

```csharp
using Microsoft.Extensions.Logging;
using OrchardCore.Users;
using OrchardCore.Users.Events;

namespace MyModule;

public sealed class LogoutAuditEvent : LogoutFormEventBase
{
    private readonly ILogger _logger;

    public LogoutAuditEvent(ILogger<LogoutAuditEvent> logger)
    {
        _logger = logger;
    }

    public override Task LoggedOutAsync(IUser user, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User {UserName} signed out.", user?.UserName);

        return Task.CompletedTask;
    }
}

public sealed class RegistrationEvents : RegistrationFormEventsBase
{
    public override Task RegisteringAsync(UserRegisteringContext context)
    {
        // Validate or change the registration context.
        return Task.CompletedTask;
    }
}
```

```csharp
services.AddScoped<ILogoutFormEvent, LogoutAuditEvent>();
services.AddScoped<IRegistrationFormEvents, RegistrationEvents>();
```

## Example 5: Persistent Sign-In Without a Remember Me Checkbox

```json
{
  "steps": [
    {
      "name": "Settings",
      "LoginSettings": {
        "AllowRememberMe": false,
        "UsePersistentAuthenticationCookie": true
      }
    }
  ]
}
```

Every local and external sign-in now survives browser restarts.

## Example 6: External Login Scripts

Generate a username from the external claims (**Generate username** script):

```javascript
var email = context.externalClaims.find(c => c.type.endsWith('/emailaddress'));
context.userName = email ? email.value.split('@')[0] : '';
```

Sync roles on each external login (**Sync properties** script):

```javascript
if (context.loginProvider === 'Google' && context.userRoles.indexOf('Customer') === -1) {
    context.rolesToAdd.push('Customer');
}
```

Both scripts run against the `context` object that Orchard Core hands over through `externalLoginContext()`.

## Example 7: Enable or Disable a User

```csharp
await _userService.EnableAsync(user);
await _userService.DisableAsync(user);
```

Use `has_permission` in Liquid permission checks. Do not depend on permission claims for administrators.
