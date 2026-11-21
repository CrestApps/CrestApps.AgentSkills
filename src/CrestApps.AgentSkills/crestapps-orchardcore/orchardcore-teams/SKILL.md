---
name: orchardcore-teams
description: Skill for the CrestApps Orchard Core Teams module (CrestApps.OrchardCore.Teams) and the shared building blocks beside it. Covers the team hierarchy, Content > Teams, ITeamManager, ITeamDeletionGuard, team editor cards, module data with team.Put, the team address, members through ITeamMembershipService and ITeamMemberProvider (staff memberships), picking who takes work with ITeamAssignmentService, load and availability sources, the TeamPicker, the Team audience rule, the Teams recipe and deployment step, where business hours now live (orchardcore-business-hours), parties with PartyRef and IPartyDirectory, and TimeWindow and CalendarTime. Use this skill when requests mention teams, crews, who is in a team, assigning work to a team member, round robin or least loaded assignment, business hours, holidays or party pickers. Strong matches include Team, TeamMember, TeamAssignmentRequest, TeamAssignmentStrategies, AddTeamsCore and AddPartySource.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Teams

## One Team Hierarchy for the Whole Site

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Teams module and the small shared building blocks (business hours, parties, time windows) that Workforce, Contact Center, Work Orders and Assets build on. A team has a name, a kind, an optional parent, a description, a linked role, a time zone, an optional address and whether it is in use. Modules keep their own data on the team and add their own cards to its editor.

### Guidelines

- Install `CrestApps.OrchardCore.Teams` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it) in the web/startup project.
- Reference `CrestApps.OrchardCore.Teams.Abstractions` (the `Team` model and contracts) or `CrestApps.OrchardCore.Teams.Core` (`TeamStore`, `TeamManager`, `TeamTree`, `TeamRecipeImport`, `AddTeamsCore()`) from your own module, never the module project.
- The module has no dependencies. Workforce depends on it (and on Staff), so turning Workforce on turns Teams on.
- Write teams through `ITeamManager` so validation and every module's `ICatalogEntryHandler<Team>` run (Workforce gives each new team its calendar). `TeamStore` is public for reads that must skip the handlers.
- `services.AddTeamsCore()` is safe to call more than once; a module that builds on teams calls it from its own registration.
- People and their dated team memberships are kept by Staff (`StaffMember.TeamMemberships`, see `orchardcore-staff`); shifts and the Workforce card by `orchardcore-workforce`; skills by `orchardcore-skills`.
- Hand work to a team member through `ITeamAssignmentService` instead of writing your own rotation.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature

