---
name: orchardcore-custom-user-settings
description: Skill for creating custom user settings sections in Orchard Core. Covers CustomUserSettings stereotype, tab-based organization for user editor, Liquid access patterns for user properties, placement customization, and SectionDisplayDriver extension. Use this skill when requests mention Orchard Core Custom User Settings, Create Custom User Settings, Enabling Custom User Settings, Creating a Custom User Settings Type, Creating via Recipe, Accessing Custom User Settings in Liquid, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Users.CustomUserSettings, OrchardCore.Users, CustomUserSettings, SectionDisplayDriver, DataMigration, IContentDefinitionManager, UserProfile, WithPart. It also helps with Creating via Recipe, Accessing Custom User Settings in Liquid, Accessing Custom User Settings in Code, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Custom User Settings - Prompt Templates

## Create Custom User Settings

You are an Orchard Core expert. Generate code and configuration for creating custom per-user settings sections using the `CustomUserSettings` stereotype.

### Guidelines

- Enable the `OrchardCore.Users.CustomUserSettings` feature to use custom user settings.
- Custom user settings are organized in sections, each represented by a content type with the `CustomUserSettings` stereotype.
- When creating a custom user settings content type, disable `Creatable`, `Listable`, `Draftable`, and `Securable` metadata — they do not apply.
- Do **not** mark existing content types with the `CustomUserSettings` stereotype; this will break existing content items of that type.
- Each section appears as a separate tab in the user editor (_Access Control → Users → Edit_).
- Custom user settings are composed of parts and fields like any other content type.
- Access user settings in Liquid by loading the user and accessing `user.Properties`.
- Placement can be customized to move sections out of tabs using the `CustomUserSettings-PartDefinitionName` differentiator.
- Extend user properties programmatically using `SectionDisplayDriver<User, TSection>`.
- All C# classes must use the `sealed` modifier, except View Models.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.

### Enabling Custom User Settings

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Users",
        "OrchardCore.Users.CustomUserSettings"
      ],
      "disable": []
    }
  ]
}
```

### Creating a Custom User Settings Type

#### Step 1: Define the Content Type with CustomUserSettings Stereotype

Use a data migration to create the content type:

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("UserProfile", type => type
            .DisplayedAs("User Profile")
            .Stereotype("CustomUserSettings")
            .WithPart("UserProfile", part => part
                .WithPosition("0")
            )
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync("UserProfile", part => part
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
            .WithField("Bio", field => field
                .OfType("TextField")
                .WithDisplayName("Biography")
                .WithPosition("2")
                .WithEditor("TextArea")
            )
            .WithField("Avatar", field => field
                .OfType("MediaField")
                .WithDisplayName("Profile Picture")
                .WithPosition("3")
            )
        );

        return 1;
    }
}
```

#### Step 2: Register Services in Startup

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<UserProfile>();
    }
}
```

After creation, each custom user settings section appears as a tab when editing a user under _Access Control → Users_.

### Creating via Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "UserProfile",
          "DisplayName": "User Profile",
          "Settings": {
            "ContentTypeSettings": {
              "Stereotype": "CustomUserSettings"
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "UserProfile",
              "Name": "UserProfile",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "0"
                }
              }
            }
          ]
        }
      ],
      "ContentParts": [
        {
          "Name": "UserProfile",
          "Settings": {},
          "ContentPartFieldDefinitionRecords": [
            {
              "FieldName": "TextField",
              "Name": "FirstName",
              "Settings": {
                "ContentPartFieldSettings": {
                  "DisplayName": "First Name",
                  "Position": "0"
                }
              }
            },
            {
              "FieldName": "TextField",
              "Name": "LastName",
              "Settings": {
                "ContentPartFieldSettings": {
                  "DisplayName": "Last Name",
                  "Position": "1"
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

### Accessing Custom User Settings in Liquid

Load the user and access properties through the `Properties` object:

```liquid
{% assign user = User | user_id | users_by_id %}
{{ user.Properties.UserProfile.UserProfile.FirstName.Text }}
{{ user.Properties.UserProfile.UserProfile.LastName.Text }}
{{ user.Properties.UserProfile.UserProfile.Bio.Text }}
```

The property path follows the pattern: `user.Properties.{ContentTypeName}.{PartName}.{FieldName}.{ValueProperty}`.

### Accessing Custom User Settings in Code

```csharp
public sealed class UserProfileService
{
    private readonly IUserService _userService;
    private readonly UserManager<IUser> _userManager;

    public UserProfileService(IUserService userService, UserManager<IUser> userManager)
    {
        _userService = userService;
        _userManager = userManager;
    }

    public async Task<string> GetFirstNameAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is not User orchardUser)
        {
            return null;
        }

        var profileContent = orchardUser.Properties["UserProfile"];

        if (profileContent == null)
        {
            return null;
        }

        return profileContent["UserProfile"]?["FirstName"]?["Text"]?.ToString();
    }
}
```

### Placement Customization

By default, each custom user settings section is placed in its own tab in the user editor. To customize placement (e.g., move out of the tab):

```json
{
  "CustomUserSettings": [
    {
      "place": "Content:10#Content",
      "differentiator": "CustomUserSettings-UserProfile"
    }
  ]
}
```

The `differentiator` format is `CustomUserSettings-{PartDefinitionName}`.

### Placement Examples

#### Move section to main content area (no tab)

```json
{
  "CustomUserSettings": [
    {
      "place": "Content:5",
      "differentiator": "CustomUserSettings-UserProfile"
    }
  ]
}
```

#### Place section in a custom tab

```json
{
  "CustomUserSettings": [
    {
      "place": "Content:10#Profile",
      "differentiator": "CustomUserSettings-UserProfile"
    }
  ]
}
```

### SectionDisplayDriver Extension

For more advanced scenarios, extend user properties programmatically by implementing a `SectionDisplayDriver<User, TSection>`:

```csharp
public sealed class UserProfile
{
    public string DisplayName { get; set; }

    public string Location { get; set; }
}
```

```csharp
public sealed class UserProfileDisplayDriver : SectionDisplayDriver<User, UserProfile>
{
    public override IDisplayResult Edit(User model, UserProfile section, BuildEditorContext context)
    {
        return Initialize<UserProfileViewModel>("UserProfile_Edit", viewModel =>
        {
            viewModel.DisplayName = section.DisplayName;
            viewModel.Location = section.Location;
        }).Location("Content:5#Profile");
    }

    public override async Task<IDisplayResult> UpdateAsync(User model, UserProfile section, UpdateEditorContext context)
    {
        var viewModel = new UserProfileViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        section.DisplayName = viewModel.DisplayName;
        section.Location = viewModel.Location;

        return await EditAsync(model, section, context);
    }
}
```

Register the driver in `Startup`:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayDriver<User>, UserProfileDisplayDriver>();
    }
}
```

### View Model (Not Sealed)

View Models must **not** use the `sealed` modifier because they are used for model binding:

```csharp
public class UserProfileViewModel
{
    public string DisplayName { get; set; }

    public string Location { get; set; }
}
```

### Important Notes

- Custom user settings are per-user, not site-wide. For site-wide settings, use `CustomSettings` instead.
- The `CustomUserSettings` stereotype is separate from `CustomSettings` — do not confuse the two.
- Content types with the `CustomUserSettings` stereotype should not have `Creatable`, `Listable`, `Draftable`, or `Securable` enabled.
- The user must be loaded from the database to access custom user settings; they are not available on the `ClaimsPrincipal`.
