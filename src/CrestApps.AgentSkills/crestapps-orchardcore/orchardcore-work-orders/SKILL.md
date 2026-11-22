---
name: orchardcore-work-orders
description: Skill for the CrestApps Orchard Core Work Orders module (CrestApps.OrchardCore.WorkOrders) and its Service Agreements feature. Covers work order types, checklists with ConditionRule questions, response and resolution timers on the shared Service Levels block, sub-statuses and status rules, notes on the shared Timeline, parts from Inventory stock locations, billing through Invoicing or Transactions, follow-ups, work orders from bookings, dispositions and tickets, recurring visits on Agreements, preferred and excluded technicians, the WorkOrders recipe and deployment steps, upgrades, workflow events, permissions, and editor and page display drivers. Use this skill when requests mention field service jobs, work orders, SLAs, checklists, maintenance agreements or work order invoices. Strong matches include WorkOrder, WorkOrderType, ChecklistTemplate, WorkOrderServiceLevelSettings, WorkOrderAgreementTerms, WorkOrderService, IWorkOrderHandler, IWorkOrderObserver, IWorkOrderBilling and WorkOrderPanels.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Work Orders

## Keep the Jobs Done for Customers

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Work Orders module. A work order is made from a **type**, which gives it its duration, priority, checklists, lines, service level policy and billing; it then moves through its statuses until it is completed, invoiced and closed.

### Guidelines

- Install `CrestApps.OrchardCore.WorkOrders` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it) in the web/startup project.
- Contracts live in `CrestApps.OrchardCore.WorkOrders.Abstractions` (models, `IWorkOrderHandler`, `IWorkOrderBilling`, `WorkOrderBillingRequest`) and services in `CrestApps.OrchardCore.WorkOrders.Core` (`WorkOrderService`, `WorkOrderServiceLevels`, `ServiceAgreementScheduler`). Reference those from your own module, not the module project.
- Work Orders depends on the shared **Service Levels** (policies and timers, which needs Business Hours; see `orchardcore-service-levels`) and **Timeline** (notes and history; see `orchardcore-timeline`) building blocks. Service Agreements depends on the shared **Agreements** module, which needs Accounts (see `orchardcore-agreements`). There is no `ServiceAgreement` record or SLA clock of Work Orders' own any more.
- Always change a work order through `WorkOrderService` (create, update, status, parts, answers, signatures, notes, complete, follow-up). It runs the handlers, the status rules, the service level timers, the timeline and the events.
- `WorkOrder.CustomerId` holds an Orchard Core **user id** (party kind `User`), not a Customers record id. The company is `AccountId` (an Accounts content item id).
- Technicians are staff members (see `orchardcore-staff`); visits and the dispatch board belong to `orchardcore-dispatch`; equipment to `orchardcore-assets`; territories and geocoding to `orchardcore-geo`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Work Orders | `CrestApps.OrchardCore.WorkOrders` | `CrestApps.OrchardCore.ServiceLevels`, `CrestApps.OrchardCore.Timeline` |
| Service Agreements | `CrestApps.OrchardCore.WorkOrders.Agreements` | `CrestApps.OrchardCore.WorkOrders`, `CrestApps.OrchardCore.Agreements` |

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
| `CrestApps.OrchardCore.Invoicing` | Completing a billed work order issues an invoice (`InvoicingWorkOrderBilling`, `Priority` 10): labor as service lines, parts as item lines, billed to the payer, once per work order. The work order keeps `InvoiceId` and its page links to the invoice. |
| `CrestApps.OrchardCore.Transactions` | Without Invoicing, completing a billed work order posts an outstanding invoice: source `WorkOrders`, `ReferenceType` `WorkOrder`, due in 30 days, with an invoice number (`TransactionsWorkOrderBilling`). The payer is the account, else the customer user, else the CRM contact. |
| `CrestApps.OrchardCore.Geo` | A new or changed service address gets its territory and is queued for geocoding as target type `WorkOrderServiceAddress`; the editor places the job with a map pin (`GeocodeSource` `Pin`). |
| `CrestApps.OrchardCore.Assets` | Work orders can be for assets; customer, account and contact pickers search through `IPartyDirectory`. |
| `CrestApps.OrchardCore.Skills` | A type edits its required skills (`SkillRequirement`: `SkillId`, `MinProficiency`) and certifications, for Dispatch. |
| `CrestApps.OrchardCore.Scheduling` | A type lists appointment types (`AppointmentTypeIds`); `AppointmentWorkOrderObserver` makes, moves and cancels a work order per confirmed booking. |
| `CrestApps.OrchardCore.Omnichannel.Managements` | The **Create Work Order** subject action (`CreateWorkOrder`) and a **Work orders** card on CRM contacts. |
| `CrestApps.OrchardCore.Accounts` | **Work orders** in the account page's **Service** tab; an account location fills an empty service address. The Agreements module adds the account's **Agreements**. |
| `CrestApps.OrchardCore.Tickets` | A help desk ticket becomes a work order from its page (`Source` `Ticket`, the ticket as `SourceId`) and hears when it is completed or canceled. |
| `CrestApps.OrchardCore.Dispatch` | Visits for each work order (a panel on its page) and the **Technicians** card in the editor (`PreferredWorkerIds`, `ExcludedWorkerIds`: staff member ids, excluded wins). |
| `OrchardCore.Workflows` | The work order and agreement events. |

