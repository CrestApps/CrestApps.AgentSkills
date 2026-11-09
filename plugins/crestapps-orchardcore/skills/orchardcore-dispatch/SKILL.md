---
name: orchardcore-dispatch
description: Skill for the CrestApps Orchard Core Dispatch module (CrestApps.OrchardCore.Dispatch) with its Technician Mobile and Field Service API features. Covers service visits, the candidate finder, locked assignment that never double-books, crews and Fleet equipment, the Dispatch Board and day optimizer, customer notices and tracking links, visits at risk, the offline technician Field page, the APIs at api/work-orders/v1, api/dispatch/v1, api/geo/v1 and api/assets/v1, the field service AI tools, KPIs and the Field Service report data source, booking capacity through IAppointmentSlotFilter, the FieldServiceSample recipe and visit page panels. Use this skill when requests mention dispatching technicians, a dispatch board, scheduling visits, route optimization, technician mobile apps or field service APIs. Strong matches include ServiceVisit, IDispatchService, IDispatchCandidateFinder, IDispatchOptimizer, IVisitObserver, IVisitNoticeSender, DispatchSettings, DispatchMobileOptions and ManageDispatch.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Dispatch

## Plan Who Does Each Job and When

You are an Orchard Core expert. Generate accurate setup, recipes, API calls and extension code for the CrestApps Dispatch module. Each work order gets a **visit**; the visit is given technicians and a start, moved along its field statuses, and shown on a board with everyone's day.

### Guidelines

