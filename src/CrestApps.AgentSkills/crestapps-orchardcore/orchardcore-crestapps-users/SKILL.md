---
name: orchardcore-crestapps-users
description: Skill for CrestApps user extensions in Orchard Core. Covers user display names, UserFullNamePart, display-name formats and Liquid filter, user cache behavior, enhanced user search and picker labels, avatar configuration, permissions, and the IndexUsers recipe step. Use this skill when requests mention CrestApps user display names, UserFullNamePart, user avatars, display_name Liquid filter, enhanced user picker, IndexUsers recipe step, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.Users, CrestApps.OrchardCore.Users.DisplayName, CrestApps.OrchardCore.Users.Avatars, IDisplayNameProvider, DisplayNameProvider, UserFullNamePart, UserAvatarPart, and UpdateUserRecipeStepHandler.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Users

## Configure enhanced user experiences

You are an Orchard Core expert. Use CrestApps Users to add cached user components, configurable display names, user-name-aware searching and picker labels, and media-backed user avatars. Treat the core feature as an internal dependency feature and enable the specific Display Name or Avatar capability required by the tenant.

### Guidelines

- Install `CrestApps.OrchardCore.Users` in the web or startup project.
- `CrestApps.OrchardCore.Users` is enabled by dependency only. Enable the exact public feature IDs `CrestApps.OrchardCore.Users.DisplayName` and or `CrestApps.OrchardCore.Users.Avatars`.
- Display Name depends on `OrchardCore.ContentFields` and the CrestApps Users core feature.
- Avatars depend on `OrchardCore.Media` and the CrestApps Users core feature.
- `UserFullNamePart` stores `DisplayName`, `FirstName`, `MiddleName`, and `LastName` on the Orchard Core `User`.
- Resolve a user label through `IDisplayNameProvider.GetAsync()` rather than reproducing name-format logic in every view.
- The display-name provider falls back to `UserName` when the user is not an Orchard Core `User`, the full-name part is absent, or no configured name can be generated.
- Configure display-name format under **Settings → User Display Name**. Its custom format option uses Liquid and must validate before it is saved.
- With `OrchardCore.Liquid` enabled, use the `display_name` Liquid filter to resolve the configured display name.
- The Display Name feature replaces the default user-picker result provider and extends the users admin list search to display name and full-name fields.
- The avatar editor requires `MediaPermissions.ManageMedia`. It restricts allowed media types to image extensions from Orchard Core media options.
- Use `IndexUsers` to resave users in batches when user indexes must be refreshed. The handler caps a positive requested batch size at 1000.
- Enable CrestApps Recipes when you need `UserFullNamePart` schema support in content recipes.

### Feature overview

| Feature ID | Purpose |
|---|---|
| `CrestApps.OrchardCore.Users` | Dependency-only core user caching and basic display-name fallback |
| `CrestApps.OrchardCore.Users.DisplayName` | Full-name part, display-name settings, picker labels, search, and Liquid filter |
| `CrestApps.OrchardCore.Users.Avatars` | Media-backed `UserAvatarPart`, settings, navigation, and permissions |

### Enable display names and avatars

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Users.DisplayName",
        "CrestApps.OrchardCore.Users.Avatars",
        "OrchardCore.Liquid"
      ],
      "disable": []
    }
  ]
}
```

Enable only the feature needed by the application. The manifest automatically activates each feature's required dependency features.

## Configure user display names

`DisplayNameSettings` offers these display formats:

| `DisplayNameType` | Result |
|---|---|
| `Username` | Orchard Core user name |
| `FirstThenLast` | First, optional middle, then last name |
| `LastThenFirst` | Last name followed by first and optional middle name |
| `DisplayName` | Explicit `UserFullNamePart.DisplayName` |
| `Other` | Validated custom Liquid template |

Each field can be `None`, `Optional`, or `Required`. Required fields are checked by `UserFullNamePartDisplayDriver` when an administrator edits a user.

### Configure it in the admin UI

1. Enable **User Display Name**.
2. Navigate to **Configuration → Settings → User Display Name**.
3. Choose a format and whether display, first, middle, and last names are optional or required.
4. For **Custom format**, enter a valid Liquid template.
5. Save the settings.

Changing the format invalidates the `user-display-name` cache tag. Do not hardcode name composition in templates or controllers.

### Resolve a display name in C#

```csharp
using CrestApps.OrchardCore.Users;
using OrchardCore.Users;