## Records

Work orders and their settings live in the `WorkOrders` YesSql collection and are `CatalogItem`s matched by `ItemId`.

| Record | Key properties |
|---|---|
| `WorkOrderType` | `Name`, `Color`, `DefaultDurationMinutes` (60), `RequiredSkills`, `RequiredCertificationIds`, `CrewSize`, `RequiredEquipmentTypeIds`, `ChecklistTemplateIds`, `DefaultLines`, `ServiceLevelPolicyId` (a shared policy of the `WorkOrders` scope), `BillingMode` (`NotBillable`, `FixedPrice`, `TimeAndMaterials`), `Currency` (`USD`), `FixedPrice`, `DefaultPriority`, `RequiresSignature`, `RequiredPhotos`, `AppointmentTypeIds` |
| `WorkOrder` | `Number` (`WO-000001`, series `WorkOrders.WorkOrder`), `TypeId`, `Priority` (`Low`, `Normal`, `High`, `Urgent`), `Status` (`New`, `Ready`, `Scheduled`, `InProgress`, `OnHold`, `Completed`, `Closed`, `Canceled`), `SubStatusId`, `Summary`, `Description`, `CustomerId`, `AccountId`, `ContactId`, `Contact` (`Name`, `Phone`, `Email`), `ServiceAddress`, `AccessNotes`, `TerritoryId`, `AssetIds`, `EarliestUtc`, `LatestUtc`, `Sla` (a copy of the timers' due times and flags), `RequiredSkills`, `EstimatedMinutes`, `PreferredWorkerIds`, `ExcludedWorkerIds`, `Lines`, `Checklists`, `Attachments`, `Signatures`, `Source`, `SourceId`, `ParentWorkOrderId`, `InvoiceTransactionId`, `InvoiceId`, `AgreementId`, `AgreedPrice`, `CompletedUtc`, `ClosedUtc` |
| `WorkOrderLine` | `Id`, `Kind` (`Product`, `Service`, `Labor`, `Expense`), `ProductId`, `StockItemId`, `Description`, `QuantityEstimated`, `QuantityUsed`, `UnitPrice`, `LocationId`, `Billable` |
| `ChecklistTemplate` | `Name`, `Questions` (`Id`, `Section`, `Text`, `Kind`, `Required`, `Help`, `Min`, `Max`, `Unit`, `Choices`, `MinPhotos`, `Meter`, `Rule`) |
| `WorkOrderSubStatus` | `Name`, `ParentStatus`, `Color`, `PausesSla` |

- `ChecklistQuestionKind` is `YesNo`, `PassFailNA`, `Text`, `Number`, `Choice`, `MultiChoice`, `Photo`, `Signature`, `MeterReading` or `Date`. `WorkOrderSource` is `Manual`, `Appointment`, `Agreement`, `Activity`, `Api`, `AI`, `Workflow` or `Ticket`.
- A question's `Rule` is a shared `ConditionRule` (`Match` `All`/`Any`, `Conditions` of `Source` = an earlier question's `Id`, `Operator` such as `Is`, `Values`). A question under a hidden question is hidden too, and a hidden required question isn't asked for on completion. The older `ShowWhen` (`QuestionId`, `EqualsValue`) is still read (`ChecklistQuestion.GetRule()`), converted by a migration, and accepted by the recipe step.
- `WorkOrder.Notes` is obsolete: notes and history are entries on the work order's **timeline** (record kind `WorkOrder`, `WorkOrdersConstants.RecordKind`). Read them with `WorkOrderService.GetNotesAsync(workOrderId, take)`; the page shows the shared `Timeline` view component.
- Service level policies are **not** Work Orders records any more: they are the shared `ServiceLevelPolicy` of the Service Levels module, in the `WorkOrders` scope. Work Orders keeps its pause statuses on the policy as `WorkOrderServiceLevelSettings` (`PauseStatuses`) in the policy's `Properties`, edited on a **Work orders** card of the policy editor.

