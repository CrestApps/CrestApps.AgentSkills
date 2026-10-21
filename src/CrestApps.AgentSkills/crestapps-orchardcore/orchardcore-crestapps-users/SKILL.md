---
name: orchardcore-crestapps-users
description: Skill for the CrestApps Users module in Orchard Core. Covers the dependency-only core feature with user caching and the UserPicker view component, User Display Name with UserFullNamePart, DisplayNameSettings formats, IDisplayNameProvider and the display_name Liquid filter, display-name search and picker labels, User Avatar with UserAvatarOptions read through IOptionsMonitor, Sign in with an email code, the ManageDisplaySettings and ManageAvatarSettings permissions, and the IndexUsers recipe step. Use this skill when requests mention CrestApps user display names, a user picker in an admin screen, user avatars, passwordless email code sign-in, or re-indexing users. Strong matches include CrestApps.OrchardCore.Users, CrestApps.OrchardCore.Users.DisplayName, CrestApps.OrchardCore.Users.Avatars, CrestApps.OrchardCore.Users.EmailCodeSignIn, IDisplayNameProvider, UserFullNamePart, UserAvatarPart, UserAvatarOptions, UserPickerViewComponent and UpdateUserRecipeStepHandler.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Users

## Configure enhanced user experiences

You are an Orchard Core expert. CrestApps Users extends Orchard Core Users with cached user lookups, a reusable user picker, configurable display names, media-backed avatars and passwordless sign-in with an emailed code. The core feature is enabled by dependency only; enable the specific capability you need.

### Guidelines

- Install `CrestApps.OrchardCore.Users` in the web or startup project.
- Enable `CrestApps.OrchardCore.Users.DisplayName`, `CrestApps.OrchardCore.Users.Avatars` and or `CrestApps.OrchardCore.Users.EmailCodeSignIn`. Each brings in the dependency-only `CrestApps.OrchardCore.Users`.
- Resolve user labels through `IDisplayNameProvider.GetAsync(user, cancellationToken)` (namespace `CrestApps.OrchardCore.Users`); never rebuild name formats in views.
- Use the `UserPicker` view component instead of a free-text user id box in admin screens. It needs the `CrestApps.OrchardCore.Resources` feature.
- Read `UserAvatarOptions` through `IOptionsMonitor<UserAvatarOptions>.CurrentValue`. Saving the avatar settings refreshes the options without a tenant reload.
- Avatar editing requires Orchard Core `ManageMedia`, not only permission to edit the user.
- Use the exact `IndexUsers` step name to re-save users after changing user indexes.
- Enable `CrestApps.OrchardCore.Recipes` for `UserFullNamePart`, `DisplayNameSettings` and `UserAvatarOptions` recipe schemas.

### Feature overview

| Feature ID | Dependencies | Purpose |
|---|---|---|
| `CrestApps.OrchardCore.Users` | - | Dependency only. User cache, basic display-name fallback, `UserPicker` and the user search endpoint, user menu display name |
| `CrestApps.OrchardCore.Users.DisplayName` | `OrchardCore.ContentFields` | `UserFullNamePart`, `DisplayNameSettings`, display-name search, picker labels, `display_name` filter |
| `CrestApps.OrchardCore.Users.Avatars` | `OrchardCore.Media` | `UserAvatarPart`, `UserAvatarOptions`, avatar styles |
| `CrestApps.OrchardCore.Users.EmailCodeSignIn` | `OrchardCore.Users`, `OrchardCore.Email` | **Email me a sign-in code** on the login form |

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

### Permissions

`UserPermissions` (namespace `CrestApps.OrchardCore.Users.Core`) declares its descriptions with `LocalizationSource.Create<UserPermissions>(...)`. Both are granted to **Administrator** by default and registered with `AddSiteSettingsPermission` for their settings group, so a role holding only that permission can open the page.

| Permission | Opens |
|---|---|
| `ManageDisplaySettings` | **Settings > User Display Name** |
| `ManageAvatarSettings` | **Settings > User Avatars** |

## Reusable user picker

The core feature ships a `UserPicker` view component that renders through the shared `ItemSelector` component (see `orchardcore-crestapps-resources`). It searches as the user types and resolves the saved values to display names on first render.

```cshtml
@await Component.InvokeAsync("UserPicker", new
{
    name = Html.NameFor(m => m.OwnerUserId).ToString(),
    selectedValues = new[] { Model.OwnerUserId },
    valueType = "userId",
    multiple = false,
    buttonText = T["Select a user"].Value,
    searchPlaceholder = T["Search users"].Value,
})
```

| Parameter | Purpose |
|---|---|
| `name` | Required. Posted field name; use `Html.NameFor(...)` inside a display driver so it binds under the driver's prefix |
| `selectedValues` | Saved values, matching `valueType` |
| `valueType` | `userId` (default), `userName` or `normalizedUserName` |
| `multiple` | Allow several users |
| `roles` | Restrict results to these role names |
| `label`, `buttonText`, `searchPlaceholder`, `smallButton` | Display options |

The picker calls `GET Admin/api/crestapps/users/search` (route name `CrestApps.Users.Search`) with `query`, `valueType` and `role`. It requires an authenticated user, matches the normalized user name, and returns up to 50 enabled users as `{ value, text, isEnabled }` with `text` from `IDisplayNameProvider`.

## User display names

`DisplayNameSettings` (namespace `CrestApps.OrchardCore.Users.Core.Models`) is edited at **Settings > User Display Name**.

