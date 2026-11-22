---
name: orchardcore-crestapps-users
description: Skill for the CrestApps Users module in Orchard Core. Covers user caching, the UserPicker view component and IUserSearchService with audiences, User Display Name with UserFullNamePart and the display_name filter, User Avatar with IUserAvatarProvider, Sign in with an email code, User Audiences with rule sources and the UserAudiences step, Cohorts with ICohortService and the UserCohorts step, the My Account area, the site Notifications pages, IPasswordConfirmation with [ConfirmPassword], IRoleGrantService and the IndexUsers step. Use this skill when requests mention CrestApps user display names, a user picker, user avatars, passwordless sign-in, audiences, cohorts, the /account area, re-confirming a password or re-indexing users. Strong matches include CrestApps.OrchardCore.Users, CrestApps.OrchardCore.Users.Audiences, CrestApps.OrchardCore.Users.Cohorts, CrestApps.OrchardCore.Users.Account, IDisplayNameProvider, IUserAudienceProvider, IUserAudienceRuleSource, ICohortService and IUserSearchService.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Users

## Configure enhanced user experiences

You are an Orchard Core expert. CrestApps Users extends Orchard Core Users with cached user lookups, a reusable user picker, configurable display names, media-backed avatars, passwordless sign-in with an emailed code, rule-based user audiences, hand-kept cohorts, a front-end "my account" area, front-end notification pages and password re-confirmation for private pages. The core feature is enabled by dependency only; enable the specific capability you need.

### Guidelines

- Install `CrestApps.OrchardCore.Users` in the web or startup project.
- Enable `CrestApps.OrchardCore.Users.DisplayName`, `CrestApps.OrchardCore.Users.Avatars`, `CrestApps.OrchardCore.Users.EmailCodeSignIn`, `CrestApps.OrchardCore.Users.Audiences` and or `CrestApps.OrchardCore.Users.Cohorts`. Each brings in the dependency-only `CrestApps.OrchardCore.Users`. `CrestApps.OrchardCore.Users.Account` is dependency-only too; depend on it from a module that adds account pages.
- Target groups of people through audiences (`IUserAudienceService`, `UserSearchQuery.Audience`) instead of hard-coding role lists; add a rule kind with `IUserAudienceRuleSource`.
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
| `CrestApps.OrchardCore.Users.Audiences` | `OrchardCore.Users`, `OrchardCore.Roles` | Rule-based audiences, **Content > User Audiences**, `UserAudiences` step, **In a user audience** rule condition |
| `CrestApps.OrchardCore.Users.Cohorts` | `OrchardCore.Users`, `CrestApps.OrchardCore.Users.Audiences` | Hand-kept cohorts, **Content > Cohorts**, `CohortMember` audience rule, `UserCohorts` step |
| `CrestApps.OrchardCore.Users.Account` | `OrchardCore.Users` | Dependency only. The `/account` area, its `commerce-account` menu and page frame |

With Orchard Core `OrchardCore.Notifications` enabled, the core feature also adds the front-end `/notifications` and `/notifications/preferences` pages. See [account and notifications](references/account-and-notifications.md).

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

`UserPermissions` (a static class in namespace `CrestApps.OrchardCore.Users.Core`) declares its descriptions with `LocalizationSource.Create("...", typeof(UserPermissions))`. Feature IDs are in `UsersConstants.Feature` (`CrestApps.OrchardCore.Users.Abstractions`). All are granted to **Administrator** by default. The two settings permissions are registered with `AddSiteSettingsPermission` for their settings group, so a role holding only that permission can open the page.

| Permission | Opens |
|---|---|
| `ManageDisplaySettings` | **Settings > User Display Name** |
| `ManageAvatarSettings` | **Settings > User Avatars** |
| `ManageUserAudiences` | **Content > User Audiences** |
| `ManageCohorts` | **Content > Cohorts** |

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
| `audience` | Restrict results to a code audience (`IUserAudienceProvider.Name`), such as `Staff` or `StaffUnlinked` from `orchardcore-staff`; ignored when no provider has that name |
| `required` | Mark the selector required |
| `label`, `buttonText`, `searchPlaceholder`, `smallButton` | Display options |

The picker calls `GET Admin/api/crestapps/users/search` (route name `CrestApps.Users.Search`) with `query`, `valueType`, `role` and `audience`. It requires the `SearchUsers` permission (**Search users in user pickers**, `UserSearchPermissions.SearchUsers`, implied by the admin panel permission, so admin roles keep their pickers while a signed-in storefront customer cannot list users) and returns `403` otherwise. It delegates to `IUserSearchService` and returns up to 50 enabled users as `{ value, text, isEnabled }` with `text` from the display name.

