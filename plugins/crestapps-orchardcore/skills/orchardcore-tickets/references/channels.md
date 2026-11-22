# Tickets Channels: Portal, Web Form, API, Contact Center, Messaging

A ticket's `TicketPart.Channel` says where it came in: `Agent`, `Portal`, `Api`, `WebForm` (`TicketsConstants.Channels`).

## Tickets Portal (`CrestApps.OrchardCore.Tickets.Portal`)

Adds **Requests** to My account (`CrestApps.OrchardCore.Users.Account`). Desk permissions play no part: a person sees what they asked for; an account administrator (account member with the Admin role) also sees their account's requests.

| Route | Name | Does |
|---|---|---|
| `GET account/requests` | `TicketPortal` | Own requests newest first with the status's `CustomerLabel`; account tab for account admins |
| `GET/POST account/requests/new` | `TicketPortalCreate`, `TicketPortalCreatePost` | Ticket types with **Customers can submit it** on desks with `Audience` `Customers` (internal desks too for staff with Staff on); the type's title plus fields marked **Customers fill this in**, description, files, optional protected item |
| `GET account/requests/{number}` | `TicketPortalDisplay` | Public replies oldest first (latest 200), files by links signed for an hour, reply box |
| `POST account/requests/{number}/reply`, `/rate` | | Reply (reopens a solved request), rate 1 to 5 |
| `GET requests/{id}?token=` | guest route | Signed link from e-mails (90 days), traded for an HttpOnly cookie scoped to that request (12 hours) |
| `GET/POST requests/{id}/rate` | rate route | Rating link (30 days) |
| `account/requests/{number}/protected/show`, `hide`, `remove`; `requests/{id}/protected/remove` | | Protected items the requester shared |

- Links are bound to the ticket, its requester and `TicketPart.AccessVersion`: a new requester, a merge or an erasure kills every earlier link.
- Pages send `Cache-Control: no-store`, `Referrer-Policy: no-referrer`, `X-Robots-Tag: noindex`, and write outcomes on the page itself.
- Rate limits (Rate Limits feature, always on with the portal): 10 new requests and 30 replies or ratings per signed-in requester in 10 minutes, 10 guest posts per address (`tickets-portal-create`, `tickets-portal-reply`, `tickets-portal-guest`); 20 replies per request an hour.
- Ratings: `ITicketService.RateAsync` keeps one rating (`TicketPart.Satisfaction`, `SatisfactionScore` in the index, a timeline entry) and raises **Ticket Rated**. The desk's `RatingRequestDelayHours` drives the **Ask requesters to rate solved tickets** task (tickets solved no more than a week before).
- Requester e-mail links use the site's **Base URL** only; without it they are left out and a warning is logged. `ITicketRequesterLinks` builds them in code.
- With Knowledge Base on, the **New request** form suggests articles while the requester types.

## Ticket Web Form (`CrestApps.OrchardCore.Tickets.WebForm`)