## Behavior

`WorkOrderService` (in `WorkOrders.Core`) is the single entry point:

| Method | Does |
|---|---|
| `NewAsync(typeId)` | A new work order with the type's defaults and copies of its checklists. |
| `CreateAsync(workOrder, actor)` | Numbers it, starts its timers, runs every `IWorkOrderHandler.CreatingAsync`, raises **Created**. |
| `UpdateAsync(workOrder, actor)` | Runs `UpdatingAsync` again (a changed address is placed again); a new priority retargets the timers, keeping the time used. |
| `ChangeStatusAsync(id, status, subStatusId, note, actor)` | Only allowed moves (`WorkOrderStatusRules`); closed and canceled never move, completed can close or reopen, canceling needs a reason, a sub-status must belong to the status. |
| `UsePartAsync(id, lineId, quantity, locationId, actor)` | Records parts used; counted parts come off the location through every `IWorkOrderPartsService`, refusing more than it has. |
| `AnswerAsync(id, checklistIndex, questionId, value, actor, answeredUtc)` | Validates an answer against its question kind, range and choices. |
| `SignAsync`, `AttachAsync`, `AddNoteAsync` | Signatures, media library attachments, and notes written to the timeline (internal `Note` entries). |
| `GetNotesAsync(workOrderId, take)` | The timeline's notes and events as `WorkOrderNote`s (used by the field app and AI tools). |
| `CompleteAsync(id, actor)` | Refuses until shown required questions, a required signature and `RequiredPhotos` are in; bills through the `IWorkOrderBilling` with the highest `Priority`; resolves the timers; raises **Completed**. |
| `FollowUpAsync(id, summary, actor)` | A new work order of the same type for the same customer, contact, place and assets (`ParentWorkOrderId`). |
| `CreateBillingRequest(workOrder, type, amount)`, `BillableAmount(workOrder, type)` | Static: the billing request (payer, lines) and the amount (fixed price, agreed price, or billable lines at used or planned quantities). |

**Service levels.** A new work order starts a **Response** (`FirstResponse`) and a **Resolution** timer on the shared `IServiceLevelTracker`, from its type's policy at its priority, counted in the policy's Business Hours schedule (`HoursScheduleId`, see `orchardcore-business-hours`) or around the clock. Moving to Ready, Scheduled or In progress completes the response; the timers pause in a pause status of the policy or a sub-status with `PausesSla`, and resume in business time; completing or closing resolves; canceling cancels. `WorkOrderService` copies the due times and flags to `WorkOrder.Sla` (`RespondByUtc`, `ResolveByUtc`, `ResponseWarned`, `ResponseBreached`, `ResolutionWarned`, `ResolutionBreached`), which the list, index and reports read. The shared monitor (every minute) raises warnings and breaches; `WorkOrderServiceLevelObserver` turns them into the **Work Order Service Level Warning** and **Breached** events (`Target` `Response` or `Resolution`), once each.

