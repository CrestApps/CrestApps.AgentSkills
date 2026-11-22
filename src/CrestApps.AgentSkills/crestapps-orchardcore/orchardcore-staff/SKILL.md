---
name: orchardcore-staff
description: Skill for the CrestApps Orchard Core Staff module (CrestApps.OrchardCore.Staff), the building block for the people who work for the business. Covers the StaffMember record (with or without a user account), employment details, the reporting line, dated team memberships, capabilities (AddStaffCapability), the staff profile and page sections, staff editor cards added with DisplayDriver<StaffMember>, the StaffPicker and the Staff user audience, manager scoping (IStaffScopeService, AddStaffScopePermissions), IStaffDirectory, IStaffHierarchy, IStaffAccountService, lifecycle observers, home links, Staff Compensation pay, Staff Badges PINs, the organization chart, the Staff role, reports, workflow events, AI tools, the Staff recipe and deployment steps, and the upgrade from Workforce workers. Use this skill when requests mention staff members, employees, the org chart or a staff picker. Strong matches include StaffMember, IStaffMemberManager, IStaffDirectory and StaffPermissions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Staff

## One Record for the People Who Work for the Business

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Staff module. Staff keeps one `StaffMember` record for every employee, contractor or crew member, with or without a user account. Workforce schedules staff members, Contact Center agents and Point of Sale cashiers are staff members, Dispatch sends them to jobs, Scheduling books them, Accounts names an account team from them, and Learning names instructors. Each of those modules adds its own card to the staff editor and its own capability to the record instead of keeping a people record of its own.

### Guidelines

