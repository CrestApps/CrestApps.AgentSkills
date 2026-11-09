# Workforce for Contact Center Recipe Examples

The `WorkforceForecasting` and `WorkforceAdherence` steps read camelCase property names, write enums as names, match records by id or name (so a second run updates them), and name the records they point at: queues by `queueNames`, teams by `teamName` or `teamNames`, planning groups by `planningGroupName` or `planningGroupNames`, activity codes by `activityName`. A name that doesn't exist is reported as a recipe error and that record is skipped.

## The Workforce for Contact Center Sample

This is the module's `WorkforceContactCenterSample` recipe, trimmed of its header. After running it, give agents the Customer Care team under **Contact Center > Agents**, then make a forecast under **Workforce > Forecasting**.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContactCenter.Queues",
        "CrestApps.OrchardCore.Workforce.ContactCenter",
        "CrestApps.OrchardCore.Workforce.Forecasting",
        "CrestApps.OrchardCore.Workforce.Intraday",
        "CrestApps.OrchardCore.Workforce.Adherence"
      ],
      "disable": []
    },
    {
      "name": "Workforce",
      "Teams": [
        { "name": "Customer Care", "kind": "Team", "timeZoneId": "America/New_York", "description": "Agents who answer the Support queue." }
      ]
    },
    {
      "name": "ContactCenterQueue",
      "Queues": [
        { "ItemId": "4wfm0cc0q00000000000000001", "Name": "Support" }
      ]
    },
    {
      "name": "WorkforceForecasting",
      "PlanningGroups": [
        {
          "name": "Support voice",
          "queueNames": [ "Support" ],
          "teamName": "Customer Care",
          "serviceLevelPercent": 80,
          "serviceLevelSeconds": 20,
          "maxOccupancyPercent": 85,
          "shrinkagePercent": 32,
          "historyWeeks": 8
        }
      ],
      "ForecastEvents": [
        { "name": "New Year's Day", "kind": "Holiday", "startDate": "2027-01-01", "endDate": "2027-01-01", "volumeMultiplier": 0.3, "recursYearly": true },
        { "name": "Spring promotion", "kind": "Promotion", "startDate": "2027-03-15", "endDate": "2027-03-19", "volumeMultiplier": 1.25, "planningGroupNames": [ "Support voice" ] }
      ],
      "CapacityPlans": [
        {
          "name": "Support voice 2027",
          "planningGroupName": "Support voice",
          "startDate": "2027-01-04",
          "weeks": 52,
          "weeklyWorkloadHours": 600,
          "growthPercentPerYear": 8,
          "hoursPerFte": 37.5,
          "currentFte": 30,
          "attritionPercentPerMonth": 2.5,
          "hiringClasses": [
            { "startDate": "2027-02-01", "size": 4, "trainingWeeks": 4, "rampWeeks": 4 },
            { "startDate": "2027-07-05", "size": 5, "trainingWeeks": 4, "rampWeeks": 4 }
          ]
        }
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
            { "kind": "Break", "allowedStatuses": [ "Break", "Away", "Offline" ], "startGraceMinutes": 2, "endGraceMinutes": 3, "minimumOutMinutes": 1, "alertAfterMinutes": 10 },
            { "kind": "Meal", "allowedStatuses": [ "Break", "Away", "Offline" ], "startGraceMinutes": 2, "endGraceMinutes": 3, "minimumOutMinutes": 1, "alertAfterMinutes": 10 }
          ]
        }
      ]
    }
  ]
}
```

## Field Reference

| Record | Fields |
|---|---|
| Planning group | `name`, `queueNames`, `channel`, `teamName`, `serviceLevelPercent`, `serviceLevelSeconds`, `averageSpeedOfAnswerSeconds`, `maxOccupancyPercent`, `concurrency` (chats or messages at once), `patienceSeconds` (turns on Erlang A), `shrinkagePercent`, `historyWeeks` |
| Forecast event | `name`, `kind` (`Holiday`, `Closure`, `Promotion`, `Outage`, `Custom`), `startDate`, `endDate`, `volumeMultiplier` (0 closes), `recursYearly`, `planningGroupNames` (none for every group) |
| Capacity plan | `name`, `planningGroupName`, `startDate` (moved to its Monday), `weeks` (up to 78), `weeklyWorkloadHours` (empty uses the last four weeks' average), `growthPercentPerYear`, `hoursPerFte`, `currentFte`, `attritionPercentPerMonth`, `hiringClasses` (`startDate`, `size`, `trainingWeeks`, `rampWeeks`) |
| Adherence rule set | `name`, `teamNames`, `rules` (`kind` an activity kind such as `Work`, `Break`, `Meal`, `Meeting`, `Training`, `Coaching`, `TimeOff`, `Travel`, `OnCall`, or `activityName` for one activity code; `allowedStatuses`; `allowedReasons`; `startGraceMinutes`; `endGraceMinutes`; `minimumOutMinutes`; `alertAfterMinutes`) |

Agent statuses for `allowedStatuses`: `Offline`, `Available`, `Reserved`, `Busy`, `WrapUp`, `Break`, `RequestBreak`, `Away`, `DoNotDisturb`, `Meeting`, `Training`, `AfterHoursUnavailable`.

Forecasts themselves are not imported: create them on **Workforce > Forecasting > Forecasts** (or with `ForecastService.CreateAsync`) once the queues have interval statistics, then activate one to write the team's coverage.

## Deployment

| Deployment step | Category | Exports |
|---|---|---|
| **Adherence rule sets** (`WorkforceAdherence`) | Workforce | Every rule set with its ids. |
| **Forecasting setup** (`WorkforceForecasting`) | Workforce | Planning groups, forecast events and capacity plans with their ids. |
| **Contact Center time stages** | Site settings | The `ContactCenterTimeStages` setting (stage of each agent state and break reason). |