| `Type` (`DisplayNameType`) | Result |
|---|---|
| `Username` (default) | The user name |
| `DisplayName` | `UserFullNamePart.DisplayName`, or the user name when empty |
| `FirstThenLast` | First, middle (when used) and last name, or the user name when all are empty |
| `LastThenFirst` | `Last, First Middle`, or the user name when all are empty |
| `Other` | The Liquid `Template`, with `User`, `FirstName`, `MiddleName`, `LastName`, `DisplayName` and `UserName` in scope; it must validate before it saves |

`DisplayName`, `FirstName`, `MiddleName` and `LastName` on the settings are each `None`, `Optional` or `Required` (`DisplayNamePropertyType`), controlling whether the user editor shows and validates the box. The name boxes show only to users who hold `EditUsers` for that user. Saving the settings removes the `user-display-name` cache tag (`UsersConstants.UserDisplayNameCacheTag`).

```json
{
  "steps": [
    {
      "name": "Settings",
      "DisplayNameSettings": {
        "Type": "FirstThenLast",
        "DisplayName": "None",
        "FirstName": "Required",
        "MiddleName": "Optional",
        "LastName": "Required"
      }
    }
  ]
}
```

### Resolve a display name

```csharp
using CrestApps.OrchardCore.Users;
using OrchardCore.Users;

namespace MyModule.Services;

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

With `OrchardCore.Liquid` enabled:

```liquid
{{ Model.User | display_name }}
```

Without the Display Name feature, the core feature's basic provider returns the user name.

### Data, search and pickers

- `UserFullNamePart` (`DisplayName`, `FirstName`, `MiddleName`, `LastName`) is stored on the Orchard Core `User`, edited by `UserFullNamePartDisplayDriver`, and indexed by `UserFullNameIndexProvider`.
- With `OrchardCore.Users`, the users admin list search also matches display, first, middle and last names (`DisplayNameUsersAdminListFilterProvider`).
- `UserPickerField` results use display names (`DisplayNameUserPickerResultProvider` replaces the default provider), and the field display driver is replaced except for the `UserNames` editor.
- Shapes `UserDisplayNameText` and `UserDisplayNameIcon` gain `__DisplayText` and `__DisplayIcon` alternates (also per display type) for theming.

## User avatars

Configure **Settings > User Avatars**. `UserAvatarOptions` has `Required` and `UseDefaultStyle` (default `true`). `UserAvatarPart` holds a `MediaField` named `Avatar`; the editor accepts only image extensions from `MediaOptions.AllowedFileExtensions` and is shown only to users with `ManageMedia`.

```csharp
using CrestApps.OrchardCore.Users.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Users.Models;

namespace MyModule.Services;

public sealed class AvatarReader
{
    private readonly IOptionsMonitor<UserAvatarOptions> _avatarOptions;

    public AvatarReader(IOptionsMonitor<UserAvatarOptions> avatarOptions)
    {
        _avatarOptions = avatarOptions;
    }

    public bool IsAvatarRequired => _avatarOptions.CurrentValue.Required;

    public IReadOnlyList<string> GetPaths(User user)
        => user.As<UserAvatarPart>()?.Avatar?.Paths ?? [];
}
```

```json
{
  "steps": [
    {
      "name": "Settings",
      "UserAvatarOptions": {
        "Required": false,
        "UseDefaultStyle": true
      }
    }
  ]
}
```

## Sign in with an email code

`CrestApps.OrchardCore.Users.EmailCodeSignIn` adds **Email me a sign-in code** to the login form (`EmailCodeLoginFormDisplayDriver`) and the pages `sign-in/email` (route `EmailSignIn`) and `sign-in/email/code` (route `EmailSignInCode`).

- `EmailSignInCodes` stores a SHA-256 hash of each six-digit code in `IDistributedCache`, per tenant, for 10 minutes and 5 attempts.
- Sends are limited to 3 per email and 10 per client address every 15 minutes.
- The request page answers the same way for any email, and the email never appears in a URL.
- Disabled or locked-out users, and users with two-factor authentication, are not signed in by a code. A successful code confirms the email.
- Use a distributed cache (for example Redis) when the site runs on several instances.

## Re-index users with a recipe

`UpdateUserRecipeStepHandler` handles `IndexUsers` when `OrchardCore.Users` is enabled. It re-saves enabled users by default; `IncludeDisabledUsers` includes disabled ones. `BatchSize` defaults to 250 when absent or not positive, and is capped at 1000.

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

## Upgrading from 2.x

- `UserAvatarOptions` is refreshed through `IOptionsMonitor` and the options update notifier; saving **User Avatars** no longer reloads the tenant. Code that injected `IOptions<UserAvatarOptions>` keeps the first value until restart and must switch to `IOptionsMonitor<UserAvatarOptions>` (or `IOptionsSnapshot` in a scoped service).
- Permission descriptions use `LocalizationSource`, so translate them from PO files with the declaring type as context.
- The display name and avatar settings groups now register their permission, so roles with only `ManageDisplaySettings` or `ManageAvatarSettings` can open those pages.
- New: the `UserPicker` view component (over the existing user search endpoint) and the Email Code Sign-in feature.

## Troubleshooting

- `display_name` is missing: enable User Display Name and `OrchardCore.Liquid`.
- Picker labels stay user names: enable Display Name and fill `UserFullNamePart` data or pick a format that uses it.
- The user picker renders as a plain select: enable `CrestApps.OrchardCore.Resources` and Orchard Core `OrchardCore.Resources`.
- Avatar box missing: check `OrchardCore.Media`, the Avatars feature and the editor's `ManageMedia` permission.
- A custom Liquid format will not save: fix the template validation errors.
- Email code limits seem per instance: configure a shared distributed cache.
