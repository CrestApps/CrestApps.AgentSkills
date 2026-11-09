# Field Service Sample Recipe

`FieldServiceSample` ships in the Dispatch module's `Recipes` folder (`field-service-sample.recipe.json`, category `field service`, not a setup recipe). Run it from **Configuration > Recipes**. Its records have fixed ids, so running it again changes nothing. After it runs, publish a schedule for the **Field Operations** team, then add work orders and dispatch them from **Field Service > Dispatch Board**; **Get started** on the board (`dispatch/get-started`) checks each part of the setup.

The steps, in order (trimmed to one entry per list):

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "CrestApps.OrchardCore.Geo",
        "CrestApps.OrchardCore.Assets",
        "CrestApps.OrchardCore.Assets.Fleet",
        "CrestApps.OrchardCore.WorkOrders",
        "CrestApps.OrchardCore.Workforce",
        "CrestApps.OrchardCore.Workforce.ShiftPlanning",
        "CrestApps.OrchardCore.Workforce.Portal",
        "CrestApps.OrchardCore.Dispatch",
        "CrestApps.OrchardCore.Dispatch.Mobile"
      ]
    },
    {
      "name": "Workforce",
      "Teams": [
        { "name": "Field Operations", "kind": "Team", "timeZoneId": "America/New_York", "description": "Technicians who visit customers.", "location": { "address": "1 Depot Way, Boston, MA 02118" } }
      ],
      "Workers": [
        { "displayName": "Ana Field", "employeeNumber": "2002", "jobTitle": "Service technician", "employmentType": "FullTime", "contractedHoursPerWeek": 40, "teamMemberships": [ { "teamName": "Field Operations" } ] }
      ]
    },
    {
      "name": "WorkforceShiftPlanning",
      "Templates": [
        { "name": "Field day", "color": "#0c8599", "start": "08:00:00", "lengthMinutes": 540, "teamNames": [ "Field Operations" ], "segments": [ { "activityName": "Meal", "offsetMinutes": 240, "lengthMinutes": 30 } ] }
      ]
    },
    {
      "name": "Geo",
      "Territories": [
        { "ItemId": "4fs7kjkcahkr0q5vh31ktg943y", "Name": "Boston", "PostalCodes": [ "021*" ], "TimeZoneId": "America/New_York", "TravelSpeedKmh": 35 },
        { "ItemId": "4f0n4hbx15vatb9tg9ykcbsym1", "Name": "Cambridge", "ParentId": "4fs7kjkcahkr0q5vh31ktg943y", "PostalCodes": [ "02138", "02139" ], "TimeZoneId": "America/New_York" }
      ]
    },
    {
      "name": "Assets",
      "Types": [
        { "ItemId": "4frxjzg9rc0mtph04y78qk38dy", "Name": "Service van", "Category": "Vehicle", "Description": "A van that carries the parts technicians use most." }
      ],
      "Assets": [
        { "ItemId": "4fvne1bd88btzjbfsx0fjs17gp", "Name": "Van 1", "AssetTypeId": "4frxjzg9rc0mtph04y78qk38dy", "Status": "Active", "OwnerKind": "Internal", "Fleet": { "Plate": "FS-1001", "CarriesStock": true } }
      ]
    },
    {
      "name": "WorkOrders",
      "SubStatuses": [
        { "ItemId": "4fqsx194f332rzgh3s4wshe051", "Name": "Waiting on parts", "ParentStatus": "OnHold", "Color": "#e8590c", "PausesSla": true }
      ],
      "Types": [
        { "ItemId": "4f4cr6a68f529ayad8d5he25xq", "Name": "No heat", "Color": "#c92a2a", "DefaultDurationMinutes": 60, "CrewSize": 1, "DefaultPriority": "High", "RequiresSignature": true }
      ]
    }
  ]
}
```

The `Workforce` and `WorkforceShiftPlanning` steps belong to the Workforce module (see `orchardcore-workforce`); the `Geo`, `Assets` and `WorkOrders` steps are described in `orchardcore-geo`, `orchardcore-assets` and `orchardcore-work-orders`.

To make your own starter recipe, copy this shape, keep 26-character lowercase ids for every `ItemId` so references survive reruns, and add a `Settings` step with `DispatchSettings` (and `GeoSettings`) when you want the board time zone, objective weights or providers preset.
