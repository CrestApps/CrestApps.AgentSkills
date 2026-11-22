# Tickets Integrations and Extension Samples

## Integrations

Each integration turns on when the other feature is enabled (`[RequireFeatures]`), with no feature of its own.

| With | Adds |
|---|---|
| Accounts (`CrestApps.OrchardCore.Accounts`) | `TicketAccountPart.ServiceLevelPolicyId` (**Ticket service level**) on the `Account` type; a **Tickets** tab (latest 25 in scope, **New ticket**, **View all in the ticket list** with `?accountId=`); a **Service level** card on the **Service** tab (`AccountServiceLevelDisplayDriver`). With **Account E-mail Domains**, a new ticket without an account is filed under the account of its requester's verified e-mail domain. |
| Agreements (`CrestApps.OrchardCore.Agreements`) | `TicketAgreementTerms { DeskIds, TicketTypes, ServiceLevelPolicyId }` (**Tickets** panel, `TicketAgreementTermsValidator`). A new ticket with an account takes the earliest agreement in force that covers its desk and type (`ITicketAgreementResolver`) unless the request names one (`TicketCreateRequest.AgreementId`); `TicketPart.AgreementId`; agents change it under **Planning** (change key `Agreement`). |
| Agreements + Time Tracker | `TicketAgreementUsageProvider` counts time in a desk's ticket stage (not its not-billed stage, not non-billable tickets) against the hours; each entry is charged to the ticket's agreement when logged (`TicketTimeChargeResolver`, `TimeEntry.ChargedTo`). |
| Time Tracker | Desk **Time on tickets** (`TicketDeskTimeSettings.StageId`, `NotBilledStageId`); ticket **Time** panel: **Start working on this ticket** (`ITimeTrackerService.SwitchAsync`, `Source` `Ticket`, `SourceRef` `Ticket:{id}`), **Stop**, **Log time** (`30`, `45m`, `1h 30m`, `1:30`, `1.5h`, up to 24 h, inserted into the agent's day); reports **Ticket Time** and **Ticket Resolution and Work Time**. |
| Work Orders | **Work order** panel (needs Edit on the ticket and `ManageWorkOrders`): creates a work order from the ticket (`TicketWorkOrders`, `Source` `Ticket`, linked **Related**); `TicketWorkOrderObserver` notes completion or cancellation on the ticket; task **Create Work Order From Ticket**. |
| Staff | Requester's manager for approval steps (`ITicketRequesterManagerSource`); internal desks on the portal for staff; a staff member who leaves hands back their open tickets. |
| Skills | `TicketPartSettings.RequiredSkills`: assignment only picks members holding them. |
| Workforce / Calendars / Contact Center | Availability sources `WorkforceShifts`, `CalendarAbsences`, `ContactCenterPresence` for assignment. |
| Omnichannel messaging workspace | **Open a ticket** from a conversation; `Tickets`-scope reply templates. |
| Knowledge Base | Article suggestions in the portal's new request form and the agent's reply box (`help/api/suggest`). |
| SignalR | Live boards through `TicketBoardHub` (`TicketBoardNotifier`); otherwise boards reload every 20 seconds. |
| Notifications | Agent and requester notification types (always available: Tickets depends on Email Notifications). |

## Approvals (`CrestApps.OrchardCore.Tickets.Approvals`)

- Configured per ticket type in `TicketApprovalSettings` (up to three steps, each `Name`, `ApproverIds`, `RequesterManager`; `WaitingStatusId`).
- On create, `TicketApprovalStarter` resolves approvers (a step with none is skipped and named on the timeline) and `ITicketService.StartApprovalAsync` holds the ticket in the waiting status; approvers get `TicketApprovalRequested` with a link to `Admin/ticket/{id}/approval`, which they can open even without a desk permission.
- One approver decides a step (`DecideApprovalAsync`); after the last step the ticket returns to its previous status (or the first open status). A refusal needs a reason, solves the ticket and replies to the requester.
- While waiting, nothing else changes the status (panel, bulk, API, workflows, replies) and a requester's reply doesn't reopen it. `CancelTicketApprovals` (`CancelApprovalAsync` with a reason) sends it back to work.

## Protected Information

- `ITicketProtectedInformation` keeps label and secret items as separate `TicketProtectedItemDocument`s in the `ServiceDesk` collection, encrypted with Data Protection (purpose `CrestApps.OrchardCore.Tickets.ProtectedInformation`). They never reach the ticket index, list, search, reports, data sets, exports, API, webhooks, workflow input, e-mails, notifications or AI.
- Labels up to 100 characters, values up to 2,000, 20 per ticket. Adding needs the desk's Note permission; showing needs `View protected information in {desk}` and the agent's current password (`POST Admin/ticket/{id}/protected/show`, a 5-minute window in `IDistributedCache`; wrong passwords count toward lockout; users without a local password are refused).
- Requesters see only items they shared, on the portal (`protectedLabel`, `protectedValue` on the forms).
- Merges move them; deleting the ticket or erasing the requester deletes them; `TicketDesk.DeleteProtectedInformationDays` deletes them that many days after closing (run by the auto-close task).

## Problems and Incidents, Merge and Links

- `LinkAsync(ticketId, record, kind)` links to a ticket or any `RecordRef`; kinds (`TicketLinkKinds`): `Related`, `Parent`, `Child`, `Duplicate`, `FollowUpOf`, `Problem`. Ticket-to-ticket links are indexed both ways (`TicketLinkIndex`).
- Solving a problem solves its open incidents (`TicketProblemObserver`, first 25 in the request, the rest by **Solve incidents of solved problems**, up to 200 a run).
- `MergeAsync(ticketId, intoTicketId)` moves the conversation into the target; the merged ticket keeps `MergedIntoId`.

## Personal Data

Tickets takes part in personal data requests: tickets are found by user and by the e-mail on a guest's ticket. Erasure removes the contact snapshot's e-mail, phone and address, replaces the name, rewrites the subject and timeline text (`ITimelineService.ScrubAsync`), deletes files the requester sent and their pending outbox e-mails, and invalidates links. It can't recall sent e-mails, delivered webhooks or workflow instances (delete those under **Workflows**).

## Data Sets and Reports

With `OrchardCore.DataPipelines.Core` on, the **Tickets** data source has `Tickets`, `TicketEntries`, `TicketChanges`, `TicketLinks`, `TicketBacklog` and `TicketCategories`, each limited to the reader's scope. With Reports on, reports needing `ViewTicketReports`: **Ticket Volume and Backlog**, **Ticket Response and Resolution Times**, **Ticket Service Level Compliance** (with a by-account table), **Ticket Agent and Team Performance**, **Ticket Distribution**, and **Account Health** (Executive; adds agreement hours for readers with `ViewAgreements`). With Time Tracker too: **Ticket Time** and **Ticket Resolution and Work Time**.

## Storage

Tickets are content items (`TicketIndex`, `TicketLinkIndex` in the default collection). Desks, statuses, priorities, views, boards, protected items, the requester outbox, backlog snapshots and time-elapsed markers live in the `ServiceDesk` collection; pass the collection when querying them. Timeline entries, timers and attachments stay in the `Timeline`, `ServiceLevels` and `Attachments` collections.

## Extension Samples

### Create a Ticket From Code

```csharp
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;

public sealed class MonitoringAlertTickets
{
    private readonly ITicketService _tickets;

    public MonitoringAlertTickets(ITicketService tickets)
    {
        _tickets = tickets;
    }

    public async Task<string> OpenAsync(string alertName, string details, string deskId)
    {
        var result = await _tickets.CreateAsync(new TicketCreateRequest
        {
            ContentType = "Ticket",
            Subject = $"Alert: {alertName}",
            Body = details,
            DeskId = deskId,
            Impact = TicketLevel.High,
            Urgency = TicketLevel.High,
            Channel = TicketsConstants.Channels.Api,
            RequesterContact = new ContactInfo { Name = "Monitoring", Email = "monitoring@example.com" },
        });

        return result.Succeeded
            ? result.Ticket.ContentItemId
            : throw new InvalidOperationException(string.Join(" ", result.Errors));
    }
}
```

`ContactInfo` (`CrestApps.OrchardCore.Addresses.Models`: `Name`, `Email`, `Phone`, `Address`) is the snapshot of a requester no party source knows. Set `Requester = new PartyRef(PartyKinds.User, userId)` for a known user instead. `IsImport = true` brings past tickets in without telling observers.

### Reply and Update

```csharp
await _tickets.ReplyAsync(ticketId, new TicketReply
{
    Body = "We replaced the toner. Please try again.",
    IsInternal = false,
    Author = new PartyRef(PartyKinds.User, agentUserId),
    SetStatusId = solvedStatusId,
});

await _tickets.UpdateAsync(ticketId, new TicketUpdate
{
    PriorityId = urgentPriorityId,
    Reason = "Customer is down.",
}, TicketActor.User(agentUserId));
```

### A Named Action Source

Only one `ITicketActionSource` is used (resolved with `GetService`). With Workflows on, `WorkflowTicketActionSource` (registered with `TryAddScoped`) offers each published workflow that starts with **Ticket Action Requested**; registering your own with `AddScoped` takes its place.

```csharp
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using OrchardCore.ContentManagement;

public sealed class TierTwoActionSource : ITicketActionSource
{
    private const string EscalateToTierTwo = "Escalate to tier 2";

    private readonly ITicketService _tickets;

    public TierTwoActionSource(ITicketService tickets)
    {
        _tickets = tickets;
    }

    public Task<IReadOnlyList<string>> ListAsync(ContentItem ticket, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([EscalateToTierTwo]);

    public async Task<bool> RequestAsync(ContentItem ticket, string actionName, CancellationToken cancellationToken = default)
    {
        if (actionName != EscalateToTierTwo)
        {
            return false;
        }

        var result = await _tickets.UpdateAsync(ticket.ContentItemId, new TicketUpdate
        {
            TeamId = "4team0tier2000000000000000a",
            RaiseEscalation = true,
            Reason = "Escalated to tier 2.",
        }, cancellationToken: cancellationToken);

        return result.Succeeded;
    }
}

// Startup: services.AddScoped<ITicketActionSource, TierTwoActionSource>();
```

### A Panel on the Ticket Page

The ticket page renders `TicketPart` in the `DetailAdmin` display type (`TicketsConstants.DisplayTypes.DetailAdmin`); built-in panels sit in `Sidebar` (Work order `Sidebar:5`, Time `Sidebar:6`, AI assistant `Sidebar:7`).

```csharp
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

public sealed class TicketWarrantyDisplayDriver : ContentPartDisplayDriver<TicketPart>
{
    private readonly ITicketAccessService _access;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TicketWarrantyDisplayDriver(ITicketAccessService access, IHttpContextAccessor httpContextAccessor)
    {
        _access = access;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(TicketPart part, BuildPartDisplayContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (context.DisplayType != TicketsConstants.DisplayTypes.DetailAdmin
            || httpContext is null
            || !await _access.CanAsync(httpContext.User, part.ContentItem, TicketOperation.ViewAll, httpContext.RequestAborted))
        {
            return null;
        }

        return Initialize<TicketWarrantyViewModel>("TicketPart_Warranty", model =>
        {
            model.AccountId = part.AccountId;
        }).Location(TicketsConstants.DisplayTypes.DetailAdmin, "Sidebar:8");
    }
}

public class TicketWarrantyViewModel
{
    public string AccountId { get; set; }
}

// Startup: services.AddContentPart<TicketPart>().UseDisplayDriver<TicketWarrantyDisplayDriver>();
```

`TicketOperation` is `ViewAll`, `ViewTeam`, `ViewAssigned`, `Edit`, `Reply`, `Note`, `Assign`, `Merge`, `Delete`, `ViewProtected`. In `CanAsync`, any of the three view operations answers whether the ticket is in the user's scope; the others also check that desk permission.
