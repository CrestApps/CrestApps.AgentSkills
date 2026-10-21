# Agent Desktop, Real-Time Hub and Supervisor Dashboard

## Agent Experiences

| Experience | Activates when | What the agent gets |
|---|---|---|
| Soft-phone projection | Voice + Real-Time + Telephony soft phone | **Work** tab (sign in to queues and campaigns), presence dropdown, Contact Center call state and offers inside the soft phone |
| Agent Workspace (**Interaction Center → My workspace**) | Agents + Real-Time + Voice + `CrestApps.OrchardCore.Telephony.SoftPhone.Core` | Full-screen desktop: offers, active interaction, customer context, recent activity, completion handoff |
| Docked agent bar | Same as the Agent Workspace | Collapsed presence tab in the admin `Footer` zone of every admin page for users with `ContactCenterSignIntoQueues` |

Soft Phone Core is dependency-only; enable `CrestApps.OrchardCore.Telephony.SoftPhone` (floating widget) or `CrestApps.OrchardCore.Telephony.SoftPhone.Extension` (standalone `/softphone` for the browser extension and desktop app). Agents also need the Telephony soft-phone permission (see `orchardcore-telephony`).

The docked bar holds its own hub connection, so offers reach the agent even when the soft phone runs in another window or the extension. It pops the matched activity's **Complete activity** page (unless the current page has unsaved form changes) and shows **Accept**/**Decline** (or **Dial**/**Skip** for preview). It is not injected on `/softphone`, on front-end pages, or on non-view responses.

## Workspace Behavior

- The workspace loads a state snapshot (`GET Admin/contact-center/workspace/state`) and stays current from hub events, re-reading authoritative state after each action.
- Sign-in, sign-out and reconnect run a self-healing pass that reclaims half-cleared offers and stale reservations, then offer any queued voice work waiting for the agent.
- **Accept** posts `Admin/contact-center/voice/offer/accept`; **Decline** posts `.../offer/decline`; sending a ringing call to voicemail posts `.../offer/voicemail`.
- Dialer work (Preview, Power, Progressive) opens the shared Omnichannel **Complete activity** page; inbound work shows the ringing offer first.
- Paced dials keep the agent `Reserved`; the snapshot's `pendingDial` carries `DialAttemptPhase` (`Dialing`, `Screening`, `Connecting`) and `showsProgress`; `activeInteraction` appears once the agent joins.
- Completion goes through `IActivityDispositionService`; a subject flow that requires a disposition blocks completion. An already finished activity cannot be completed twice.
- Presence changes (`POST Admin/contact-center/workspace/presence`) and queued-call recovery run separately, so presence never waits on voice routing.
- Recording controls: `.../workspace/recording/pause`, `.../recording/resume`, `.../recording/disclosed`. Secure capture: `.../workspace/secure-capture/begin` and `.../cancel`.
- **My voicemail greeting** records or uploads MP3, WAV, OGG or WebM up to 20 MB and stores it in the provider's media storage. The page provisions an agent profile on first visit.

## Real-Time Hub

**Contact Center Real-Time** maps `ContactCenterHub` with `SignalRHubRoutes.GetHubPath<ContactCenterHub>()`. Agents connect with `ContactCenterSignIntoQueues` and join their user group plus one group per signed-in queue; supervisors connect with `MonitorContactCenter`, join the supervisor group, and may call `WatchQueue`/`UnwatchQueue`. Groups are qualified by the immutable shell name (`TenantSignalRGroupName`). Agent hub methods include `Heartbeat`, `GetSnapshot`, `SignIn(queueIds, campaignIds)`, `SignOut`, `UpdateMemberships`, `SetPresence(status, reason)`, `SyncQueuedVoiceWork` and `GetCurrentIncomingOffer`; most return an `AgentDesktopSnapshot`. Supervisors also get `SwitchMonitorMode` and `StopMonitoring`. `ContactCenterRealTimeEventHandler` projects the durable event log into `PresenceChanged`, `OfferReceived`, `OfferRevoked` and `QueueStatsChanged`.

```html
<script asp-name="contact-center-realtime" at="Foot"></script>
<script at="Foot" depends-on="contact-center-realtime">
    const client = window.contactCenterRealTime.connect({
        hubUrl: '@Html.SignalRHubUrl<ContactCenterHub>()',
        onSnapshot: (snapshot) => { /* restore desktop state */ },
        onPresenceChanged: (n) => { /* n.userId, n.status, n.reason */ },
        onOfferReceived: (n) => { /* show the offer until n.expiresUtc */ },
        onOfferRevoked: (n) => { /* dismiss the offer (n.reason) */ },
        onQueueStatsChanged: (n) => { /* n.queueId, n.waitingCount */ }
    });
</script>
```