- Install `CrestApps.OrchardCore.Staff` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle) in the web/startup project.
- From your own module, reference `CrestApps.OrchardCore.Staff.Abstractions` (`StaffMember`, contracts, `StaffConstants`, `StaffPermissions`, `StaffProfileSections`, `AddStaffCapability` and the other registration helpers) and, when you need the store or indexes, `CrestApps.OrchardCore.Staff.Core` (`AddStaffCore()`, `StaffMemberIndex`, `StaffPayPrivacy`). Never reference the module project.
- Put Staff integrations in a startup marked `[RequireFeatures(StaffConstants.Features.Staff)]` so your module works without Staff.
- A **user** is a sign-in account; a **staff member** is an employment record. A user account belongs to one staff member at most, and once saved the link can't be changed or removed (pay, history and badges never move to another person). For a different account, add a new staff member.
- Store staff ids (`StaffMember.ItemId`) in your records, and read names, time zones and home bases from Staff when you show them. Never copy the name: a linked staff member takes `DisplayName` and `Email` from the user account on every save.
- Save through `IStaffMemberManager` (validation, history, lifecycle observers, the Staff role). Use `IStaffDirectory` for reads; it caches for the request.
- Teams are optional. When `CrestApps.OrchardCore.Teams` is on, `StaffOptions.UsesTeams` is true and team memberships drive manager scoping, the main team's time zone and the team filter.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models. `LocalizationSource` properties in your own models must carry `[JsonIgnore]`.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Staff | `CrestApps.OrchardCore.Staff` | `CrestApps.OrchardCore.Users`, `OrchardCore.Roles`, `CrestApps.OrchardCore.Resources` |
| Staff Compensation | `CrestApps.OrchardCore.Staff.Compensation` | `CrestApps.OrchardCore.Staff` |
| Staff Badges | `CrestApps.OrchardCore.Staff.Badges` | `CrestApps.OrchardCore.Staff` |
| Staff Organization Chart | `CrestApps.OrchardCore.Staff.OrgChart` | `CrestApps.OrchardCore.Staff`, `CrestApps.OrchardCore.Resources` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Staff",
        "CrestApps.OrchardCore.Staff.Compensation",
        "CrestApps.OrchardCore.Staff.Badges",
        "CrestApps.OrchardCore.Staff.OrgChart"
      ],
      "disable": []
    }
  ]
}
```

### Integrations Lit Up by Other Features

| Feature on | What Staff adds |
|---|---|
| `CrestApps.OrchardCore.Teams` | The **Teams** card and profile section, team scoping, the team filter and team columns (`orchardcore-teams`). |
| `CrestApps.OrchardCore.Skills` | The **Skills** card and profile section; a staff member and their user are one skill holder (`orchardcore-skills`). |
| `CrestApps.OrchardCore.Geo` | Home bases are geocoded (`orchardcore-geo`). |
| `OrchardCore.DataPipelines.Core` | The **Staff** report data source (`orchardcore-report-builder`). |
| `OrchardCore.Workflows` | Staff Member Hired, Staff Member Left and Staff Manager Changed events. |
| `CrestApps.OrchardCore.AI` | The `staffFind` and `staffReportingLine` AI tools. |
| Parties | A staff member is a party of kind `Staff` (`StaffConstants.PartyKind`). |

## The Staff Member

`StaffMember` (a `CatalogItem`, YesSql document in the `Staff` collection):

| Field | Notes |
|---|---|
| `UserId` | The linked Orchard user, optional and unique. Kiosk-only and seasonal people have none. |
| `DisplayName`, `Email` | For a linked person, taken from the user on every save and when the user changes. |
| `Phone`, `EmployeeNumber`, `ExternalId` | The employee number is unique and doubles as the badge number. `ExternalId` is the HR or payroll id. |
| `JobTitle`, `EmploymentType`, `Status` | `EmploymentType`: `FullTime`, `PartTime`, `Seasonal`, `Temporary`, `Contractor`, `Subcontractor`. `Status`: `Active`, `OnLeave`, `Suspended`, `Terminated`. |
| `HireDate`, `SeniorityDate`, `TerminationDate`, `DateOfBirth` | `DateOnly`. The date of birth needs `ViewSensitiveStaffData`. The hourly upkeep task terminates people whose last day has passed. |
| `ManagerId` | Another staff member. Unknown managers, the person themselves and loops are refused. |
| `PrimaryTeamId`, `TeamMemberships` | `TeamMembership` (`TeamId`, `Role` `Member`/`Lead`/`Scheduler`/`Manager`, `EffectiveFrom`, `EffectiveTo`). The first is the main team. |
| `TimeZoneId`, `HomeBase`, `WorkLocation` | `HomeBase` is a `StaffLocation` (`Address`, `Latitude`, `Longitude`); `WorkLocation` is a name. |
| `ContractedHoursPerWeek`, `Capabilities` | Capabilities are the registered names the person has. |
| `Properties` | Other modules' data: `StaffPayRates`, `StaffBadge`, Workforce's `WorkforceStaffSettings`. |

Helpers: `MembershipsOn(date)`, `HasCapability(name)`, `IsEmployedOn(date)` (active or on leave, between hire and termination). Indexes: `StaffMemberIndex` (one row per person), `StaffTeamMembershipIndex` (one row per membership, dates as `yyyyMMdd`), `StaffCapabilityIndex` (one row per capability) and `StaffHistoryIndex`. Every change to the job title, status, employment type, manager, teams, work location, last day or pay adds a `StaffHistoryEntry` (`Field`, `From`, `To`, `ChangedByUserId`, `ChangedUtc`; field names in `StaffHistoryFields`) to an append-only log.

## Admin UI

| Menu | Route (under `Admin/`) | Permission |
|---|---|---|
| **Staff > My Profile** | `Me` action of `StaffController` | `ViewOwnStaffProfile` |
| **Staff > All Staff** (list, profile `staff/profile/{id}`, editor) | `Index` action of `StaffController` | `ViewStaff`, `ManageOwnStaff`, or a permission registered with `AddStaffScopePermissions` |
| **Staff > Organization Chart** (Organization Chart feature) | `OrgChart` action of `StaffController` | `ViewStaff` |
| **Staff > Capabilities** | `Index` action of `StaffCapabilitiesController` | `ManageStaff` |
| **Settings > Staff** (**Offer only staff where people are assigned work**) | site settings group of `StaffSettingsDisplayDriver` | `ManageStaff` |

- The list has a search and filters for status, capability and (with Teams) team. A manager sees the staff of the teams where they are `Manager` or `Scheduler`, else their direct and indirect reports.
- The profile is the hub for a person: a header with picture, job title, main team and status, and side sections other modules fill (Overview, Teams, Skills, Work, Pay, Employment history). The selected section is kept in the URL hash (`#staff-teams`).
- The user editor gets a **Staff** card that makes the user a staff member or links to their profile.
- A user linked to a staff member who isn't terminated and holds `ViewOwnStaffProfile` may open the admin without an admin role (`StaffAdminAccessAuthorizationHandler`); every page still checks its own permission.
- The organization chart draws the reporting line with the `d3-org-chart` resource (pictures from User Avatars, else initials).

## Capabilities

Capabilities put people in pools: who the dispatch board offers, who takes calls, who can be booked, who runs a register.

