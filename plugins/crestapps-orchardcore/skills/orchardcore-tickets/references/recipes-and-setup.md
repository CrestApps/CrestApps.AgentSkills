# Tickets Recipes and Setup

Settings records are catalog entries in the `ServiceDesk` YesSql collection (`TicketsConstants.CollectionName`). Property names are PascalCase and enums are written as names. Records that other records name need fixed, valid 26-character `ItemId`s (letters and digits); an invalid id is refused with its name rather than imported under a new id.

## Statuses and Priorities

```json
{
  "steps": [
    {
      "name": "TicketStatuses",
      "Statuses": [
        { "ItemId": "4st0new000000000000000000a", "Name": "New", "Category": "New", "CustomerLabel": "Received", "Color": "#0d6efd", "Order": 10 },
        { "ItemId": "4st0open00000000000000000a", "Name": "Open", "Category": "Open", "CustomerLabel": "In progress", "Color": "#dc3545", "Order": 20 },
        { "ItemId": "4st0wait00000000000000000a", "Name": "Waiting for the requester", "Category": "Pending", "CustomerLabel": "Waiting for your reply", "PausesTimers": true, "Color": "#6f42c1", "Order": 30 },
        { "ItemId": "4st0appr00000000000000000a", "Name": "Waiting for approval", "Category": "OnHold", "CustomerLabel": "Waiting for approval", "Color": "#6c757d", "Order": 35 },
        { "ItemId": "4st0solv00000000000000000a", "Name": "Solved", "Category": "Solved", "CustomerLabel": "Solved", "Color": "#198754", "Order": 50 },
        { "ItemId": "4st0clos00000000000000000a", "Name": "Closed", "Category": "Closed", "CustomerLabel": "Closed", "Color": "#495057", "Order": 60 }
      ]
    },
    {
      "name": "TicketPriorities",
      "Priorities": [
        { "ItemId": "4pr0low000000000000000000a", "Name": "Low", "Rank": 1, "Color": "#6c757d" },
        { "ItemId": "4pr0norm00000000000000000a", "Name": "Normal", "Rank": 2, "Color": "#0d6efd" },
        { "ItemId": "4pr0high00000000000000000a", "Name": "High", "Rank": 3, "Color": "#fd7e14" },
        { "ItemId": "4pr0urg000000000000000000a", "Name": "Urgent", "Rank": 4, "Color": "#dc3545" }
      ]
    }
  ]
}
```

A site already has seeded statuses and priorities (created on first enable when none exist). Importing new records with other ids adds to them; update the seeded ones by exporting them first (**Tools → Deployments**) to learn their ids.

## Desks

```json
{
  "steps": [
    {
      "name": "TicketDesks",
      "Desks": [
        {
          "ItemId": "4desk0support000000000000a",
          "Name": "Support",
          "Description": "Questions and problems from customers.",
          "Audience": "Customers",
          "DefaultTeamId": "4team0support000000000000a",
          "HoursScheduleId": "44fy8z1wfv91trshefx9dqcsyj",
          "ServiceLevelPolicyId": "47bpbxt94eb7qbn8wjb9rg9eb3",
          "TicketTypes": [ "Ticket" ],
          "ReopenWindowDays": 7,
          "AutoCloseDays": 4,
          "DeleteProtectedInformationDays": 30,
          "AssignmentStrategy": "LeastLoaded",
          "AvailabilitySources": [ "WorkforceShifts", "CalendarAbsences" ],
          "NotifyRequester": true,
          "NotifyAssignee": true,
          "RatingRequestDelayHours": 24,
          "PriorityMatrix": [
            { "Impact": "High", "Urgency": "High", "PriorityId": "4pr0urg000000000000000000a" },
            { "Impact": "High", "Urgency": "Medium", "PriorityId": "4pr0high00000000000000000a" },
            { "Impact": "Low", "Urgency": "Low", "PriorityId": "4pr0low000000000000000000a" }
          ],
          "Properties": {
            "TicketDeskQueueSettings": { "QueueId": "4queue0support00000000000a" },
            "TicketDeskAISettings": { "ProfileId": "", "Disabled": false, "ApplyPriority": true },
            "TicketDeskTimeSettings": { "StageId": "4stage0tickets00000000000a", "NotBilledStageId": "4stage0nobill000000000000a" }
          }
        }
      ]
    }
  ]
}
```

