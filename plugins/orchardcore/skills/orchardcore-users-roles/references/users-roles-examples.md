# Users & Roles Examples

## Example 1: Custom Permission Provider

```csharp
using OrchardCore.Security.Permissions;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManageProducts =
        new("ManageProducts", "Manage product catalog");

    public static readonly Permission ViewProducts =
        new("ViewProducts", "View product catalog", new[] { ManageProducts });

    public static readonly Permission ManageOrders =
        new("ManageOrders", "Manage customer orders");

    public static readonly Permission ViewOrders =
        new("ViewOrders", "View customer orders", new[] { ManageOrders });

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        return Task.FromResult<IEnumerable<Permission>>(new[]
        {
            ManageProducts,
            ViewProducts,
            ManageOrders,
            ViewOrders
        });
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return new[]
        {
            new PermissionStereotype
            {
                Name = "Administrator",
                Permissions = new[] { ManageProducts, ManageOrders }
            },
            new PermissionStereotype
            {
                Name = "Editor",
                Permissions = new[] { ViewProducts, ViewOrders }
            },
            new PermissionStereotype
            {
                Name = "Contributor",
                Permissions = new[] { ViewProducts }
            }
        };
    }
}
```

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
          "Permissions": [
            "ManageProducts",
            "ViewProducts",
            "ViewOrders",
            "AccessAdminPanel"
          ]
        },
        {
          "Name": "OrderProcessor",
          "Description": "Can manage orders",
          "Permissions": [
            "ManageOrders",
            "ViewOrders",
            "ViewProducts",
            "AccessAdminPanel"
          ]
        },
        {
          "Name": "Customer",
          "Description": "Registered customer with basic access",
          "Permissions": [
            "ViewProducts",
            "ViewOwnOrders"
          ]
        }
      ]
    }
  ]
}
```

## Example 3: Custom User Settings

```csharp
// Migration to create a custom user profile
public int Create()
{
    _contentDefinitionManager.AlterTypeDefinition("UserProfile", type => type
        .DisplayedAs("User Profile")
        .Stereotype("CustomUserSettings")
        .WithPart("UserProfile", part => part
            .WithPosition("0")
        )
    );

    _contentDefinitionManager.AlterPartDefinition("UserProfile", part => part
        .WithField("FirstName", field => field
            .OfType("TextField")
            .WithDisplayName("First Name")
            .WithPosition("0")
        )
        .WithField("LastName", field => field
            .OfType("TextField")
            .WithDisplayName("Last Name")
            .WithPosition("1")
        )
        .WithField("ProfilePicture", field => field
            .OfType("MediaField")
            .WithDisplayName("Profile Picture")
            .WithPosition("2")
        )
        .WithField("Bio", field => field
            .OfType("TextField")
            .WithDisplayName("Bio")
            .WithEditor("TextArea")
            .WithPosition("3")
        )
    );

    return 1;
}
```

## Example 4: Registration and Login Events

Orchard Core 3.0 uses event contracts for registration and login
customization:

```csharp
public sealed class RegistrationEvents : RegistrationFormEventsBase
{
    public override Task RegisteringAsync(UserRegisteringContext context)
    {
        return Task.CompletedTask;
    }
}
```

`ILoginFormEvent.ValidatingLoginAsync(IUser user)` validates a user during
login. The external-login form action is now on
`ExternalAuthenticationsController`, not `AccountController`.

## Example 5: Explicit Role Assignment Permission

`AssignRoleToUsers` is no longer implied by `EditUsers`. Include it explicitly
when importing a role that must assign roles:

```json
{
  "steps": [
    {
      "name": "Roles",
      "Roles": [
        {
          "Name": "UserManager",
          "Permissions": [
            "EditUsers",
            "AssignRoleToUsers"
          ]
        }
      ]
    }
  ]
}
```

Use `User | has_permission` in Liquid permission checks. Do not depend on
permission claims for administrators.

## Example 6: Enable or Disable a User

Use the 3.0 user service methods instead of an edit-form checkbox:

```csharp
await _userService.EnableAsync(user);
await _userService.DisableAsync(user);
```