- **Built in** ones are registered in code with `services.AddStaffCapability(name, displayName, description, position)`: Contact Center `Agent` (10), Dispatch `Technician` (20), Scheduling `Bookable` (30), Point of Sale `Cashier` (40), Learning `Instructor` (60). Admins can relabel, reorder or turn them off, but not delete or rename them; **Reset to defaults** drops the changes.
- **Custom** ones are added by admins with a key (letters, digits, dots, dashes, underscores, fixed once saved).
- Screens read capabilities through `IStaffCapabilityService` (`ListAsync`, `FindAsync`, `SaveAsync`, `DeleteAsync`, `GetDefinitionsAsync`), never the code registrations. A capability turned off isn't offered in editors, filters or pickers; people who have it keep it.
- How good someone is at something belongs in Skills; capabilities only decide the pools.

## Permissions

| Permission | Display name | Implied by | Default roles |
|---|---|---|---|
| `ManageStaff` | Manage staff | | Administrator |
| `ManageOwnStaff` | Manage the staff you manage | `ManageStaff` | Administrator |
| `ViewStaff` | View staff | `ManageStaff`, `ManageOwnStaff` | Administrator |
| `ViewOwnStaffProfile` | View one's own staff profile | `ManageStaff` | Administrator, Staff |
| `ViewSensitiveStaffData` | View sensitive staff data | | Administrator |
| `ManageStaffPay` | Manage staff pay rates | | Administrator |

**The Staff role.** Working staff with a user account are put in a role named **Staff** (`StaffConstants.DefaultStaffRoleName`, created with only `ViewOwnStaffProfile`) through `IRoleGrantService`, and taken out when they stop working or the account is unlinked. Give the role only what every member of staff needs.

## Services

