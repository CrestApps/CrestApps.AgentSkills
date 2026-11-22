# User audiences and cohorts

## User Audiences (`CrestApps.OrchardCore.Users.Audiences`)

Depends on `OrchardCore.Users`, `OrchardCore.Roles` and `CrestApps.OrchardCore.Users`. **Content > User Audiences** (permission `ManageUserAudiences`, granted to Administrator) lists the audiences modules register in code (`IUserAudienceProvider`, such as `Staff`) beside the ones administrators define, shows the people in each one, and edits the defined ones.

A defined audience (`UserAudienceDefinition`) has a technical `Name` (letters, digits, dots, dashes and underscores; never the name of a module audience), `DisplayName`, `Description`, `Join` (`All` or `Any`, `UserAudienceJoin`) and at least one rule. A rule (`UserAudienceRule`) is a `Source`, the `Values` chosen (a person matches any of them) and, for some sources, a `Number`. Definitions are kept in one `UserAudiencesDocument`; membership is worked out when it is read, so it is always current.

| Rule source | Registered by | Matches |
|---|---|---|
| `Role` | Users | Users in any of the roles |
| `Team` | Teams | Members of the teams today, with their sub-teams |
| `StaffHireDate` | Staff | Active staff linked to a user, hired at least `Number` days ago |
| `StaffEmploymentType` | Staff | Active staff linked to a user, with any of the employment types |
| `SkillHolder` | Skills | People who hold the skills today |
| `CertificationHolder` | Skills | People with a valid record of the certifications |
| `CourseLearner` | Learning | People enrolled in the courses, active or completed |
| `CohortMember` | Users (Cohorts) | People in any of the cohorts |

### Read audiences in code

`IUserAudienceService` (namespace `CrestApps.OrchardCore.Users`), registered by the feature with `services.AddUserAudiences()` (Users.Core):

- `ListAsync()` returns `UserAudienceSummary(Name, DisplayName, IsDefined)` for every audience.
- `GetMembersAsync(name, skip, take)` pages member user ids with the count.
- `IsMemberAsync(name, userId)` checks one person.
- `FindDefinitionAsync`, `SaveDefinitionAsync` (returns the reasons it was refused, empty when saved) and `DeleteDefinitionAsync` manage defined audiences. An audience an `IUserAudienceUsage` reports as used cannot be deleted; register one with `services.AddUserAudienceUsage<T>()`.

### Add a rule source

Register the source from a startup of the module that owns the data. Registering costs nothing when the Audiences feature is off.

```csharp
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.Localization;

namespace MyModule.Audiences;

public sealed class DepartmentAudienceRuleSource : IUserAudienceRuleSource
{
    private readonly IDepartmentStore _departments;

    internal readonly IStringLocalizer S;

    public DepartmentAudienceRuleSource(IDepartmentStore departments, IStringLocalizer<DepartmentAudienceRuleSource> stringLocalizer)
    {
        _departments = departments;
        S = stringLocalizer;
    }

    public string Name => "Department";

    public LocalizedString DisplayName => S["In a department"];

    public async Task<IReadOnlyList<UserAudienceRuleOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
        => (await _departments.ListAsync(cancellationToken))
            .Select(department => new UserAudienceRuleOption(department.Id, department.Name))
            .ToList();

    public Task<IReadOnlyCollection<string>> ResolveUserIdsAsync(UserAudienceRule rule, CancellationToken cancellationToken = default)
        => _departments.GetUserIdsAsync(rule.Values, cancellationToken);
}
```

```csharp
services.AddUserAudienceRuleSource<DepartmentAudienceRuleSource>();
```

Override `NumberLabel` when the rule takes a number, and `IsMatchAsync` when one person can be checked faster than listing everyone.

### Audiences in user searches and layer rules

