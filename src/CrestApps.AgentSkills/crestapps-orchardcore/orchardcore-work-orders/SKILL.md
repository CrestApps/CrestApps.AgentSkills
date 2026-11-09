---
name: orchardcore-work-orders
description: Skill for the CrestApps Orchard Core Work Orders module (CrestApps.OrchardCore.WorkOrders) and its Service Agreements feature. Covers work order types and their defaults, checklists with conditional questions, service levels whose SLA clock runs in opening hours and pauses while waiting, sub-statuses and status rules, parts used from Inventory stock locations, invoices through Transactions, follow-ups, work orders from Scheduling bookings and dispositions, recurring work orders from agreement RRULEs, the WorkOrders recipe and deployment steps, workflow events, permissions, and extending the editor and page with DisplayDriver of WorkOrder and WorkOrderPanels. Use this skill when requests mention field service jobs, work orders, SLAs, checklists, maintenance agreements or work order invoices. Strong matches include WorkOrder, WorkOrderType, ChecklistTemplate, ServiceLevelPolicy, ServiceAgreement, WorkOrderService, IWorkOrderHandler, IWorkOrderObserver, IServiceHoursProvider and WorkOrderPanels.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Work Orders

## Keep the Jobs Done for Customers

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Work Orders module. A work order is made from a **type**, which gives it its duration, priority, checklists, lines, service level and billing; it then moves through its statuses until it is completed, invoiced and closed.

### Guidelines