Hub conventions: work that changes durable state or group membership runs under `HubConnectionWork.MustComplete`, never the connection token; a connection that cannot be fully registered is aborted so the agent stops looking available. Multi-node requires the Redis backplane (`OrchardCore.SignalR.Redis`) and `OrchardCore.Redis.Lock`; see `orchardcore-signalr`.

## Live Dashboard (Supervision)

**Interaction Center → Live dashboard** (`MonitorContactCenter`) shows summary metrics, a tile per queue (waiting, signed-in, available, busy, longest wait, SLA amber and red), an agent board, the dialer banner (`GET Admin/contact-center/dashboard/dialer`, 204 without Paced Dialing) and the **Why isn't this dialing?** card (`.../dashboard/dispatch`). It re-reads state every 10 seconds. A supervisor sees only queues and campaigns on their own agent entitlement record; without one the dashboard is empty.

| Action | Requires |
|---|---|
| **Listen** (`Monitor`), **Whisper**, **Barge**, switch mode, stop | `MonitorContactCenter`, provider capability plus `IContactCenterVoiceMonitoringProvider`; blocked while recording is secure-paused |
| **Message…** (500 characters) | `MonitorContactCenter` |
| **Take over** | `ContactCenterInterveneInCalls`, provider `IContactCenterVoiceSupervisorInterventionProvider`, supervisor has an agent profile |
| **End call…**, **Transfer…** (needs `CallTransfer`), **Start/Stop recording** (needs `Recording`) | `ContactCenterInterveneInCalls` |
| **Set Available**, **Set Not ready**, **Set Break**, **Sign out of queues** | `ContactCenterInterveneInCalls` |
| **Free agent** (stale Busy or Reserved) | `ContactCenterInterveneInCalls`; never ends a call the provider confirms alive |

Provider actions run first and the audited event is published only after the provider confirms. Audit events: `SupervisorTookOver`, `SupervisorEndedCall`, `SupervisorTransferredCall`, `SupervisorChangedRecording`, `SupervisorSetAgentState`, `SupervisorMessagedAgent`, `SupervisorMonitorModeChanged`, `SupervisorFreedAgent`. The supervisor hears the call on their own soft phone, which auto-answers the announced leg. Stale-work detection is `IStaleAgentWorkService` (registered by Supervision).

## Extending the UI

Every screen uses display management. Queue, skill, entry point and dialer profile editors are `IDisplayManager<T>` shapes with display drivers; list pages use Orchard Core `AdminList` (place row shapes for both `SummaryAdmin` and `SummaryAdminGrid`, see `orchardcore-admin-lists`). The soft phone is extended with `DisplayDriver<SoftPhoneWidget>`. The incoming-call modal is extended with `IIncomingCallContextProvider` (Contact Center adds matched customers and accept/decline wiring). Editors that use multi-selects load Orchard Core's `bootstrap-select` resource.

```csharp
using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

public sealed class QueueCostCenterSettings
{
    public string CostCenter { get; set; }
}

public sealed class QueueCostCenterDisplayDriver : DisplayDriver<ActivityQueue>
{
    public override IDisplayResult Edit(ActivityQueue queue, BuildEditorContext context)
    {
        return Initialize<QueueCostCenterViewModel>("QueueCostCenter_Edit", model =>
        {
            if (queue.TryGet<QueueCostCenterSettings>(out var settings))
            {
                model.CostCenter = settings.CostCenter;
            }
        }).Location("Content:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(ActivityQueue queue, UpdateEditorContext context)
    {
        var model = new QueueCostCenterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        queue.Put(new QueueCostCenterSettings
        {
            CostCenter = model.CostCenter?.Trim(),
        });

        return Edit(queue, context);
    }
}

public class QueueCostCenterViewModel
{
    public string CostCenter { get; set; }
}
```

Register with `services.AddDisplayDriver<ActivityQueue, QueueCostCenterDisplayDriver>();` under `[RequireFeatures("CrestApps.OrchardCore.ContactCenter.Queues")]`. `Put`/`TryGet` (from `CrestApps.Core`) store the settings in the catalog entry's properties, the same pattern the shipped `OutboundLineEndpointDisplayDriver` uses, so the data travels with the queue's recipe step.
