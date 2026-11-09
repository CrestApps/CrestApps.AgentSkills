---
name: orchardcore-workforce-contact-center
description: Skill for the CrestApps Orchard Core Workforce for Contact Center module (CrestApps.OrchardCore.Workforce.ContactCenter), which joins Workforce to the Contact Center. Covers a worker for every agent, Contact Center Sign-in Punches, Workforce Adherence (live board, rule sets, exceptions, daily adherence and conformance, the Agent Out Of Adherence event), Workforce Forecasting (planning groups, Holt-Winters, Erlang C and Erlang A staffing, coverage from the active forecast, capacity plans with hiring classes), Workforce Intraday (reforecast and the My Work Offers page), the Contact Center Time Tracker stages, the Interval Statistics feature it reads, report data sets, AI tools and the WorkforceForecasting and WorkforceAdherence recipe steps. Use this skill when requests mention contact center WFM, agent schedules, adherence, call volume forecasting, Erlang, intraday or capacity planning. Strong matches include AgentWorkerService, AdherenceService, ForecastService, ErlangCalculator and IntradayService.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Workforce for Contact Center

## Schedule, Forecast and Track Agents Like Every Other Worker

You are an Orchard Core expert. Generate accurate setup, recipes and extension guidance for the CrestApps Workforce for Contact Center module. Workforce never references the Contact Center; this module references both. It gives every agent a worker, clocks agents in when they sign in, compares their states with their published shifts, forecasts queue work into coverage for the auto-scheduler, manages the day as it happens, and keeps agent states in Time Tracker.

### Guidelines