- Install `CrestApps.OrchardCore.Dispatch` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it, Work Orders, Assets, Workforce and Geo) in the web/startup project.
- Dispatch depends on Work Orders, Workforce and Geo. Technicians are Workforce workers on **published** shifts; nothing can be assigned until a schedule is published for their team.
- Contracts are in `CrestApps.OrchardCore.Dispatch.Abstractions`; services (`DefaultDispatchService`, `DefaultDispatchCandidateFinder`, `DefaultDispatchOptimizer`, `DispatchOptimizationService`, `FieldSyncService`) are in `CrestApps.OrchardCore.Dispatch.Core`.
- Always assign, move and change visits through `IDispatchService`. It takes a distributed lock per worker (`DISPATCH_RESOURCE_Worker_{id}`) and on the visit, re-checks availability inside the lock, and saves before releasing it.
- For a better optimizer, add the `orchardcore-or-tools` add-on rather than replacing the service.
- Work orders are covered by `orchardcore-work-orders`, maps and travel times by `orchardcore-geo`, equipment and van stock by `orchardcore-assets`, technicians, teams and shifts by `orchardcore-workforce` and `orchardcore-teams`, skills by `orchardcore-skills`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Dispatch | `CrestApps.OrchardCore.Dispatch` | `CrestApps.OrchardCore.WorkOrders`, `CrestApps.OrchardCore.Workforce`, `CrestApps.OrchardCore.Geo` |
| Technician Mobile | `CrestApps.OrchardCore.Dispatch.Mobile` | `CrestApps.OrchardCore.Dispatch`, `CrestApps.OrchardCore.Workforce.Portal`, `OrchardCore.Media` |
| Field Service API | `CrestApps.OrchardCore.Dispatch.Api` | `CrestApps.OrchardCore.Dispatch` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Workforce.ShiftPlanning",
        "CrestApps.OrchardCore.Dispatch",
        "CrestApps.OrchardCore.Dispatch.Mobile",
        "CrestApps.OrchardCore.Dispatch.Api"
      ],
      "disable": []
    }
  ]
}
```

### Optional Integrations

| Feature on | What it adds |
|---|---|
| `OrchardCore.SignalR` | Open boards reload when any visit changes (`DispatchHub`); otherwise every minute. |
| `OrchardCore.Workflows` | **Visit Assigned**, **Visit Status Changed** and **Visit At Risk** events. |
| `CrestApps.OrchardCore.Skills` | Required skills (lowest level) and certifications checked on the day. |
| `CrestApps.OrchardCore.Assets.Fleet` | Required equipment (asset types) booked on Equipment calendars (`FleetDispatchEquipmentService`). |
| `OrchardCore.Sms`, `OrchardCore.Email` | Customer notices by text when the contact has a phone, else email. |
| `CrestApps.OrchardCore.AI` | The seven field service AI tools. |
| `OrchardCore.DataPipelines.Core` | The **Field Service** report data source. |
| `CrestApps.OrchardCore.Scheduling` | Booking capacity (`DispatchCapacitySlotFilter`). |
| `CrestApps.OrchardCore.Inventory` with Technician Mobile | Stock locations for parts used, the technician's van first (`IFieldStockSource`). |

## Visits

`ServiceVisit` lives in the `Dispatch` YesSql collection: `Number` (`V-000001`, series `Dispatch.Visit`), `WorkOrderId` with copies of the work order's number, summary, type, priority, territory and due time, `Status`, `DurationMinutes`, `StartUtc`, `EndUtc`, `ArrivalStartUtc`, `ArrivalEndUtc`, `LeadWorkerId`, `WorkerIds`, `CrewSize`, `RequiredEquipmentTypeIds`, `EquipmentAssetIds`, `EtaUtc`, `AtRiskReason`, `Notices`, `Pinned`, `TravelMinutes`, `TravelKm`, `IncompleteReason`, `ReturnVisitId`, `CalendarEvents` and `History`.

`VisitStatus` is `Unscheduled`, `Scheduled`, `Dispatched`, `EnRoute`, `OnSite`, `Paused`, `Completed`, `Incomplete` or `Canceled`, moved only along `VisitStatusRules`.

| `IDispatchService` method | Does |
|---|---|
| `CreateAsync(workOrder, actor)` | An unscheduled visit (also done automatically for new work orders when `CreateVisitsForNewWorkOrders` is on). |
| `CreateMissingAsync(actor)` | A visit for every open work order without one. |
| `AssignAsync(visitId, VisitAssignment, actor)` | `WorkerIds` (first leads), `StartUtc`, optional `DurationMinutes` and `Pinned`. Checks published shift, breaks, time off, other visits with travel and buffer, crew size and equipment; sets the arrival window, the lead's trip and a **Job** event on each worker's team calendar; moves the work order to Scheduled. |
| `UnassignAsync`, `PinAsync` | Back to unscheduled; pinned visits are not moved by the optimizer. |
| `ChangeStatusAsync(visitId, status, note, actor)` | En route and on site start the work order; canceling needs a reason and removes job events; incomplete needs a reason and makes a return visit. |

`IDispatchCandidateFinder.FindAsync` lists every active worker between two times: ruled-out ones with the reason (excluded, missing skill or certification, outside a strict territory, no shift, time off, not free long enough), the rest with their earliest start (`SlotFitter`) and a score from the `DispatchObjective` weights. Travel comes from `IGeoService`.

## Board, Optimizer, Notices and Risk

- **Dispatch Board** (`dispatch/board`): a row per technician with shift bands, time off and visits, the unscheduled list, and a Leaflet map. Drag to assign (quarter-hour steps), **Find technician**, and **Optimize the day**.
- **Optimizer**: `IDispatchOptimizationService.ProposeAsync(day, workerIds, includeUnscheduled)` builds a `DispatchProblem` and runs the **last registered** `IDispatchOptimizer` (`DefaultDispatchOptimizer` by default: cheapest insertion plus move, swap and reverse within `OptimizerSeconds`). Nothing changes until `ApplyAsync(moves, actor)`, which assigns each move through `AssignAsync`.
- **Notices**: with `NoticesEnabled`, `VisitNoticeService` sends Scheduled, Reminder (from `ReminderHour`), EnRoute, Completed and Canceled texts with `{Company}`, `{Number}`, `{Date}`, `{Window}`, `{Technician}`, `{Eta}` and `{TrackingUrl}`. The tracking page `track/{token}` needs no sign-in, is signed with data protection, lasts a week and stops when the visit is over.
- **At risk**: the **Dispatch: visits at risk** task (every five minutes) flags today's visits running over, not started, arriving late or with the technician on time off (`AtRiskMinutes`).

## Settings

**Settings > Dispatch** (site settings group `dispatch`, `DispatchSettings`): `CreateVisitsForNewWorkOrders` (true), `ArrivalWindowMinutes` (120), `HorizonDays` (7), `BufferMinutes` (10), `StrictTerritory`, `TimeZoneId`, `Objective` (`TravelWeight` 5, `UrgencyWeight` 3, `ProficiencyWeight` 2, `PreferredWeight` 4, `BalanceWeight` 2), `AtRiskMinutes` (15), `OptimizerSeconds` (5), `NoticesEnabled`, `TrackingEnabled` (true), `ReminderHour` (17), `ScheduledText`, `ReminderText`, `EnRouteText`, `CompletedText`, `CanceledText`, `BookingWorkOrderTypeId`.

```json
{
  "steps": [
    {
      "name": "Settings",
      "DispatchSettings": {
        "TimeZoneId": "America/New_York",
        "ArrivalWindowMinutes": 120,
        "BufferMinutes": 10,
        "StrictTerritory": true,
        "NoticesEnabled": true,
        "Objective": { "TravelWeight": 5, "UrgencyWeight": 3, "ProficiencyWeight": 2, "PreferredWeight": 4, "BalanceWeight": 2 }
      }
    }
  ]
}
```

## Admin UI

| Menu | Route | Permission |
|---|---|---|
| **Field Service > Dispatch Board** | `dispatch/board` (data from `dispatch/board/data`, moves to `dispatch/board/move`) | `ViewDispatch` |
| **Field Service > Visits** | `dispatch/visits`, `dispatch/visits/{id}` | `ViewDispatch` |
| **Reports > Field Service > KPIs** | `dispatch/kpis` | `ViewDispatch` |
| **Settings > Dispatch** | site settings group `dispatch` | `ManageDispatchSettings` |
| Get started (not in the menu, linked from the board) | `dispatch/get-started` | `ManageDispatchSettings` |
| My Work **Field** page (Technician Mobile) | `~/Admin/my-work/field` | Use the My Work portal, and a worker profile |

## Permissions

| Permission | Display name | Implied by | Default role |
|---|---|---|---|
| `ManageDispatchSettings` | Manage dispatch settings | none | Administrator |
| `ManageDispatch` | Dispatch visits | `ManageDispatchSettings` | Administrator |
| `ViewDispatch` | View visits and the dispatch board | `ManageDispatch` | Administrator |

## Recipes and Deployment

- Dispatch has no recipe step of its own. Its settings travel in Orchard Core's `Settings` step (above); the **Dispatch settings** deployment step exports them.
- Visits, the technician app's command receipts, consent and positions are never exported.
- The **Field service sample** recipe (`FieldServiceSample`, not a setup recipe) enables Geo, Assets with Fleet, Work Orders, Workforce with Shift Planning and My Work, Dispatch and Technician Mobile, and adds a team with a dispatcher and three technicians, a shift template, two territories, asset types and assets, a checklist, a service level, a sub-status and two work order types with fixed ids. Publish a schedule before dispatching. Its full shape is in [references/field-service-sample.md](references/field-service-sample.md).

## Workflow Events

Category **Dispatch**. Each receives `VisitId`, `Number`, `WorkOrderId`, `WorkOrderNumber`, `Status`, `LeadWorkerId`, `WorkerIds`, `StartUtc`, `EndUtc`, `ArrivalStartUtc` and `ArrivalEndUtc`.

| Activity class | Display name | Event name | Extra input |
|---|---|---|---|
| `VisitAssignedEvent` | Visit Assigned | `VisitAssigned` | `PreviousStatus`, `Moved` |
| `VisitStatusChangedEvent` | Visit Status Changed (can be limited to statuses) | `VisitStatusChanged` | `PreviousStatus` |
| `VisitAtRiskEvent` | Visit At Risk | `VisitAtRisk` | `Reason` |

## AI Tools

With `CrestApps.OrchardCore.AI` on, these are selectable on an AI profile under **Field Service**; all but the last run as the signed-in user with the admin page's permission.

| Tool name | Title | Permission |
|---|---|---|
| `workOrderCreate` | Create a work order | `ManageWorkOrders` |
| `workOrderStatus` | Work order status | `ViewWorkOrders` |
| `workOrderSummarize` | Summarize a work order | `ViewWorkOrders` |
| `dispatchFindTechnician` | Find a technician | `ViewDispatch` |
| `dispatchOptimizeAndExplain` | Optimize and explain a day (call again with `apply` and approved visit numbers) | `ManageDispatch` |
| `dispatchRescheduleVisit` | Reschedule a visit | `ManageDispatch` |
| `dispatchWhereIsMyTechnician` | Where is my technician (customer-facing; finds visits only by the caller's phone or tracking token) | none |

## Field Service API and Technician Mobile

The **Field Service API** feature maps `api/work-orders/v1`, `api/dispatch/v1`, `api/geo/v1` and (with Assets) `api/assets/v1` under the `Api` authentication scheme, with the admin pages' permissions, enum names as strings and RFC 9457 problem details. **Technician Mobile** adds the offline Field page and `api/dispatch/v1/mobile` (`GET pack`, `POST sync`, `POST consent`, `POST pings`). Endpoint tables and request bodies are in [references/field-service-api.md](references/field-service-api.md).

Technician Mobile reads `DispatchMobileOptions` from the tenant configuration section `CrestApps:Dispatch:Mobile` (described in the module's `ConfigurationSchema.json`):

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Dispatch": {
        "Mobile": {
          "OfflineDays": 2,
          "PingIntervalSeconds": 60,
          "LocationRetentionDays": 30,
          "ReceiptRetentionDays": 30,
          "ConsentNoticeVersion": "1",
          "MaxFileKilobytes": 4096,
          "MaxCommandsPerSync": 200
        }
      }
    }
  }
}
```

