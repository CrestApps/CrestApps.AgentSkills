---
name: orchardcore-time-tracker
description: Skill for the CrestApps Orchard Core Time Tracker module (CrestApps.OrchardCore.TimeTracker). Covers time stages such as Work, Break and Lunch, categories with a factor, My Time with clock in, switch stage and clock out, manager corrections kept in the Audit Trail, the Hours by Category report, the TimeTracker recipe and deployment steps, ITimeTrackerService with InsertAsync and SetBilledAsync, what time counts against with ChargedTo and ITimeEntryChargeResolver, ticket time, the My time staff link, other clocks through SyncAsync and IExternalTimeClock, the Workforce Time Clock and time off, and Contact Center agent states. Use this skill when requests mention time tracking, clocking in, billable or payable hours, time categories, logging time on a ticket, billing tracked time or correcting tracked time. Strong matches include TimeStage, TimeCategory, TimeEntry, TimeEntrySync, TimeEntryQuery, ITimeTrackerService, ITimeEntryObserver, ITimeEntryChargeResolver, IExternalTimeClock and TimeTrackerPermissions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Time Tracker

## Where Everyone's Working Time Is Kept

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Time Tracker module. A person is always in at most one **stage** (Work, Break, Lunch); each stage counts toward any number of **categories** (Payable, Billable, Vacation), and a category can count its minutes more or less with a **factor**. People switch stage on **My Time**; managers correct time with a reason; the **Hours by Category** report totals every category. It needs only Users, so it works without workforce planning, and the Workforce time clock, Workforce time off and the Contact Center feed their time into it.

### Guidelines

- Install `CrestApps.OrchardCore.TimeTracker` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it) in the web/startup project.
- Reference `CrestApps.OrchardCore.TimeTracker.Abstractions` (models, `ITimeTrackerService`, `IExternalTimeClock`, `ITimeEntryObserver`, `TimeTrackerPermissions`, `TimeTrackerConstants`) or `CrestApps.OrchardCore.TimeTracker.Core` (`AddTimeTrackerCore()`, stores, managers) from your module.
- Every change goes through `ITimeTrackerService`, which takes a distributed lock per person (`TIMETRACKER_USER_{userId}`) and saves before releasing it, so a day never has gaps or overlaps and no time is recorded in the future.
- Entries kept by another clock (`ManagedBy` set) are read-only here; correct them in that clock (for the Workforce time clock, its punches).
- Billed entries (`BilledInvoiceId` set) can't be changed or removed until the invoice lets them go.
- Show entry times in the local time zone (`ILocalClock`, the `DateTime` shape), never as UTC; see `orchardcore-time-zones`.
- Workforce punches, timesheets and pay codes are covered by `orchardcore-workforce`; agent states by `orchardcore-workforce-contact-center`; the people themselves by `orchardcore-staff`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Time Tracker | `CrestApps.OrchardCore.TimeTracker` | `OrchardCore.Users`, `CrestApps.OrchardCore.Users` |