| Feature | ID | Depends on |
|---|---|---|
| Teams | `CrestApps.OrchardCore.Teams` | none |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Teams"
      ],
      "disable": []
    }
  ]
}
```

## The Team Model

Each team is a YesSql document in the `Teams` collection (`TeamsConstants.CollectionName`) with a `TeamIndex` of `ItemId`, `Name`, `Kind`, `ParentId` and `Enabled` (declared `.ExclusiveToDocumentType().SingleRecordPerDocument()`). The whole tree is read and walked in memory (`TeamTree`).

| Property | Notes |
|---|---|
| `Name` | Required, trimmed. |
| `Kind` | `TeamKind`: `Organization`, `Region`, `Site`, `Department`, `Team` (default) or `Crew`. |
| `ParentId` | `null` at the top. Must exist and must not be the team or one of its own sub-teams. |
| `Description` | Optional. |
| `LinkedRoleName` | Users in this role are treated as part of the team (for example, to read its calendar). |
| `TimeZoneId` | IANA or Windows id (a Windows id is stored as its IANA id); `null` uses the site's. |
| `Address` | Optional structured `Address` (Addresses building block): where the team works. Workforce geocodes its geofence from it and Payroll reads a work site's tax jurisdictions from it. With Addresses (`CrestApps.OrchardCore.Addresses`) on, the editor shows an **Address** card (`TeamAddress_Edit` at `Content:2`) laid out for the country; a started address must be complete. |
| `Enabled` | Whether the team is in use (default `true`). |
| `CreatedUtc`, `ModifiedUtc` | Set by the catalog handler. |

## Admin UI and Permissions

| Menu | Routes | Permission |
|---|---|---|
| **Content > Teams** | `Admin/teams`, `Admin/teams/create?parentId=`, `Admin/teams/edit/{id}`, `Admin/teams/delete/{id}` | `ManageTeams` |

| Permission | Display name | Default role |
|---|---|---|
| `ManageTeams` | Manage teams | Administrator |

With Workforce on, `ManageWorkforceTeams` (Manage workforce teams) also grants `ManageTeams` (`WorkforceTeamsAuthorizationHandler`), so older roles keep their access.

## Services

| Contract | What it does |
|---|---|
| `ITeamManager` | `ICatalogManager<Team>` plus `ListTreeAsync()` (each team followed by its sub-teams, with its depth), `GetSubtreeIdsAsync(teamId)`, `GetAncestorsAsync(teamId)` (nearest first) and `GetDeletionBlockersAsync(team)`. |
| `TeamStore` | The `DocumentCatalog<Team, TeamIndex>` behind the manager. |
| `TeamTree` | `GetSubtreeIds` and `Order` over a list of teams. |
| `ITeamMembershipService` | `GetMembersAsync(teamId, includeSubTeams = true, on = null)`, `GetMembershipsAsync(userId, on = null)`, `IsMemberAsync(teamId, userId, on = null)`. A team's members include its sub-teams' members; one membership per team and person (the user, or the staff record for people without an account). |
| `ITeamAssignmentService` | Picks the member who takes a piece of work: `PickAsync(TeamAssignmentRequest)` returns `TeamAssignmentResult(Member, Outcome)`; `SelectAsync` returns only the member. See [Team Assignment](#team-assignment). |
| `TeamRecipeImport` | `ImportAsync(teamManager, nodes, errors, stepName, configure)` used by the `Teams` and `Workforce` steps. |

Teams keeps **no member list of its own**. Members come from every registered `ITeamMemberProvider` as `TeamMember { TeamId, UserId, StaffMemberId, Role, EffectiveFrom, EffectiveTo, Source }`; `Role` is an open string and `TeamMemberRoles` names `Member`, `Lead`, `Scheduler` and `Manager`. With no provider, teams have no members. Staff registers `StaffTeamMemberProvider` (source `Staff`): every staff member from their dated team memberships, people without a user account included (`StaffMemberId` set, `UserId` empty). Workforce no longer provides members: its `WorkerProfile` was replaced by Staff's `StaffMember` (see `orchardcore-staff`).

### Staff Memberships

With Staff (`CrestApps.OrchardCore.Staff`) on, Teams sets `StaffOptions.UsesTeams = true` (Staff then scopes managers, time zones, filters and reports by team), adds the **Teams** card (`StaffTeams_Edit` at `Content:5`) to the staff editor and a **Teams** section to the staff profile page. Each row is a Staff `TeamMembership { TeamId, Role, EffectiveFrom, EffectiveTo }` with `Role` a `TeamMemberRole` enum (`Member`, `Lead`, `Scheduler`, `Manager`); the first row becomes `StaffMember.PrimaryTeamId`. A manager can only add people to the teams they manage.

### Team Picker and Audience Rule

The `TeamPicker` view component lists the enabled teams as a tree (a chosen team stays listed after it is disabled); the posted value is the team id. Its other arguments are `buttonText` and `smallButton`.

```cshtml
@await Component.InvokeAsync("TeamPicker", new { name = "TeamIds", selectedValues = Model.TeamIds, multiple = true, required = false, excludeId = Model.CurrentTeamId })
```

Teams also registers the `Team` user audience rule ("Member of a team or its sub-teams", an `IUserAudienceRuleSource`), which matches the members of the chosen teams on today's date. See `orchardcore-crestapps-users`.

## Team Assignment

`ITeamAssignmentService` (`DefaultTeamAssignmentService`, registered by `AddTeamsCore()`) keeps no state: whose turn it is comes from each member's load, so every node picks the same person. Tickets uses it for desk assignment.

| `TeamAssignmentRequest` | Notes |
|---|---|
| `TeamId`, `IncludeSubTeams` (`true`) | Whose members are considered; members without a `UserId` are skipped and each person counts once. |
| `WorkKind` | Open string (Tickets uses its record kind); the load comes from the `ITeamMemberLoadSource` with that `WorkKind`. |
| `Strategy` | `TeamAssignmentStrategies.LeastLoaded` (default: fewest open items) or `RoundRobin` (given work longest ago, never-assigned first). The user id breaks ties. |
| `MaxOpen` | Most open items a member may hold and still be picked; `null` is no limit. |
| `OnlyAvailable` (`true`), `AvailabilitySources` | Pass over members any honored `ITeamMemberAvailabilitySource` reports; `null` honors every source, an empty list none. A source that throws is logged and skipped. |
| `ExcludedUserIds`, `CandidateFilter` | Users never picked; an async filter over the candidates (for example, required skills). |

`TeamAssignmentOutcome`: `Picked`, `NoTeam`, `NoMembers`, `NoneAvailable`, `NoneQualified` (the filter left nobody), `AllAtCapacity` (`MaxOpen`).

| Availability source `Name` | Registered by | `HonoredByDefault` |
|---|---|---|
| `WorkforceShifts` (on a published shift, not on a break) | Workforce | yes |
| `CalendarAbsences` (not out of office or on time off) | Calendars, when Teams is on | yes |
| `ContactCenterPresence` (signed in, not on a break) | Contact Center agent services | no |

```csharp
var result = await _teamAssignmentService.PickAsync(new TeamAssignmentRequest
{
    TeamId = teamId,
    WorkKind = "Repair",
    Strategy = TeamAssignmentStrategies.RoundRobin,
    MaxOpen = 10,
    ExcludedUserIds = [currentAssigneeId],
}, cancellationToken);