## Booking Capacity

With Scheduling on, `DispatchCapacitySlotFilter` (an `IAppointmentSlotFilter`) narrows the slots of every appointment type a work order type lists under **Bookings**: a slot stays only while enough technicians are on a published shift and free for the whole job (skills, certifications, crew size, length), less one for each unassigned (`New` or `Ready`) overlapping booking of the type, and `Remaining` is lowered to match. Appointment types no work order type lists keep their slots.

## Reports and KPIs

**Reports > Field Service > KPIs** (`FieldServiceKpiService`, last 30 days by default, at most a year, at most 5,000 visits and 5,000 work orders) shows first-time fix, on-time arrival, jobs per technician a day, return visits, travel against time on site, response and resolution compliance, time to repair, revenue per work order, repeat work and parts used by stock location.

With `OrchardCore.DataPipelines.Core`, the `FieldService` report data source offers `FieldServiceWorkOrders` (`ViewWorkOrders`) and `FieldServiceVisits` (`ViewDispatch`), and with Assets `FieldServiceAssets`, `FieldServiceMeterReadings` and `FieldServiceServiceHistory`. Add data sets with `IFieldServiceReportDataSet`. See `orchardcore-report-builder`.

## Extension Points

| Seam | Use it to |
|---|---|
| `IVisitObserver` | React after visit events (`DispatchConstants.Events`: `VisitCreated`, `VisitAssigned`, `VisitUnassigned`, `VisitStatusChanged`, `VisitAtRisk`). |
| `IDispatchOptimizer` | Plug a better solver (`Name`, `SolveAsync(DispatchProblem)`); the last registered wins. |
| `IDispatchEquipmentService` | Equipment from somewhere other than Fleet. |
| `IVisitNoticeSender` | Another customer notice channel (`Channel`, `CanSend`, `SendAsync`). |
| `IDispatchSettingsProvider` | Settings from somewhere other than site settings. |
| `IFieldFileStore`, `IFieldStockSource`, `IFieldAssetSource` | Technician app photo storage, stock locations and equipment. |
| `DisplayDriver<WorkOrderPanels>` | A panel on the work order page (Dispatch adds `WorkOrderVisits` at `Content:5`). |
| `DisplayDriver<ServiceVisit>` | A panel on the visit page `dispatch/visits/{id}`, built with `IDisplayManager<ServiceVisit>` and the `Detail` display type. A visit has no editor form. |

