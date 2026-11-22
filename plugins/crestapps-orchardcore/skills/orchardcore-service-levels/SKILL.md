---
name: orchardcore-service-levels
description: Skill for the CrestApps Orchard Core Service Levels module (CrestApps.OrchardCore.ServiceLevels), the shared SLA engine for tickets, work orders and other work. Covers policies with targets per metric and priority counted on a Business Hours schedule, scopes, Content > Service Levels, the ManageServiceLevelPolicies permission, the ServiceLevelPolicies recipe and deployment step, timers that pause, resume, complete, reopen and retarget, the monitor background task, warnings and breaches, observers, policy usage, the ServiceLevelTimers data set and timer access providers. Use this skill when requests mention SLAs, first response or resolution targets, breach or warning events, business-hour clocks or SLA timers on a custom record. Strong matches include ServiceLevelPolicy, ServiceLevelTimer, ServiceLevelStart, ServiceLevelMetrics, IServiceLevelTracker, IServiceLevelScopeProvider, IServiceLevelObserver and AddServiceLevelsCore.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Service Levels

## SLA Targets for Any Kind of Work

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Service Levels module. A policy says how long a first response, next response or resolution may take at each priority, counted on a business hours schedule or around the clock. The module that owns a record (Tickets, Work Orders, your own) starts and stops a stored timer per record and metric; a monitor raises warnings and breaches and tells the owner through observers.

### Guidelines

- Install `CrestApps.OrchardCore.ServiceLevels` in the web/startup project. It depends on Business Hours (`orchardcore-business-hours`).
- Reference `CrestApps.OrchardCore.ServiceLevels.Abstractions` (models, `IServiceLevelTracker`, scopes, observers, `ServiceLevelsConstants`, `ServiceLevelsPermissions`, registration helpers) or `CrestApps.OrchardCore.ServiceLevels.Core` (`AddServiceLevelsCore()`, stores, `DefaultServiceLevelTracker`, `ServiceLevelMonitor`) from your module, never the module project.
- Call `IServiceLevelTracker` while holding your record's own lock, from the code path that changes the record, so a timer always matches the record.
- Register a scope for your kind of work; a policy of an unregistered scope is refused.
- Raise your own events (workflow events, notifications) from an `IServiceLevelObserver`; the monitor calls observers only after its save commits.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature

| Feature | ID | Depends on |
|---|---|---|
| Service Levels | `CrestApps.OrchardCore.ServiceLevels` | `CrestApps.OrchardCore.BusinessHours`, `CrestApps.OrchardCore.Resources` |

