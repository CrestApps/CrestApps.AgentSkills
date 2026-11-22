---
name: orchardcore-tickets
description: Skill for the CrestApps Orchard Core Tickets help desk (CrestApps.OrchardCore.Tickets) and Tickets for Contact Center. Covers ticket types with TicketPart, desks with per-desk permissions and view scopes, statuses, priorities and the priority matrix, views, boards, categories with conditional questions, replies and notes on the timeline, team assignment, service-level targets, approvals, protected information, the Tickets Portal, the Ticket Form widget, the api/tickets/v1 API, e-mails and notifications, workflows, webhooks, AI tools and assistant, background tasks, reports, Contact Center queue routing, the Service Desk menu and the Ticket recipe steps. Use this skill for help desk, service desk, support ticket, IT or HR request and customer portal work. Strong matches include TicketPart, TicketDesk, TicketStatus, ITicketService, ITicketObserver, ITicketAccessService, TicketsPermissions and TicketDeskQueueSettings.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Tickets

## A Help Desk on Content Items

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Tickets help desk. A ticket is a content item of a ticket type (any type with `TicketPart`, stereotype `Ticket`), filed under a **desk** (IT, HR, customer support) that decides who may see and work on it. Replies, internal notes and every change are on the ticket's timeline; first response, next response and resolution targets run on Service Levels counted in business hours; new tickets go to a team's members through Teams.

### Guidelines

- Install `CrestApps.OrchardCore.Tickets` (and `CrestApps.OrchardCore.Tickets.ContactCenter` for queue routing) in the web/startup project.
- Reference `CrestApps.OrchardCore.Tickets.Abstractions` (models, `ITicketService`, `ITicketAccessService`, `ITicketObserver`, `TicketsPermissions`, `TicketsConstants`) or `CrestApps.OrchardCore.Tickets.Core` (index, default services, `TicketScopeFilter`) from your module, never the module project.
- Change tickets only through `ITicketService` (create, update, reply, assign, merge, link, edit, delete, rate, approvals). It locks the ticket, writes the timeline event, runs the life cycle and SLA timers and tells observers after commit. Never save `TicketPart` directly.
- Check access with `ITicketAccessService` and apply its scope (`ApplyScopeAsync`) to every ticket query you write, so a user never sees a ticket outside their desk scope.
- Recipe records referenced by id (statuses in boards, desks in ticket types, policies, schedules) need fixed, valid 26-character `ItemId`s; an invalid id is refused, not replaced.
- Tickets themselves travel with Orchard Core's `Content` step and their history with `TimelineEntries`; settings records have their own steps.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

## Features

| Feature | ID | Depends on |
|---|---|---|
| Tickets | `CrestApps.OrchardCore.Tickets` | `CrestApps.OrchardCore.Timeline`, `CrestApps.OrchardCore.ServiceLevels`, `CrestApps.OrchardCore.Teams`, `CrestApps.OrchardCore.Resources`, `CrestApps.OrchardCore.Users`, `OrchardCore.Contents`, `OrchardCore.Title`, `CrestApps.OrchardCore.ContentFields.ConditionalFields`, `OrchardCore.Notifications.Email` |
| Tickets Portal | `CrestApps.OrchardCore.Tickets.Portal` | Tickets, `CrestApps.OrchardCore.Users.Account` (My account), `OrchardCore.RateLimits` |
| Ticket Web Form | `CrestApps.OrchardCore.Tickets.WebForm` | Tickets, `OrchardCore.RateLimits` |
| Ticket Approvals | `CrestApps.OrchardCore.Tickets.Approvals` | Tickets |
| Tickets API | `CrestApps.OrchardCore.Tickets.Api` | Tickets |
| Tickets for Contact Center | `CrestApps.OrchardCore.Tickets.ContactCenter` (own module) | Tickets, `CrestApps.OrchardCore.ContactCenter.Queues` |