if (!result.Picked)
{
    _logger.LogInformation("Nobody could take the repair: {Outcome}.", result.Outcome);
}
```

Load and availability source examples are in [references/teams-extensions.md](references/teams-extensions.md).

## Recipes and Deployment

The `Teams` step (needs `OrchardCore.Recipes.Core`) creates teams, or updates them by `itemId` or by name (case and surrounding spaces ignored), with the parent named by `parentName` in any order. Property names are camelCase and enums are written as names; `address` is an object (`addressLine1`, `addressLine2`, `city`, `region`, `postalCode`, `country` as a two-letter code, and the other address parts) or `null`. Each team goes through `ITeamManager`, so it is validated and other modules' handlers run; what other modules keep on a team is left as it is.

```json
{
  "steps": [
    {
      "name": "Teams",
      "Teams": [
        { "name": "Acme", "kind": "Organization" },
        { "name": "East", "kind": "Region", "parentName": "Acme" },
        {
          "name": "Boston",
          "kind": "Site",
          "parentName": "East",
          "timeZoneId": "America/New_York",
          "address": { "addressLine1": "1 Federal St", "city": "Boston", "region": "MA", "postalCode": "02110", "country": "US" }
        },
        { "name": "Billing", "kind": "Team", "parentName": "Boston", "linkedRoleName": "Billing", "description": "Invoices and payments." },
        { "name": "Van 7", "kind": "Crew", "parentName": "Boston", "enabled": true }
      ]
    }
  ]
}
```

- A parent name that never resolves is reported as a recipe error; the other teams are still saved.
- The **Teams** deployment step (category **Organization**) exports every team in tree order with `parentName`, without `properties`, `createdUtc`, `modifiedUtc` or `parentId`. Module settings stay out because they can name records of this site; their own steps carry them (the `Workforce` step still imports and exports teams with their Workforce settings).
- With CrestApps Recipes on, both steps publish a JSON schema.

## Extension Points

| Seam | Use it to |
|---|---|
| `ICatalogEntryHandler<Team>` | Keep your records in step with a team (create, update, delete). |
| `ITeamDeletionGuard` | Refuse to delete a team you still use; return the reason or `null`. Register with `services.AddTeamDeletionGuard<T>()` (in `Teams.Core`). The **Delete** action calls `GetDeletionBlockersAsync` first; a team with sub-teams is always refused. |
| `ITeamMemberProvider` | Give the members of teams your module knows. Register with `services.AddTeamMemberProvider<T>()` (in `Teams.Abstractions`). |
| `ITeamMemberLoadSource` | Report each user's open items of one `WorkKind` and `LastAssignedUtc`. Count in the database: it runs on every assignment. Register with `services.AddTeamMemberLoadSource<T>()`. |
| `ITeamMemberAvailabilitySource` | Report who can't take work now (`Name`, a `[JsonIgnore]` `DisplayName`, `HonoredByDefault`). Register with `services.AddTeamMemberAvailabilitySource<T>()`. |
| `DisplayDriver<Team>` | Add a card to the team editor (the built-in **Team** card is `TeamFields_Edit` at `Content:1`) or row actions between **Add a team under it** (`ActionsMenu:5`) and **Delete** (`ActionsMenu:100`) for `SummaryAdmin` and `SummaryAdminGrid`. |

### Keep Module Data on the Team

```csharp
public sealed class RosterTeamSettings
{
    public string Color { get; set; }
}

