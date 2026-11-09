# Work Orders Recipe Examples

The `WorkOrders` step imports its sections in this order: `Checklists`, `ServiceLevels`, `SubStatuses`, `Types`, then `Agreements` (only when the Service Agreements feature is on). Each entry is matched by `ItemId`; a known id is updated, an unknown valid id is created with that id, and an invalid id is replaced with a new one. Each entry goes through the same validation as the admin editor, and a refused entry is reported as a recipe error while the others are still saved.

Property names are the model names (PascalCase). Enums are written as their names. `CreatedUtc`, `ModifiedUtc` and `OwnerId` are environment-owned and are not exported.

## Checklists, Service Levels, Sub-statuses and Types

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
      "name": "WorkOrders",
      "Checklists": [
        {
          "ItemId": "4fexpaa3vgvp6s1h1y9jzak6dt",
          "Name": "Boiler safety check",
          "Questions": [
            { "Id": "gas-smell", "Section": "Before", "Text": "Gas smell on arrival?", "Kind": "YesNo", "Required": true },
            { "Id": "gas-action", "Section": "Before", "Text": "What did you do?", "Kind": "Choice", "Required": true, "Choices": [ "Isolated supply", "Called the gas emergency line" ], "ShowWhen": { "QuestionId": "gas-smell", "EqualsValue": "Yes" } },
            { "Id": "flue-co", "Section": "After", "Text": "Flue gas CO reading", "Kind": "Number", "Required": true, "Min": 0, "Max": 1000, "Unit": "ppm" },
            { "Id": "pressure", "Section": "After", "Text": "System pressure", "Kind": "PassFailNA", "Required": true },
            { "Id": "run-hours", "Section": "After", "Text": "Run hours", "Kind": "MeterReading", "Meter": "Run hours" },
            { "Id": "photo", "Section": "After", "Text": "Photo of the data plate", "Kind": "Photo", "MinPhotos": 1 }
          ]
        }
      ],
      "ServiceLevels": [
        {
          "ItemId": "4ff712gq9zvzfks9tqyqgawkmb",
          "Name": "Standard service",
          "TimeZoneId": "America/New_York",
          "WeeklyHours": { "Days": [ "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" ], "Opens": "08:00:00", "Closes": "18:00:00" },
          "WarningPercent": 75,
          "PauseStatuses": [ "OnHold" ],
          "Targets": [
            { "Priority": "Urgent", "ResponseMinutes": 120, "ResolveMinutes": 480 },
            { "Priority": "High", "ResponseMinutes": 240, "ResolveMinutes": 1440 },
            { "Priority": "Normal", "ResponseMinutes": 480, "ResolveMinutes": 2880 },
            { "Priority": "Low", "ResponseMinutes": 1440, "ResolveMinutes": 7200 }
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

- `ShowWhen` shows a question only when an earlier question's answer equals `EqualsValue`; the editor refuses a condition on the question itself or on a later question.
- Give a policy either `WeeklyHours` or `HoursCalendarId` (a calendar listed by an `IServiceHoursProvider`, such as a Contact Center business hours calendar). With neither, the clock runs around the clock.
- A `Product` line can name `StockItemId` (an Inventory stock item) so parts used are taken off a stock location; `RequiredSkills` (`SkillId`, `MinProficiency`), `RequiredCertificationIds` and `RequiredEquipmentTypeIds` (asset type ids) are read by Dispatch.
- `AppointmentTypeIds` lists Scheduling appointment types whose confirmed bookings make work orders of the type.

## Service Agreements

Needs `CrestApps.OrchardCore.WorkOrders.Agreements`. The customer (`CustomerId`, a user id), account (`AccountId`) and assets must already exist on the target site.

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

- `Rule` is an RFC 5545 rule expanded from `StartDate` in `TimeZoneId` with the Calendars recurrence engine. The editor offers `FREQ=WEEKLY`, `FREQ=WEEKLY;INTERVAL=2`, `FREQ=MONTHLY`, `FREQ=MONTHLY;INTERVAL=3` and `FREQ=MONTHLY;INTERVAL=6`.
- A schedule with empty `AssetIds` covers all of the agreement's assets.
- `Pricing` `Included` gives each visit's work order an `AgreedPrice` of zero; `PerVisit` uses `PricePerVisit`.

## Deployment Plan Step

The **Work order settings** deployment step (`WorkOrdersDeploymentStep`) writes a `WorkOrders` recipe step with the four settings sections. Set `IncludeAgreements` to export `Agreements` too.