All in category **Service Desk**. Service Levels brings Business Hours. Enable `CrestApps.OrchardCore.Attachments` for files on replies, notes, the portal, the web form and the API.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Tickets",
        "CrestApps.OrchardCore.Tickets.Portal",
        "CrestApps.OrchardCore.Tickets.WebForm",
        "CrestApps.OrchardCore.Tickets.Approvals",
        "CrestApps.OrchardCore.Tickets.Api",
        "CrestApps.OrchardCore.Attachments",
        "OrchardCore.Taxonomies",
        "OrchardCore.Workflows"
      ],
      "disable": []
    }
  ]
}
```

Integrations switch on with other features (no feature of their own): Workflows, Webhooks, Audit Trail, SignalR (live boards), Notifications, AI (`CrestApps.OrchardCore.AI`), Data Pipelines Core and Reports, Accounts, Agreements, Staff, Time Tracker, Work Orders, Omnichannel messaging workspace, Skills, Knowledge Base suggestions. See [references/integrations.md](references/integrations.md).

| Project | Holds |
|---|---|
| `CrestApps.OrchardCore.Tickets.Abstractions` | `TicketPart`, `TicketStatus`, `TicketPriority`, `TicketDesk`, `TicketView`, `TicketBoard`, requests and results, `ITicketService`, `ITicketAccessService`, `ITicketBoardService`, `ITicketCategoryService`, `ITicketObserver`, `ITicketActionSource`, `ITicketReplyTemplateSource`, `ITicketAgreementResolver`, `TicketsPermissions`, `TicketsConstants` |
| `CrestApps.OrchardCore.Tickets.Core` | `TicketIndex`, `TicketLinkIndex`, `TicketBacklogIndex`, `DefaultTicketService`, `DefaultTicketAccessService`, `DefaultTicketBoardService`, `TicketScopeFilter`, `TicketLifecycle`, auto closer, backlog recorder, e-mail observer, service-level scope and observer, API models |
| `CrestApps.OrchardCore.Tickets` (module) | Pages, drivers, background tasks, workflow activities, webhooks, audit trail, data sets and reports, API, portal, web form, approvals, AI tools, migrations, recipe and deployment steps |

## Admin Menu

| Menu | Route | Who sees it |
|---|---|---|
| **Service Desk** (position 6, headset icon) → **Tickets** | `Admin/tickets` | Anyone with a view scope on at least one desk |
| **Service Desk → Boards**, then each pinned board | `Admin/tickets/boards`, `Admin/tickets/board/{id}` | Same |
| **Service Desk → Desks** | `Admin/tickets/desks` | `ManageTicketSettings` |
| **Service Desk → Statuses** | `Admin/tickets/statuses` | `ManageTicketSettings` |
| **Service Desk → Priorities** | `Admin/tickets/priorities` | `ManageTicketSettings` |
| **Service Desk → Views** | `Admin/tickets/views` | `ManageTicketSettings` |
| **Settings → Service Desk** (group `service-desk`) | Notification templates; with AI on, **AI profile for replies** | `ManageTicketSettings` |

Other pages: **New ticket** `Admin/tickets/new/{contentType?}`, ticket page `Admin/ticket/{id}` (Conversation of `Reply`/`Note` entries, History of `Event` entries, Details card, properties, Planning, Links, Files, Approval, Time, Work order and AI panels), approval page `Admin/ticket/{id}/approval`, bulk actions `Admin/tickets/bulk`. Ticket categories are a taxonomy (**Content → Taxonomies**); ticket types are edited under **Content → Content Definition**. Business hours and service-level policies are under **Content → Business Hours** and **Content → Service Levels**.

## Core Concepts

| Record | Key members |
|---|---|
| `TicketDesk` | `Name`, `Description`, `Audience` (`Customers`, `Internal`), `DefaultTeamId`, `HoursScheduleId` (for **Time elapsed** in business hours), `ServiceLevelPolicyId`, `TicketTypes`, `ReopenWindowDays` (7), `AutoCloseDays` (4), `DeleteProtectedInformationDays`, `AssignmentStrategy` (`LeastLoaded` default, `RoundRobin`, empty for nobody), `AvailabilitySources` (`WorkforceShifts`, `CalendarAbsences`, `ContactCenterPresence`; `null` = shifts and absences), `NotifyRequester`, `NotifyAssignee`, `RatingRequestDelayHours`, `PriorityMatrix`, `Properties` |
| `TicketStatus` | `Name`, `Category`, `CustomerLabel`, `PausesTimers`, `Color`, `Order` |
| `TicketPriority` | `Name`, `Rank` (higher is more urgent), `Color` |
| `TicketView` | `Name`, `Query` (`TicketQuery`), `OwnerId` (personal) or `SharedWithTeamId`, `Order` |
| `TicketBoard` | `Name`, `Description`, `Columns` (`Id`, `Name`, `StatusIds`, `DefaultStatusId`, `Limit`; at most 20), `Query`, `OwnerId`, `SharedWithTeamId`, `Order` |

| Status category | Meaning |
|---|---|
| `New`, `Open` | Being worked on |
| `Pending` | Waiting for the requester; `PausesTimers` pauses SLA timers |
| `OnHold` | Waiting for something else |
| `Solved` | Answered; a requester reply within the desk's reopen window reopens it |
| `Closed` | Final and read only; a reply opens a follow-up ticket |

The migration seeds statuses **New**, **Open**, **Waiting for the requester** (pauses timers), **On hold**, **Solved**, **Closed**; priorities **Low**, **Normal**, **High**, **Urgent**; a **Support** desk (Customers); default views; and a **Service desk** board.

A new ticket's values come from the request, then the category (`TicketCategoryPart`), the ticket type (`TicketPartSettings`), then the desk. The service-level policy is the agreement's, then the account's (`TicketAccountPart`), the category's, the type's and the desk's. A desk's priority matrix (`TicketPriorityRule { Impact, Urgency, PriorityId }`, levels `Low`, `Medium`, `High`) sets the priority from impact and urgency. Details in [references/recipes-and-setup.md](references/recipes-and-setup.md).

## Permissions

Each ticket permission exists globally and per desk (`PermissionTemplate`, such as `ViewAllTickets_{0}` **View all tickets in {desk}**); the global one implies every desk's.

| Permission | Display name | Implied by |
|---|---|---|
| `ManageTicketSettings` | Manage ticket settings (desks, statuses, priorities, shared views, e-mails) | |
| `ManageTicketBoards` | Manage shared ticket boards | `ManageTicketSettings` |
| `ManageOwnTicketBoards` | Manage my own ticket boards | `ManageTicketBoards` |
| `ViewAllTickets` / `ViewTeamTickets` / `ViewAssignedTickets` | View scopes All, My teams (plus assigned), Assigned | each by the wider one |
| `EditTickets`, `ReplyToTickets`, `AddTicketNotes` | Edit; reply (implied by edit); internal notes (implied by reply) | |
| `AssignTickets`, `MergeTickets`, `DeleteTickets` | On tickets the user may view | |
| `ViewProtectedTicketInformation` (security critical) | Reveal protected items (`ViewProtectedTicketInformation_{0}` per desk) | |
| `ViewTicketReports` | Ticket reports | |
| `CancelTicketApprovals` | Call off an approval (Ticket Approvals feature) | |

Administrators get the global permissions by default. Opening a board needs only a view scope. The portal uses no desk permissions: requesters see their own requests, account administrators their account's. `TicketScopeFilter` is applied by the list, API, data sets, reports and the account tab.

## Recipes and Deployment

| Step | Entries | Matched by |
|---|---|---|
| `TicketStatuses` | `Statuses` | `ItemId` |
| `TicketPriorities` | `Priorities` | `ItemId` |
| `TicketDesks` | `Desks` | `ItemId`, then name (case-insensitive) |
| `TicketViews` | `Views` | `ItemId`; personal views carry `OwnerUserName` |
| `TicketBoards` | `Boards` | `ItemId`; columns validated; personal boards carry `OwnerUserName` |

Each step needs `OrchardCore.Recipes.Core`; each has a deployment step (needs `OrchardCore.Deployment`) and, with CrestApps Recipes on, JSON schemas. Colors must be hex or a color name. Order: `BusinessHoursSchedules` → `ServiceLevelPolicies` → `TicketStatuses` → `TicketPriorities` → `TicketDesks` → `ContentDefinition` (ticket types) → `Content` (category taxonomy) → `TicketViews` → `TicketBoards`.

```json
{
  "steps": [
    {
      "name": "TicketDesks",
      "Desks": [
        {
          "ItemId": "49vyvps9npqdzz9mz7xfwqgdzx",
          "Name": "IT",
          "Description": "Requests and problems from employees.",
          "Audience": "Internal",
          "HoursScheduleId": "44fy8z1wfv91trshefx9dqcsyj",
          "ServiceLevelPolicyId": "47bpbxt94eb7qbn8wjb9rg9eb3",
          "ReopenWindowDays": 7,
          "AutoCloseDays": 4,
          "AssignmentStrategy": "RoundRobin",
          "NotifyRequester": true,
          "NotifyAssignee": true
        }
      ]
    }
  ]
}
```

Statuses, priorities, views, boards, ticket types with `TicketPartSettings`, approval settings, categories, the Ticket Form widget and the **Help desk starter** recipe (`HelpDeskStarter`) are in [references/recipes-and-setup.md](references/recipes-and-setup.md).

## Service Levels

Tickets registers the `Tickets` scope (metrics **First response** `FirstResponse`, **Next response** `NextResponse`, **Resolution** `Resolution`; priorities keyed by ticket priority id, writable by name in recipes). The first public agent reply completes first response; each customer reply starts a next response; solving completes resolution; a status with `PausesTimers` pauses them; changing priority or policy retargets. `TicketPart.NextDueUtc` keeps the next due time. Warnings and breaches raise the **Ticket Service Level Warning**/**Breached** events and the `TicketServiceLevel` notification. See `orchardcore-service-levels`.

## Notifications and E-mails

- Requester messages `ticket-received`, `ticket-reply` (public replies only), `ticket-solved`, `ticket-rating` are templated under **Settings → Service Desk** and go through an outbox (retries at 5 min, 15 min, 1 h, 4 h, 12 h). A requester with a site account gets them as notifications (`TicketRequestReceived`, `TicketReplied`, `TicketRequestSolved`, `TicketRatingRequested`); others by e-mail. Links need the site's **Base URL**.
- Agents get notification types `TicketAssigned`, `TicketTeamAssigned`, `TicketRequesterReplied`, `TicketNoteAdded`, `TicketStatusChanged`, `TicketServiceLevel`, `TicketMerged`, `TicketWorkflow` and, with Approvals, `TicketApprovalRequested`. The person who made the change is never told.

## Workflows, Webhooks and Audit Trail

With `OrchardCore.Workflows` on (category **Tickets**):

| Events | Tasks |
|---|---|
| Ticket Created, Updated (changed-field filter), Status Changed (category filter), Assigned, Customer Replied, Agent Replied, Note Added, Service Level Warning, Service Level Breached, Solved, Reopened, Closed, Merged, Rated, Action Requested, Time Elapsed | Create Ticket, Update Ticket, Assign Ticket, Add Ticket Reply, Add Ticket Note, Escalate Ticket, Get Ticket, Find Tickets, Check Ticket Service Level, Link Tickets, Notify About Ticket; Create Work Order From Ticket (Work Orders); Triage Ticket With AI (AI) |

Events can be narrowed to desks, ticket types and categories. Workflow-caused changes stop starting workflows at depth 3. Webhook events (Webhooks feature, category Tickets): `ticket.created`, `ticket.updated`, `ticket.assigned`, `ticket.reply_added` (public only), `ticket.solved`, `ticket.closed`. Audit Trail records desk moves, merges and deletes with ids only. Details and a workflow JSON sample in [references/automation-and-ai.md](references/automation-and-ai.md).

## AI

With `CrestApps.OrchardCore.AI` on, selectable AI tools (category **Tickets**, acting as the signed-in user): `listTicketOptions`, `searchTickets`, `getTicket`, `createTicket`, `addTicketReply`, `updateTicket`. **Settings → Service Desk** gets **AI profile for replies** (`TicketAISettings.ReplyProfileId`); each desk can use it, another profile or none (`TicketDeskAISettings`), and the ticket page gets an **AI assistant** panel with **Suggest a reply** and **Triage**. See [references/automation-and-ai.md](references/automation-and-ai.md).

## Background Tasks

| Task | Schedule | Registered by |
|---|---|---|
| Close solved tickets | every 15 min | Tickets |
| Solve incidents of solved problems | every 5 min | Tickets |
| Record the ticket backlog | hourly at :05 | Tickets |
| Send ticket e-mails | every minute | Tickets |
| Ask requesters to rate solved tickets | hourly at :20 | Tickets Portal |
| Ticket time elapsed | every 15 min | Workflows integration |
| Service levels | every minute | Service Levels |

## Channels: Portal, Web Form, API, Contact Center

- **Tickets Portal**: `account/requests`, `account/requests/new`, `account/requests/{number}` in My account; guests use signed links `requests/{id}?token=`; ratings at `requests/{id}/rate`. Rate-limit groups `tickets-portal-create`, `tickets-portal-reply`, `tickets-portal-guest`.
- **Ticket Web Form**: the `TicketFormWidget` (`TicketFormPart` with `TicketType`, `DeskId`) posts to `tickets/forms/{widgetId}`; group `tickets-web-form`, a trap field and ReCaptcha when configured.
- **Tickets API**: `api/tickets/v1` (`GET tickets`, `GET tickets/{id}`, `GET tickets/{id}/entries`, `POST tickets`, `PATCH tickets/{id}`, `POST tickets/{id}/replies`, `POST tickets/{id}/notes`) on the `Api` scheme with `Idempotency-Key`.
- **Tickets for Contact Center**: a desk's **Contact Center queue** (`TicketDeskQueueSettings.QueueId` in the desk's `Properties`) turns each new unassigned ticket into a task activity enqueued with its priority; the agent who accepts the offer gets the ticket (`TicketOfferAcceptedHandler`, handler id `Tickets/ContactCenterOfferAccepted/v1`); assigning, solving, closing, merging or deleting first withdraws the queued work.

Details in [references/channels.md](references/channels.md).

## Extension Points

| To | Use |
|---|---|
| React to ticket changes | `ITicketObserver` (`CreatedAsync`, `UpdatedAsync`, `RepliedAsync`, `MergedAsync`, `DeletedAsync`, `AssignedAsync`, `StatusChangedAsync`, `ServiceLevelAsync`, `RatedAsync`, `ApprovalRequestedAsync`), `services.AddTicketObserver<T>()` |
| Named actions on the ticket page | `ITicketActionSource` (one source; Workflows registers `WorkflowTicketActionSource` with `TryAddScoped`) |
| Reply templates in the composer | `ITicketReplyTemplateSource` (one source; Messaging registers one for the `Tickets` scope) |
| Add a panel or field to the ticket page | `ContentPartDisplayDriver<TicketPart>` or a driver of the ticket type |
| Add desk or board settings | `DisplayDriver<TicketDesk>` / `DisplayDriver<TicketBoard>`, values in `desk.Put(...)` |
| Read or change tickets | `ITicketService`, `ITicketAccessService`, `ITicketBoardService`, `ITicketCategoryService` |
| Requester's manager for approvals | `ITicketRequesterManagerSource` (Staff registers one) |

```csharp
public sealed class VipTicketObserver : ITicketObserver
{
    private readonly ITicketService _tickets;