namespace MyModule;

public sealed class UserLabelService
{
    private readonly IDisplayNameProvider _displayNameProvider;

    public UserLabelService(IDisplayNameProvider displayNameProvider)
    {
        _displayNameProvider = displayNameProvider;
    }

    public Task<string> GetLabelAsync(IUser user, CancellationToken cancellationToken)
        => _displayNameProvider.GetAsync(user, cancellationToken);
}
```

### Resolve it in Liquid

```liquid
{{ Model.User | display_name }}
```

The filter is registered only when `OrchardCore.Liquid` is enabled. It uses the same `DisplayNameProvider` behavior as C# consumers.

## Full-name data and recipe schema

The Display Name feature registers `UserFullNamePart` as a section on the Orchard Core user editor, a data migration, and a user full-name index. It also adds recipe schema support when CrestApps Recipes is enabled.

```json
{
  "steps": [
    {
      "name": "content",
      "data": [
        {
          "ContentType": "User",
          "UserName": "jane.doe",
          "UserFullNamePart": {
            "DisplayName": "Jane Doe",
            "FirstName": "Jane",
            "MiddleName": "",
            "LastName": "Doe"
          }
        }
      ]
    }
  ]
}
```

Use the enabled tenant's Users recipe behavior for user creation and updates. The `UserFullNamePart` schema describes the full-name payload; it does not bypass the normal user-management permissions or password and identity requirements.

## Configure user avatars

Enable **User Avatar**, then configure it in **Configuration → Settings → User Avatars**. `UserAvatarOptions.Required` controls whether an avatar must be selected. `UseDefaultStyle` defaults to `true`.

`UserAvatarPart` stores an Orchard Core `MediaField` named `Avatar`. The editor permits image media types based on the tenant's `MediaOptions.AllowedFileExtensions`, supports anchors and media text, and prevents multiple selection when the configured field settings disallow it.

```csharp
using CrestApps.OrchardCore.Users.Models;
using OrchardCore.Users.Models;

namespace MyModule;

public sealed class AvatarReader
{
    public IReadOnlyList<string> GetPaths(User user)
        => user.As<UserAvatarPart>()?.Avatar?.Paths ?? [];
}
```

Avatar editing is authorized with Orchard Core `ManageMedia`, not merely the permission to edit a user. Keep this separation when adding custom avatar UI.

## Re-index users with a recipe

`UpdateUserRecipeStepHandler` registers the exact step name `IndexUsers` whenever `OrchardCore.Users` is enabled. It re-saves enabled users by default. `IncludeDisabledUsers` changes that behavior, and `BatchSize` defaults to 250 when absent or nonpositive. A supplied positive size is capped at 1000.

```json
{
  "steps": [
    {
      "name": "IndexUsers",
      "IncludeDisabledUsers": false,
      "BatchSize": 250
    }
  ]
}
```

Use `IndexUsers` after introducing or correcting an index based on user properties, not as a replacement for normal user-edit workflows.

## Search and picker behavior

When the Display Name feature and `OrchardCore.Users` are enabled:

- The users administration list can search display, first, middle, and last names.
- `UserPickerField` uses display names in results through `DisplayNameUserPickerResultProvider`.
- The standard user-picker field display driver is replaced except for the `UserNames` editor.

When fallback behavior is sufficient, leave Display Name disabled. The core feature still provides a basic `IDisplayNameProvider` that resolves to the ordinary user name.

## Troubleshooting

- If the `display_name` filter is unavailable, enable both User Display Name and `OrchardCore.Liquid`.
- If user picker labels remain usernames, check that Display Name is enabled and the edited users have `UserFullNamePart` data.
- If avatar upload is blocked, verify `OrchardCore.Media`, `CrestApps.OrchardCore.Users.Avatars`, and the caller's `ManageMedia` permission.
- Use the exact `IndexUsers` step name. Lowercase `indexUsers` in older documentation does not match the current handler registration.
- If a custom Liquid display-name format will not save, fix the template validation errors rather than storing an unvalidated template.
