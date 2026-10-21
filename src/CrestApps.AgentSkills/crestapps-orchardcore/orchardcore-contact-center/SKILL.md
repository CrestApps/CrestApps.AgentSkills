---
name: orchardcore-contact-center
description: Skill for enabling, configuring, extending and operating the CrestApps Orchard Core Contact Center module that orchestrates work between the Omnichannel CRM and Telephony. Covers the CrestApps.OrchardCore.ContactCenter features (Agents, AgentEntitlements, BusinessHours, Queues, Dialer, Dialer.Paced, EntryPoints, InboundVoice, Voice, Voice.Media, Recording, SecureCapture, Supervision, RealTime), presence, queues, reservations and routing, Preview, Power, Progressive and Predictive dialing with compliance screening, entry points and IVR, voicemail, recording governance, call topology, the Agent Workspace and Live dashboard, recipe steps, configuration sections, workflows, reports, health checks and runbooks. Use it for Contact Center, call center, ACD, dialer, IVR or supervisor monitoring work. Strong matches include IContactCenterEventHandler, IActivityRoutingStrategy, IContactCenterWorkStateService, IContactCenterVoiceProvider, ISecureCaptureTokenSink, ContactCenterHub, and ContactCenterPermissions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Contact Center - Prompt Templates

## Build a Contact Center on Orchard Core

You are an Orchard Core expert. Generate accurate feature selections, recipes, configuration, extension code, and operational guidance for the CrestApps **Contact Center** module set. It turns the Omnichannel CRM into a contact center: the CRM owns the work (contacts, activities, subjects, dispositions), Contact Center owns orchestration (queues, routing, reservations, presence, dialing, interactions, events), and Telephony owns media execution.

### Guidelines

- Install `CrestApps.OrchardCore.ContactCenter` (and the provider package, for example Telnyx or Asterisk) in the web/startup project.
- Enable the capability you need and let Orchard resolve dependencies. There is no separate administration feature; each capability ships its own screens.
- `CrestApps.OrchardCore.ContactCenter.Voice`, `.RealTime`, `.ProviderInbox`, `.Recording.Core` and `.AgentServices` are dependency-only. Never list them as the thing to enable; enable Inbound Voice, the Dialer, Recording or Supervision instead.
- The Agent Workspace, the docked agent bar, the soft-phone projection and each provider's Contact Center voice adapter are integration glue with no feature toggle. They activate when the features they compose are all enabled.
- The CRM `OmnichannelActivity` is the unit of work; an `Interaction` is the history of one attempt and never owns a disposition. Complete work through the Omnichannel `IActivityDispositionService`, never by writing activity status directly.
- Decide routing from `IContactCenterWorkStateService`, not from the activity's projected `AssignedToId` or `ReservationId` columns.
- Treat the telephony provider as the source of truth for live call state. UI clicks are intents; provider events and reconciliation finish every transition.
- Contact Center tuning lives in `appsettings.json` under `CrestApps:ContactCenter:*` (wrapped in `OrchardCore` in the host file) and binds to `IOptions<T>` at tenant start, so a change needs a tenant restart. Invalid values stop the tenant.
- Production needs PostgreSQL, `OrchardCore.Redis.Lock`, and the Redis SignalR backplane. See [references/production-support.md](references/production-support.md).
- Contact Center is not an emergency-calling service. Emergency short codes are always refused by `IDialDestinationPolicy`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

## Feature Overview