- Install `CrestApps.OrchardCore.Workforce.ContactCenter` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it) in the web/startup project.
- Forecasting, Intraday and Adherence are features of **this** module even though their IDs read `CrestApps.OrchardCore.Workforce.Forecasting`, `CrestApps.OrchardCore.Workforce.Intraday` and `CrestApps.OrchardCore.Workforce.Adherence`.
- The 15-minute queue and agent statistics they read come from the Contact Center feature **Contact Center Interval Statistics** (`CrestApps.OrchardCore.ContactCenter.IntervalStatistics`, which depends on Work Distribution (Queues)). It is enabled for you as a dependency.
- An agent and a worker are the same person when they share a user account. Shifts must be **published** (Workforce Shift Planning) before adherence has anything to compare.
- Workers, shifts, the time clock and My Work are covered by `orchardcore-workforce`; agents, queues and presence by `orchardcore-contact-center`; Time Tracker by `orchardcore-time-tracker`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Workforce for Contact Center | `CrestApps.OrchardCore.Workforce.ContactCenter` | `CrestApps.OrchardCore.Workforce`, `CrestApps.OrchardCore.ContactCenter.Agents` |
| Contact Center Sign-in Punches | `CrestApps.OrchardCore.Workforce.ContactCenter.SignInPunches` | `CrestApps.OrchardCore.Workforce.ContactCenter`, `CrestApps.OrchardCore.Workforce.TimeClock` |
| Workforce Forecasting | `CrestApps.OrchardCore.Workforce.Forecasting` | `CrestApps.OrchardCore.Workforce.ContactCenter`, `CrestApps.OrchardCore.Workforce.ShiftPlanning`, `CrestApps.OrchardCore.ContactCenter.IntervalStatistics` |
| Workforce Intraday | `CrestApps.OrchardCore.Workforce.Intraday` | `CrestApps.OrchardCore.Workforce.Forecasting` |
| Workforce Adherence | `CrestApps.OrchardCore.Workforce.Adherence` | `CrestApps.OrchardCore.Workforce.ContactCenter`, `CrestApps.OrchardCore.Workforce.ShiftPlanning`, `CrestApps.OrchardCore.ContactCenter.IntervalStatistics` |
| Contact Center Time Tracker | `CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracker` | `CrestApps.OrchardCore.Workforce.ContactCenter`, `CrestApps.OrchardCore.ContactCenter.IntervalStatistics`, `CrestApps.OrchardCore.TimeTracker` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContactCenter.Queues",
        "CrestApps.OrchardCore.Workforce.ContactCenter",
        "CrestApps.OrchardCore.Workforce.ContactCenter.SignInPunches",
        "CrestApps.OrchardCore.Workforce.Forecasting",
        "CrestApps.OrchardCore.Workforce.Intraday",
        "CrestApps.OrchardCore.Workforce.Adherence",
        "CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracker"
      ],
      "disable": []
    }
  ]
}
```

## Agents and Workers

- `AgentWorkerService` lists agents with their worker, finds the agent of a worker, and creates workers (`EnsureWorkerAsync`, `CreateMissingAsync`). A catalog handler on `AgentProfile` creates the worker when an agent is **created** (not on updates, so frequent presence saves never look it up).
- Agents created before the feature was on get workers from **Workforce > Contact Center Agents** (`Admin/workforce/agents`, **Create workers**), which can put them in a team. Needs `ManageWorkers`.

## Sign-in Punches

`SignInPunchHandler` (an `IContactCenterEventHandler`, delivered at least once through the Contact Center outbox) handles `AgentStateChanged`:

- **Sign-in** (source `SignIn`, or Offline to another state) clocks the worker in with source `ContactCenter`, unless already clocked in another way.
- **Sign-out** (source `SignOut`, or any state to Offline) clocks out only when the open stretch began with a contact center punch.
- Idempotency key `contact-center:<event id>`, so a redelivered event punches once. A refused punch (no time clock policy, source not allowed) is logged and skipped.
- The `ContactCenter` punch source is described by `ContactCenterTimePunchSourceProvider` (an `ITimePunchSourceProvider`), so time clock policies offer **The contact center** under **Workers can punch from** only while this feature is on.

## Adherence

- `AdherenceCalculator` (pure) builds the schedule from published shifts and compares each moment with the agent's state. **Adherence** = time in adherence over scheduled time less exceptions; **conformance** = time in a working state (available, reserved, busy, wrap-up) over scheduled work.
- `AdherenceRuleSet` (`Name`, `TeamIds`, `Rules`): one `AdherenceRule` per activity `Kind` or `ActivityCodeId`, with `AllowedStatuses` (`AgentPresenceStatus` names such as `Available`, `Reserved`, `Busy`, `WrapUp`, `Break`, `Away`, `Offline`, `Meeting`, `Training`), `AllowedReasons`, `StartGraceMinutes`, `EndGraceMinutes`, `MinimumOutMinutes`, `AlertAfterMinutes`. A team uses its own set or its nearest parent's, else `AdherenceCalculator.DefaultRules` (grace 2, shortest slip 1, alert after 5 minutes).
- `AdherenceService`: `CurrentAsync`, `BoardAsync` (live board), `RaiseAlertsAsync` (once per out-of-adherence episode, recorded in `AdherenceAlert`), `ComputeDayAsync`, `SaveDayAsync` (an `AdherenceDay` with segments, in the worker's zone from `IWorkerTimeZoneService`), exceptions as `AdherenceException` documents.
- Background tasks: **Workforce Adherence Alerts** (every minute) and **Workforce Adherence History** (hourly at :15, saves each agent's finished previous day).
- Agent state spans come from the Contact Center's `IAgentStateHistory` (in `CrestApps.OrchardCore.ContactCenter.Core`).
- With the My Work Portal on, `Admin/my-work/adherence` shows the worker's latest 14 saved days; **Adherence** appears in the My Work menu once a day is saved (`MyAdherenceLinkProvider`).

## Forecasting

- `PlanningGroup`: `Name`, `QueueIds`, optional `Channel`, `TeamId` (who staffs it), `ServiceLevelPercent` and `ServiceLevelSeconds` and/or `AverageSpeedOfAnswerSeconds`, `MaxOccupancyPercent`, `Concurrency` (contacts at once), `PatienceSeconds` (turns on Erlang A), `ShrinkagePercent`, `HistoryWeeks`.
- `ForecastEvent`: `Name`, `Kind` (`Holiday`, `Closure`, `Promotion`, `Outage`, `Custom`), `StartDate`, `EndDate`, `VolumeMultiplier` (0 closes), `RecursYearly`, `PlanningGroupIds` (empty for all).
- `ForecastService.CreateAsync` reads the group's queue intervals in the team's zone and builds a `Draft` forecast: daily volume by `WeightedHistory` or `HoltWinters` (`Auto` back-tests both on two weeks and keeps the lower error, recording MAPE and WAPE), events applied, 15-minute intervals by each weekday's curve, handle time per interval, and staffing per interval with `ErlangCalculator` (Erlang C, or Erlang A with patience) to the service goal within the occupancy cap, then shrinkage.
- `AdjustAsync` changes days by a percentage; `ActivateAsync` makes it the group's `Active` forecast (the previous one is `Superseded`) and writes the team's coverage requirement *Forecast: group name* (`CoverageSource.Forecast`; minimum = agents needed, target = agents to schedule), which the planner board and auto-scheduler use unchanged.
- **Capacity plans** (`CapacityPlan`: `PlanningGroupId`, `StartDate` moved to its Monday, up to 78 `Weeks`, `WeeklyWorkloadHours` or the last four weeks' average, `GrowthPercentPerYear`, `HoursPerFte`, `CurrentFte`, `AttritionPercentPerMonth`, `HiringClasses` with `StartDate`, `Size`, `TrainingWeeks`, `RampWeeks`). `CapacityPlanner.Project` (pure) works out agents needed against agents expected each week and the class that would cover the worst week.

## Intraday

`IntradayService.DayAsync` shows a planning group's day every 15 minutes: forecast against actual volume, handle time and service level, agents needed against scheduled (`ICoverageService`) and available. `ReforecastAsync` scales the rest of the day by `IntradayCalculator.ReforecastFactor` (half the difference, 0.25 to 4) and updates coverage. `IntradayCalculator.FindWindows` finds the quietest times for a meeting. An `IntradayOffer` posts voluntary overtime or time off for a number of places; workers take it on the **Offers** page of My Work (`Admin/my-work/offers`), which lengthens or shortens their shift through `IShiftService.ChangeTimesAsync` as their own change (no predictability pay).

## Contact Center Time Tracker

- The `ContactCenterTimeStages` site setting maps each `AgentPresenceStatus` and each break reason (which wins) to a Time Tracker stage by name; a state with no stage isn't tracked. Turning the feature on seeds Available, On a call, Wrap-up, Meeting and Training (in `Paid` and `Worked`) and Break and Away (no category). Set them on **Time Tracker > Contact Center Stages** (`Admin/time/contact-center-stages`).
- `AgentStateTimeTrackerHandler` rebuilds the last ten minutes of an agent's states on every `AgentStateChanged` with `ITimeTrackerService.SyncAsync` (`ReplacesRangeOnly`, `ManagedBy` `Workforce.ContactCenter`, overriding `Workforce.TimeClock`), so clocked work is cut around states while the agent is signed in. Works for any agent with a user, worker or not.

## Admin UI

| Menu | Routes (under `Admin/`) | Permission |
|---|---|---|
| **Workforce > Contact Center Agents** | `workforce/agents` | `ManageWorkers` |
| **Workforce > Adherence > Real-time**, **Exceptions** | `workforce/adherence`, `workforce/adherence/exceptions`, `workforce/adherence/worker/{workerId}` | `ViewWorkforceAdherence` |
| **Workforce > Adherence > Rule Sets** | `workforce/adherence/rule-sets` | `ManageWorkforceAdherence` |
| **Workforce > Forecasting > Intraday** | `workforce/intraday` | `ManageWorkforceForecasts` |
| **Workforce > Forecasting > Forecasts**, **Planning Groups**, **Capacity Plans**, **Forecast Events** | `workforce/forecasts`, `workforce/forecasting/planning-groups`, `workforce/capacity-plans`, `workforce/forecasting/events` | `ManageWorkforceForecasts` |
| **Time Tracker > Contact Center Stages** | `time/contact-center-stages` | `ManageTimeTrackerSettings` |
| Contact Center interval statistics (rebuild) | `contact-center/interval-statistics` | Contact Center |

## Permissions

| Permission | Display name | Implied by | Default role |
|---|---|---|---|
| `ManageWorkforceForecasts` | Manage forecasts (planning groups, events, forecasts, capacity plans, intraday and offers, forecast data set) | `ManageAllWorkforce` | Administrator |
| `ManageWorkforceAdherence` | Manage adherence rules and exceptions | `ManageAllWorkforce` | Administrator |
| `ViewWorkforceAdherence` | View adherence (board, days, exceptions, data sets, for managed teams) | `ManageWorkforceAdherence` | Administrator |

## Recipes and Deployment

| Step | Feature | Sections |
|---|---|---|
| `WorkforceForecasting` | Workforce Forecasting | `PlanningGroups` (`queueNames`, `teamName`), `ForecastEvents` (`planningGroupNames`, none for all), `CapacityPlans` (`planningGroupName`, `hiringClasses`) |
| `WorkforceAdherence` | Workforce Adherence | `RuleSets` (`teamNames`; a rule's activity code by `activityName`) |

```json
{
  "steps": [
    {
      "name": "WorkforceForecasting",
      "PlanningGroups": [
        { "name": "Support voice", "queueNames": [ "Support" ], "teamName": "Customer Care", "serviceLevelPercent": 80, "serviceLevelSeconds": 20, "maxOccupancyPercent": 85, "shrinkagePercent": 32, "historyWeeks": 8 }
      ],
      "ForecastEvents": [
        { "name": "New Year's Day", "kind": "Holiday", "startDate": "2027-01-01", "endDate": "2027-01-01", "volumeMultiplier": 0.3, "recursYearly": true }
      ]
    },
    {
      "name": "WorkforceAdherence",
      "RuleSets": [
        {
          "name": "Customer Care",
          "teamNames": [ "Customer Care" ],
          "rules": [
            { "kind": "Work", "allowedStatuses": [ "Available", "Reserved", "Busy", "WrapUp" ], "startGraceMinutes": 2, "endGraceMinutes": 2, "minimumOutMinutes": 1, "alertAfterMinutes": 5 },
            { "kind": "Break", "allowedStatuses": [ "Break", "Away", "Offline" ], "startGraceMinutes": 2, "endGraceMinutes": 3, "minimumOutMinutes": 1, "alertAfterMinutes": 10 }
          ]
        }
      ]
    }
  ]
}
```

- Records are matched by id or name so reruns update them; a missing queue, team or planning group name is reported and that record skipped.
- Deployment steps (category **Workforce**): **Adherence rule sets** (`WorkforceAdherence`), **Forecasting setup** (`WorkforceForecasting`), and the **Contact Center time stages** site settings step.
- The **Workforce for Contact Center sample** recipe (`WorkforceContactCenterSample`) sets up a Customer Care team, a Support queue, a planning group, a holiday and a promotion, a capacity plan with two hiring classes and adherence rules. Full JSON is in [references/workforce-contact-center-recipes.md](references/workforce-contact-center-recipes.md).

## Workflows, AI Tools and Reports

- **Agent Out Of Adherence** (`WorkforceOutOfAdherenceEvent`, category **Workforce**, team filter), once per episode, input `WorkerId`, `WorkerName`, `TeamId`, `Status`, `Reason`, `ScheduledActivity`, `OutSinceUtc`, `Minutes`. Needs `OrchardCore.Workflows`.
- AI tools (with `CrestApps.OrchardCore.AI`): `workforceForecastSummary` (Forecasting, needs `ManageWorkforceForecasts`) and `workforceOutOfAdherence` (Adherence, needs `ViewWorkforceAdherence`).
- Data sets added to the **Workforce** report data source (needs `OrchardCore.DataPipelines.Core`): `WorkforceAdherenceDays` and `WorkforceAdherenceSegments` (date-only **Day**, readers with View adherence, limited to their teams) and `WorkforceForecastIntervals` (Manage forecasts; filter **Status** to `Active`). The Contact Center data source lists **Queue intervals** and **Agent intervals** (`orchardcore-report-builder`).

## Extension Points

| Seam | Owner | Use it to |
|---|---|---|
| `IContactCenterEventHandler` | Contact Center | React to `AgentStateChanged` and other outbox events, as the punch and Time Tracker handlers do. |
| `IAgentStateHistory` | Contact Center Core | Read an agent's state spans for a range. |
| `IIntervalStatisticsReader` | Contact Center Core | Read the 15-minute queue and agent statistics. |
| `IWorkerTimeZoneService` | Workforce | A worker's zone (theirs, the team's, the site's). |
| `ITimePunchSourceProvider` | Workforce | Describe a new punch source for policies and timesheets. |
| `IMyWorkLinkProvider` | Workforce | Add a page to the My Work menu (Offers and Adherence use it). |

```csharp
public sealed class ChatPunchSourceProvider : ITimePunchSourceProvider
{
    internal readonly IStringLocalizer S;