| Feature on | What it adds |
|---|---|
| `OrchardCore.AuditTrail` | Category `TimeTracker` with events `TimeEntryCreated`, `TimeEntryEnded`, `TimeEntryEdited`, `TimeEntryDeleted` (who, when and why). |
| `OrchardCore.Recipes.Core`, `OrchardCore.Deployment` | The `TimeTracker` recipe and deployment steps. |
| `CrestApps.OrchardCore.Recipes` | JSON schemas for both steps. |
| `CrestApps.OrchardCore.Reports` | The **Hours by Category** report (`time-tracker-hours-by-category`, under **Reports > Workforce & Payroll**). |
| `CrestApps.OrchardCore.Workforce.TimeClock` | Each worker's day from punches (depends on Time Tracker). |
| `CrestApps.OrchardCore.Workforce.TimeOff` | Approved time off kept as entries. |
| `CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracker` | Agent states as entries, over the time clock's work. |
| `CrestApps.OrchardCore.Staff` | A **My time** link (`~/Admin/time/my`, for users with `TrackOwnTime`) on **Staff > My Profile**, through `IStaffHomeLinkProvider`. See `orchardcore-staff`. |
| Tickets | A desk names the stage ticket work is recorded in; agents start and stop work on a ticket (`Source` `Ticket`, `SourceRef` `Ticket:{id}`) or **Log time** after the fact (`InsertAsync`); with ticket agreements, time is charged to the ticket's agreement. |
| Invoicing Billing | Bills ended entries at an hourly rate and marks them with `SetBilledAsync`. |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.TimeTracker",
        "CrestApps.OrchardCore.Reports"
      ],
      "disable": []
    }
  ]
}
```

## Records

All in the `TimeTracker` YesSql collection.

| Record | Key properties |
|---|---|
| `TimeCategory` | `Name` (unique), `Code`, `Color`, `Position` (report column order), `Factor` (default `1`; `1.5` counts a minute as 1.5), `Enabled` |
| `TimeStage` | `Name` (unique), `Code` (exports and mapping), `Color`, `Position`, `CategoryIds`, `SelfService` (default `true`; people may pick it on their clock), `Enabled` |
| `TimeEntry` | `UserId`, `UserName`, `StageId`, `StartUtc`, `EndUtc` (`null` while open), `Source`, `SourceRef` (such as `Ticket:{id}`), `ChargedTo` (a `RecordRef` the time counts against, such as `Agreement:{id}`), `Note`, `ManagedBy` (the clock that keeps it), `BilledInvoiceId`, `BilledLineId`, `CreatedBy`, `Changes` (`Utc`, `By`, `Reason`, `Before`) |

`TimeTrackerConstants.Sources`: `Self`, `Manager`, `Kiosk`, `Mobile`, `ContactCenter`, `TimeOff`, `Api`, `Import`, `Ticket`. People are recorded under their display name; the time entry editor's person picker lists staff (`audience = "Staff"`).

The indexes (`TimeEntryIndex`, `TimeCategoryIndex`, `TimeStageIndex`) are declared `.ExclusiveToDocumentType().SingleRecordPerDocument()`. `TimeEntryIndex` has a `ChargedTo` column (`Kind:Id`, indexed with `StartUtc`); migration 2 added it and requested the **TimeTracker.ChargeEntries** resumable step, which asks the charge resolvers about older entries 200 at a time.

## ITimeTrackerService

| Method | Does |
|---|---|
| `GetStatusAsync(userId)` | The open entry, if any. |
| `SwitchAsync(userId, userName, stageId, source, actor, sourceRef)` | Clocks in, or ends the open stage this second and starts the new one. Same stage and same `sourceRef` changes nothing; the same stage for another `sourceRef` (another ticket) ends one entry and starts the next. Refused for people another clock keeps. |
| `ClockOutAsync(userId, actor)` | Ends the open stage. |
| `AddAsync(entry, actor, reason)` | Records an ended entry (a manager's, an import's). |
| `InsertAsync(entry, actor, reason)` | Records an ended entry **over** the person's other time (work logged after the fact): covered entries are cut around it (one it falls inside becomes two, one it covers is removed), so the total doesn't change. Refused when it overlaps time of its own `Source` or a `ManagedBy` entry, ends in the future or has no end. Cut entries keep the reason in `Changes`. |
| `UpdateAsync(entryId, stageId, startUtc, endUtc, note, actor, reason)` | Changes stage, times or note; needs a reason, kept in `Changes`. Refused for a `ManagedBy` or billed entry. |
| `DeleteAsync(entryId, actor, reason)` | Needs a reason. Refused for a `ManagedBy` or billed entry. |
| `SetBilledAsync(entryId, invoiceId, lineId)` | Marks an ended entry billed on an invoice line, or with `invoiceId = null` lets it go. Refused for an open entry or one billed on another invoice. |
| `ListAsync(TimeEntryQuery)` | By `UserIds`, `StageId`, `FromUtc`, `ToUtc`, `Source`, `SourceRef`, `SourceRefs`, `ChargedTo`. |
| `GetTotalsAsync(query, timeZoneId, byDay)` | `TimeTotalsRow`s per person (or person and day, splitting entries over midnight): `MinutesByStage`, `MinutesByCategory` after factors, `TotalMinutes`. Open entries count up to now. |
| `SyncAsync(TimeEntrySync)` | Replaces the entries another clock keeps for a person over a range. |

`ITimeStageManager` and `ITimeCategoryManager` (`ICatalogManager<T>`) let other modules set up stages and categories.

```csharp
var result = await _timeTracker.SwitchAsync(userId, userName, workStage.ItemId, TimeTrackerConstants.Sources.Api, actor: userName);

if (result.Errors.Count > 0)
{
    // Refused: unknown stage, a clock keeps this person, and so on.
}