`TicketFormWidget` with `TicketFormPart` (`TicketType`, `DeskId`, optional success message) opens a ticket from any page. It asks for the request, a category (customers' only), the type's customer questions with show-when rules, a subject unless the type has its own title, and a name and e-mail for guests. It posts to `tickets/forms/{widgetId}` with the `WebForm` channel.

- Robots: a hidden trap field (silently ignored), Rate Limits group `tickets-web-form` (10 per visitor in 10 minutes; set up Reverse Proxy behind a proxy), at most 5 per address per node (`TicketFormGuard`), and with `OrchardCore.ReCaptcha` on and keys set, a challenge for guests (`ITicketFormChallenge`). The widget editor warns when no challenge is set up.
- Limits: post 64 KB, guest name 200 characters, subject cut at 200, request at 10,000. Posts only while the widget is published and visible to the visitor.
- A guest's ticket is `RequesterUnverified`: never filed under an account by e-mail domain, and e-mails to that address carry fixed text and the number only.
- The widget's desk is a suggestion (`DeskIsSuggestion`): the category's desk wins when it serves the same audience and takes the type.

## Tickets API (`CrestApps.OrchardCore.Tickets.Api`)

`api/tickets/v1` on Orchard Core's `Api` authentication scheme (bearer token). Each call applies the caller's desk permissions and scope; an out-of-scope ticket is `404`. Writes honor an `Idempotency-Key` header. Enable OpenAPI to document it.

| Endpoint | Does | Needs |
|---|---|---|
| `GET tickets` | `q`, `deskId`, `status` (status categories, comma separated), `assignee` (`me`, `none` or a user id), `requester`, `accountId`, `categoryId`, `skip`, `take` (max 100); newest change first | A view scope |
| `GET tickets/{id}` | By id or number | View |
| `GET tickets/{id}/entries` | Replies, notes and events newest first, file links valid an hour | View |
| `POST tickets` | `type`, `subject`, `body`, `deskId`, `priorityId`, `categoryId`, `requester` (`Kind:Id`) or `requesterName` + `requesterEmail`, `accountId`, `attachments` | Reply on the desk |
| `PATCH tickets/{id}` | `subject`, `statusId`, `priorityId`, `deskId`, `escalationLevel`, `categoryId` (edit); `teamId`, `assigneeId` (assign) | Edit or assign |
| `POST tickets/{id}/replies` | Public reply: `body`, `setStatusId`, `attachments` | Reply |
| `POST tickets/{id}/notes` | Internal note: `body`, `attachments` | Note |

```json
{
  "type": "Ticket",
  "subject": "Printer on floor 3 is jammed",
  "body": "It shows error E-102.",
  "deskId": "49vyvps9npqdzz9mz7xfwqgdzx",
  "requesterName": "Sam Lee",
  "requesterEmail": "sam@example.com",
  "attachments": [
    { "fileName": "error.png", "contentType": "image/png", "content": "iVBORw0KGgo..." }
  ]
}
```

Files travel as base64 under the Attachments policy (the Attachments feature must be on). This is the agents' API: `entries` returns internal notes to anyone who may view the ticket. Never expose it to requesters.

## Tickets for Contact Center (`CrestApps.OrchardCore.Tickets.ContactCenter`)

A separate module; depends on Tickets and Contact Center Work Distribution (`CrestApps.OrchardCore.ContactCenter.Queues`).

- A desk's editor gets **Contact Center queue** (`TicketDeskQueueDisplayDriver`), stored as `TicketDeskQueueSettings { QueueId }` in the desk's `Properties`.
- `TicketQueueRouter` (an `ITicketObserver`): a new ticket on that desk with no assignee and not waiting for approval becomes an `ActivityKind.Task` activity (source `Api`, number and subject as instructions, `TicketActivityLink { TicketId }`) enqueued with the ticket's priority through `IActivityQueueService.EnqueueAsync`. The ticket links to it (`OmnichannelActivity:{id}`, record kind `TicketsContactCenterConstants.ActivityRecordKind`).
- `TicketOfferAcceptedHandler` (an `IContactCenterEventHandler`, `HandlerId` `Tickets/ContactCenterOfferAccepted/v1`, naturally idempotent) gives the ticket to the agent who accepts the offer (`QueueItemAssigned`), unless someone already has it.
- Assigning by hand, solving, closing, merging or deleting before an agent takes it withdraws the queued work (`IQueuedWorkWithdrawalService`); work an agent already took stays theirs.
- Agents accept offers in the agent desktop, which needs the Contact Center Voice and Real-Time features. Queue presence, capacity and skills routing apply; to honor Contact Center presence in the desk's own assignment instead, add `ContactCenterPresence` to `AvailabilitySources`.

```json
{
  "steps": [
    {
      "name": "TicketDesks",
      "Desks": [
        {
          "ItemId": "4desk0support000000000000a",
          "Name": "Support",
          "Audience": "Customers",
          "AssignmentStrategy": "",
          "Properties": {
            "TicketDeskQueueSettings": { "QueueId": "queue-support-tickets" }
          }
        }
      ]
    }
  ]
}
```

Import the Contact Center queue (`ContactCenterQueue` step) before the desk. See `orchardcore-contact-center`.

## From a Messaging Conversation

With the Omnichannel messaging workspace on, each conversation row has **Open a ticket**: **New ticket** opens with the customer as requester, a subject and the latest 30 messages as the first message (`TicketConversationSource`), and the ticket links back (`MessagingConversation:{id}`, Related). Reply templates of the `Tickets` scope are offered in the composer and can set a status, priority or team. See `orchardcore-omnichannel-messaging`.
