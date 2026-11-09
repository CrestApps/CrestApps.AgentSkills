---
name: orchardcore-teams
description: Skill for the CrestApps Orchard Core Teams module (CrestApps.OrchardCore.Teams) and the shared building blocks beside it. Covers the one team hierarchy of a site, Content > Teams, ITeamManager, ITeamDeletionGuard, team editor cards and row actions with DisplayDriver of Team, module data with team.Put, team members through ITeamMembershipService and ITeamMemberProvider, the Teams recipe and deployment step, business hours with HoursSchedule and IOpenHoursService, parties with PartyRef, IPartySource and IPartyDirectory, and TimeWindow and CalendarTime in Calendars.Abstractions. Use this skill when requests mention teams, a team hierarchy, crews, who is in a team, business hours, holidays or party pickers. Strong matches include Team, TeamKind, TeamMember, TeamRecipeImport, AddTeamsCore, HoursScheduleEvaluator, OpenHoursBusinessHoursGate, OpenHoursServiceHoursProvider, PartyKinds and AddPartySource.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Teams

## One Team Hierarchy for the Whole Site

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Teams module and the small shared building blocks (business hours, parties, time windows) that Workforce, Contact Center, Work Orders and Assets build on. A team has a name, a kind, an optional parent, a description, a linked role, a time zone and whether it is in use. Modules keep their own data on the team and add their own cards to its editor.

### Guidelines

- Install `CrestApps.OrchardCore.Teams` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it) in the web/startup project.
- Reference `CrestApps.OrchardCore.Teams.Abstractions` (the `Team` model and contracts) or `CrestApps.OrchardCore.Teams.Core` (`TeamStore`, `TeamManager`, `TeamTree`, `TeamRecipeImport`, `AddTeamsCore()`) from your own module, never the module project.
- The module has no dependencies. Workforce depends on it, so turning Workforce on turns Teams on.
- Write teams through `ITeamManager` so validation and every module's `ICatalogEntryHandler<Team>` run (Workforce gives each new team its calendar). `TeamStore` is public for reads that must skip the handlers.
- `services.AddTeamsCore()` is safe to call more than once; a module that builds on teams calls it from its own registration.
- Workers, shifts and the Workforce card are covered by `orchardcore-workforce`; skills by `orchardcore-skills`.
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

Each team is a YesSql document in the `Teams` collection (`TeamsConstants.CollectionName`) with a `TeamIndex` of `ItemId`, `Name`, `Kind`, `ParentId` and `Enabled`. The whole tree is read and walked in memory (`TeamTree`).

| Property | Notes |
|---|---|
| `Name` | Required, trimmed. |
| `Kind` | `TeamKind`: `Organization`, `Region`, `Site`, `Department`, `Team` (default) or `Crew`. |
| `ParentId` | `null` at the top. Must exist and must not be the team or one of its own sub-teams. |
| `Description` | Optional. |
| `LinkedRoleName` | Users in this role are treated as part of the team (for example, to read its calendar). |
| `TimeZoneId` | IANA or Windows id (a Windows id is stored as its IANA id); `null` uses the site's. |
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
| `ITeamMembershipService` | `GetMembersAsync(teamId, includeSubTeams = true, on = null)`, `GetMembershipsAsync(userId, on = null)`, `IsMemberAsync(teamId, userId, on = null)`. A team's members include its sub-teams' members. |
| `TeamRecipeImport` | `ImportAsync(teamManager, nodes, errors, stepName, configure)` used by the `Teams` and `Workforce` steps. |

Teams keeps **no member list of its own**. Members come from every registered `ITeamMemberProvider` as `TeamMember { TeamId, UserId, Role, EffectiveFrom, EffectiveTo, Source }`; `Role` is an open string and `TeamMemberRoles` names `Member`, `Lead`, `Scheduler` and `Manager`. With no provider, teams have no members. Workforce registers `WorkerTeamMemberProvider` (each worker with a user account, from its team memberships).

## Recipes and Deployment

The `Teams` step (needs `OrchardCore.Recipes.Core`) creates teams, or updates them by `itemId` or by name (case and surrounding spaces ignored), with the parent named by `parentName` in any order. Property names are camelCase and enums are written as names. Each team goes through `ITeamManager`, so it is validated and other modules' handlers run; what other modules keep on a team is left as it is.