## Service Agreements

The feature adds recurring visits to agreements of the shared **Agreements** module (`Agreement`: `Number` `SA-00042`, `Name`, `Status` `Draft`/`Active`/`Paused`/`Expired`/`Canceled`, `AccountId`, `StartDate`, `EndDate`, `TimeZoneId`, `Hours`, `ExpiringNoticeDays`, `Properties`). Work Orders keeps its terms on the agreement as `WorkOrderAgreementTerms`: `CustomerId` (a user, when visits are for a person), `Contact`, `ServiceAddress`, `AccessNotes`, `AssetIds`, `Schedules`, `Pricing` (`Included` or `PerVisit`) and `PricePerVisit`. The agreement editor gets a **Visits** panel (`WorkOrderAgreementTermsDisplayDriver`), checked through `IAgreementTermsValidator`.

- `AgreementSchedule`: `Id`, `Name`, `TypeId`, `Rule` (RFC 5545 such as `FREQ=MONTHLY;INTERVAL=6`), `LeadDays` (14), `AssetIds` (none means all of the agreement's assets).
- The **Service agreement visits** background task (hourly, at minute 5) runs `ServiceAgreementScheduler` over agreements in force: for every visit day within the lead time it makes one work order (source `Agreement`, source id = agreement, schedule and day), windowed to that day in the agreement's time zone, with `AgreementId` and `AgreedPrice` zero when included or the per-visit price. **Make due work orders now** on the visits page does the same.
- The Agreements module expires agreements and tells observers (`IAgreementObserver.OnExpiringAsync`); for an agreement with visits that raises **Service Agreement Expiring**. Billable labor hours of completed work orders that name an agreement count against its covered hours (`WorkOrderAgreementUsageProvider`, an `IAgreementUsageProvider`).

## Admin UI

| Menu | Route | Permission |
|---|---|---|
| **Field Service > Work Orders** | `work-orders`, `work-orders/new/{typeId}`, `work-orders/edit/{id}`, `work-orders/view/{id}` | `ViewWorkOrders` (list, page), `ManageWorkOrders` (edit) |
| **Field Service > Service Agreements** (Service Agreements feature) | `work-orders/agreements` redirects to the Agreements list; visits page `work-orders/agreements/view/{id}` (`.../run` to make visits now) | `ViewAgreements` (list, visits), `ManageAgreements` (run, edit) |
| **Field Service > Work Order Types**, **Checklists**, **Sub-statuses** | `work-orders/settings/{kind}` (`types`, `checklists`, `sub-statuses`); editors `work-orders/types/edit/{id?}`, `work-orders/checklists/edit/{id?}`, `work-orders/sub-statuses/edit/{id?}` | `ManageWorkOrderSettings` |
| **Content > Service Levels** (Service Levels module) | the shared policy list and editor; pick the `WorkOrders` scope | `ManageServiceLevelPolicies` |
| **Content > Agreements** (Agreements module) | the agreement list and editor with the **Visits** panel | `ManageAgreements` |

The customer and contact pickers call `Admin/api/crestapps/work-orders/customers/search?kind=&query=` (requires `ManageWorkOrders`); the account picker uses the Accounts search.

## Permissions

| Permission | Display name | Implied by | Default role |
|---|---|---|---|
| `ManageWorkOrderSettings` | Manage work order types, checklists and sub-statuses | none | Administrator |
| `ManageWorkOrders` | Manage work orders | `ManageWorkOrderSettings` | Administrator |
| `ViewWorkOrders` | View work orders | `ManageWorkOrders` | Administrator |

`ManageServiceAgreements` was removed: agreement pages use the Agreements module's `ManageAgreements` and `ViewAgreements`, and policies need `ManageServiceLevelPolicies`. Upgrades grant them to the roles that had the old permissions.

## Recipes and Deployment

Put the shared policies first (`ServiceLevelPolicies` step of the Service Levels module), then the `WorkOrders` step (needs `OrchardCore.Recipes.Core`). The `WorkOrders` step creates or updates by `ItemId`, with the editor's validation, the sections `Checklists`, `SubStatuses`, `Types` and, with Service Agreements on, `Agreements`, in that order. Properties use the model names (PascalCase) and enums are written as names.

```json
{
  "steps": [
    {
      "name": "ServiceLevelPolicies",
      "Policies": [
        {
          "ItemId": "4ff712gq9zvzfks9tqyqgawkmb",
          "Name": "Standard service",
          "Scope": "WorkOrders",
          "WarningPercent": 75,
          "Targets": [
            { "Metric": "FirstResponse", "Priority": "Urgent", "Minutes": 120 },
            { "Metric": "Resolution", "Priority": "Urgent", "Minutes": 480 },
            { "Metric": "FirstResponse", "Minutes": 480 },
            { "Metric": "Resolution", "Minutes": 2880 }
          ],
          "Properties": {
            "WorkOrderServiceLevelSettings": { "PauseStatuses": [ "OnHold" ] }
          }
        }
      ]
    },
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
          "ServiceLevelPolicyId": "4ff712gq9zvzfks9tqyqgawkmb",
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

Full checklists with rules, types with lines and agreements are in [references/work-order-recipes.md](references/work-order-recipes.md).

- Use 26-character lowercase Orchard Core ids for `ItemId`; any other value is replaced with a new id, breaking references and duplicating records on rerun.
- A target without `Priority` applies to every other priority; `HoursScheduleId` names a Business Hours schedule (empty counts around the clock).
- A `ServiceLevels` section in an older `WorkOrders` step is still read and copied to shared policies with the same ids; write new recipes with `ServiceLevelPolicies`.
- The **Work order settings** deployment step (category **Work**, `WorkOrdersDeploymentStep`, `IncludeAgreements` false by default) exports checklists, sub-statuses and types, and agreements with visits only when asked. Policies are exported by the **Service level policies** step; every agreement by the Agreements module's step. Work orders themselves are never exported (numbers and timers belong to their site).

## Upgrading Older Sites

The data migrations run in the background through resumable steps, so a large store never blocks tenant activation:

| Step | Moves |
|---|---|
| `WorkOrders.ServiceLevelTimers` | Copies Work Orders' old policies to shared policies (weekly hours become a Business Hours schedule named after the policy) and gives each open work order its timers, keeping time already used, answered responses, holds, warnings and breaches. |
| `WorkOrders.Notes` | Moves `WorkOrder.Notes` to the timeline. |
| `WorkOrders.ServiceAgreements` | Moves old `ServiceAgreement` records to the Agreements module with the same id and number, their visits as `WorkOrderAgreementTerms`, then grants `ManageAgreements` to roles that had `ManageServiceAgreements`. |

Roles with `ManageWorkOrderSettings` get `ManageServiceLevelPolicies`, and checklist `ShowWhen` conditions are converted to `Rule`.

## Workflow Events

All are in the **Work Orders** category, can be limited to some work order types, and receive `WorkOrderId`, `Number`, `TypeId`, `Status`, `Priority`, `Summary`, `CustomerId` and `TerritoryId`.

| Activity class | Display name | Event name | Extra input |
|---|---|---|---|
| `WorkOrderCreatedEvent` | Work Order Created | `WorkOrderCreated` | |
| `WorkOrderStatusChangedEvent` | Work Order Status Changed | `WorkOrderStatusChanged` | `PreviousStatus` |
| `WorkOrderSlaWarningEvent` | Work Order Service Level Warning | `WorkOrderSlaWarning` | `Target` |
| `WorkOrderSlaBreachedEvent` | Work Order Service Level Breached | `WorkOrderSlaBreached` | `Target` |
| `WorkOrderCompletedEvent` | Work Order Completed | `WorkOrderCompleted` | |
| `ServiceAgreementExpiringEvent` | Service Agreement Expiring (only agreements with visits) | `ServiceAgreementExpiring` | `AgreementId`, `Number`, `Name`, `EndDate`, `CustomerId`, `AccountId` |

## Extension Points

| Seam | Use it to |
|---|---|
| `IWorkOrderHandler` | Change a work order before it is created (`CreatingAsync`) or saved (`UpdatingAsync`). |
| `IWorkOrderObserver` | React after events (`OnEventAsync` with a `WorkOrdersConstants.Events` name). |
| `IWorkOrderPartsService` | Take parts off stock somewhere other than Inventory. |
| `IWorkOrderBilling` | Bill somewhere else: `Priority` (highest wins; Invoicing is 10, Transactions 0) and `InvoiceAsync(WorkOrderBillingRequest)` returning a `WorkOrderBillingResult` (`TransactionId`, `InvoiceId`, `InvoiceNumber`). A billing that only posts an amount implements `InvoiceAsync(workOrder, amount, currency)` and gets the request method by default. |
| `IAgreementObserver`, `IAgreementTermsValidator` (Agreements) | React when an agreement with visits is about to end or passes a share of its hours; check terms. |
| `DisplayDriver<WorkOrderPanels>` | Add a panel to the work order page (`work-orders/view/{id}`, display type `Detail`). |
| `DisplayDriver<WorkOrder>` | Add a section to the **New work order** and **Edit** editor, built with `IDisplayManager<WorkOrder>`. |
| `DisplayDriver<ServiceLevelPolicy>` | Add a card to the shared policy editor for your own scope (return `null` for other scopes). |

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

The built-in editor fields are one driver, the `WorkOrderFields_Edit` shape at `Content:1`; Dispatch adds its **Technicians** card (`WorkOrderTechnicians_Edit`) at `Content:6` and its visits panel (`WorkOrderVisits`) at `Content:5` on the page. A full panel driver, an editor section driver and a custom billing are in [references/work-order-extensions.md](references/work-order-extensions.md).

- Register with `services.AddDisplayDriver<WorkOrderPanels, TDriver>()` or `services.AddDisplayDriver<WorkOrder, TDriver>()`.
- In an editor section, bind your own view model with the driver's `Prefix` and add errors under `$"{Prefix}.Field"`. Any error keeps the work order from being saved, and the editor cancels the session, so records your driver wrote during the update are discarded too.

## Telemetry

The `CrestApps.OrchardCore.WorkOrders` meter counts `crestapps.workorders.sla.events` (tags `event` `warning`/`breached`, `target`). Collect it with the Orchard Core OpenTelemetry setup (see `orchardcore-opentelemetry`).

## Gotchas

| Symptom | Check |
|---|---|
| Completing is refused | Only an In progress work order can be completed; or a shown required question is unanswered, the type requires a signature, or fewer than `RequiredPhotos` attachments. |
| No invoice after completing | Invoicing or Transactions must be enabled and the type billable (or `AgreedPrice` set). |
| Parts are not taken off stock | Inventory must be on, the line needs a `StockItemId`, and the location must have the units. |
| Due times look wrong | Check the policy's Business Hours schedule (`HoursScheduleId`) and its time zone; an empty schedule counts around the clock. |
| Policy editor has no pause statuses | Pick the `WorkOrders` scope; the **Work orders** card shows only for it. |
| Status change refused | `WorkOrderStatusRules` blocks moves out of Closed or Canceled; canceling needs a note. |
| Agreement makes no work orders | It must be `Active` and in its period, have visits (`WorkOrderAgreementTerms`), a `Rule` that parses, and a visit day within `LeadDays`. |
| `WorkOrders` step does nothing for agreements | Enable `CrestApps.OrchardCore.WorkOrders.Agreements` (and Agreements, Accounts) before the step runs. |
| Old notes missing after upgrade | The `WorkOrders.Notes` step moves them in batches a minute; wait for it to finish. |

Related skills: `orchardcore-dispatch`, `orchardcore-staff`, `orchardcore-service-levels`, `orchardcore-agreements`, `orchardcore-timeline`, `orchardcore-tickets`, `orchardcore-business-hours`, `orchardcore-assets`, `orchardcore-geo`, `orchardcore-inventory`, `orchardcore-transactions`, `orchardcore-invoicing` (bills work orders as invoices), `orchardcore-scheduling`, `orchardcore-skills`, `orchardcore-accounts`, `orchardcore-contact-center`, `orchardcore-addresses`.
