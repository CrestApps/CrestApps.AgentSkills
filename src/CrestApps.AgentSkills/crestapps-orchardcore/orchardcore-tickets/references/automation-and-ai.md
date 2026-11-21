# Tickets Automation: Workflows, Webhooks, Notifications and AI

## Workflow Events

Registered with `OrchardCore.Workflows` on (`WorkflowsStartup`), category **Tickets**. The activity `Name` used in a `WorkflowType` recipe is the event name in `TicketsConstants.Events`.

| Event (display) | Activity name | Extra filter |
|---|---|---|
| Ticket Created | `TicketCreated` | |
| Ticket Updated | `TicketUpdated` | `ChangedFields` (change keys) |
| Ticket Status Changed | `TicketStatusChanged` | `Categories` (status categories) |
| Ticket Assigned | `TicketAssigned` | |
| Ticket Customer Replied / Agent Replied / Note Added | `TicketCustomerReplied`, `TicketAgentReplied`, `TicketNoteAdded` | |
| Ticket Service Level Warning / Breached | `TicketServiceLevelWarning`, `TicketServiceLevelBreached` | |
| Ticket Solved / Reopened / Closed / Merged | `TicketSolved`, `TicketReopened`, `TicketClosed`, `TicketMerged` | |
| Ticket Rated | `TicketRated` | (`Score`, `Comment` in the input) |
| Ticket Action Requested | `TicketActionRequested` | `ActionName`; each published workflow starting with it becomes a named action in the ticket's **Actions** menu |
| Ticket Time Elapsed | `TicketTimeElapsed` | `Hours` (default 24), `Since` (`Created`, `LastCustomerReply`, `LastAgentReply`, `StatusChanged`), `BusinessHours` (count in the desk's `HoursScheduleId`), `Categories`; checked every 15 minutes, once per ticket and moment |

Every event filters on `DeskIds`, `TicketTypes` and `CategoryIds` (a category includes those below it). `Workflow.Input` holds `TicketId`, `Number`, `Subject`, `DeskId`, `StatusId`, `StatusCategory`, `PriorityId`, `TeamId`, `AssigneeId`, `Requester`, `ContentType`, `CategoryId`, `CategoryPath`, plus per event `ChangedFields`, `Previous`, `Body`, `Metric`, `MergedIntoId`, `ActionName`. Change keys (`TicketChangeKeys`): `Subject`, `Desk`, `Status`, `Priority`, `Team`, `Assignee`, `EscalationLevel`, `Link`, `Category`, `Impact`, `Urgency`, `Estimate`, `BillingType`, `Agreement`, `Fields`. A workflow starts after the change commits and the lock is released; workflow-caused changes stop starting workflows at depth 3.

## Workflow Tasks

Each task (activity name is the class name) reads the ticket from `TicketIdExpression` (Liquid, usually `{{ Workflow.Input.TicketId }}`), except Create Ticket. Field values are stored as properties named after the field.

| Task | Activity name | Fields | Outcomes |
|---|---|---|---|
| Create Ticket | `CreateTicketTask` | `Subject`, `Body`, `DeskId`, `PriorityId`, `ContentType`, `Requester` (`Kind:Id`), `RequesterName`, `RequesterEmail` | Done, Failed |
| Update Ticket | `UpdateTicketTask` | `StatusId`, `PriorityId`, `DeskId`, `TeamId`, `AssigneeId`, `Subject`, `CategoryId` | Done, Failed |
| Assign Ticket | `AssignTicketTask` | `TeamId`, `AssigneeId`, `Mode` (empty or team member by the desk's strategy) | Done, Failed |
| Add Ticket Reply | `AddTicketReplyTask` | `Body` (public, e-mailed), `StatusId` | Done, Failed |
| Add Ticket Note | `AddTicketNoteTask` | `Body` (internal) | Done, Failed |
| Escalate Ticket | `EscalateTicketTask` | `Level` (empty raises by one), `TeamId`, `PriorityId` | Done, Failed |
| Get Ticket | `GetTicketTask` | none; sets `Workflow.Properties["Ticket"]` | Done, Failed |
| Find Tickets | `FindTicketsTask` | `Requester`, `DeskId`, `State` (`open`, `solved`, `all`); sets `TicketIds`, `TicketCount` | Found, None |
| Check Ticket Service Level | `CheckTicketServiceLevelTask` | none | OnTrack, Warning, Breached, Paused, None, Failed |
| Link Tickets | `LinkTicketsTask` | `Record` (ticket number or `Kind:Id`), `Kind` | Done, Failed |
| Notify About Ticket | `NotifyTicketTask` | `To` (requester by e-mail, assignee by notification), `Subject`, `Body` | Done, Failed |
| Create Work Order From Ticket | (Work Orders on) | work order type by name or id; sets `WorkOrderId`, `WorkOrderNumber` | |
| Triage Ticket With AI | `TriageTicketTask` (AI on) | adds the triage note; output `Triage` | |

With CrestApps Recipes on, every activity has a workflow activity schema.

```json
{
  "steps": [
    {
      "name": "WorkflowType",
      "data": [
        {
          "WorkflowTypeId": "4c1rmcvv9gpqb07sbwv77fw9da",
          "Name": "Escalate a ticket whose target is breached",
          "IsEnabled": true,
          "IsSingleton": false,
          "LockTimeout": 0,
          "LockExpiration": 0,
          "DeleteFinishedWorkflows": true,
          "Activities": [
            {
              "ActivityId": "4hhw431jq6wm939h26zwkyj5vc",
              "Name": "TicketServiceLevelBreached",
              "X": 20,
              "Y": 40,
              "IsStart": true,
              "Properties": { "ActivityMetadata": { "Title": "A target was breached" }, "DeskIds": [] }
            },
            {
              "ActivityId": "4qjq2k0hjabbrbgf7s11gdk2gh",
              "Name": "EscalateTicketTask",
              "X": 320,
              "Y": 40,
              "IsStart": false,
              "Properties": { "ActivityMetadata": { "Title": "Escalate" }, "TicketIdExpression": "{{ Workflow.Input.TicketId }}" }
            },
            {
              "ActivityId": "4v2a3qqthkgssqakyxsaxvt6bd",
              "Name": "AddTicketNoteTask",
              "X": 620,
              "Y": 40,
              "IsStart": false,
              "Properties": {
                "ActivityMetadata": { "Title": "Say why" },
                "TicketIdExpression": "{{ Workflow.Input.TicketId }}",
                "Body": "Escalated: the {{ Workflow.Input.Metric }} target of {{ Workflow.Input.Number }} was breached."
              }
            }
          ],
          "Transitions": [
            { "Id": 0, "SourceActivityId": "4hhw431jq6wm939h26zwkyj5vc", "SourceOutcomeName": "Done", "DestinationActivityId": "4qjq2k0hjabbrbgf7s11gdk2gh" },
            { "Id": 0, "SourceActivityId": "4qjq2k0hjabbrbgf7s11gdk2gh", "SourceOutcomeName": "Done", "DestinationActivityId": "4v2a3qqthkgssqakyxsaxvt6bd" }
          ],
          "Properties": {}
        }
      ]
    }
  ]
}
```

Business-hours checks inside a workflow use **Check Business Hours** (`orchardcore-business-hours`).

## Webhooks and Audit Trail

With the CrestApps Webhooks feature on (`WebhooksStartup`), events in category **Tickets**: `ticket.created`, `ticket.updated`, `ticket.assigned`, `ticket.reply_added` (public replies only), `ticket.solved`, `ticket.closed`, each carrying the ticket as `TicketApiModel` (the API's shape). With Audit Trail on, desk moves, merges and deletes are recorded with ids only (`TicketAuditData`). See `orchardcore-webhooks`.

## Requester Messages and Notifications

| Requester message (`TicketsConstants.EmailKinds`) | Notification type for account holders |
|---|---|
| `ticket-received` | `TicketRequestReceived` (**Your requests received**) |
| `ticket-reply` (public replies) | `TicketReplied` (**Replies to your requests**) |
| `ticket-solved` | `TicketRequestSolved` (**Your requests solved**) |
| `ticket-rating` | `TicketRatingRequested` (**Rating your solved requests**) |

- Templates (`TicketEmailSettings`: `Layout`, `Templates`) are edited under **Settings → Service Desk → Notification templates**. Template model: `Ticket` (`Number`, `Subject`, `Status`, `RequesterName`, `Category`, `Link`) and, for replies, `Reply.Body` (plain) and `Reply.Html` (`{{ Reply.Html | raw }}`). Reply files are linked with signed links valid 30 days, from the site's **Base URL**.
- Every message goes through the outbox (`TicketEmailOutboxItem`) and the **Send ticket e-mails** task; while undelivered, the reply shows a "not delivered" notice (`TimelineConstants.Properties.Notice`).
- A guest's unconfirmed address (web form) gets only fixed text and the number: `Ticket.Subject`, `Ticket.RequesterName` and `Ticket.Link` are empty.
- Tickets created with `TicketCreateRequest.IsImport`, or by a recipe's `Content` step, tell no observer: no e-mail, workflow or webhook.

| Agent notification type | Display name | Sent to |
|---|---|---|
| `TicketAssigned` | Tickets assigned to you | New assignee |
| `TicketTeamAssigned` | Tickets given to your team | Team members other than the assignee |
| `TicketRequesterReplied` | Replies from requesters | Assignee, or the team when unassigned |
| `TicketNoteAdded` | Internal notes on your tickets | Assignee (unless mentioned) |
| `TicketStatusChanged` | Status changes on your tickets | Assignee |
| `TicketServiceLevel` | Service-level targets | Assignee or team, once per metric and state while unread |
| `TicketMerged` | Tickets merged into yours | Assignee of the target |
| `TicketWorkflow` | Ticket workflow notices | From **Notify About Ticket** |
| `TicketApprovalRequested` | (Ticket Approvals) | Approvers of the current step |

Notifications carry the ticket id as `ReferenceId`, the admin page as `Url` and `TicketNotificationInfo` (number, subject, desk); `TicketNotificationVisibilityHandler` hides them from users who can no longer view the ticket. Mentions (`@username`) are the Timeline's `TimelineMention` type. See `orchardcore-notifications`.

## AI Tools

With `CrestApps.OrchardCore.AI` on (`AIToolsStartup`), selectable tools in category **Tickets** run as the signed-in user, within their scope:

| Tool name | Title | Does |
|---|---|---|
| `listTicketOptions` | List ticket options | Desks the user may open tickets in, statuses, priorities, categories of the default ticket type, with ids |
| `searchTickets` | Search tickets | By number or subject, status category, desk, assignee |
| `getTicket` | Get ticket | A ticket and its latest 20 entries |
| `createTicket` | Create ticket | On a desk the user may reply in, with a category |
| `addTicketReply` | Add ticket reply | Public reply or internal note |
| `updateTicket` | Update ticket | Status, priority, category, team or assignee |

Pick them on an AI profile's tools (see `orchardcore-ai-tools`).

## AI Assistant

- **Settings → Service Desk → AI profile for replies** (`TicketAISettings.ReplyProfileId`): the chat profile used for every desk that names none.
- A desk's **AI assistant** card (`TicketDeskAISettings`): `ProfileId`, `Disabled` (**No AI assistant on this desk**), `ApplyPriority` (**A triage sets the priority it suggests**).
- The ticket page's **AI assistant** panel: **Suggest a reply** (`POST Admin/ticket/{id}/assistant/reply`, Reply permission) reads the latest 30 replies and notes and fills the composer; nothing is sent on its own and internal notes are never quoted. **Triage** (`POST Admin/ticket/{id}/assistant/triage`, Edit permission) adds an internal note from "AI assistant" with a summary and a suggested priority; it may lower the priority or raise it at most one step above the type's default (`TicketAssistant.PriorityToApply`), a higher suggestion is only noted.
- `TicketAssistant` runs with the profile's tools off, counts usage under the `Tickets` category, and answers "not available" when the profile, model or connection fails.
- What is sent to the provider: the ticket number and subject, the latest 30 replies and internal notes with author names, plus the profile's instructions and knowledge. Protected information never is.