public sealed class RosterTeamDisplayDriver : DisplayDriver<Team>
{
    public override IDisplayResult Edit(Team team, BuildEditorContext context)
    {
        return Initialize<RosterTeamViewModel>("RosterTeamFields_Edit", model =>
        {
            model.Color = team.TryGet<RosterTeamSettings>(out var settings) ? settings.Color : null;
        }).Location("Content:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(Team team, UpdateEditorContext context)
    {
        var model = new RosterTeamViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        team.Alter<RosterTeamSettings>(settings => settings.Color = model.Color);

        return Edit(team, context);
    }
}

// Startup: services.AddDisplayDriver<Team, RosterTeamDisplayDriver>();
```

`team.Put(settings)`, `team.TryGet<T>(out var settings)` and `team.Alter<T>(...)` work as on any catalog item. Workforce keeps `WorkforceTeamSettings` this way, with `team.GetWorkforceSettings()` and `team.AlterWorkforceSettings(...)`.

### Refuse a Delete and Give Members

Full guard and provider examples, plus a team row action, are in [references/teams-extensions.md](references/teams-extensions.md).

## Shared Building Blocks

The changelog section "Shared time, business hours, teams and parties" moved these into small libraries. Parties and time windows have no feature of their own; business hours now have their own module.

### Business Hours

Business hours are no longer a library-only block: they are the **Business Hours** module (`CrestApps.OrchardCore.BusinessHours`, schedules under **Content → Business Hours**, permission `ManageBusinessHoursSchedules`). See `orchardcore-business-hours` for the schedule model, `IOpenHoursService`, `IHoursScheduleSource`, the `BusinessHoursSchedules` recipe step, the picker and the workflow task.

- Teams keeps no hours of its own. A team's time zone (`TimeZoneId`) is separate from any schedule; modules that need hours store a schedule id (ticket desks `HoursScheduleId`, service-level policies, Contact Center queues `BusinessHoursCalendarId`).
- Contact Center calendars became Business Hours schedules with the same ids; Contact Center Business Hours (`CrestApps.OrchardCore.ContactCenter.BusinessHours`) now depends on the Business Hours feature and only gates queues, entry points, the dialer and Omnichannel sends (`IBusinessHoursService`, `OpenHoursBusinessHoursGate`).
- Work-order SLA clocks moved to Service Levels (`orchardcore-service-levels`); `OpenHoursServiceHoursProvider` and `IServiceHoursProvider` are gone.

### Parties

`CrestApps.OrchardCore.Parties.Abstractions` holds everything; there is no feature.

| Type | Notes |
|---|---|
| `PartyRef(Kind, Id)` | A `readonly record struct`; `ToString()` writes `Kind:Id`, `TryParse` reads it, `IsEmpty`. |
| `PartyKinds` | `User`, `Customer`, `Account`, `Contact`, `Guest` (the kind is an open string). |
| `IPartySource` | `Kind`, `SearchAsync(query, count)` returning `PartyOption(Id, Name)`, `GetNamesAsync(ids)`. Register with `services.AddPartySource<T>()`. |
| `IPartyDirectory` | `Kinds`, `GetSource(kind)` (case-insensitive, first registered wins), `SearchAsync(kind, query, count)`, `GetNameAsync(party)`, `GetNamesAsync(parties)` (one lookup per kind). Register with `services.AddPartyDirectory()` (`AddPartySource` calls it). |

| Source | Project | Registered by |
|---|---|---|
| `UserPartySource` | `CrestApps.OrchardCore.Users.Core` | CrestApps Users, and Assets when `OrchardCore.Users` is on |
| `CustomerPartySource` | `CrestApps.OrchardCore.Customers.Core` | Customers |
| `AccountPartySource` | `CrestApps.OrchardCore.Accounts.Core` | Accounts |
| `ContactPartySource` | Assets module | Assets, when Omnichannel Management is on |

`IAssetOwnerSource` and `AssetOwnerOption` were replaced by `IPartySource` and `PartyOption`. Stored values did not change: an asset's `Customer` owner and `WorkOrder.CustomerId` still hold a **user id** (party kind `User`). See `orchardcore-assets`, `orchardcore-accounts` and `orchardcore-crestapps-users`.

### Time Windows and Calendar Time

`CalendarTime`, `CalendarTimeZones`, `TimeWindow`, `RecurrenceRule` and `RecurrenceExpander` live in `CrestApps.OrchardCore.Calendars.Abstractions` (namespaces `CrestApps.OrchardCore.Calendars` and `CrestApps.OrchardCore.Calendars.Recurrence`); they used to be in `Calendars.Core`. `CalendarTimeWindow` became the `TimeWindow(StartUtc, EndUtc)` record struct (`Duration`, `Contains`, `Overlaps`, `Intersect`, `Merge`, `Subtract`, `Clip`), and Dispatch's `TimeSpanUtc` is gone. Reference the Abstractions project when you only need these. See `orchardcore-calendars`.

## Gotchas

| Symptom | Check |
|---|---|
| Delete refused | The team has sub-teams, or a guard refused (Workforce refuses a team with current workers). |
| `ITeamMembershipService` returns nobody | No `ITeamMemberProvider` is registered (enable Staff and add team memberships on the staff editor, or register your own). |
| Assignment returns `NoneAvailable` | Every member is off shift, absent or (when honored) signed out of Contact Center; narrow `AvailabilitySources` or set `OnlyAvailable = false`. |
| Assignment never balances | No `ITeamMemberLoadSource` has the request's `WorkKind`, so every load is zero and the user id decides. |
| A staff member without a login is never picked | Assignment needs a `UserId`; such people are members but not candidates. |
| Team address not saved | Addresses must be on, and a started address must be complete for its country. |
| A recipe team lands at the top level | `parentName` must match an existing or imported team's name. |
| Imported team lost its Workforce settings | The `Teams` step never touches module settings; use the `Workforce` step for them. |
| Time zone rejected | Use an IANA or Windows id; `CalendarTimeZones.IsValid` decides. |
| `IsOpenAsync` returns `null` | No `IHoursScheduleSource` knows the id, or the schedule is disabled; treat as open. See `orchardcore-business-hours`. |

Related skills: `orchardcore-staff`, `orchardcore-business-hours`, `orchardcore-tickets`, `orchardcore-workforce`, `orchardcore-skills`, `orchardcore-calendars`, `orchardcore-contact-center`, `orchardcore-work-orders`, `orchardcore-assets`, `orchardcore-accounts`, `orchardcore-crestapps-users`.