### A Visit Page Panel

```csharp
public sealed class VisitEquipmentPanelDisplayDriver : DisplayDriver<ServiceVisit>
{
    public override IDisplayResult Display(ServiceVisit visit, BuildDisplayContext context)
        => Initialize<VisitEquipmentPanelViewModel>("VisitEquipmentPanel", model =>
        {
            model.Number = visit.Number;
            model.EquipmentAssetIds = visit.EquipmentAssetIds;
        }).Location("Content:5");
}

public class VisitEquipmentPanelViewModel
{
    public string Number { get; set; }

    public IList<string> EquipmentAssetIds { get; set; } = [];
}

// Startup: services.AddDisplayDriver<ServiceVisit, VisitEquipmentPanelDisplayDriver>();
```

### A Notice Channel

```csharp
public sealed class LogVisitNoticeSender : IVisitNoticeSender
{
    private readonly ILogger _logger;

    public LogVisitNoticeSender(ILogger<LogVisitNoticeSender> logger)
    {
        _logger = logger;
    }

    public string Channel => "Log";

    public bool CanSend(VisitNoticeMessage message) => !string.IsNullOrEmpty(message.Email);

    public Task<string> SendAsync(VisitNoticeMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("{Kind} notice for visit {Number}: {Text}", message.Kind, message.Visit.Number, message.Text);

        // Return null when sent, or the reason it failed.
        return Task.FromResult<string>(null);
    }
}

// Startup: services.AddScoped<IVisitNoticeSender, LogVisitNoticeSender>();
```

## Gotchas

| Symptom | Check |
|---|---|
| Every technician is ruled out | Publish a schedule for the team; check skills, certifications, territory (`StrictTerritory`) and time off. |
| Assign refused with "This job needs 2 people." | The type's `CrewSize`; pass enough `WorkerIds`. |
| Assign refused "No ... is free then." | Fleet has no free active schedulable asset of the required type. |
| Two dispatchers, one wins | Expected: the lock per worker re-checks; the loser is told who is in the way. |
| Board does not update live | Enable `OrchardCore.SignalR`. |
| No customer texts | `NoticesEnabled`, `OrchardCore.Sms` or `OrchardCore.Email`, and a contact phone or email on the work order. |
| Field page answers `moved` or `canceled` | The server wins: the visit was reassigned or canceled; it leaves the device with the next pack. |
| Recipe records lose links on rerun | Use 26-character lowercase Orchard Core ids for every `ItemId`. |

Related skills: `orchardcore-work-orders`, `orchardcore-geo`, `orchardcore-assets`, `orchardcore-or-tools`, `orchardcore-workforce`, `orchardcore-teams`, `orchardcore-skills`, `orchardcore-scheduling`, `orchardcore-calendars`, `orchardcore-inventory`, `orchardcore-report-builder`, `orchardcore-ai-tools`.
