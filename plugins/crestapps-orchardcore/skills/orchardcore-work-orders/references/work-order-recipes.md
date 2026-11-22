# Work Orders Recipe Examples

The `WorkOrders` step imports its sections in this order: `Checklists`, `SubStatuses`, `Types`, then `Agreements` (only when the Service Agreements feature is on). Each entry is matched by `ItemId`; a known id is updated, an unknown valid id is created with that id, and an invalid id is replaced with a new one. Each entry goes through the same validation as the admin editor, and a refused entry is reported as a recipe error while the others are still saved.

Property names are the model names (PascalCase). Enums are written as their names. `CreatedUtc`, `ModifiedUtc` and `OwnerId` are environment-owned and are not exported.

Service level policies are the shared ones of the Service Levels module. Put the `ServiceLevelPolicies` step **before** the `WorkOrders` step so a type's `ServiceLevelPolicyId` points at an existing policy.

## Policies, Checklists, Sub-statuses and Types

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Recipes.Core",
        "CrestApps.OrchardCore.WorkOrders"
      ],
      "disable": []
    },
    {
      "name": "ServiceLevelPolicies",
      "Policies": [
        {
          "ItemId": "4ff712gq9zvzfks9tqyqgawkmb",
          "Name": "Standard service",
          "Scope": "WorkOrders",
          "HoursScheduleId": "",
          "WarningPercent": 75,
          "Targets": [
            { "Metric": "FirstResponse", "Priority": "Urgent", "Minutes": 120 },
            { "Metric": "Resolution", "Priority": "Urgent", "Minutes": 480 },
            { "Metric": "FirstResponse", "Priority": "High", "Minutes": 240 },
            { "Metric": "Resolution", "Priority": "High", "Minutes": 1440 },
            { "Metric": "FirstResponse", "Priority": "Normal", "Minutes": 480 },
            { "Metric": "Resolution", "Priority": "Normal", "Minutes": 2880 },
            { "Metric": "FirstResponse", "Priority": "Low", "Minutes": 1440 },
            { "Metric": "Resolution", "Priority": "Low", "Minutes": 7200 }
          ],
          "Properties": {
            "WorkOrderServiceLevelSettings": { "PauseStatuses": [ "OnHold" ] }
          }
        }
      ]
    },
    {
      "name": "WorkOrders",
      "Checklists": [
        {
          "ItemId": "4fexpaa3vgvp6s1h1y9jzak6dt",
          "Name": "Boiler safety check",
          "Questions": [
            { "Id": "gas-smell", "Section": "Before", "Text": "Gas smell on arrival?", "Kind": "YesNo", "Required": true },
            {
              "Id": "gas-action",
              "Section": "Before",
              "Text": "What did you do?",
              "Kind": "Choice",
              "Required": true,
              "Choices": [ "Isolated supply", "Called the gas emergency line" ],
              "Rule": { "Match": "All", "Conditions": [ { "Source": "gas-smell", "Operator": "Is", "Values": [ "Yes" ] } ] }
            },
            { "Id": "flue-co", "Section": "After", "Text": "Flue gas CO reading", "Kind": "Number", "Required": true, "Min": 0, "Max": 1000, "Unit": "ppm" },
            { "Id": "pressure", "Section": "After", "Text": "System pressure", "Kind": "PassFailNA", "Required": true },
            { "Id": "run-hours", "Section": "After", "Text": "Run hours", "Kind": "MeterReading", "Meter": "Run hours" },
            { "Id": "photo", "Section": "After", "Text": "Photo of the data plate", "Kind": "Photo", "MinPhotos": 1 }
          ]
        }
      ],
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
          "ChecklistTemplateIds": [ "4fexpaa3vgvp6s1h1y9jzak6dt" ],
          "ServiceLevelPolicyId": "4ff712gq9zvzfks9tqyqgawkmb",
          "BillingMode": "TimeAndMaterials",
          "Currency": "USD",
          "DefaultLines": [
            { "Id": "labor", "Kind": "Labor", "Description": "Service labor (hours)", "QuantityEstimated": 1.5, "UnitPrice": 95, "Billable": true },
            { "Id": "filter", "Kind": "Product", "Description": "Boiler filter", "QuantityEstimated": 1, "UnitPrice": 24, "Billable": true }
          ],
          "RequiresSignature": true,
          "RequiredPhotos": 1
        },
        {
          "ItemId": "4f4cr6a68f529ayad8d5he25xq",
          "Name": "No heat",
          "Color": "#c92a2a",
          "DefaultDurationMinutes": 60,
          "CrewSize": 1,
          "DefaultPriority": "High",
          "ServiceLevelPolicyId": "4ff712gq9zvzfks9tqyqgawkmb",
          "BillingMode": "FixedPrice",
          "FixedPrice": 129,
          "RequiresSignature": true
        }
      ]
    }
  ]
}
```

Notes:

- A policy's `Scope` must be `WorkOrders` for a work order type to use it. `Targets` hold minutes per `Metric` (`FirstResponse`, `Resolution`) and `Priority` (`Urgent`, `High`, `Normal`, `Low`); a target with no `Priority` covers every other priority. `HoursScheduleId` names a Business Hours schedule; empty counts around the clock. The policy is refused when the schedule doesn't exist or is never open.
- `WorkOrderServiceLevelSettings.PauseStatuses` are the work order statuses the timers stop in; a sub-status with `PausesSla` stops them too.
- A question's `Rule` reads earlier questions of the same checklist by `Id` (`Source`); the editor refuses a condition on the question itself or on a later question. The older `"ShowWhen": { "QuestionId": "gas-smell", "EqualsValue": "Yes" }` is still accepted and saved as a rule.
- A `Product` line can name `StockItemId` (an Inventory stock item) so parts used are taken off a stock location; `RequiredSkills` (`SkillId`, `MinProficiency`), `RequiredCertificationIds` and `RequiredEquipmentTypeIds` (asset type ids) are read by Dispatch.
- `AppointmentTypeIds` lists Scheduling appointment types whose confirmed bookings make work orders of the type.
- A `ServiceLevels` section (the older Work Orders policy shape with `Targets` of `Priority`, `ResponseMinutes`, `ResolveMinutes`, plus `WeeklyHours`, `HoursCalendarId`, `TimeZoneId`, `PauseStatuses`) is still read: each policy is copied to a shared policy with the same id, weekly hours becoming a Business Hours schedule. Don't write it in new recipes.

## Agreements With Visits

Needs `CrestApps.OrchardCore.WorkOrders.Agreements` (and therefore Agreements and Accounts). The account (`AccountId`), the customer (`CustomerId`, a user id) and the assets must already exist on the target site.

The `WorkOrders` step's `Agreements` section uses Work Orders' own flat shape and makes an Agreements-module agreement with the visits stored as `WorkOrderAgreementTerms`:

```json
{
  "steps": [
    {
      "name": "WorkOrders",
      "Agreements": [
        {
          "ItemId": "4fagr0000000000000000000a1",
          "Name": "Boiler maintenance plan",
          "Status": "Active",
          "AccountId": "4facc0000000000000000000a1",
          "Contact": { "Name": "Dana Reyes", "Phone": "+16175550100", "Email": "dana@example.com" },
          "ServiceAddress": { "AddressLine1": "12 Elm Street", "City": "Boston", "Region": "MA", "PostalCode": "02118", "Country": "US" },
          "AccessNotes": "Boiler room key at reception.",
          "AssetIds": [ "4fs9svm62gjxb5tzqxya3q8q20" ],
          "StartDate": "2027-01-01",
          "EndDate": "2027-12-31",
          "TimeZoneId": "America/New_York",
          "Pricing": "PerVisit",
          "PricePerVisit": 99,
          "ExpiringNoticeDays": 30,
          "Schedules": [
            { "Id": "semiannual", "Name": "Twice-yearly service", "TypeId": "4fsyaebpefs8a296tfsf55b5ec", "Rule": "FREQ=MONTHLY;INTERVAL=6", "LeadDays": 14 }
          ]
        }
      ]
    }
  ]
}
```

The same agreement in the Agreements module's own `Agreements` step, with the visits in `Properties` (use this to also set covered hours):

```json
{
  "steps": [
    {
      "name": "Agreements",
      "Agreements": [
        {
          "ItemId": "4fagr0000000000000000000a1",
          "Name": "Boiler maintenance plan",
          "Status": "Active",
          "AccountId": "4facc0000000000000000000a1",
          "StartDate": "2027-01-01",
          "EndDate": "2027-12-31",
          "TimeZoneId": "America/New_York",
          "ExpiringNoticeDays": 30,
          "Hours": { "Kind": "Block", "Hours": 20, "RoundingMinutes": 15 },
          "Properties": {
            "WorkOrderAgreementTerms": {
              "Contact": { "Name": "Dana Reyes", "Phone": "+16175550100", "Email": "dana@example.com" },
              "ServiceAddress": { "AddressLine1": "12 Elm Street", "City": "Boston", "Region": "MA", "PostalCode": "02118", "Country": "US" },
              "AssetIds": [ "4fs9svm62gjxb5tzqxya3q8q20" ],
              "Pricing": "Included",
              "Schedules": [
                { "Id": "semiannual", "Name": "Twice-yearly service", "TypeId": "4fsyaebpefs8a296tfsf55b5ec", "Rule": "FREQ=MONTHLY;INTERVAL=6", "LeadDays": 14 }
              ]
            }
          }
        }
      ]
    }
  ]
}
```

- `Status` is an Agreements `AgreementStatus`: `Draft`, `Active`, `Paused`, `Expired` or `Canceled`. Only an active agreement within its period makes visits.
- `Rule` is an RFC 5545 rule expanded from `StartDate` in `TimeZoneId` with the Calendars recurrence engine. The **Visits** panel offers `FREQ=WEEKLY`, `FREQ=WEEKLY;INTERVAL=2`, `FREQ=MONTHLY`, `FREQ=MONTHLY;INTERVAL=3` and `FREQ=MONTHLY;INTERVAL=6`.
- A schedule with empty `AssetIds` covers all of the agreement's assets.
- `Pricing` `Included` gives each visit's work order an `AgreedPrice` of zero; `PerVisit` uses `PricePerVisit`. Billable labor hours of completed visits count against `Hours`.

## Deployment Plan Step

The **Work order settings** deployment step (`WorkOrdersDeploymentStep`) writes a `WorkOrders` recipe step with `Checklists`, `SubStatuses` and `Types`. Set `IncludeAgreements` to also export the agreements that have visits, in the flat shape above. Add the **Service level policies** step to the same plan, before it, for the policies.