- `IUserAudienceProvider` (`Name`, `GetRoleNamesAsync`, optional `GetExcludedUserIdsAsync`) defines a code audience by roles. `UserSearchQuery.Audience` and the `UserPicker` `audience` parameter limit a search to it; an unknown audience is ignored. `GetRoleNamesAsync` returning `null` means "not limited right now". `GetExcludedUserIdsAsync` receives only the users a search found; Staff uses it for `StaffUnlinked` so the staff editor offers only accounts not yet linked (see `orchardcore-staff`).
- With `OrchardCore.Rules`, the **In a user audience** condition (`UserAudienceCondition`, property `AudienceName`) matches when the signed-in user is in the audience, for layers and other rule hosts.

### Recipe and deployment

`UserAudiences` creates or updates defined audiences by name (camelCase properties, `join` as a string). The **User audiences** deployment step (category **Users**) exports them.

```json
{
  "steps": [
    {
      "name": "UserAudiences",
      "Audiences": [
        {
          "name": "field-veterans",
          "displayName": "Field veterans",
          "join": "All",
          "rules": [
            { "source": "Team", "values": ["<team id>"] },
            { "source": "StaffHireDate", "number": 30 }
          ]
        }
      ]
    }
  ]
}
```

## Cohorts (`CrestApps.OrchardCore.Users.Cohorts`)

Depends on `OrchardCore.Users` and User Audiences. A cohort is a flat, hand-kept list of any users, such as "2026 new hires" (a team is an organization unit; a cohort is not). **Content > Cohorts** (permission `ManageCohorts`, granted to Administrator) creates, renames and deletes cohorts, lists members, adds people with the user picker or in bulk (user names or e-mail addresses, one per line) and removes them.

- Storage: `Cohort` and `CohortMember` documents in the `Cohorts` YesSql collection (one document per cohort and per member), indexed by `CohortIndex` (`CohortId`, `Name`, `NormalizedName`) and `CohortMemberIndex` (`CohortId`, `UserId`, `AddedUtc`). Both index providers declare `.ExclusiveToDocumentType().SingleRecordPerDocument()`.
- Names are unique without regard to case; ids are at most 50 characters.
- The feature registers the `CohortMember` audience rule (**Cohort member**), so anything that targets audiences can target cohorts. Learning also links courses and paths to cohorts directly.
- A deleted user leaves every cohort (`CohortUserEventHandler`).

`ICohortService` (namespace `CrestApps.OrchardCore.Users`), registered with `services.AddCohorts()`:

| Members | Purpose |
|---|---|
| `PageAsync`, `ListAsync`, `FindByIdAsync`, `FindByNameAsync` | Read cohorts |
| `CreateAsync`, `UpdateAsync` | Return `CohortResult(Cohort, Errors)`; `Succeeded` when no errors |
| `DeleteAsync`, `ListUsesAsync` | Delete is refused while an `ICohortUsage` (`services.AddCohortUsage<T>()`) reports a use |
| `PageMembersAsync`, `CountMembersAsync`, `ListMemberIdsAsync`, `ListForUserAsync`, `IsMemberAsync` | Read members |
| `AddMembersAsync`, `RemoveMembersAsync`, `RemoveFromAllAsync` | Return the users who actually joined or left and notify each `ICohortMembershipObserver` (`services.AddCohortMembershipObserver<T>()`) after the change is flushed |

```csharp
using CrestApps.OrchardCore.Users;

namespace MyModule.Cohorts;

public sealed class WelcomeCohortObserver : ICohortMembershipObserver
{
    public Task MembersAddedAsync(Cohort cohort, IReadOnlyList<string> userIds, string actor, CancellationToken cancellationToken = default)
    {
        // Enroll or notify only the people who actually joined.
        return Task.CompletedTask;
    }

    public Task MembersRemovedAsync(Cohort cohort, IReadOnlyList<string> userIds, string actor, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

`UserCohorts` creates or updates cohorts, matched by `Id` and then by `Name`, and adds the `Members` it names by user name or e-mail; people already in the cohort stay. The **Cohorts** deployment step exports every cohort with its members by user name.

```json
{
  "steps": [
    {
      "name": "UserCohorts",
      "Cohorts": [
        {
          "Id": "new-hires-2026",
          "Name": "2026 new hires",
          "Description": "Everyone who started this year",
          "Members": ["ana", "ben@example.com"]
        }
      ]
    }
  ]
}
```
