# Contact Center Production Support

## Supported Topology

- The commercial release is gated; supported combinations are the `ContactCenterTopologyProfiles` plus the GA-Core tenant profile `ga-core-asterisk` (Asterisk, inbound voice plus Manual and Preview dialing). Unlisted combinations are unsupported.
- Production database is PostgreSQL 16.x. SQLite is for development and tests.
- Production topology is `single-node-distributed`: one region, one application node, shared database, `OrchardCore.Redis`, `OrchardCore.Redis.Lock` and the Redis SignalR backplane. Multi-node is not certified yet; the code is lock- and backplane-agnostic.
- Prohibited in the certified profile: Power, Progressive and Predictive dialing; recording, monitor, whisper, barge and bidirectional media; more than one voice provider profile per tenant; Elasticsearch in any correctness path.
- Tier-1 capacity target: 100 signed-in agents and 50 concurrent voice interactions per tenant, 10 new interactions per second, 5 tenants and 250 agents per deployment.

Declare the topology so it is enforced at activation:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "Topology": {
          "ProfileId": "single-node-distributed"
        }
      }
    }
  }
}
```

With `single-node-distributed`, activation verifies the `Postgres` provider, the `OrchardCore.Redis`, `OrchardCore.Redis.Lock` and `CrestApps.OrchardCore.SignalR.Redis` features (the deprecated CrestApps ID that turns on `OrchardCore.SignalR.Redis`), and that the resolved `IDistributedLock` is not process-local. Every unmet requirement is logged at once as critical, all Contact Center work admission is refused, and readiness reports unready. Omitting `ProfileId` runs single-node with no checks (a production host logs a one-time warning). An unknown id fails validation.

## Base-Voice Acceptance

A production host withholds tenant readiness (`contactcenter-base-voice-verification`, registered by Voice) until the operator proves the WebRTC audio path (direct ICE, forced TURN, restart drain, capacity floor) and declares it:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "BaseVoiceVerification": {
          "AudioVerificationAcknowledged": true,
          "AudioVerificationEvidenceReference": "https://example.com/evidence/base-voice-2026-07"
        }
      }
    }
  }
}
```

An acknowledgment without an evidence reference fails startup on every host. Enable Call Recording only after acceptance, because recording starts as soon as the feature is on.

## Health Probes

Probes and checks are registered only when `OrchardCore.HealthChecks` is enabled.

| Route | Scope | Wire to |
|---|---|---|
| `/health/process` | Host middleware, prefix-independent, anonymous | Liveness. Register `builder.Services.AddContactCenterProcessLiveness()` and call `app.UseContactCenterProcessLiveness()` before `app.UseOrchardCore()` |
| `{tenantPrefix}/api/contact-center/health/ready` | Tenant, anonymous, status word only | Readiness and load balancer. Checks tagged `contactcenter-ready` |
| `{tenantPrefix}/api/contact-center/health/dependencies` | Tenant, requires `MonitorContactCenter` | Dashboards and alerting only. Checks tagged `contactcenter-dependency` |

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddContactCenterProcessLiveness()
    .AddOrchardCms();

var app = builder.Build();

app.UseContactCenterProcessLiveness();
app.UseOrchardCore();

app.Run();
```

Readiness checks (node-local or static verdicts): `contactcenter-node`, `contactcenter-topology`, `contactcenter-base-voice-verification`, and the opt-in `contactcenter-node-serving` gate (`HealthChecks:EnableNodeServingGate`; enable only behind a load balancer that fails open). Dependency checks: `contactcenter-storage`, `contactcenter-outbox`, `contactcenter-active-calls` (gauge), `contactcenter-queue-backlog` (gauge, Queues), `contactcenter-dialer-pacing` (Paced Dialing), `contactcenter-provider-ingress` (Voice), `contactcenter-distributed-lock`, `contactcenter-redis` and `contactcenter-backplane` (only with valid Redis), and `contactcenter-shared-endpoint`.

Rules:

- Never tag a database, Redis, provider or backplane check `contactcenter-ready`; a shared dependency would drain every node at once.
- Never point a probe at the `OrchardCore.HealthChecks` aggregate endpoint. If it still claims `/health/live`, Contact Center logs critical and reports a degraded `contactcenter-shared-endpoint`. Move it (`OrchardCore_HealthChecks:Url` to `/health/aggregate`) or acknowledge with `HealthChecks:AllowUnsafeSharedEndpointRoute`.
- `/health/process` must not be `/health/live`; pass a custom path to `AddContactCenterProcessLiveness("/probe/alive")` only.
- Include the tenant URL prefix in tenant probes; an unprefixed route returns 404.

## Telemetry

`ContactCenterDiagnostics` publishes a `Meter` and an `ActivitySource` both named `CrestApps.OrchardCore.ContactCenter` (instruments `contactcenter.outbox.redelivered` and `contactcenter.outbox.dead_lettered`, tagged `reason`). Asterisk adds the `CrestApps.OrchardCore.Asterisk` meter (`asterisk.realtime.connected`, `asterisk.realtime.reconnect_attempted`, `asterisk.realtime.ingestion.saturated`).

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter("CrestApps.OrchardCore.ContactCenter"))
    .WithTracing(tracing => tracing.AddSource("CrestApps.OrchardCore.ContactCenter"));
```