| Contract | What it does |
|---|---|
| `IStaffMemberManager` | `ICatalogManager<StaffMember>` plus `FindByUserIdAsync`, `FindByEmployeeNumberAsync`, `GetByUserIdsAsync`, `SearchAsync(StaffQuery, skip, take)`, `ListByTeamAsync(teamId, date)`, `ListByTeamInRangeAsync`, `ListDirectReportsAsync`. `StaffQuery`: `Search`, `TeamId` (with sub-teams), `TeamIds`, `Status`, `HasUser`, `EmploymentTypes`, `Capability`, `Capabilities` (any of), `ManagerId`, `Ids`. |
| `IStaffDirectory` | The read side: `IsStaffAsync(userId)`, `FindByUserIdAsync`, `GetAsync(ids)`, `GetNamesAsync(ids)`, `GetNamesByUserIdAsync(userIds)`, `SearchAsync(text, capability, requireUser, take)` (working staff only), `GetCapabilities()`. |
| `IStaffHierarchy` | `GetManagerChainAsync`, `GetReportsAsync(id, includeIndirect)`, `ManagesAsync(managerId, staffMemberId)`. |
| `IStaffScopeService` | `GetScopeAsync(user)` returns a `StaffScope` (`All`, `TeamIds`, `StaffMemberIds`, `CoversTeam`, `Covers(staff, date)`); `CanManageAsync(user, staff)`. |
| `IStaffTimeZoneResolver` | `GetTimeZoneIdAsync` (own, else main team or its parents, else the site's); `FindTimeZoneIdAsync` stops before the site's, for modules with their own fallback. |
| `IStaffAccountService` | `EnsureAsync(userId, displayName, capabilities, defaults)`: makes the person doing your module's work a staff member, or adds the capabilities to the one they have. |
| `IStaffBadgeService` | Badges feature: `SetPin`, `ClearPin`, `HasPin`, `VerifyAsync(employeeNumber, pin)` returning `StaffBadgeResult` (`Failure`: `Invalid`, `Locked`, `Inactive`). PBKDF2; five wrong PINs lock the badge for 15 minutes. |
| `IStaffHistoryService` | `ListAsync(staffMemberId, take)`, newest first. |

`services.AddStaffCore()` registers the services and is safe to repeat. Search endpoint for pickers: `GET Admin/api/staff/search` (route name `CrestApps.Staff.Search`, takes a `capability`).

## Building a Module on Staff

| Seam | Use it to |
|---|---|
| `AddStaffCapability(...)` | Seed a built-in capability your code relies on. |
| `DisplayDriver<StaffMember>` (`AddDisplayDriver<StaffMember, TDriver>()`) | Add a card to the staff editor, or a section to the profile for the `StaffProfileSections.DisplayType` (`DetailAdmin`) display type placed with `StaffProfileSections.Location(section, position)`. |
| `services.AddPageSection(StaffProfileSections.PageName, name, title, icon, position)` | Register a new profile section (title as a `LocalizationSource`). Built in: `Overview` (0), `Skills` (30), `Work` (40), `Pay` (60), `History` (1000). |
| `IStaffLifecycleObserver` (`AddStaffLifecycleObserver<T>()`) | React after a save (`StaffChangedContext`: `IsNew`, `Activated`, `Terminated`, `ManagerChanged`, `UserChanged`, `TeamsChanged`, `PreviousStatus`) or a delete. |
| `IStaffHomeLinkProvider` (`AddStaffHomeLinkProvider<T>()`) | Add a `StaffHomeLink(Title, Url, Description, Icon, Position)` to **My Profile**. Register it in the startup of the feature that owns the page. |
| `AddStaffScopePermissions(manageEveryone, manageOwn)` | Let holders of your module's permissions manage staff (Workforce passes `ManageAllWorkforce` and `ManageWorkers`). |
| `ICatalogEntryHandler<StaffMember>` | Hook the manager's lifecycle, such as writing your settings before a save. |
| `StaffPicker` view component | Pick working staff: `name`, `selectedValues`, `capability` (comma-separated), `valueType` (`staffId` or `userId`), `multiple`, `excludeId`, `buttonText`, `smallButton`, `required`. |
| `StaffConstants.UserAudience` (`Staff`) | Ask the `UserPicker` and `UserSearchQuery.Audience` for working staff only, once **Offer only staff where people are assigned work** is on. |
| `StaffPayPrivacy.DecideAsync(httpContext)` | Ask for the viewer's password before showing a pay card (`Show`, `Prompt` with the `StaffPayConfirmation` shape, `Hide`). |

```csharp
[RequireFeatures(StaffConstants.Features.Staff)]
public sealed class StaffStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddStaffCapability(
            "Driver",
            LocalizationSource.Create<StaffStartup>("Driver"),
            LocalizationSource.Create<StaffStartup>("Drives delivery routes."),
            70);

        services.AddStaffHomeLinkProvider<DeliveryStaffHomeLinks>();
        services.AddStaffLifecycleObserver<DeliveryStaffLifecycleObserver>();
    }
}

internal sealed class DeliveryStaffHomeLinks : IStaffHomeLinkProvider
{
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public DeliveryStaffHomeLinks(IAuthorizationService authorizationService, IStringLocalizer<DeliveryStaffHomeLinks> stringLocalizer)
    {
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public async Task<IEnumerable<StaffHomeLink>> GetLinksAsync(StaffMember staffMember, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => staffMember.HasCapability("Driver") && await _authorizationService.AuthorizeAsync(user, DeliveryPermissions.DriveRoutes)
            ? [new StaffHomeLink(S["My routes"], "~/Admin/delivery/my-routes", S["Today's stops."], "fa-solid fa-truck", 30)]
            : [];
}

internal sealed class DeliveryStaffLifecycleObserver : IStaffLifecycleObserver
{
    private readonly IRouteAssignmentService _routes;

    public DeliveryStaffLifecycleObserver(IRouteAssignmentService routes)
    {
        _routes = routes;
    }

    public async Task ChangedAsync(StaffChangedContext context, CancellationToken cancellationToken = default)
    {
        if (context.Terminated)
        {
            await _routes.UnassignFutureAsync(context.StaffMember.ItemId, context.StaffMember.TerminationDate, cancellationToken);
        }
    }
}
```

Picking staff in a Razor editor:

```html
@await Component.InvokeAsync("StaffPicker", new
{
    name = Html.NameFor(m => m.DriverStaffId).ToString(),
    selectedValues = new[] { Model.DriverStaffId },
    capability = "Driver",
    buttonText = T["Choose a driver"].Value,
})
```

An editor card, a profile section and an index on your own staff data are in [references/staff-recipes-and-extensions.md](references/staff-recipes-and-extensions.md).

## Recipes and Deployment

The `Staff` step (`StaffConstants.RecipeStepName`) first saves `Capabilities` (custom ones, or changes to built-in labels), then creates or updates each entry of `Staff`, matched by `itemId`, then `employeeNumber`, then the user (`userName`). Teams are given by `teamName` (or `teamId`) and read only while Teams is on; the manager by `managerId` or `managerEmployeeNumber`, set after everyone is saved, so order doesn't matter. `pay`, when present, replaces the person's `StaffPayRates`.

```json
{
  "steps": [
    {
      "name": "Staff",
      "Capabilities": [
        { "name": "FirstAid", "displayName": "First aider", "description": "Trained to give first aid.", "position": 80, "enabled": true }
      ],
      "Staff": [
        {
          "displayName": "Avery Brooks",
          "employeeNumber": "1001",
          "userName": "avery",
          "jobTitle": "Clinic manager",
          "employmentType": "FullTime",
          "status": "Active",
          "hireDate": "2024-03-01",
          "timeZoneId": "America/Chicago",
          "capabilities": [ "FirstAid" ],
          "teams": [ { "teamName": "Riverside Clinic", "role": "Manager" } ]
        },
        {
          "displayName": "Jordan Lee",
          "employeeNumber": "1002",
          "jobTitle": "Receptionist",
          "employmentType": "PartTime",
          "contractedHoursPerWeek": 24,
          "managerEmployeeNumber": "1001",
          "homeBase": { "address": "12 Elm Street, Springfield" },
          "teams": [ { "teamName": "Front Desk", "effectiveFrom": "2026-01-01" } ],
          "pay": { "payBasis": "Hourly", "currency": "USD", "rates": [ { "effectiveFrom": "2026-01-01", "hourlyRate": 21.5 } ] }
        }
      ]
    }
  ]
}
```

- The **Staff** deployment step (category **Organization**) exports the capability definitions and every staff member with teams by name and the user by user name. It never exports pay, badges or `Properties`, and leaves out `dateOfBirth` unless **Include sensitive data** (`IncludeSensitiveData`) is ticked.
- `StaffSettings` (`LimitPickersToStaff`) travels in Orchard Core's `Settings` step.
- Workforce's `Workforce` step still accepts a `Workers` section, saved as staff members (`orchardcore-workforce`).

## Reports, Workflows and AI

- **Report data source** `Staff` (Data Pipelines): **Staff members** (one row per person) and **Staff history** (one row per change; pay entries only for readers who manage pay), for headcount, hires, departures and turnover.
- **Workflow events** (category **Staff**): `StaffMemberHired`, `StaffMemberLeft`, `StaffManagerChanged`. Input: `StaffMemberId`, `UserId`, `DisplayName`, `JobTitle`, `ManagerId`, `PreviousManagerId`, `TeamId`, `Status`.
- **AI tools**: `staffFind` (by name, job title, team or capability) and `staffReportingLine`, for people who may view staff (`orchardcore-ai-tools`).

## Upgrading from Workforce Workers

Workforce's `WorkerProfile`, `IWorkerManager`, `WorkerQuery`, `WorkerStatus` and the Workers list are gone. The Workforce migration turns the Staff feature on and moves each worker into the `Staff` collection **with the same id**, so stored worker ids (punches, timesheets, shifts, time off, visits, territories, skills) still point at the same person. Hours and availability move to `WorkforceStaffSettings`, pay rates to `StaffPayRates`, the kiosk PIN to `StaffBadge`, and `ManagerWorkerId` becomes `ManagerId`. In code, replace `IWorkerManager` with `IStaffMemberManager`, `WorkerQuery` with `StaffQuery`, `WorkerProfile` with `StaffMember`, and `DisplayDriver<WorkerProfile>` with `DisplayDriver<StaffMember>`. Skills reads the old `Worker` subject kind as `Staff`.

## Gotchas

| Symptom | Check |
|---|---|
| Name or email edits on the staff editor are refused or ignored | The staff member is linked to a user; change them on the user account. |
| The user picker on the staff editor doesn't offer a user | That user is already linked to another staff member (`StaffUnlinked` audience). |
| A capability is missing from the editor or a picker | It's turned off under **Staff > Capabilities**, or the module that registers it is off. |
| Pickers still offer every user | **Settings > Staff > Offer only staff where people are assigned work** is off (by design, until staff are set up). |
| A manager can't see someone | They are not `Manager`/`Scheduler` of the person's team (or a parent team) and the person doesn't report to them. |
| Teams card or team filter missing | The Teams feature is off. |
| Recipe dropped teams | Teams is off, or the `teamName` doesn't exist (reported as an error). |
| Pay card asks for a password | `StaffPayPrivacy` asks for the viewer's password before showing pay. |

Related skills: `orchardcore-workforce`, `orchardcore-teams`, `orchardcore-skills`, `orchardcore-workforce-contact-center`, `orchardcore-contact-center`, `orchardcore-dispatch`, `orchardcore-scheduling`, `orchardcore-point-of-sale`, `orchardcore-accounts`, `orchardcore-payroll`, `orchardcore-time-tracker`, `orchardcore-crestapps-users`, `orchardcore-geo`.