Category **Organization**. Tickets (`CrestApps.OrchardCore.Tickets`) depends on it; Work Orders registers its `WorkOrders` scope when both are on.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ServiceLevels"
      ],
      "disable": []
    }
  ]
}
```

| Project | Holds |
|---|---|
| `CrestApps.OrchardCore.ServiceLevels.Abstractions` | `ServiceLevelPolicy`, `ServiceLevelTarget`, `ServiceLevelTimer`, `ServiceLevelTimerState`, `ServiceLevelMetrics`, `ServiceLevelStart`, `ServiceLevelOption`, `IServiceLevelTracker`, `IServiceLevelObserver`, `IServiceLevelScopeProvider`, `IServiceLevelPolicyManager`, `IServiceLevelPolicyUsageProvider`, `IServiceLevelTimerAccessProvider`, registration helpers |
| `CrestApps.OrchardCore.ServiceLevels.Core` | YesSql stores and indexes (collection `ServiceLevels`), `DefaultServiceLevelTracker`, `ServiceLevelClock`, `ServiceLevelMonitor`, `ServiceLevelTargetGrid`, `ServiceLevelPriorityKeys`, `ServiceLevelScheduleObserver`, `AddServiceLevelsCore()` |
| `CrestApps.OrchardCore.ServiceLevels` (module) | **Content → Service Levels**, policy editor, monitor background task, migration, recipe and deployment step, data set |

## Admin UI and Permission

| Menu | Controller | Permission |
|---|---|---|
| **Content → Service Levels** | `PoliciesController` (shared admin list) | `ManageServiceLevelPolicies` |

| Permission | Display name | Default role |
|---|---|---|
| `ManageServiceLevelPolicies` | Manage service level policies | Administrator |

**Add Policy** opens the editor of the only registered scope, or lists the scopes when there are several. The editor (`IDisplayManager<ServiceLevelPolicy>`) shows a grid of minutes per metric and priority plus an **Any other priority** row (targets with an empty `Priority`). The scope is fixed once the policy exists.

## Policies

| Member | Meaning |
|---|---|
| `Name`, `Description` | Name up to 255 characters, unique per scope |
| `Scope` | Name of a registered `IServiceLevelScopeProvider` (`Tickets`, `WorkOrders`, yours) |
| `Targets` | `ServiceLevelTarget { Metric, Priority, Minutes }`. `FindTarget(metric, priority)` returns the priority's target, else the metric's target for every priority |
| `HoursScheduleId` | A schedule from any Business Hours source; empty counts around the clock |
| `WarningPercent` | 1 to 99, default 80 (`ServiceLevelsConstants.DefaultWarningPercent`) |
| `Properties` | Settings other modules keep on the policy (Work Orders keeps `WorkOrderServiceLevelSettings` with its pause statuses) |

- Built-in metrics are `FirstResponse`, `NextResponse` and `Resolution` (`ServiceLevelMetrics`); a scope may add its own.
- `Priority` is the scope's key (`ServiceLevelOption.Key`): Tickets uses the ticket priority's `ItemId`, Work Orders the `WorkOrderPriority` name. A target written with a priority's **name** (as a recipe that can't know a site's ids writes it) is rekeyed by `ServiceLevelPriorityKeys` on save, so renaming a priority keeps its targets.
- A policy is refused when its schedule doesn't exist or is never open, its scope isn't registered, its id isn't valid, or its name is longer than 255 characters. A policy with running or paused timers, or one a module still uses (`IServiceLevelPolicyUsageProvider`), can't be deleted.

## Recipes and Deployment

The `ServiceLevelPolicies` step (`ServiceLevelsConstants.RecipeStepName`, needs `OrchardCore.Recipes.Core`) creates or updates policies, matched by `ItemId`, then by `Scope` and `Name`. Put the `BusinessHoursSchedules` step first when the policy names a schedule.

```json
{
  "steps": [
    {
      "name": "ServiceLevelPolicies",
      "Policies": [
        {
          "ItemId": "47bpbxt94eb7qbn8wjb9rg9eb3",
          "Name": "Standard help desk",
          "Scope": "Tickets",
          "HoursScheduleId": "44fy8z1wfv91trshefx9dqcsyj",
          "WarningPercent": 75,
          "Targets": [
            { "Metric": "FirstResponse", "Priority": "Urgent", "Minutes": 60 },
            { "Metric": "Resolution", "Priority": "Urgent", "Minutes": 480 },
            { "Metric": "FirstResponse", "Priority": "Normal", "Minutes": 240 },
            { "Metric": "Resolution", "Priority": "Normal", "Minutes": 1920 },
            { "Metric": "FirstResponse", "Minutes": 480 }
          ]
        },
        {
          "Name": "Standard service",
          "Scope": "WorkOrders",
          "HoursScheduleId": "",
          "Targets": [
            { "Metric": "FirstResponse", "Priority": "Urgent", "Minutes": 120 },
            { "Metric": "Resolution", "Priority": "Urgent", "Minutes": 480 }
          ],
          "Properties": {
            "WorkOrderServiceLevelSettings": { "PauseStatuses": [ "OnHold" ] }
          }
        }
      ]
    }
  ]
}
```

The **Service level policies** deployment step exports every policy in the same shape. With CrestApps Recipes on, both steps publish a JSON schema.

## Timers

Each record and metric has a `ServiceLevelTimer` (collection `ServiceLevels`) with `Subject` (`RecordRef`), `Metric`, `PolicyId`, `Priority`, `TargetSeconds`, `HoursScheduleId`, `State` (`Running`, `Paused`, `Completed`, `Cancelled`), `StartedUtc`, `DueUtc`, `WarnAtUtc`, `PausedSinceUtc`, `UsedSeconds`, `WarnedUtc`, `BreachedUtc`, `EndedUtc`, `ElapsedSeconds` and `NoBusinessHours`.

| `IServiceLevelTracker` method | Call it when |
|---|---|
| `StartAsync(ServiceLevelStart)` | The record is created or a repeating metric starts a new cycle (a customer replies). Cancels an active timer of the same metric first. `Paused`, `AlreadyWarned`, `AlreadyBreached`, `DueUtc`, `PausedSinceUtc` and `CompletedUtc` bring state from another system. Starting never calls observers. |
| `PauseAsync(subject, metric = null)` / `ResumeAsync` | The record waits (on hold, waiting for the customer) and stops waiting |
| `CompleteAsync(subject, metric, atUtc = null)` | The work was done; business time used goes to `ElapsedSeconds`. A cycle completed after its due time is announced as breached first |
| `ReopenAsync(subject, metric)` | Reopened after being answered; the newest completed cycle counts on from the time it used |
| `CancelAsync(subject, metric = null)` | No longer measured (spam, deleted) |
| `RetargetAsync(subject, policyId, priority)` | Priority or policy changed; time already used counts against the new target |
| `GetTimersAsync(subject, activeOnly = false)` | The record's timers, newest first |

A timer keeps the business seconds it used, so pauses and retargets never add wall-clock time to a deadline.

## Monitor, Observers and Schedule Changes

- The **Service levels** background task (`ServiceLevelMonitorBackgroundTask`, every minute) runs `ServiceLevelMonitor`: it pages the running timers that are due, marks each warning and breach in its own concurrency-checked transaction (an owner's change always wins), then calls `IServiceLevelObserver.WarningAsync` and `BreachedAsync`. Completing a timer calls `CompletedAsync`. A failing observer is logged and the others still run.
- When a schedule's hours change or it is deleted, `ServiceLevelScheduleObserver` (an `IBusinessHoursScheduleObserver`) keeps the old hours and a resumable step counts each running timer up to the change on the old hours and the rest on the new ones, a batch at a time.
- A schedule changed so that it is never open (no open hour in the next year) doesn't freeze timers: `ServiceLevelClock` counts them around the clock, sets `NoBusinessHours` and calls `IServiceLevelObserver.NoBusinessHoursAsync` once (Tickets notes it on the ticket's history and shows **No business hours** by the target).

## Data Set

With `OrchardCore.DataPipelines.Core` on, the **Service levels** data source (`ServiceLevels`) has the `ServiceLevelTimers` data set: one row per timer with its record, metric, policy, priority, state, target, start, warning and due times, end, met/warned/breached and the business minutes used. Policy managers see every timer; others see the timers of records an `IServiceLevelTimerAccessProvider` for that record kind lets them see (Tickets and Work Orders register one). Kinds without a provider are left to policy managers.

## Extension Points

| Seam | Register with | Use it to |
|---|---|---|
| `IServiceLevelScopeProvider` | `services.AddServiceLevelScope<T>()` | Name a scope with its metrics and priorities (`Name`, `DisplayName`, optional `Description`, `GetMetricsAsync`, `GetPrioritiesAsync`) |
| `IServiceLevelObserver` | `services.AddServiceLevelObserver<T>()` | Act on `WarningAsync`, `BreachedAsync`, `CompletedAsync`, `NoBusinessHoursAsync` |
| `IServiceLevelPolicyUsageProvider` | `services.AddServiceLevelPolicyUsage<T>()` | Return what still uses a policy, so it can't be deleted |
| `IServiceLevelTimerAccessProvider` | `services.AddServiceLevelTimerAccessProvider<T>()` | Return which record ids of your kind a user may see in the data set |
| `DisplayDriver<ServiceLevelPolicy>` | `services.AddDisplayDriver<ServiceLevelPolicy, T>()` | Add a card for your scope (return `null` for other scopes) and keep its values in `Properties` |
| `IServiceLevelPolicyManager` | (injected) | `ICatalogManager<ServiceLevelPolicy>` plus `ListByScopeAsync(scope)` and `FindByNameAsync(scope, name)` |

A complete scope, observer, usage provider, timer access provider and tracker calls for a custom record kind are in [references/service-levels-extensions.md](references/service-levels-extensions.md).

## Consumers

| Module | Scope | Notes |
|---|---|---|
| Tickets | `Tickets` | Metrics First response, Next response, Resolution; priorities are the ticket priorities (keys are their ids). Policy from the agreement, account, category, type, then desk. See `orchardcore-tickets`. |
| Work Orders | `WorkOrders` | Metrics Response (`FirstResponse`) and Resolution; priorities Urgent, High, Normal, Low; a **Work orders** card with the statuses that pause timers; raises **Work Order Service Level Warning** and **Breached** events. Its migration copied old work-order policies to shared policies with the same ids. See `orchardcore-work-orders`. |

## Troubleshooting

| Symptom | Check |
|---|---|
| Policy refused on save or import | Unknown `Scope` (feature that registers it is off), `HoursScheduleId` names no schedule or one never open, invalid `ItemId`, name over 255 characters. |
| Targets missing after import | `Priority` must be the scope's key or a priority's exact name; an unknown name is not rekeyed. |
| No warnings or breaches fire | The **Service levels** background task is disabled under **Tools → Background Tasks**, or no observer is registered for your scope. |
| Due time ignores nights and weekends | The policy has no `HoursScheduleId`, or the schedule is disabled (unknown schedules count around the clock). |
| Can't delete a policy | It has running or paused timers, or a usage provider reports it (a ticket desk, ticket type or work order type names it). |
| **No business hours** shown on a record | The schedule was changed to never open; the timer counts around the clock until it opens again. |

Related skills: `orchardcore-business-hours`, `orchardcore-tickets`, `orchardcore-work-orders`, `orchardcore-report-builder`, `orchardcore-background-tasks`.
