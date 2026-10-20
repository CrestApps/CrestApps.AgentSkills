---
name: orchardcore-custom-user-settings
description: Skill for creating custom user settings sections in Orchard Core. Covers the CustomUserSettings stereotype, tab-based organization in the user editor, per-type ManageOwnCustomUserSettings permissions, Liquid and code access through user Properties and CustomUserSettingsService, SummaryAdmin display in the users list, placement customization, the custom-user-settings recipe step and deployment step, and SectionDisplayDriver extension. Use this skill when requests mention Orchard Core Custom User Settings, Creating a Custom User Settings Type, Creating via Recipe, Importing user settings values, Accessing Custom User Settings in Liquid, showing user profile fields in the users list, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include OrchardCore.Users.CustomUserSettings, CustomUserSettings, CustomUserSettingsService, SectionDisplayDriver, IContentDefinitionManager, and UserProfile.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
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
- Each section gets a `ManageOwnCustomUserSettings_{ContentTypeName}` permission (implied by `ManageUsers`); a section's editor and summary render only for users holding it.
- Access user settings in Liquid by loading the user and accessing `user.Properties`.
- Placement can be customized to move sections out of tabs using the `CustomUserSettings-{ContentTypeName}` differentiator.
- Sections can also be shown in the users admin list (`SummaryAdmin`); they are hidden there by default.
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

After creation, each custom user settings section appears as a tab when editing a user under _Access Control → Users_. This field-only definition does not require a CLR `UserProfile` part or `AddContentPart` registration.

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

### Granting Access to a Section

Users edit their own section only with the matching permission. For a `UserProfile` type:

```json
{
  "steps": [
    {
      "name": "Roles",
      "Roles": [
        {
          "Name": "Authenticated",
          "PermissionBehavior": "Add",
          "Permissions": [
            "ManageOwnCustomUserSettings_UserProfile"
          ]
        }
      ]
    }
  ]
}
```

### Accessing Custom User Settings in Code

```csharp
public sealed class UserProfileService
{
    private readonly UserManager<IUser> _userManager;

    public UserProfileService(UserManager<IUser> userManager)
    {
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

`CustomUserSettingsService` (namespace `OrchardCore.Users.Services`, package `OrchardCore.Users.Core`) returns a section as a `ContentItem` merged with the current type definition, which is safer than reading raw JSON:

```csharp
var settingsType = await _customUserSettingsService.GetSettingsTypeAsync("UserProfile");
var profile = await _customUserSettingsService.GetSettingsAsync(user, settingsType);
var firstName = profile.Content.UserProfile.FirstName.Text;

// All users' sections of a type, keyed by user ID.
var allProfiles = await _customUserSettingsService.GetSettingsAsync("UserProfile");
```

### Importing and Exporting Values

With `OrchardCore.Deployment`, the **Custom User Settings** deployment step (`IncludeAll` or selected `SettingsTypeNames`) exports every user's sections. The matching recipe step is `custom-user-settings`; each entry targets a user by `userId`, and each content item replaces that user's section of the same `ContentType`:

```json
{
  "steps": [
    {
      "name": "custom-user-settings",
      "custom-user-settings": [
        {
          "userId": "4kxfx7ctgc9y6ag0dk1tb7jx0r",
          "user-custom-user-settings": [
            {
              "ContentItemId": "4p1xr5f2k3jvg0q7a2zcbpsnfa",
              "ContentType": "UserProfile",
              "UserProfile": {
                "FirstName": { "Text": "Ada" },
                "LastName": { "Text": "Lovelace" }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

Users that do not exist are skipped. Define the content type first (`ContentDefinition` step) in the same or an earlier recipe.

### Placement Customization

By default, each custom user settings section is placed in its own tab in the user editor. The differentiator uses the content type name (which usually equals the part name). To customize placement (e.g., move out of the tab):

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

The `differentiator` format is `CustomUserSettings-{ContentTypeName}`.

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

#### Show a section in the users admin list

Orchard Core 4.0 builds a `CustomUserSettings_SummaryAdmin` shape per section for the `SummaryAdmin` and `SummaryAdminGrid` display types, hidden by default. Place it to show it in the users list (only for viewers holding the section permission):

```json
{
  "CustomUserSettings_SummaryAdmin": [
    {
      "displayType": "SummaryAdmin",
      "differentiator": "CustomUserSettings-UserProfile",
      "place": "Description:10"
    }
  ]
}
```

The section content item is rendered with the `SummaryAdmin` display type, so add `SummaryAdmin` placement or templates for its fields as needed.

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
        return Initialize<UserProfileViewModel, UserProfile>("UserProfile_Edit", static (viewModel, section) =>
        {
            viewModel.DisplayName = section.DisplayName;
            viewModel.Location = section.Location;
        }, section).Location("Content:5#Profile");
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
        services.AddDisplayDriver<User, UserProfileDisplayDriver>();
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