| Feature | Feature ID | Depends on | Notes |
|---|---|---|---|
| Contact Center | `CrestApps.OrchardCore.ContactCenter` | `CrestApps.OrchardCore.Omnichannel.Activities` | Interaction lifecycle, durable event log and outbox, baseline permissions, Voice Media library, dialer decision log, admin menu |
| Contact Center Agent Services | `CrestApps.OrchardCore.ContactCenter.AgentServices` | none | Dependency-only agent profile directory (also used by the messaging workspace) |
| Contact Center Agents | `CrestApps.OrchardCore.ContactCenter.Agents` | Contact Center, Agent Services, `CrestApps.OrchardCore.Users` | Agent profiles, presence, reason codes, sessions, heartbeat, after-call recovery |
| Contact Center Agent Entitlements | `CrestApps.OrchardCore.ContactCenter.AgentEntitlements` | Agents | Restricts which queues and campaigns an agent may join. Disabled means any agent may join any queue |
| Contact Center Business Hours | `CrestApps.OrchardCore.ContactCenter.BusinessHours` | none | Calendars and the evaluation service (also gates automated Omnichannel sends) |
| Contact Center Work Distribution | `CrestApps.OrchardCore.ContactCenter.Queues` | Agents, Business Hours | Queues, queue groups, skills, reservations, routing strategies |
| Contact Center Outbound Dialer | `CrestApps.OrchardCore.ContactCenter.Dialer` | Voice, Queues | Dialer profiles, callbacks, Preview batches, mandatory compliance gate, manual soft-phone screening |
| Contact Center Paced Dialing | `CrestApps.OrchardCore.ContactCenter.Dialer.Paced` | Dialer | Power, Progressive and Predictive pacing, pacing cycle records, dialer health |
| Contact Center Provider Webhook Inbox | `CrestApps.OrchardCore.ContactCenter.ProviderInbox` | Contact Center | Dependency-only durable at-least-once webhook inbox |
| Contact Center Voice | `CrestApps.OrchardCore.ContactCenter.Voice` | Queues, Real-Time, Recording Core, Provider Inbox, `CrestApps.OrchardCore.Omnichannel.ChannelEndpoints`, `CrestApps.OrchardCore.Telephony` | Dependency-only Voice Contact Center Call Router, outbound lines, external transfer destinations |
| Contact Center Inbound Entry Points | `CrestApps.OrchardCore.ContactCenter.EntryPoints` | Queues, Channel Endpoints | Entry points per channel (no voice dependency), IVR menu editor |
| Contact Center Inbound Voice | `CrestApps.OrchardCore.ContactCenter.InboundVoice` | Voice, Queues, Entry Points | Caller qualification, welcome and closed messages, IVR, voicemail, shared voicemail |
| Contact Center Voice Media | `CrestApps.OrchardCore.ContactCenter.Voice.Media` | Voice | Opt-in bidirectional media resolution; required for realtime speech-to-speech AI calls |
| Contact Center Recording Governance | `CrestApps.OrchardCore.ContactCenter.Recording.Core` | Contact Center | Dependency-only recording access governance and audit (voicemail playback) |
| Contact Center Call Recording | `CrestApps.OrchardCore.ContactCenter.Recording` | Voice, Recording Core | Recording orchestration, disclosure, secure pause, Call recordings page |
| Contact Center Secure Data Capture | `CrestApps.OrchardCore.ContactCenter.SecureCapture` | Recording, `CrestApps.OrchardCore.Telephony.SoftPhone.Core` | Hosted tokenized capture of card or identity data |
| Contact Center Supervision & Live Dashboard | `CrestApps.OrchardCore.ContactCenter.Supervision` | Real-Time, Voice | Live dashboard, monitor, whisper, barge, take over, Free agent |
| Contact Center Real-Time | `CrestApps.OrchardCore.ContactCenter.RealTime` | Queues, `OrchardCore.SignalR` | Dependency-only `ContactCenterHub` and real-time projections |

Activated automatically (no feature ID):

| Capability | Active when |
|---|---|
| Agent Workspace (**My workspace**) and docked agent bar | Agents + Real-Time + Voice + `CrestApps.OrchardCore.Telephony.SoftPhone.Core` |
| Contact Center soft-phone projection (**Work** tab, presence menu) | Voice + Real-Time + Telephony soft phone |
| Provider voice adapter (Telnyx, Asterisk) | Provider module + Contact Center Voice |
| Provider media adapter | Provider module + Voice Media |
| Contact Center reports | Queues + `CrestApps.OrchardCore.Reports` (Dialer activity and health also needs Paced Dialing) |
| Workflow activities | `OrchardCore.Workflows` + the capability that owns each task |
| Health checks and probes | `OrchardCore.HealthChecks` |
| Report Builder data sources | `OrchardCore.DataPipelines.Core` |

## Enable via Recipe