```json
{
  "steps": [
    {
      "name": "Teams",
      "Teams": [
        { "name": "Acme", "kind": "Organization" },
        { "name": "East", "kind": "Region", "parentName": "Acme" },
        { "name": "Boston", "kind": "Site", "parentName": "East", "timeZoneId": "America/New_York" },
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

The changelog section "Shared time, business hours, teams and parties" moved these into small libraries with no feature of their own.

### Business Hours

| Project | Holds |
|---|---|
| `CrestApps.OrchardCore.BusinessHours.Abstractions` | `HoursSchedule` (`TimeZoneId`, `Windows` of `HoursWindow { Day, OpenMinute, CloseMinute }`, `DateExceptions` of `HoursDateException { Date, Name, Closed = true, OpenMinute, CloseMinute }`), `HoursScheduleOption(Id, Name)`, `IOpenHoursService`, `IHoursScheduleSource`, `AddHoursScheduleSource<T>()` and the pure `HoursScheduleEvaluator` (`IsOpen`, `GetOpenWindows`, `GetDayStretches`). |
| `CrestApps.OrchardCore.BusinessHours.Core` | `DefaultOpenHoursService` and `services.AddOpenHours()`. |

- `IOpenHoursService.IsOpenAsync(scheduleId, utcInstant, timeZoneId)` returns `bool?`; `null` means no source knows the schedule, so treat it as open. `GetOpenWindowsAsync` returns merged `TimeWindow`s or `null`; `ListSchedulesAsync` feeds pickers.
- A window that opens and closes at the same minute is open all day; one that closes before it opens runs past midnight. A date exception owns its whole local date.
- Schedule sources today are the Contact Center **Business Hours** feature (`CrestApps.OrchardCore.ContactCenter.BusinessHours`) calendars.
- `OpenHoursBusinessHoursGate` (an `IBusinessHoursGate`, in `CrestApps.OrchardCore.Omnichannel.Core`, registered by Omnichannel Activities) replaces `AlwaysOpenBusinessHoursGate` and the Contact Center `BusinessHoursGate`.
- `OpenHoursServiceHoursProvider` (an `IServiceHoursProvider`, in `CrestApps.OrchardCore.WorkOrders.Core`, registered by Work Orders) replaces the Contact Center `BusinessHoursServiceHoursProvider`; `IServiceHoursProvider.GetOpenAsync` returns `TimeWindow`s. See `orchardcore-work-orders`.

```csharp
public sealed class StoreHoursScheduleSource : IHoursScheduleSource
{
    public Task<HoursSchedule> FindAsync(string scheduleId, CancellationToken cancellationToken = default)
    {
        if (scheduleId != "store")
        {
            return Task.FromResult<HoursSchedule>(null);
        }

        var schedule = new HoursSchedule { TimeZoneId = "America/New_York" };

        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            schedule.Windows.Add(new HoursWindow { Day = day, OpenMinute = 9 * 60, CloseMinute = 17 * 60 });
        }

        schedule.DateExceptions.Add(new HoursDateException { Date = new DateOnly(2026, 12, 25), Name = "Christmas Day" });

        return Task.FromResult(schedule);
    }

    public Task<IReadOnlyList<HoursScheduleOption>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<HoursScheduleOption>>([new HoursScheduleOption("store", "Store hours")]);
}

// Startup: services.AddOpenHours().AddHoursScheduleSource<StoreHoursScheduleSource>();
```

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
| `ITeamMembershipService` returns nobody | No `ITeamMemberProvider` is registered (enable Workforce, or register your own). |
| A recipe team lands at the top level | `parentName` must match an existing or imported team's name. |
| Imported team lost its Workforce settings | The `Teams` step never touches module settings; use the `Workforce` step for them. |
| Time zone rejected | Use an IANA or Windows id; `CalendarTimeZones.IsValid` decides. |
| `IsOpenAsync` returns `null` | No `IHoursScheduleSource` knows the id, or the schedule is disabled; treat as open. |

Related skills: `orchardcore-workforce`, `orchardcore-skills`, `orchardcore-calendars`, `orchardcore-contact-center`, `orchardcore-work-orders`, `orchardcore-assets`, `orchardcore-accounts`, `orchardcore-crestapps-users`.