var totals = await _timeTracker.GetTotalsAsync(new TimeEntryQuery
{
    UserIds = [userId],
    FromUtc = weekStartUtc,
    ToUtc = weekEndUtc,
}, "America/New_York", byDay: true);
```

## Admin UI

| Menu | Routes | Permission |
|---|---|---|
| **Time Tracker > My Time** | `Admin/time/my` (`time/my/switch`, `time/my/out`) | `TrackOwnTime` |
| **Time Tracker > Time Entries** | `Admin/time/entries`, `time/entries/edit/{id?}`, `time/entries/delete/{id}` | `ViewTimeEntries` to list, `ManageTimeEntries` to change |
| **Time Tracker > Stages**, **Categories** | `Admin/time/settings/{kind}` (`stages`, `categories`), `time/stages/edit/{id?}`, `time/categories/edit/{id?}` | `ManageTimeTrackerSettings` |
| **Time Tracker > Contact Center Stages** | `Admin/time/contact-center-stages` (Contact Center Time Tracker feature) | `ManageTimeTrackerSettings` |
| **Reports > Workforce & Payroll > Hours by Category** | `Admin/Reports/view/time-tracker-hours-by-category` (default range this week from Monday, the site's time zone) | `ViewTimeEntries` |

My Time lets a person pick a self-service stage from a list. For a worker another clock keeps, it shows that clock's own buttons (`ExternalTimeClockInfo.ControlsViewComponent`, the Workforce `TimeClockControls` when the My Work portal is on) or links to it.

## Permissions

| Permission | Display name | Implied by | Default role |
|---|---|---|---|
| `ManageTimeTrackerSettings` | Manage time stages and categories | none | Administrator |
| `ManageTimeEntries` | Edit anyone's time | `ManageTimeTrackerSettings` | Administrator |
| `ViewTimeEntries` | View anyone's time and the time report | `ManageTimeEntries` | Administrator |
| `TrackOwnTime` | Track one's own time | `ViewTimeEntries` | Administrator |

Grant `TrackOwnTime` to the roles that should clock themselves in; no role but Administrator has it by default.

## Recipes and Deployment

The `TimeTracker` step creates or updates `Categories` and `Stages` (matched by `ItemId`, then by name, case-insensitive), then adds `Entries`. A new category or stage keeps a valid `ItemId` so records that point at it (activity codes, time-off types) still do. A stage names its categories by name in `categories`.

```json
{
  "steps": [
    {
      "name": "TimeTracker",
      "Categories": [
        { "name": "Payable", "code": "PAY", "color": "#2b8a3e", "position": 1 },
        { "name": "Billable", "code": "BILL", "position": 2 },
        { "name": "Overtime premium", "code": "OTP", "position": 3, "factor": 1.5 }
      ],
      "Stages": [
        { "name": "Work", "code": "WORK", "categories": [ "Payable", "Billable" ] },
        { "name": "Break", "code": "BRK", "categories": [ "Payable" ] },
        { "name": "Lunch", "code": "LUNCH", "categories": [] },
        { "name": "Training", "code": "TRN", "selfService": false, "categories": [ "Payable" ] }
      ],
      "Entries": [
        { "userName": "ana", "stage": "Work", "startUtc": "2026-11-02T14:00:00Z", "endUtc": "2026-11-02T22:00:00Z", "note": "Imported" }
      ]
    }
  ]
}
```

- An entry needs an existing `userName`, an existing `stage` name, `startUtc` and `endUtc`; it is skipped when the person already has time in that range, so reruns don't duplicate. `source` defaults to `Import`.
- The **Time stages, categories and entries** deployment step (category **Time Tracker**) exports categories and stages, and, with `IncludeEntries`, the ended entries of the last `Days` (31 by default).

## Other Clocks

| Seam | Use it to |
|---|---|
| `TimeEntrySync` + `SyncAsync` | Rebuild your entries (`ManagedBy` = your clock id) for a person over `FromUtc`..`ToUtc` from `BuildAsync`, called under the person's lock. Unchanged entries keep their ids; time kept here (a manager's entry) stays and your entries are cut around it. `ReplacesRangeOnly` rebuilds only the range (for a growing log); `Overrides` names clocks whose entries are cut around yours. |
| `IExternalTimeClock` | `FindAsync(userId)` says your clock keeps this person (My Time then refuses `SwitchAsync` and shows your controls); `FindByIdAsync(id)` names the clock for read-only entries. Returns `ExternalTimeClockInfo { Id, Name, Url, ManageUrl, ControlsViewComponent }`. |
| `ITimeEntryObserver` | `OnChangedAsync(kind, entry, before, actor, reason)` after every save (`Created`, `Ended`, `Edited`, `Deleted`); the Audit Trail is one. |
| `ITimeEntryChargeResolver` | `ResolveAsync(entry)` returns the `RecordRef` (`CrestApps.OrchardCore.Records`) a new entry with a source ref counts against, or `default` when it doesn't know the source. Asked once, inside the creating lock and save (by `SwitchAsync`, `AddAsync`, `InsertAsync` or a sync); the first non-empty answer wins and later changes to the source record never move logged time. Register with `services.TryAddEnumerable(ServiceDescriptor.Scoped<ITimeEntryChargeResolver, T>())`. Tickets' agreements feature charges ticket time to the ticket's agreement. |

```csharp
public sealed class RepairTimeChargeResolver : ITimeEntryChargeResolver
{
    private readonly RepairStore _repairs;