Minimal inbound voice contact center with the Agent Workspace and Telnyx:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContactCenter",
        "CrestApps.OrchardCore.ContactCenter.Agents",
        "CrestApps.OrchardCore.ContactCenter.Queues",
        "CrestApps.OrchardCore.ContactCenter.InboundVoice",
        "CrestApps.OrchardCore.Telephony.SoftPhone",
        "CrestApps.OrchardCore.Telnyx"
      ]
    }
  ]
}
```

Paced outbound with supervision, recording and reports:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContactCenter.Dialer.Paced",
        "CrestApps.OrchardCore.ContactCenter.Supervision",
        "CrestApps.OrchardCore.ContactCenter.Recording",
        "CrestApps.OrchardCore.Telephony.SoftPhone",
        "CrestApps.OrchardCore.Reports",
        "CrestApps.OrchardCore.Telnyx",
        "OrchardCore.Workflows",
        "OrchardCore.HealthChecks"
      ]
    }
  ]
}
```

The module also ships the harvestable recipe `contact-center-asterisk-ga-core.recipe.json` (name `ContactCenterAsteriskGaCore`, **Contact Center — Asterisk (GA-Core)**), which enables the certified `ga-core-asterisk` feature set. Feature recipes carry no queues or entry points; add those with the configuration steps in [references/configuration-and-recipes.md](references/configuration-and-recipes.md).

## Key Concepts

### Layers

```text
Omnichannel CRM   contacts, activities, campaigns, subjects, dispositions, subject actions
Contact Center    queues, reservations, routing, presence, dialer, interactions, events, metrics
Telephony         soft phone, provider resolver, call control, call state lookup
Providers         Telnyx, Asterisk, custom adapters (media, webhooks, provider APIs)
```

### Agents and presence

An `AgentProfile` links an Orchard user to capacity (`MaxConcurrentInteractions`, default 1), skills, entitled queues and campaigns, and presence. Agent-set states are `Available`, `RequestBreak`, `Break`, `Away`, `DoNotDisturb`, `Meeting`, `Training`, `AfterHoursUnavailable` and `Offline`; `Reserved`, `Busy` and `WrapUp` are system-managed. Agents sign in from the soft phone's **Work** tab. Routing only uses an agent whose live session is online with a fresh heartbeat (`HeartbeatTimeout`, 90 seconds) and spare capacity. Breaks are system-approved: granted at once when idle, otherwise held as `RequestBreak` until work ends.

### Queues, reservations and routing

A queue owns priority, SLA threshold, reservation timeout, unanswered-offer action (`Requeue`, `Voicemail`, `Reject`), required skills with proficiency, business-hours calendar, overflow chain, max wait, queue size, and caller treatment. Routing runs a chain of `IActivityRoutingStrategy` implementations: required skills, preferred skills, capacity, sticky agent, then the queue's scoring strategy (`LongestIdle` default, `RoundRobin`, `LeastBusy`). A reservation offers one activity to one agent under distributed locks plus database compare-and-set, so work is never offered twice. Every assignment publishes an auditable `RoutingDecisionMade` event.

### Work state

`ContactCenterWorkState` (one per activity) owns assignment status, reservation, assigned agent and attempt count. The activity's matching columns are a read model reconciled after commit. Mutate through `IContactCenterWorkStateService.MutateAsync`, and schedule CRM-owned changes through `IContactCenterActivityWriter.ScheduleUpdateAsync`.

### Dialer modes

A dialer profile is an execution policy (mode, provider, pacing, retries, caller ID, compliance), not a source of work. Activities are loaded with the **Dialer** source, picking both campaign and profile; the campaign acts as the routing queue.

| Mode | Feature | Behavior |
|---|---|---|
| `Preview` | Dialer | Agent reviews and accepts, then the call is placed |
| `Power` | Paced Dialing | Up to **Calls per agent** (max 3) calls per cycle, each with a reserved agent |
| `Progressive` | Paced Dialing | One call per available agent, bounded at 100 per cycle |
| `Predictive` | Paced Dialing | `ReservedPerCall` (default) throttled by abandonment rate, or `OverDial` sized from the measured answer rate |

Every attempt passes `IDialerEligibilityService` (attempts, retry delay, do-not-call, calling window, abandonment cap, national registries) and fails closed. `Manual` is not a profile mode; soft-phone dials are screened by the Dialer's `IOutboundCallScreener`.