## Retention, Erasure and Governance

- `CrestApps:ContactCenter:Retention` windows default to `0` (keep forever) except `DialerPacingCycleRetentionDays` (14). Records age from when they settled, never from arrival; live records are never purged.
- Floors only extend retention: `ProjectionReplayHorizonDays` (event log), `LegalHoldMinimumDays` (communication history), `ProcessedEventDeliveryEnvelopeDays` (dedup markers).
- Purges drain in batches (`PurgeBatchSize`, `MaxPurgeBatchesPerCycle`, budget per entity) and log a warning when the budget runs out. Configuration tables are never purged.
- `ContactCenterDataGovernanceCatalog` classifies every entity's sensitivity and erasure strategy. Recording erasure clears both pointers, stamps a tombstone and deletes media through the outbox; a per-record `RecordingLegalHold` is excluded from purge.
- Omnichannel message content is outside this catalog and has no automated subject erasure (see `orchardcore-omnichannel-management`).
- There is no in-app reset of operational data; restore or drop the tenant database instead. After a point-in-time restore run `IContactCenterMetricsProjectionMaintenanceService.RebuildAsync`.

## Shutdown, Drain and Feature Lifecycle

- Disabling a feature quiesces then drains its participants for up to `FeatureLifecycle:DrainTimeoutSeconds` (30). Host shutdown stops Queues, Dialer and Paced Dialing admission and waits `ShutdownDrainTimeoutSeconds` (5); live AI calls are handed off within ten seconds; webhooks are accepted until exit.
- Third-party background recovery must gate on `IContactCenterFeatureWorkManager` in an `IBackgroundTask`, not run during tenant activation.
- On IIS disable periodic recycling, set Idle Time-out to 0, Start Mode `AlwaysRunning` and Preload Enabled. With Telnyx set a Webhook Failover URL.

## Runbooks

| Symptom | Signal | Response |
|---|---|---|
| SQL outage | `contactcenter-storage` unhealthy | Fail nodes out, restore the database, let the outbox and inbox replay; rebuild metrics if restored from backup |
| Redis or backplane | `contactcenter-redis`, `-distributed-lock`, `-backplane` | Backplane loss degrades only cross-node fan-out; lock loss pauses sweeps without corruption. Restore Redis |
| Provider outage | Growing `contactcenter-provider-ingress` backlog | Inbox drains after recovery; check `contactcenter.outbox.dead_lettered{reason="retry_exhausted"}` |
| Node failure | Readiness fails | Leases expire and survivors re-acquire through fence tokens |
| Dialer stopped | `contactcenter-dialer-pacing` fails, Live dashboard **Dialer stalled** | Read the last outcome on **Dialer activity and health**: time budget, failed provider, held lock, or pacing not running |
| Over-dial falls back | `Over-dial cycle of queue ...` log, `PredictivePacingState.LastDecision` | Reasons like `AnswerRateSampleBelowFloor`, `RollingRateAtCap`, `PolicySuppressed` clear as data arrives |
| Network partition | Flapping checks | Do not force-release locks or replay the outbox manually |

Deployments: every migration is additive (expand, migrate, contract), so rolling and blue-green deploys work for the stateless tier. The Asterisk ARI listener is single-active-process per `(BaseUrl, ApplicationName)`: hand it over non-overlapping (drain and release the old node first), size the gap with `contactcenter-active-calls`, and roll back the same way. Add a dialplan continuation after `Stasis()` so parked calls are not stranded. See `orchardcore-telephony-asterisk`.

## Troubleshooting Matrix

| Problem | Check |
|---|---|
| Menu or button missing | Feature enabled, permission granted, site setting on (secure pause, secure capture), provider capability advertised |
| Agent cannot sign in to a queue | Entitlement (Agent Entitlements feature), `ContactCenterSignIntoQueues`, soft phone signed in |
| Phone does not ring | Signed in and `Available`, queue open, entry point routes the number, skills and proficiency, workspace *Connected*; Telnyx webhook public key set |
| Agent set Away with *Phone unreachable* | Soft phone registration lapsed while the hub stayed connected; reconnect and set Available |
| Agent stuck Busy, Reserved or WrapUp | Live dashboard **Free agent**; wrap-up auto-releases after `MaximumWrapUpDuration` |
| Live dashboard or shared voicemail empty | The viewer has no agent entitlement record with those queues |
| Dialer not calling | Profile enabled, agents signed in to the campaign, calling window, abandonment cap, one profile per campaign queue, Paced Dialing enabled for paced modes |
| Settings pages throw `IDialDestinationPolicy` | Enable a voice capability so Telephony is present |
| Tenant starts but refuses work | Topology or option validation failed; read critical startup log entries |
| Realtime AI calls silent | Voice Media enabled, realtime conversation deployment, public `https` site base URL and node affinity for Telnyx media streams |
| Report empty or refused | Date range (defaults to today), filters, `ViewContactCenterReports`, workforce 31-day limit |
| Duplicate or missed provider events after restart | `ProviderCallStateReconciliationBackgroundTask` repairs known calls each minute; calls with no local record are reported by the Telnyx orphan pass |

Production credentials: a production host refuses to activate a tenant whose `CrestApps:Asterisk:Default` uses a published development credential or an obvious placeholder.
