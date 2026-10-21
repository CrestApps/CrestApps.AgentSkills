# Voice Routing, Recording and Provider Integration

The provider is the source of truth for live call state. Contact Center owns routing, reservations, assignment and call-session orchestration, and the soft phone mirrors server-projected state.

## Inbound Flow

1. A provider webhook (for example Telnyx `POST /api/telnyx/webhook/call`) or the generic `POST /api/contact-center/voice/inbound` endpoint (normalized `InboundVoiceEvent`, `ManageInteractions` permission, 1 MiB body limit) receives the call.
2. Authenticated deliveries are rate- and concurrency-limited per node (`WebhookIngress`), checked for timestamp freshness, and committed to the durable provider webhook inbox before HTTP 2xx. Redeliveries deduplicate on `{canonicalProvider}|{deliveryId}`; failures retry with back-off and dead-letter after ten attempts.
3. `IVoiceContactCenterCallRouter` locks the provider call, resolves the dialed address (`ToAddress`, required) to a channel endpoint, the subject flow, the contact (E.164 match on every contact number) and the entry-point plan.
4. It creates an `OmnichannelActivity` (`Kind = Call`, `Source = Inbound`) and an `Interaction`. Contact attribution is `Resolved`, `Ambiguous` (agent must pick before completion) or `Unresolved`.
5. Open entry points answer and play the welcome message and IVR, or enqueue directly. Closed ones apply the closed action; closed and unroutable calls never become routable work.
6. Assignment reserves an agent; `OfferNextAsync` re-checks provider truth (a dead call is reconciled away, never offered) and dispatches through `IIncomingCallDispatcher`.
7. `POST Admin/contact-center/voice/offer/accept` runs `IContactCenterCallCommandService`: accept the reservation, refresh provider truth, connect media for `ServerSideAcd` providers, create the `CallSession`, publish `OfferAccepted`. `RequiresDeviceAnswer` is true only for `AgentDeviceNative` providers.
8. Provider events through `IProviderVoiceEventService` drive `Connected`, `OnHold`, `Ended`; an answered queue call moves the agent to `WrapUp`.

Number routing order: an entry point whose `AddressIds` include the number (legacy `DialedNumbers` still match), then a queue's `InboundChannelEndpointId`, then the single enabled queue with no mapping. Several unmapped queues are never guessed between.

## Entry Points

| Member | Purpose |
|---|---|
| `Channel` | Address capability served, `Phone` for calls (messaging uses the same entry points) |
| `TargetType` | `Queue`, `Agent` (personal line with `RingTimeoutSeconds`), `AIAgent` (`TargetAIProfileId`) |
| `BusinessHoursCalendarId`, `ClosedAction`, `OverflowQueueId` | Hours gating; `HoldInQueue`, `Voicemail`, `Overflow`, `Reject` |
| `WelcomeMessage`, `ClosedMessage` | Spoken once per caller before the menu, queue or closed action |
| `IvrFlow` | Phone menu (`RootNodeId`, `MaxRetries`, `FallbackAction`, `Nodes`) |
| `VoicemailEnabled`, `VoicemailGreetingText`, `VoicemailRecipientAgentId`, `VoicemailDestination` | Voicemail behavior; `AgentInbox` or `QueueSharedBox` |

IVR rules: the menu plays only while open; one key per prompt with an 8-second timeout; tries are counted per menu; an unreachable choice falls to the entry point target, then voicemail, so a caller is never left on the line. External transfers resolve only from the approved catalog (`ContactCenterExternalTransferSettings`) and settle as transferred only when the destination answers; otherwise the fallback runs. Events recorded: `IvrMenuEntered`, `IvrDigitsReceived`, `IvrActionTaken`, `IvrFallbackTaken`, `CallAbandoned`.

Prompts speak with the provider's text-to-speech voice and language, or play a voice media clip (`PromptMediaId`). Platform sentences (position, wait estimate, callback offer, default greeting) are localized by the text-to-speech language.

## Queue Treatment

`QueueTreatmentSettings`: `WelcomeMessage`, `AnnouncementIntervalSeconds`, `AnnouncePosition`, `AnnounceEstimatedWait`, `HoldMusicMediaId`, `CallbackDtmfKey`, `CallbackOfferAfterSeconds`, estimate bounds and `AverageHandleTimeSeconds`. Callers already answered (after a menu, a blind transfer or an AI handoff) hear hold music or a ringing tone, never silence. Accepting the callback offer completes the inbound activity with `queued_callback` and records `CallbackRequested`.

## Voicemail

Sent to voicemail on a closed entry point, an unanswered personal line, an unanswered offer with the `Voicemail` action, the queue's max wait or full action, or the agent's **Voicemail** button. Sequence is greeting, then beep, then record. Greeting order: the agent's recorded greeting, the agent's spoken greeting, the entry point's `VoicemailGreetingText`, the system greeting.