    public ChatPunchSourceProvider(IStringLocalizer<ChatPunchSourceProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public IEnumerable<TimePunchSourceDescriptor> GetSources()
    {
        // Name, display name, the phrase used inside sentences, and an optional hint.
        yield return new TimePunchSourceDescriptor("ChatDesk", S["The chat desk"], S["the chat desk"], S["Agents opening and closing the chat desk."]);
    }
}

// Startup: services.AddScoped<ITimePunchSourceProvider, ChatPunchSourceProvider>();
```

Punches with your source then go through `ITimeClockService.PunchAsync` with one idempotency key per event, as `SignInPunchHandler` does with `contact-center:<event id>`.

## Gotchas

| Symptom | Check |
|---|---|
| Adherence board empty | No published shifts for the agents' workers, or agents have no worker (create them on Contact Center Agents). |
| Agents not clocked in on sign-in | Sign-in Punches off, no time clock policy on the worker's team, or the policy doesn't allow the contact center source. |
| Forecast is flat or empty | The planning group's queues have no interval statistics yet; Interval Statistics needs history (rebuild it from its page). |
| Auto-scheduler ignores the forecast | Activate the forecast; only the active one writes coverage. |
| Offers don't appear in My Work | Intraday and the My Work Portal must be on and the worker on the staffing team. |
| Agent time shows twice in reports | Contact Center Time Tracker overrides the time clock only while signed in; check the stage mapping. |

Related skills: `orchardcore-workforce`, `orchardcore-contact-center`, `orchardcore-time-tracker`, `orchardcore-teams`, `orchardcore-skills`, `orchardcore-report-builder`.