    public VipTicketObserver(ITicketService tickets)
    {
        _tickets = tickets;
    }

    public async Task CreatedAsync(ContentItem ticket, CancellationToken cancellationToken = default)
    {
        var part = ticket.As<TicketPart>();

        if (part.AccountId == "4vipaccount000000000000000" && part.EscalationLevel == 0)
        {
            await _tickets.UpdateAsync(ticket.ContentItemId, new TicketUpdate
            {
                RaiseEscalation = true,
                Reason = "VIP account.",
            }, TicketActor.Site, cancellationToken);
        }
    }
}

// Startup: services.AddTicketObserver<VipTicketObserver>();
```

Observers run after the change commits and the ticket's lock is released. More samples (creating tickets from code, an action source, a ticket panel) are in [references/integrations.md](references/integrations.md).

## Troubleshooting

| Symptom | Check |
|---|---|
| No **Service Desk** menu | The user has no view scope on any desk and no `ManageTicketSettings`. Desk permissions appear per desk in the role editor. |
| Agent can't see a ticket | Their scope on the ticket's desk: Assigned sees only their tickets, My teams their teams' tickets. |
| New tickets stay unassigned | Desk strategy empty, no `DefaultTeamId`/team, nobody available by the desk's availability sources, or no member with the type's required skills; the timeline says why. |
| Recipe record refused with its id | `ItemId` isn't a valid 26-character id, or a board breaks column rules (no column, a status in two columns, unknown status, default outside its column). |
| Requesters get no e-mails | Desk `NotifyRequester` off, no **Base URL** for links, e-mail not configured; check **Send ticket e-mails** and the "not delivered" notice on the reply. |
| Status change refused | The ticket waits for approval; only approvers decide or `CancelTicketApprovals` calls it off. |
| Portal shows no ticket types | The type's Ticket part needs **Customers can submit it** and a desk with `Audience` `Customers` that takes the type. |
| Web form posts do nothing | Widget not published, the trap field was filled, rate limit hit, or ReCaptcha failed. |
| No files on replies | `CrestApps.OrchardCore.Attachments` is off. |
| Ticket not offered in Contact Center | Desk has no queue, the ticket already has an assignee or waits for approval, or Tickets for Contact Center is off. |

Related skills: `orchardcore-knowledge-base`, `orchardcore-service-levels`, `orchardcore-business-hours`, `orchardcore-timeline`, `orchardcore-attachments`, `orchardcore-agreements`, `orchardcore-teams`, `orchardcore-accounts`, `orchardcore-contact-center`, `orchardcore-work-orders`, `orchardcore-time-tracker`, `orchardcore-staff`, `orchardcore-ai-tools`, `orchardcore-workflows`, `orchardcore-webhooks`, `orchardcore-notifications`.