| Path | Inbox |
|---|---|
| Direct call to an agent, agent pressed Voicemail, offer rang out | That agent |
| Max wait after an expired offer | Agent last offered |
| Queue line with no agent of its own | `VoicemailRecipientAgentId`, or the queue's shared box when `VoicemailDestination` is `QueueSharedBox` |

The **Shared voicemail** page (Inbound Voice) lets users with `AccessContactCenterSharedVoicemail` and an entitlement to the queue claim, call back, resolve, return or (with `ManageContactCenterSharedVoicemail`) delete messages. Changes record `SharedVoicemailReceived`, `...Claimed`, `...Released`, `...Resolved`, `...CallbackRequested`, `...Deleted`. Voicemail endpoints answer refusals with problem details (`401`, `403`, `404`, `409` legal hold), never redirects.

## Outbound Flow

`DialerAttemptService` reserves the agent, runs `IDialerEligibilityService`, creates the outbound `Interaction` before dialing, and stages a durable Dial command. `VoiceContactCenterCallRouter.RouteOutboundAsync` resolves the `IContactCenterVoiceProvider`, requires `IContactCenterVoiceCallControlProvider`, and treats a missing provider call id as a failure. Answer, bridge and completion come from provider events. Answering-machine verdicts map to `AnswerClassification` (`Human`, `Machine`, `Fax`, `Unknown`) under `amd_answer_classification`.

## Provider Truth and Reconciliation

- Every provider event is a normalized `ProviderVoiceEvent` ingested by `IProviderVoiceEventService` under a tenant-scoped distributed lock per canonical provider call. `IProviderIdentityResolver` maps aliases (contributed by `IProviderIdentityProvider`) to one canonical name.
- State machine rules: lifecycle rank never moves backward, a terminal session absorbs later reports, terminal deliveries bypass staleness guards, and live-only flags clear at termination. An optional `SequenceNumber` sets a high-water mark.
- `HangupCause`: `NormalClearing`, `Busy`, `NoAnswer`, `Rejected`, `Congestion`, `Failed`, `NotInService` (adds the number to Numbers Not In Service), `Canceled`, `AnsweringMachine`, `Unknown`. No call ends without a cause. The twelve-state call session vocabulary is narrowed to the soft phone's seven states in exactly one projection.
- `ProviderCallStateReconciliationBackgroundTask` runs each minute (and after a provider stream reconnects) using `ITelephonyCallStateProvider`; `ProviderVoiceOfferSynchronizationService` clears queue items, every non-terminal reservation and agent pointers for calls that ended before answer.
- PBX mutations run under the `CrestApps_Telephony:Commands:Timeout` deadline, not the request token. A timeout after contact is an unknown outcome, never proof the command did not execute.

## Live Call Topology

`CallSession` holds legs (role, status, times), the current bridge and `PriorBridges` with append-only membership, `ConsultCall`s, `MonitorSession`s and typed relationships (transferred from or to, consult of, conferenced with, callback of). `CallTopologyProjector` is the only writer and enforces invariants (no leg ends before start, one live engagement per supervisor, no self-monitoring). `CallSession.ParticipantsAt(instant)` answers historical membership; `Bridge.ReportedParticipantCount` keeps provider counts apart from observed membership. Only barge joins the supervisor to the bridge.

## Transfers