    public RepairTimeChargeResolver(RepairStore repairs)
    {
        _repairs = repairs;
    }

    public async Task<RecordRef> ResolveAsync(TimeEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.SourceRef is null || !entry.SourceRef.StartsWith("Repair:", StringComparison.Ordinal))
        {
            return default;
        }

        var repair = await _repairs.FindAsync(entry.SourceRef["Repair:".Length..], cancellationToken);

        return string.IsNullOrEmpty(repair?.ContractId) ? default : new RecordRef("Contract", repair.ContractId);
    }
}

// Time charged to one contract:
// var entries = await _timeTracker.ListAsync(new TimeEntryQuery { ChargedTo = new RecordRef("Contract", contractId) });
```

The clocks that ship:

| Clock id (`ManagedBy`) | From | Keeps |
|---|---|---|
| `Workforce.TimeClock` | Workforce Time Clock | After every punch, correction and removal, the worker's entries from a day before to a day after, paired like timesheets but without rounding. A span's stage is its activity code's `StageId`. Only workers with a login, not terminated, whose team uses a time clock policy. Earlier punches are brought in by the **Time clock to Time Tracker** task (25 workers every five minutes, once). |
| `Workforce.TimeOff` | Workforce Time Off | Approved time off once each day comes, in the stage of its type (`TimeOffType.StageId`; by default a stage named after the type, counting toward the `Time off` category, plus `Paid` when the type is paid). The **Time off to Time Tracker** task syncs hourly. |
| `Workforce.ContactCenter` | Contact Center Time Tracker | Each agent state (or break reason) in the stage set on **Contact Center Stages**, rebuilt from the state log with `ReplacesRangeOnly`, overriding the time clock while the agent is signed in. |

When the time clock starts, every activity code gets a stage (one with the same code or name, or a new one in the `Paid` and `Worked` categories). A worked example of a custom clock is in [references/time-tracker-integrations.md](references/time-tracker-integrations.md).

## Gotchas

| Symptom | Check |
|---|---|
| "Clock in" refused on My Time | Another clock keeps this person (the Workforce time clock); use its buttons, or remove the team's time clock policy. |
| Entry can't be edited or removed | It has `ManagedBy`; correct the punch (Workforce > Time Management > Timesheets) and the clock syncs it again. |
| Update or delete refused | A reason is required, the change would overlap another entry, the time is in the future, or the entry is billed on an invoice (take the line off or void the invoice first). |
| `InsertAsync` refused | The entry overlaps time of its own source (another ticket's) or a `ManagedBy` entry, has no end, or ends in the future. |
| Old entries have no `ChargedTo` | The **TimeTracker.ChargeEntries** resumable step runs a page at a time; entries no resolver knows stay uncharged. |
| Category totals look inflated | Check the category `Factor`. |
| Hours by Category missing | Enable `CrestApps.OrchardCore.Reports`; the old `/Admin/time/report` page is gone. |
| Recipe stage has no categories | Category names in `categories` must exist (imported earlier in the same step is fine). |
| Workers without a login don't appear | Time Tracker records users; the time clock skips workers without one. |

Related skills: `orchardcore-staff`, `orchardcore-invoicing` (Invoicing Billing bills tracked time at an hourly rate), `orchardcore-payroll` (pays hours that come from the Workforce time clock, not from tracked time), `orchardcore-workforce`, `orchardcore-workforce-contact-center`, `orchardcore-reports`, `orchardcore-crestapps-users`.