### Voice routing and entry points

An entry point picks omnichannel addresses (`AddressIds`) and routes them to a queue, a specific agent (personal line) or an AI voice agent (`TargetType` `Queue`, `Agent`, `AIAgent`), with priority, business hours, closed action (`HoldInQueue`, `Voicemail`, `Overflow`, `Reject`), welcome and closed messages, and an optional IVR menu. Inbound calls flow provider webhook → durable inbox → `IVoiceContactCenterCallRouter` → activity and interaction → queue → reservation → offer → `POST Admin/contact-center/voice/offer/accept`.

### AI voice agent

An entry point with `TargetType` `AIAgent` and `TargetAIProfileId` answers with an automated voice conversation from `CrestApps.OrchardCore.Omnichannel.Voice` (see `orchardcore-omnichannel-voice`). With **Voice Media** enabled and a realtime-capable conversation deployment, the call runs as live speech-to-speech; otherwise it uses the turn-based loop. The **Hand Off to Live Agent** workflow task (and the model's own transfer tool) seat the caller in a queue.

### Recordings and voicemail

Recording is permitted while **Call Recording** is enabled; `ContactCenterRecordingSettings.RecordAllCalls` (default on) records every routed call. `IRecordingGovernancePolicy` fails closed on consent, the recording disclosure captures consent, agents can secure-pause, and erasure honors legal hold. Media lives in the Telephony `IRecordingMediaStore`, never in Contact Center. Voicemail is greeting, then beep, then record, delivered to an agent inbox or to the queue's shared voicemail box.

### Live call topology

Each `CallSession` carries legs, bridges with append-only membership, consults, monitor sessions and typed relationships. `CallTopologyProjector` is the only writer; `CallSession.ParticipantsAt(utc)` answers who was on the call at any instant.

### Domain events

Every change is an immutable `InteractionEvent` published through `IContactCenterEventPublisher` and dispatched at least once by the outbox to `IContactCenterEventHandler` implementations, each with a stable `HandlerId` and a declared `ContactCenterHandlerReplaySafety`. Failed handlers retry with back-off and dead-letter after ten attempts.

## Admin Locations

| Menu | Feature |
|---|---|
| **Interaction Center → Management → Agent states** | Agents |
| **Interaction Center → Management → Agent entitlements** | Agent Entitlements |
| **Interaction Center → Management → Business hours** | Business Hours |
| **Interaction Center → Management → Queues**, **Queue groups**, **Skills** | Queues |
| **Interaction Center → Management → Dialer Profiles** | Dialer |
| **Interaction Center → Management → Inbound entry points** | Entry Points |
| **Interaction Center → Management → Voice Media** | Contact Center |
| **Interaction Center → My workspace**, **My voicemail greeting** | Agent Workspace glue |
| **Interaction Center → Live dashboard** | Supervision |
| **Interaction Center → Shared voicemail** | Inbound Voice |
| **Interaction Center → Call recordings** | Call Recording |
| **Settings → Contact Center** (group `contactcenter`) | Tabs from Voice, Recording, Secure Capture and Channel Endpoints |
| **Reports** (Executive, Operations, Queue & Routing, Agent Performance, Workforce & Payroll, CRM & Campaigns, Compliance & Audit) | Queues + Reports |

Campaigns, channel endpoints, subject flows and dispositions are Omnichannel screens (see `orchardcore-omnichannel-management`).

## Permissions and Roles

| Permission | Default stereotype | Grants |
|---|---|---|
| `ManageContactCenter` | Administrator | Implies every manage permission below |
| `ManageInteractions`, `ViewInteractions` | Agent and Supervisor get `ViewInteractions` | Interaction access, generic voice ingress, recording erasure |
| `ManageContactCenterAgents`, `ManageContactCenterQueues`, `ManageContactCenterQueueGroups`, `ManageContactCenterSkills`, `ManageContactCenterBusinessHours`, `ManageContactCenterDialer`, `ManageContactCenterVoiceMedia` | Administrator | Configuration screens |
| `ContactCenterSignIntoQueues` | Agent | Sign in, presence, My workspace, My voicemail greeting |
| `ContactCenterSecurePauseRecording`, `ContactCenterInitiateSecureCapture` | Agent | Pause recording or start a secure capture on own live interaction |
| `MonitorContactCenter` | Supervisor | Live dashboard, listen, whisper, barge, message, dependency probe |
| `ContactCenterInterveneInCalls` | Supervisor | Take over, end, transfer, record, set agent state, Free agent |
| `ContactCenterTransferExternally` | Supervisor | Transfer to external destinations |
| `ViewContactCenterReports` | Supervisor | Contact Center reports |
| `AccessContactCenterSharedVoicemail`, `ManageContactCenterSharedVoicemail` | Supervisor | Work and manage shared queue voicemail |
| `ListenToOwnCallRecordings` / `ListenToAllCallRecordings`, `DeleteCallRecordings` | Agent / Supervisor, Administrator | Call recordings page (Call Recording feature) |

Supervisors see only the queues and campaigns on their own agent entitlement record; `ManageContactCenter` sees all. Permissions are declared in `ContactCenterPermissions` (`CrestApps.OrchardCore.ContactCenter.Core`).

## Key Extension Points

| Contract | Use it to |
|---|---|
| `IContactCenterEventHandler` | React to domain events (projections, integrations). Declare `HandlerId` and `ReplaySafety` |
| `IActivityRoutingStrategy` | Add a filter or score to candidate agents (`Order`, `ApplyAsync(ActivityRoutingContext)`) |
| `IEntryPointResolver` | Contribute an entry-point source; resolvers are asked in order and the first plan wins |
| `IDialerStrategy` | Implement a dialing mode |
| `INationalDoNotCallRegistry` | Add a national DNC registry (see `orchardcore-dnc-registry`) |
| `IContactCenterVoiceProvider` plus `IContactCenterVoiceCallControlProvider`, `...TransferProvider`, `...RecordingProvider`, `...MonitoringProvider`, `IContactCenterVoiceMediaProvider` | Write a provider adapter. Reference only `CrestApps.OrchardCore.ContactCenter.Abstractions` |
| `IProviderWebhookInboxHandler` | Process durable provider webhook deliveries |
| `ISecureCaptureTokenSink` | Forward captured values to a PCI-DSS vault (required outside Development) |
| `IRecordingDisclosureProvider` | Supply the disclosure text per call type |
| `IVoiceMediaProvisioningService` | Resolve playable media for a provider (`ResolveAsync`) |
| `IContactCenterFeatureWorkManager` | Gate background work during feature quiesce and drain |

A minimal event handler:

```csharp
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Logging;

public sealed class CallEndedAuditHandler : IContactCenterEventHandler
{
    private readonly ILogger _logger;

    public CallEndedAuditHandler(ILogger<CallEndedAuditHandler> logger)
    {
        _logger = logger;
    }

    // Stable forever: the outbox checkpoints completed handlers by this id.
    public string HandlerId => "MyCompany/CallEndedAudit/v1";

    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

    public Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        if (interactionEvent.EventType != ContactCenterConstants.Events.CallEnded)
        {
            return Task.CompletedTask;
        }

        var data = interactionEvent.GetData<CallLifecycleEventData>();

        _logger.LogInformation(
            "Call {InteractionId} ended with cause {HangupCause}.",
            interactionEvent.InteractionId,
            data?.HangupCause);

        return Task.CompletedTask;
    }
}
```

Register it with `services.AddScoped<IContactCenterEventHandler, CallEndedAuditHandler>();` in a startup marked `[RequireFeatures("CrestApps.OrchardCore.ContactCenter")]`. Registration rejects a blank or duplicate `HandlerId` and `ReplaySafety` of `Unspecified`. More samples are in [references/routing-and-dialer.md](references/routing-and-dialer.md) and [references/workflows.md](references/workflows.md).

## Upgrading from 2.x

Contact Center is new in 3.0. Code from 3.0 previews or neighboring modules must apply these changes:

- SignalR moved to Orchard Core. Real-Time depends on `OrchardCore.SignalR`; map hubs with `SignalRHubRoutes.GetHubPath<T>()` and build client URLs with `Html.SignalRHubUrl<T>()` (`HubRouteManager` is gone). The topology check still looks for `CrestApps.OrchardCore.SignalR.Redis`. See `orchardcore-signalr`.
- Dialpad was removed; use Telnyx (`orchardcore-telephony-telnyx`) or Asterisk (`orchardcore-telephony-asterisk`). Remove `CrestApps.OrchardCore.Telephony.Admin` from recipes.
- Play voice media through `IVoiceMediaProvisioningService.ResolveAsync(...)` instead of reading `VoiceMediaItem.MediaReference`. `VoiceMediaItem` gains `MasterStorageReference`, `ContentType` and `ProviderReferences`.
- Admin lists render through Orchard Core `AdminList`. A driver that adds a shape to a Contact Center row must also place it for `SummaryAdminGrid` (see `orchardcore-admin-lists`).
- `crestapps-bootstrap-select` was removed; views use Orchard Core's `bootstrap-select` resource.
- `RoutingDecisionMade` repeats are coalesced. Sum `ActivityRoutingDecisionEventData.OccurrenceCount` and read `LastSeenUtc`.
- Feature consolidation: there is no Administration feature, Voice and Real-Time are dependency-only, and the Agent Workspace and provider adapters are glue. Remove those IDs from recipes.
- Custom reports use the 3.0 Reports API (`services.AddReport<T>()`, `ReportFilter.GetDateRange()`); see `orchardcore-reports`.

## Troubleshooting Pointers

- **Agent never receives offers**: check sign-in to the queue on the **Work** tab, `Available` presence, live session and heartbeat, entitlements, skills and proficiency, and the queue's business hours. The **Agent availability and wait time** report states the exact reason.
- **Dialer not calling**: read the Live dashboard **Why isn't this dialing?** card, the **Dialer decision log** and **Dialer activity and health** reports, and the `contactcenter-dialer-pacing` health check.
- **Agent stuck Busy or Reserved**: use **Free agent** on the Live dashboard; it never ends a call the provider confirms is alive.
- **Settings pages throw for `IDialDestinationPolicy`**: enable a voice capability so Telephony is present.
- **Tenant refuses work or readiness fails**: the declared topology or base-voice verification is unmet; read the critical startup log.
- **Health routes return 404**: enable `OrchardCore.HealthChecks` and include the tenant URL prefix.

Full diagnostics and runbooks are in [references/production-support.md](references/production-support.md).

## References

- [references/configuration-and-recipes.md](references/configuration-and-recipes.md) - configuration sections, recipe and deployment steps, settings objects, step ordering
- [references/routing-and-dialer.md](references/routing-and-dialer.md) - presence, queues, routing strategies, work state, dialer modes, pacing, compliance, callbacks, outbound lines
- [references/voice-routing.md](references/voice-routing.md) - inbound and outbound flows, entry points and IVR, voicemail, provider truth, call topology, recording governance, secure capture, provider adapters
- [references/agent-desktop.md](references/agent-desktop.md) - Agent Workspace, docked agent bar, `ContactCenterHub`, Live dashboard actions, endpoints
- [references/workflows.md](references/workflows.md) - Contact Center Event activity, tasks, payloads, excluded actions
- [references/analytics.md](references/analytics.md) - report catalog, KPI rules, reporting configuration
- [references/production-support.md](references/production-support.md) - topology, health checks, telemetry, retention, runbooks, troubleshooting

## Related Skills

- `orchardcore-omnichannel`, `orchardcore-omnichannel-management` - contacts, activities, campaigns, subject flows, dispositions, channel endpoints
- `orchardcore-omnichannel-voice` - automated AI voice conversations
- `orchardcore-omnichannel-messaging` - queue-routed SMS threads that share queues and agent profiles
- `orchardcore-telephony`, `orchardcore-telephony-telnyx`, `orchardcore-telephony-asterisk` - soft phone, providers, recording storage
- `orchardcore-signalr`, `orchardcore-websockets` - real-time transport and media stream sockets
- `orchardcore-dnc-registry`, `orchardcore-phone-numbers` - do-not-call screening and E.164 handling
- `orchardcore-reports` - the Reports framework
- `orchardcore-workflows`, `orchardcore-health-checks`, `orchardcore-redis` - Orchard Core framework features used here