- Install `CrestApps.OrchardCore.WorkOrders` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it) in the web/startup project.
- Contracts live in `CrestApps.OrchardCore.WorkOrders.Abstractions` (models, `IWorkOrderHandler`, seams) and services in `CrestApps.OrchardCore.WorkOrders.Core` (`WorkOrderService`, `SlaClock`, `SlaMonitor`, `ServiceAgreementScheduler`). Reference those from your own module, not the module project.
- The module has no hard dependencies; Inventory, Transactions, Geo, Assets, Skills, Scheduling, Omnichannel Management, Accounts and Workflows each light up an integration when enabled.
- Always change a work order through `WorkOrderService` (create, update, status, parts, answers, signatures, notes, complete, follow-up). It runs the handlers, the status rules, the SLA clock and the events.
- `WorkOrder.CustomerId` and `ServiceAgreement.CustomerId` hold an Orchard Core **user id** (party kind `User`), not a Customers record id. The company is `AccountId` (an Accounts content item id).
- Visits, technicians and the dispatch board belong to `orchardcore-dispatch`; equipment to `orchardcore-assets`; territories and geocoding to `orchardcore-geo`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Work Orders | `CrestApps.OrchardCore.WorkOrders` | none |
| Service Agreements | `CrestApps.OrchardCore.WorkOrders.Agreements` | `CrestApps.OrchardCore.WorkOrders` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.WorkOrders",
        "CrestApps.OrchardCore.WorkOrders.Agreements"
      ],
      "disable": []
    }
  ]
}
```

### Integrations Lit Up by Other Features

| Feature on | What it adds |
|---|---|
| `CrestApps.OrchardCore.Inventory` | Lines name a stock item; `UsePartAsync` takes counted parts off a stock location (such as a van's) with `IInventoryService.ConsumeAsync`, reference type `WorkOrder` (`InventoryWorkOrderParts`). |
| `CrestApps.OrchardCore.Transactions` | Completing a billed work order makes an outstanding invoice: source `WorkOrders`, `ReferenceType` `WorkOrder`, due in 30 days, with an invoice number (`TransactionsWorkOrderBilling`). |
| `CrestApps.OrchardCore.Geo` | A new or changed service address gets its territory and is queued for geocoding as target type `WorkOrderServiceAddress`; the editor places the job with a map pin (`GeocodeSource` `Pin`). |
| `CrestApps.OrchardCore.Assets` | Work orders can be for assets; customer, account and contact pickers search through `IPartyDirectory`. |
| `CrestApps.OrchardCore.Skills` | A type edits its required skills (with a lowest level) and certifications, for Dispatch. |
| `CrestApps.OrchardCore.Scheduling` | A type lists appointment types (`AppointmentTypeIds`); `AppointmentWorkOrderObserver` makes, moves and cancels a work order per confirmed booking. |
| `CrestApps.OrchardCore.Omnichannel.Managements` | The **Create Work Order** subject action (`CreateWorkOrder`) and a **Work orders** card on CRM contacts. |
| `CrestApps.OrchardCore.Accounts` | **Work orders** (and **Service agreements**) in the account page's **Service** tab; an account location fills an empty service address. |
| `OrchardCore.Workflows` | The work order and agreement events. |
| `CrestApps.OrchardCore.Dispatch` | Visits for each work order, shown in a panel on its page. |

## Records

All records live in the `WorkOrders` YesSql collection and are `CatalogItem`s matched by `ItemId`.

| Record | Key properties |
|---|---|
| `WorkOrderType` | `Name`, `Color`, `DefaultDurationMinutes` (60), `RequiredSkills` (`SkillId`, `MinProficiency`), `RequiredCertificationIds`, `CrewSize`, `RequiredEquipmentTypeIds`, `ChecklistTemplateIds`, `DefaultLines`, `ServiceLevelPolicyId`, `BillingMode` (`NotBillable`, `FixedPrice`, `TimeAndMaterials`), `Currency` (`USD`), `FixedPrice`, `DefaultPriority`, `RequiresSignature`, `RequiredPhotos`, `AppointmentTypeIds` |
| `WorkOrder` | `Number` (`WO-000001`, series `WorkOrders.WorkOrder`), `TypeId`, `Priority` (`Low`, `Normal`, `High`, `Urgent`), `Status` (`New`, `Ready`, `Scheduled`, `InProgress`, `OnHold`, `Completed`, `Closed`, `Canceled`), `SubStatusId`, `Summary`, `Description`, `CustomerId`, `AccountId`, `ContactId`, `Contact` (`Name`, `Phone`, `Email`), `ServiceAddress`, `AccessNotes`, `TerritoryId`, `AssetIds`, `EarliestUtc`, `LatestUtc`, `Sla`, `Lines`, `Checklists`, `Attachments`, `Signatures`, `Notes`, `Source`, `SourceId`, `ParentWorkOrderId`, `InvoiceTransactionId`, `AgreementId`, `AgreedPrice` |
| `WorkOrderLine` | `Id`, `Kind` (`Product`, `Service`, `Labor`, `Expense`), `ProductId`, `StockItemId`, `Description`, `QuantityEstimated`, `QuantityUsed`, `UnitPrice`, `LocationId`, `Billable` |
| `ChecklistTemplate` | `Name`, `Questions` (`Id`, `Section`, `Text`, `Kind`, `Required`, `Help`, `Min`, `Max`, `Unit`, `Choices`, `MinPhotos`, `Meter`, `ShowWhen` with `QuestionId` and `EqualsValue`) |
| `ServiceLevelPolicy` | `Name`, `Targets` (`Priority`, `ResponseMinutes`, `ResolveMinutes`), `WeeklyHours` (`Days`, `Opens`, `Closes`) or `HoursCalendarId`, `TimeZoneId`, `PauseStatuses`, `WarningPercent` (80) |
| `WorkOrderSubStatus` | `Name`, `ParentStatus`, `Color`, `PausesSla` |

`ChecklistQuestionKind` is `YesNo`, `PassFailNA`, `Text`, `Number`, `Choice`, `MultiChoice`, `Photo`, `Signature`, `MeterReading` or `Date`. `WorkOrderSource` is `Manual`, `Appointment`, `Agreement`, `Activity`, `Api`, `AI` or `Workflow`.

## Behavior

`WorkOrderService` (in `WorkOrders.Core`) is the single entry point:

| Method | Does |
|---|---|
| `NewAsync(typeId)` | A new work order with the type's defaults and copies of its checklists. |
| `CreateAsync(workOrder, actor)` | Numbers it, sets SLA due times, runs every `IWorkOrderHandler.CreatingAsync`, raises **Created**. |
| `UpdateAsync(workOrder, actor)` | Runs `UpdatingAsync` again (a changed address is placed again). |
| `ChangeStatusAsync(id, status, subStatusId, note, actor)` | Only allowed moves (`WorkOrderStatusRules`); closed and canceled never move, completed can close or reopen, canceling needs a reason, a sub-status must belong to the status. |
| `UsePartAsync(id, lineId, quantity, locationId, actor)` | Records parts used; counted parts come off the location through every `IWorkOrderPartsService`, refusing more than it has. |
| `AnswerAsync(id, checklistIndex, questionId, value, actor, answeredUtc)` | Validates an answer against its question kind, range and choices. |
| `SignAsync`, `AttachAsync`, `AddNoteAsync` | Signatures, media library attachments and notes. |
| `CompleteAsync(id, actor)` | Refuses until shown required questions, a required signature and `RequiredPhotos` are in; invoices the billable amount through `IWorkOrderBilling`; raises **Completed**. |
| `FollowUpAsync(id, summary, actor)` | A new work order of the same type for the same customer, contact, place and assets (`ParentWorkOrderId`). |
| `BillableAmount(workOrder, type)` | Static: the fixed price, the agreed price, or billable lines at used (or planned) quantities. |

**SLA clock.** `SlaClock` counts targets in the policy's `WeeklyHours` in its `TimeZoneId`, in the hours an `IServiceHoursProvider` gives for `HoursCalendarId`, or around the clock. The module registers `OpenHoursServiceHoursProvider`, which reads the shared business hours (`IOpenHoursService`), so Contact Center business hours calendars can drive it. The clock stops in a pause status or a sub-status with `PausesSla`, and due times move out by the paused time. Moving to Ready, Scheduled or In progress is the first response. The **Work order service levels** background task runs `SlaMonitor` every minute and raises the warning (at `WarningPercent`) and the breach once each.

## Service Agreements

`ServiceAgreement` (`Number` `SA-00001`, `Name`, `Status` `Draft`/`Active`/`Expired`/`Canceled`, `CustomerId`, `AccountId`, `Contact`, `ServiceAddress`, `AccessNotes`, `AssetIds`, `StartDate`, `EndDate`, `TimeZoneId`, `Schedules`, `Pricing` `Included`/`PerVisit`, `PricePerVisit`, `ExpiringNoticeDays` 30). Each `AgreementSchedule` has `Id`, `Name`, `TypeId`, `Rule` (RFC 5545 such as `FREQ=MONTHLY;INTERVAL=6`), `LeadDays` (14) and optional `AssetIds`.

The **Service agreement visits** background task (hourly, at minute 5) runs `ServiceAgreementScheduler`: for every visit day within the lead time it makes one work order (source `Agreement`, source id = agreement, schedule and day), windowed to that day in the agreement's time zone, with `AgreedPrice` zero when included or the per-visit price. Past the end date the agreement expires; within its notice it raises **Service Agreement Expiring** once (`IServiceAgreementObserver`).

## Admin UI

| Menu | Route | Permission |
|---|---|---|
| **Field Service > Work Orders** | `work-orders`, `work-orders/new/{typeId}`, `work-orders/edit/{id}`, `work-orders/view/{id}` | `ViewWorkOrders` (list, page), `ManageWorkOrders` (edit) |
| **Field Service > Service Agreements** | `work-orders/agreements`, `work-orders/agreements/edit/{id?}`, `work-orders/agreements/view/{id}` | `ManageServiceAgreements` |
| **Field Service > Work Order Types**, **Checklists**, **Service Levels**, **Sub-statuses** | `WorkOrderSettings` with `kind` `types`, `checklists`, `service-levels`, `sub-statuses` | `ManageWorkOrderSettings` |

The customer and contact pickers call `Admin/api/crestapps/work-orders/customers/search?kind=&query=` (requires `ManageWorkOrders`); the account picker uses the Accounts search.

## Permissions

| Permission | Display name | Implied by | Default role |
|---|---|---|---|
| `ManageWorkOrderSettings` | Manage work order types, checklists and service levels | none | Administrator |
| `ManageWorkOrders` | Manage work orders | `ManageWorkOrderSettings` | Administrator |
| `ViewWorkOrders` | View work orders | `ManageWorkOrders` | Administrator |
| `ManageServiceAgreements` | Manage service agreements (Service Agreements feature) | none | Administrator |

## Recipes and Deployment

The `WorkOrders` step (needs `OrchardCore.Recipes.Core`) creates or updates by `ItemId`, with the editor's validation, the sections `Checklists`, `ServiceLevels`, `SubStatuses`, `Types` and, with Service Agreements on, `Agreements`, in that order. Properties use the model names (PascalCase) and enums are written as names.

```json
{
  "steps": [
    {
      "name": "WorkOrders",
      "SubStatuses": [
        { "ItemId": "4fqsx194f332rzgh3s4wshe051", "Name": "Waiting on parts", "ParentStatus": "OnHold", "Color": "#e8590c", "PausesSla": true }
      ],
      "Types": [
        {
          "ItemId": "4fsyaebpefs8a296tfsf55b5ec",
          "Name": "Boiler service",
          "Color": "#0c8599",
          "DefaultDurationMinutes": 90,
          "CrewSize": 1,
          "DefaultPriority": "Normal",
          "BillingMode": "FixedPrice",
          "Currency": "USD",
          "FixedPrice": 149,
          "RequiresSignature": true
        }
      ]
    }
  ]
}
```

Full checklists, service levels, types with lines and agreements are in [references/work-order-recipes.md](references/work-order-recipes.md).

- Use 26-character lowercase Orchard Core ids for `ItemId`; any other value is replaced with a new id, breaking references and duplicating records on rerun.
- The **Work order settings** deployment step (category **Work**, step `WorkOrdersDeploymentStep`, `IncludeAgreements` false by default) exports the same sections. Work orders themselves are never exported (numbers and SLA clocks belong to their site).

## Workflow Events

All are in the **Work Orders** category, can be limited to some work order types, and receive `WorkOrderId`, `Number`, `TypeId`, `Status`, `Priority`, `Summary`, `CustomerId` and `TerritoryId`.

| Activity class | Display name | Event name | Extra input |
|---|---|---|---|
| `WorkOrderCreatedEvent` | Work Order Created | `WorkOrderCreated` | |
| `WorkOrderStatusChangedEvent` | Work Order Status Changed | `WorkOrderStatusChanged` | `PreviousStatus` |
| `WorkOrderSlaWarningEvent` | Work Order Service Level Warning | `WorkOrderSlaWarning` | `Target` |
| `WorkOrderSlaBreachedEvent` | Work Order Service Level Breached | `WorkOrderSlaBreached` | `Target` |
| `WorkOrderCompletedEvent` | Work Order Completed | `WorkOrderCompleted` | |
| `ServiceAgreementExpiringEvent` | Service Agreement Expiring | `ServiceAgreementExpiring` | `AgreementId`, `Number`, `Name`, `EndDate` |

## Extension Points

| Seam | Use it to |
|---|---|
| `IWorkOrderHandler` | Change a work order before it is created (`CreatingAsync`) or saved (`UpdatingAsync`). |
| `IWorkOrderObserver` | React after events (`OnEventAsync` with a `WorkOrdersConstants.Events` name). |
| `IWorkOrderPartsService`, `IWorkOrderBilling` | Take parts off stock or invoice somewhere other than Inventory and Transactions. |
| `IServiceHoursProvider` | Open hours for a policy calendar (`GetOpenAsync` returns `TimeWindow`s) and `ListCalendarsAsync` for the **Hours calendar** picker. |
| `IServiceAgreementObserver` | React when an agreement is expiring. |
| `DisplayDriver<WorkOrderPanels>` | Add a panel to the work order page (`work-orders/view/{id}`, display type `Detail`). |
| `DisplayDriver<WorkOrder>` | Add a section to the **New work order** and **Edit** editor, built with `IDisplayManager<WorkOrder>`. |

### Default a Field Before Create

```csharp
public sealed class UrgentAccessNotesHandler : IWorkOrderHandler
{
    public Task CreatingAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        if (workOrder.Priority == WorkOrderPriority.Urgent && string.IsNullOrEmpty(workOrder.AccessNotes))
        {
            workOrder.AccessNotes = "Call the contact before arriving.";
        }