## Shared user building blocks

The Users abstractions (`CrestApps.OrchardCore.Users.Abstractions`) hold contracts other modules reuse instead of querying users themselves:

- `IUserSearchService` (registered by the core feature with `services.AddUserSearchService()`): `SearchAsync(UserSearchQuery)` finds enabled users by `Text` (optionally `MatchEmail`), `Roles`, `Audience` and `MaxResults` (default 50), ordered by user name; `GetAsync(userIds)` loads users by id, enabled or not. Results are `UserSearchResult` (`UserId`, `UserName`, `NormalizedUserName`, `Email`, `PhoneNumber`, `DisplayName`, `IsEnabled`, `AvatarUrl`). The service does not authorize; an endpoint that exposes it checks `UserSearchPermissions.SearchUsers` or its own permission first.
- `IRoleGrantService` (register with `services.AddRoleGrants()` from Users.Core): `GrantAsync(userId, roleName, source, sourceId)`, `RevokeAsync(...)` and `ListAsync(userId)` put users in roles on behalf of a source and record each grant (`RoleGrant`). A role is removed only when no source still grants it, and never when the user had it before any grant. Implement `IRoleGrantSource` (`Source`, `ListExistingGrantsAsync`) so grants made before recording are kept. Memberships and Subscriptions use it.
- `IUserAudienceProvider` defines a named code audience by roles: `Name`, `GetRoleNamesAsync()` (role names; empty means no members; `null` means not limited right now) and optional `GetExcludedUserIdsAsync(foundUserIds)` that drops users from a search result (it only receives the users the search found). `UserSearchQuery.Audience` matches only these providers, not the audiences administrators define (those are read through `IUserAudienceService`). The audience's roles are added to `Roles`, so pass one or the other.
- `IUserAvatarProvider.GetAvatarUrlAsync(user)` returns a user's picture URL or `null`. The User Avatar feature registers one; the search fills `UserSearchResult.AvatarUrl` from the first provider that answers.
- The core feature registers `UserPartySource` for the `User` party kind of the shared parties building block (`CrestApps.OrchardCore.Parties.Abstractions`, `IPartyDirectory`), plus `UserBillingProfileSource` (a user is billed under the display name at the user's email) and `UserPartyMemberSource` (a user acts for themselves).

```csharp
public sealed class PlanRoleSync
{
    private readonly IRoleGrantService _roleGrants;

    public PlanRoleSync(IRoleGrantService roleGrants)
    {
        _roleGrants = roleGrants;
    }

    public Task ActivateAsync(string userId, string planId)
        => _roleGrants.GrantAsync(userId, "Premium", "Plan", planId);

    public Task DeactivateAsync(string userId, string planId)
        => _roleGrants.RevokeAsync(userId, "Premium", "Plan", planId);
}
```

### Define a code audience

```csharp
using CrestApps.OrchardCore.Users;

namespace MyModule.Services;

public sealed class VolunteersAudienceProvider : IUserAudienceProvider
{
    public string Name => "Volunteers";

    public Task<IReadOnlyCollection<string>> GetRoleNamesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<string>>(["Volunteer", "VolunteerLead"]);
}
```

Register it with `services.TryAddEnumerable(ServiceDescriptor.Scoped<IUserAudienceProvider, VolunteersAudienceProvider>());`, then pass `audience = "Volunteers"` to the `UserPicker`. It is also listed on **Content > User Audiences** when that feature is on.

## User audiences and cohorts

`CrestApps.OrchardCore.Users.Audiences` lets administrators define audiences: a technical name, a join (`All` or `Any`) and rules from rule sources such as `Role` (Users), `Team` (Teams), `StaffHireDate` and `StaffEmploymentType` (Staff, see `orchardcore-staff`), `SkillHolder`, `CertificationHolder`, `CourseLearner` and `CohortMember`. Membership is resolved when read, through `IUserAudienceService` (`ListAsync`, `GetMembersAsync`, `IsMemberAsync`, `SaveDefinitionAsync`). Modules add a rule kind with `IUserAudienceRuleSource` and `services.AddUserAudienceRuleSource<T>()`, and block deleting an audience they use with `IUserAudienceUsage`. With `OrchardCore.Rules`, the **In a user audience** condition tests the signed-in user.

`CrestApps.OrchardCore.Users.Cohorts` keeps hand-maintained groups of users (`ICohortService`, `ICohortMembershipObserver`, `ICohortUsage`) in the `Cohorts` YesSql collection and adds the `CohortMember` audience rule.

```json
{
  "steps": [
    {
      "name": "UserAudiences",
      "Audiences": [
        {
          "name": "admins-and-editors",
          "displayName": "Admins and editors",
          "join": "Any",
          "rules": [
            { "source": "Role", "values": ["Administrator", "Editor"] }
          ]
        }
      ]
    },
    {
      "name": "UserCohorts",
      "Cohorts": [
        { "Name": "2026 new hires", "Members": ["ana", "ben@example.com"] }
      ]
    }
  ]
}
```

See [audiences and cohorts](references/audiences-and-cohorts.md) for rule sources, service members, a custom rule source and the index declarations.

## Confirm the password before private pages

`IPasswordConfirmation` (Users.Abstractions, registered by the core feature with `services.AddPasswordConfirmation()`) asks the signed-in person for their password again before a page shows private data such as pay. Put `[ConfirmPassword]` (namespace `CrestApps.OrchardCore.Users`, assembly Users.Core) on a controller or action: until confirmed, a GET redirects to **Confirm your password** at `~/Admin/confirm-password` and back; a form posted after the confirmation expired returns to the page it came from. A confirmation is an `HttpOnly`, `SameSite=Strict` cookie protected for that user, valid for `Window` (10 minutes); a wrong password counts toward lockout. Without the Users feature the attribute forbids the request.

```csharp
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;

namespace MyModule.Controllers;

[Admin]
[ConfirmPassword]
public sealed class MyPayController : Controller
{
    public IActionResult Index() => View();
}
```

A display driver checks `IsConfirmedAsync(user)` and links to `GetConfirmationUrl(returnUrl)`. A page that must ask every time it opens checks `IsJustConfirmedAsync(user)`, true only on the first page opened right after confirming (a one-minute cookie the check deletes).

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

- `UserFullNamePart` (`DisplayName`, `FirstName`, `MiddleName`, `LastName`) is stored on the Orchard Core `User`, edited by `UserFullNamePartDisplayDriver`, and indexed by `UserFullNameIndexProvider` (`UserFullNameIndex`, declared `.ExclusiveToDocumentType().SingleRecordPerDocument()` because only `User` documents map and each maps once).
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
- The user search endpoint now requires `SearchUsers` instead of any authenticated user. Roles with the admin panel permission get it implicitly; grant it explicitly to a non-admin role that uses a user picker.
- Search users through `IUserSearchService` rather than querying `UserIndex` directly.
- New: User Audiences, Cohorts, the dependency-only My Account feature (the `/account` area moved from Commerce, names unchanged), the front-end notification pages, `IPasswordConfirmation` with `[ConfirmPassword]`, `IUserAudienceProvider` and `IUserAvatarProvider`. `UserSearchQuery` gained `Audience`; `UserSearchResult` gained `PhoneNumber` and `AvatarUrl`.
- `UserFullNameIndexProvider` and the cohort index providers declare `.ExclusiveToDocumentType().SingleRecordPerDocument()` (YesSql 6.3).

## Troubleshooting

- `display_name` is missing: enable User Display Name and `OrchardCore.Liquid`.
- Picker labels stay user names: enable Display Name and fill `UserFullNamePart` data or pick a format that uses it.
- The user picker renders as a plain select: enable `CrestApps.OrchardCore.Resources` and Orchard Core `OrchardCore.Resources`.
- Avatar box missing: check `OrchardCore.Media`, the Avatars feature and the editor's `ManageMedia` permission.
- A custom Liquid format will not save: fix the template validation errors.
- Email code limits seem per instance: configure a shared distributed cache.
- The user picker returns 403: grant `SearchUsers` (or the admin panel permission) to the role.
- The picker `audience` has no effect: the name must match an `IUserAudienceProvider` (such as `Staff`); audiences defined on **Content > User Audiences** are not used by the search.
- **Content > User Audiences** or **Content > Cohorts** is missing: enable the feature and grant `ManageUserAudiences` or `ManageCohorts`.
- An audience will not delete: something registered as `IUserAudienceUsage` still uses it. A cohort will not delete while an `ICohortUsage` (such as a Learning course) uses it.
- `/notifications` returns 404: enable Orchard Core `OrchardCore.Notifications`.