Agent transfers go through `IContactCenterTransferService` (authorized against the agent's principal; `ITransferTargetPolicy` resolves targets through the external destination catalog). Warm transfers use `IConsultTransferService` (consult, then complete or cancel; only a connected consult completes). Telnyx supports consultative transfer; Asterisk supports blind transfer and two-call conference only. Emergency and premium destinations are always refused by `IDialDestinationPolicy`.

## Recording Governance

- Recording runs while **Call Recording** is enabled. `RecordAllCalls` (default true) records queue and dialer calls on connect, AI voice calls on answer, and keypad dials on answer.
- `IRecordingGovernancePolicy` is evaluated before every start or resume. It fails closed (`RecordingDenied`) when the all-parties model requires consent not yet captured. Pause and stop are never gated.
- `IRecordingDisclosureProvider` supplies the disclosure per `RecordingDisclosureCallType`; `IRecordingDisclosureService.RecordDisclosedAsync` stamps `RecordingDisclosedUtc`, captures consent and starts a held recording. Agents confirm through `POST Admin/contact-center/workspace/recording/disclosed`.
- Retention is stamped once (`RecordingRetainUntilUtc`); legal hold can only be raised by a resume.
- `IRecordingAccessGovernanceService` audits every playback (`RecordingAccessed`) and handles erasure (`RecordingErased`, or `RecordingErasureDenied` under legal hold). Admin erasure is `POST {admin}/contact-center/recordings/erase` with a required reason.
- Media lives in Telephony's `IRecordingMediaStore` (encrypted locally by default, Azure Blob with `CrestApps.OrchardCore.Telephony.Azure`). Asterisk recordings ingest through a durable per-tenant job. Asterisk records a mixed mono WAV; monitor and whisper legs are not recorded, barge is.
- Agent secure pause needs `AllowAgentSecurePause`, `ContactCenterSecurePauseRecording`, and the provider `RecordingPause` capability. While paused, supervisor monitoring fails closed and an auto-resume guard (`MaxSecurePauseSeconds`) publishes `RecordingAutoResumed`.

## Secure Data Capture

The agent sends the customer a one-time link (only its SHA-256 hash is stored; served with `no-store` and `no-referrer`). Every field is tokenized by `ISecureCaptureTokenSink.TokenizeAsync(field, rawValue, idempotencyKey)` before anything is stored; only masks and token references persist. One capture per interaction; recording pauses during capture when `PauseRecordingDuringCapture` is on and resumes self-healingly. The built-in sink is registered only in Development and is not PCI-DSS compliant; outside Development capture fails closed until you register a vault-backed sink that honors the idempotency key. The server-side model keeps the application in PCI-DSS scope.

```csharp
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;

public sealed class VaultSecureCaptureTokenSink : ISecureCaptureTokenSink
{
    private readonly IMyVaultClient _vault;

    public VaultSecureCaptureTokenSink(IMyVaultClient vault)
    {
        _vault = vault;
    }

    public async Task<SecureCaptureTokenResult> TokenizeAsync(
        SecureCaptureField field,
        string rawValue,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        // The same key with the same value must return the original token; a different value must fail.
        return await _vault.TokenizeAsync(field, rawValue, idempotencyKey, cancellationToken);
    }
}
```

`IMyVaultClient` stands for your vault SDK wrapper; build the `SecureCaptureTokenResult` from the vault response. Register it with `services.AddScoped<ISecureCaptureTokenSink, VaultSecureCaptureTokenSink>();` under `[RequireFeatures("CrestApps.OrchardCore.ContactCenter.SecureCapture")]`.

## Voice Media

The **Voice Media** library (base feature, `ManageContactCenterVoiceMedia`) stores hold music and prompts. The master copy lives in `IRecordingMediaStore`; each provider copy (`ProviderReferences`) is a cache rebuilt on demand through `IVoiceMediaProvisioningService.ResolveAsync`, with availability cached for ten minutes (`VoiceMediaAvailabilityCache.Window`). **Re-sync media** always asks the provider. Deleting a clip deletes its master and every provider copy.

## Provider Adapters

A provider module references only `CrestApps.OrchardCore.ContactCenter.Abstractions` and implements `IContactCenterVoiceProvider` (`TechnicalName`, `Name`, `Capabilities`, `DeliveryModel` of `AgentDeviceNative` or `ServerSideAcd`) plus only the executable contracts it supports: `IContactCenterVoiceCallControlProvider`, `IContactCenterVoiceQueueAssignmentProvider`, `IContactCenterVoiceTransferProvider`, `IContactCenterVoiceConferenceProvider`, `IContactCenterVoiceRecordingProvider`, `IContactCenterVoiceMonitoringProvider`, `IContactCenterVoiceSupervisorInterventionProvider`, and `IContactCenterVoiceMediaProvider` (registered only by a feature gated on Voice Media). A capability flag without its contract fails closed. Webhook processing implements `IProviderWebhookInboxHandler` with a declared `ContactCenterHandlerReplaySafety`. Restart recovery belongs in an `IBackgroundTask` gated on `IContactCenterFeatureWorkManager`, never in tenant activation. See `orchardcore-telephony-telnyx`, `orchardcore-telephony-asterisk` and `orchardcore-telephony`.

## Bidirectional Media and AI Voice

Voice Media resolves `IContactCenterVoiceMediaProvider` for a provider whose base voice provider is registered. Asterisk uses ARI External Media (RTP to a socket Orchard opens; development-only per the support matrix). Telnyx dials back to `api/telnyx/media/stream`, hosted through `CrestApps.OrchardCore.WebSockets`; set a public `https` site base URL and keep host affinity, because live sockets cannot move between nodes (see `orchardcore-websockets`). Automated AI calls run as live speech-to-speech when Voice Media is on and the profile's conversation deployment is realtime-capable, otherwise the turn-based loop; behavior is described in `orchardcore-omnichannel-voice`.

## Network Requirements

| Path | Protocol |
|---|---|
| Browser soft phone to Orchard | `https` + `wss` (SignalR may fall back to SSE or long polling) |
| Provider to Orchard webhooks | `https` |
| Orchard to provider REST | `https` |
| Orchard to Asterisk ARI | `http(s)` plus a long-lived `ws(s)` event stream |

Proxies must allow WebSocket upgrades; locked-down hosts must allow outbound `https` and `wss`.