        return Task.CompletedTask;
    }
}

// Startup: services.AddScoped<IWorkOrderHandler, UrgentAccessNotesHandler>();
```

### React to Completion

```csharp
public sealed class CompletedWorkOrderObserver : IWorkOrderObserver
{
    private readonly ILogger _logger;

    public CompletedWorkOrderObserver(ILogger<CompletedWorkOrderObserver> logger)
    {
        _logger = logger;
    }

    public Task OnEventAsync(WorkOrder workOrder, string eventName, IReadOnlyDictionary<string, object> detail, CancellationToken cancellationToken = default)
    {
        if (eventName == WorkOrdersConstants.Events.Completed)
        {
            _logger.LogInformation("Work order {Number} was completed.", workOrder.Number);
        }

        return Task.CompletedTask;
    }
}

// Startup: services.AddScoped<IWorkOrderObserver, CompletedWorkOrderObserver>();
```

### Add a Panel or an Editor Section

The built-in editor fields are one driver, the `WorkOrderFields_Edit` shape at `Content:1`; Dispatch's visits panel is `WorkOrderVisits` at `Content:5`. A full panel driver and an editor section driver (prefix, validation, placement) are in [references/work-order-extensions.md](references/work-order-extensions.md).

- Register with `services.AddDisplayDriver<WorkOrderPanels, TDriver>()` or `services.AddDisplayDriver<WorkOrder, TDriver>()`.
- In an editor section, bind your own view model with the driver's `Prefix` and add errors under `$"{Prefix}.Field"`. Any error keeps the work order from being saved, and the editor cancels the session, so records your driver wrote during the update are discarded too.

## Gotchas

| Symptom | Check |
|---|---|
| Completing is refused | Only an In progress work order can be completed; or a shown required question is unanswered, the type requires a signature, or fewer than `RequiredPhotos` attachments. |
| No invoice after completing | Transactions must be enabled and the type billable (or `AgreedPrice` set). |
| Parts are not taken off stock | Inventory must be on, the line needs a `StockItemId`, and the location must have the units. |
| SLA due times look wrong | Check the policy's `TimeZoneId`, `WeeklyHours` or `HoursCalendarId` (a missing calendar counts around the clock). |
| Status change refused | `WorkOrderStatusRules` blocks moves out of Closed or Canceled; canceling needs a note. |
| Agreement makes no work orders | It must be `Active`, the `Rule` must parse, and the visit day must be within `LeadDays`. |
| `WorkOrders` step does nothing for agreements | Enable `CrestApps.OrchardCore.WorkOrders.Agreements` before the step runs. |

Related skills: `orchardcore-dispatch`, `orchardcore-assets`, `orchardcore-geo`, `orchardcore-inventory`, `orchardcore-transactions`, `orchardcore-scheduling`, `orchardcore-skills`, `orchardcore-accounts`, `orchardcore-contact-center`, `orchardcore-addresses`.