| Desk member | Notes |
|---|---|
| `Audience` | `Customers` desks serve the portal and web form; `Internal` desks are for staff (portal shows them to staff with Staff on) |
| `AssignmentStrategy` | `LeastLoaded`, `RoundRobin` (`TeamAssignmentStrategies`) or empty for nobody |
| `AvailabilitySources` | Names of `ITeamMemberAvailabilitySource`s: `WorkforceShifts` (Workforce), `CalendarAbsences` (Calendars), `ContactCenterPresence` (Contact Center); `null` means shifts and absences |
| `TicketTypes` | Content types the desk takes; empty for every ticket type |
| `RatingRequestDelayHours` | 0 never asks (Tickets Portal sends the rating e-mail) |
| `Properties.TicketDeskQueueSettings` | Tickets for Contact Center |
| `Properties.TicketDeskAISettings` | AI on: `ProfileId` (empty uses the service desk's), `Disabled`, `ApplyPriority` |
| `Properties.TicketDeskTimeSettings` | Time Tracker on: `StageId`, `NotBilledStageId` |

A desk, status or priority still in use can't be deleted.

## Saved Views and Boards

```json
{
  "steps": [
    {
      "name": "TicketViews",
      "Views": [
        {
          "ItemId": "4view0urgent0000000000000a",
          "Name": "Urgent and unassigned",
          "Order": 10,
          "Query": {
            "Categories": [ "New", "Open" ],
            "PriorityIds": [ "4pr0urg000000000000000000a" ],
            "Assignee": "none",
            "Sort": "Updated",
            "Take": 50
          }
        }
      ]
    },
    {
      "name": "TicketBoards",
      "Boards": [
        {
          "ItemId": "4board0support00000000000a",
          "Name": "Support board",
          "SharedWithTeamId": "4team0support000000000000a",
          "Query": { "DeskIds": [ "4desk0support000000000000a" ] },
          "Columns": [
            { "Id": "new", "Name": "New", "StatusIds": [ "4st0new000000000000000000a" ] },
            { "Id": "doing", "Name": "In progress", "StatusIds": [ "4st0open00000000000000000a" ], "Limit": 15 },
            { "Id": "waiting", "Name": "Waiting", "StatusIds": [ "4st0wait00000000000000000a", "4st0appr00000000000000000a" ], "DefaultStatusId": "4st0wait00000000000000000a" },
            { "Id": "done", "Name": "Solved", "StatusIds": [ "4st0solv00000000000000000a" ] }
          ]
        }
      ]
    }
  ]
}
```

- `TicketQuery`: `Search`, `DeskIds`, `Categories` (status categories), `StatusIds`, `PriorityIds`, `TeamIds`, `Assignee` (`me`, `none` for unassigned (`TicketAssignees`), or a user id), `ContentTypes`, `Requester`, `AccountId`, `CategoryId`, `DueWithin`, `SolvedBeforeUtc`, `Sort`, `Skip`, `Take`.
- A view or board with no `SharedWithTeamId` and no owner is shared with everybody. A personal one carries `"OwnerUserName": "ana"` on the entry (prefer it to `OwnerId`, since user ids differ between sites); the import resolves it to that user and refuses the record when the user doesn't exist.
- A status may be in one column of a board only; a column with several statuses and no default asks the agent which one on a move.

## Ticket Types

A ticket type is any content type with `TicketPart` and stereotype `Ticket`; the feature creates `Ticket` (title shown as **Subject**). Ticket types are not versioned or drafted.

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "ITRequest",
          "DisplayName": "IT request",
          "Settings": {
            "ContentTypeSettings": { "Stereotype": "Ticket", "Listable": false, "Creatable": false }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart",
              "Settings": {
                "ContentTypePartSettings": { "DisplayName": "Subject", "Position": "1" },
                "TitlePartSettings": { "Options": "EditableRequired" }
              }
            },
            {
              "PartName": "TicketPart",
              "Name": "TicketPart",
              "Settings": {
                "ContentTypePartSettings": { "Position": "2" },
                "TicketPartSettings": {
                  "DefaultDeskId": "49vyvps9npqdzz9mz7xfwqgdzx",
                  "DefaultTeamId": "",
                  "DefaultPriorityId": "",
                  "ServiceLevelPolicyId": "",
                  "CustomerSubmittable": true,
                  "PortalDescription": "A problem with your laptop, printer, network or software.",
                  "CategoryTaxonomyId": "4375vnkj86k0k9jxjgrkd2hfbz",
                  "CategoryRequired": true,
                  "CategoryLeavesOnly": false
                },
                "TicketApprovalSettings": {
                  "Steps": [
                    { "Name": "Manager", "ApproverIds": [], "RequesterManager": true },
                    { "Name": "Finance", "ApproverIds": [ "4userfinance0000000000000a" ], "RequesterManager": false }
                  ],
                  "WaitingStatusId": "4st0appr00000000000000000a"
                }
              }
            },
            {
              "PartName": "ITRequest",
              "Name": "ITRequest",
              "Settings": { "ContentTypePartSettings": { "Position": "3" } }
            }
          ]
        }
      ],
      "ContentParts": [
        {
          "Name": "ITRequest",
          "ContentPartFieldDefinitionRecords": [
            {
              "FieldName": "TextField",
              "Name": "LaptopModel",
              "Settings": {
                "ContentPartFieldSettings": { "DisplayName": "Laptop model", "Position": "0" },
                "TextFieldSettings": { "Required": true },
                "TicketFieldCustomerSettings": { "CustomerEditable": true },
                "ConditionalFieldSettings": {
                  "ShowWhen": {
                    "Match": "All",
                    "Conditions": [
                      { "Source": "TicketCategory", "Operator": "IsOrUnder", "Values": [ "4hcz2tdnjksr3ptafr4cvrvmas" ] }
                    ]
                  }
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

- `TicketPartSettings`: `DefaultDeskId`, `DefaultTeamId`, `DefaultPriorityId`, `ServiceLevelPolicyId`, `RequiredSkills` (with Skills on), `CustomerSubmittable` (**Customers can submit it**), `PortalDescription` (**Description for customers**), `CategoryTaxonomyId`, `CategoryRequired` (forms only, never the API, workflows or e-mail), `CategoryLeavesOnly`.
- `TicketApprovalSettings` (Ticket Approvals): up to three `Steps` (`Name`, `ApproverIds` user ids, `RequesterManager`) and `WaitingStatusId` (a Pending or OnHold status; the first waiting status when empty).
- `TicketFieldCustomerSettings.CustomerEditable` (**Customers fill this in**, off by default) decides which fields the portal and web form show; other fields can't be set by posting them.
- Fields can have **Show this field when** rules (`ConditionalFieldSettings`); Tickets offers the category as the `TicketCategory` condition source with `IsOrUnder`.

## Categories

A ticket type's categories are terms of an Orchard Core taxonomy whose terms carry `TicketCategoryPart` (term type `TicketCategory`). A category gives its tickets `DeskId`, `TeamId`, `PriorityId` and `ServiceLevelPolicyId`, inherits what it leaves empty from its parent, and can be `HiddenFromCustomers` with a `CustomerDescription`.

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "4375vnkj86k0k9jxjgrkd2hfbz",
          "ContentType": "Taxonomy",
          "DisplayText": "IT categories",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "IT categories" },
          "TaxonomyPart": {
            "TermContentType": "TicketCategory",
            "Terms": [
              {
                "ContentItemId": "478zt8thf3100vq7z7av10bkg1",
                "ContentType": "TicketCategory",
                "DisplayText": "Hardware",
                "TitlePart": { "Title": "Hardware" },
                "TicketCategoryPart": { "DeskId": "49vyvps9npqdzz9mz7xfwqgdzx" },
                "Terms": [
                  {
                    "ContentItemId": "4hcz2tdnjksr3ptafr4cvrvmas",
                    "ContentType": "TicketCategory",
                    "DisplayText": "Laptop",
                    "TitlePart": { "Title": "Laptop" },
                    "TicketCategoryPart": { "CustomerDescription": "Have the laptop's model ready." }
                  }
                ]
              }
            ]
          }
        }
      ]
    }
  ]
}
```

A ticket keeps `CategoryId` and `CategoryPath` (ids from the top down). Moving a category under another parent runs the resumable `Tickets.CategoryPaths` step; a deleted term shows as "Removed category". **Apply the category's routing** on the ticket (`Admin/ticket/{id}/category-routing`) moves desk, priority and team to the category's.

## Ticket Form Widget

With Ticket Web Form on, `TicketFormWidget` (a `Widget`-stereotype type with `TicketFormPart`) is placed like any widget:

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "4ticketform000000000000000a",
          "ContentType": "TicketFormWidget",
          "DisplayText": "Contact support",
          "Latest": true,
          "Published": true,
          "TicketFormPart": {
            "TicketType": "Ticket",
            "DeskId": "4desk0support000000000000a"
          },
          "LayerMetadata": { "Layer": "Always", "Zone": "Content", "Position": 1 }
        }
      ]
    }
  ]
}
```

## Help Desk Starter Recipe

`HelpDeskStarter` (**Help desk starter**, `help-desk-starter.recipe.json` in the Tickets module, not a setup recipe) runs: `feature` (Business Hours, Service Levels, Tickets, Content Fields, Taxonomies, Workflows), `BusinessHoursSchedules` (**Help desk hours**, weekdays 08:00 to 18:00 New York), `ServiceLevelPolicies` (**Standard help desk**, scope `Tickets`, targets by priority name), `TicketDesks` (Support for customers, IT internal with round robin), `ContentDefinition` (`ITRequest` type with conditional questions and an outage answer), `content` (**IT categories** taxonomy: Hardware > Laptop, Printer; Software > Access request; Network) and `WorkflowType` (two disabled examples: escalate on a breached target with a note, and note a ticket reopened by its requester). Run it under **Tools → Recipes**.
