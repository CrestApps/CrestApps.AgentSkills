---
name: orchardcore-telephony-asterisk
description: Skill for integrating Asterisk with the CrestApps Orchard Core Telephony soft phone through the Asterisk REST Interface (ARI). Covers the CrestApps.OrchardCore.Asterisk feature, the tenant Asterisk provider and the host-configured Default Asterisk provider, the Default and Coordination configuration sections, per-tenant Stasis application names, browser WebRTC audio through chan_pjsip with short-lived PJSIP Realtime credentials, coturn TURN, voicemail routing, the ARI event listener and reconciliation, the AsteriskSettings recipe step, and the Contact Center voice and External Media adapters. Use this skill when requests mention Asterisk, ARI, Stasis, PJSIP, SIP.js, coturn, External Media, or Default Asterisk in CrestApps Orchard Core. Strong matches include CrestApps.OrchardCore.Asterisk, AsteriskSettings, DefaultAsteriskOptions, AsteriskCoordinationOptions, AsteriskTelephonyProvider, AsteriskContactCenterVoiceProvider, AsteriskContactCenterVoiceMediaProvider, and AsteriskConstants.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Telephony Asterisk - Prompt Templates

## Connect Asterisk to the Soft Phone

You are an Orchard Core expert. Generate accurate configuration, recipes, and troubleshooting guidance for the CrestApps Asterisk provider. It controls calls server-side through ARI over HTTP basic authentication, so ARI credentials never reach the browser. The provider-neutral soft phone, hub, and provider contracts are covered by `orchardcore-telephony`.

### Guidelines

- Install `CrestApps.OrchardCore.Asterisk` in the web/startup project and enable `CrestApps.OrchardCore.Asterisk`. It depends on `CrestApps.OrchardCore.Telephony`, which enables the settings screen.
- There is only one selectable feature. The Contact Center voice adapter activates automatically when `CrestApps.OrchardCore.ContactCenter.Voice` is also enabled, and the media adapter when `CrestApps.OrchardCore.ContactCenter.Voice.Media` is enabled. Install the Contact Center package in the startup project for those.
- Always set a **Stasis application name**; a blank value fails closed. Give each tenant sharing one Asterisk server a unique application name.
- Keep ARI and PJSIP Realtime credentials in tenant settings (encrypted) or a secret store, never in committed `appsettings.json`.
- Run real-time Asterisk voice on a single active application node; the ARI application claim is node-local.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.

### Enable the Feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Telephony",
        "CrestApps.OrchardCore.Telephony.SoftPhone",
        "CrestApps.OrchardCore.Asterisk"
      ],
      "disable": []
    }
  ]
}
```

## Two Providers

| Provider | Technical name | Configured by | Available when |
|---|---|---|---|
| Asterisk | `Asterisk` | **Settings → Communication → Telephony → Asterisk** (`AsteriskSettings`) | Enabled and `BaseUrl`, `UserName`, password and `ApplicationName` are set |
| Default Asterisk | `Default Asterisk` | Shell configuration section `CrestApps:Asterisk:Default` (`DefaultAsteriskOptions`) | `BaseUrl`, `UserName`, `Password`, and `ApplicationName` all have values, on every tenant that enables the module |

Both contribute to `TelephonyProviderOptions` through `AsteriskProviderOptionsConfigurations`. Saving the settings or importing them calls `IOptionsUpdateNotifier.RequestUpdate<TelephonyProviderOptions>()`, so the provider map refreshes without a tenant restart. Enabling **Asterisk** with no default provider makes it the default; disabling it while it is the default clears the default.

## Capabilities

The provider advertises `Dial`, `Hangup`, `SendDigits`, `ReceiveCalls`, `Hold`, `Resume`, `Mute`, `Merge`, and `Directory` (ARI endpoints). It adds `Transfer` when the endpoint template resolves to a `Local/...@context` dialplan route, and `Voicemail` when a voicemail context and extension template are configured. It does not advertise `AttendedTransfer`, `ExtensionDial`, or `ExtensionConference` yet.

Audio: the provider reports `TelephonyAudioCapabilities.Browser` with the `sipjs` browser adapter when complete WebRTC settings are present, and `None` otherwise (ARI controls the call and audio stays on the PBX endpoint).

## Tenant Settings

| Setting | Property | Notes |
|---|---|---|
| Enable Asterisk provider | `IsEnabled` | Makes the provider selectable |
| ARI base URL | `BaseUrl` | For example `http://localhost:8088/ari/`; `/ari` is appended when missing |
| ARI user name and password | `UserName`, `Password` | Password stored encrypted |
| Stasis application name | `ApplicationName` | Required, must match the dialplan `Stasis()` application |
| Endpoint template | `EndpointTemplate` | `{number}` placeholder, for example `PJSIP/{number}@phones` or `Local/{number}@default` |
| Outbound caller id | `OutboundCallerId` | Optional |
| Dial timeout | `TimeoutSeconds` | Default `30` |
| Voicemail context, extension template, priority | `VoicemailContext`, `VoicemailExtensionTemplate`, `VoicemailPriority` | Template tokens such as `{voicemailRecipientUserName}`, `{voicemailRecipientUserId}`, `{calledAddress}`, `{queueName}` |
| SIP WebSocket URL, SIP domain | `WebSocketUrl`, `SipDomain` | Required for browser audio; `wss://` only |
| ICE server URLs, TURN shared secret, ICE transport policy | `TurnUrls`, `TurnSharedSecret`, `IceTransportPolicy` | Policy `all` (default) or `relay`; secret stored encrypted |
| Browser audio codecs | `WebRtcCodecs` | Default `opus,g722,ulaw` |
| PJSIP credential lifetime, contact expiration | `PjsipCredentialLifetimeMinutes`, `PjsipContactExpirationSeconds` | Defaults `15` minutes and `120` seconds |
| PJSIP Realtime provider, connection string, table prefix | `PjsipRealtimeProviderInvariantName`, `PjsipRealtimeConnectionString`, `PjsipRealtimeTablePrefix` | Required for browser audio; the prefix must be a strict SQL identifier with an optional `schema.` part |

### Recipe

Settings export through the **Asterisk Settings** deployment step and import through the `AsteriskSettings` recipe step. `Password`, `TurnSharedSecret`, and `PjsipRealtimeConnectionString` are never exported; a hand-written recipe may supply them in clear text and they are encrypted on import. Members the step omits keep their stored values.

```json
{
  "steps": [
    {
      "name": "AsteriskSettings",
      "Settings": {
        "IsEnabled": true,
        "BaseUrl": "https://pbx.example.com/ari/",
        "UserName": "orchard",
        "Password": "supply-in-clear-text-or-omit",
        "ApplicationName": "orchard-tenant-a",
        "EndpointTemplate": "PJSIP/{number}@phones",
        "TimeoutSeconds": 30,
        "WebSocketUrl": "wss://pbx.example.com:8089/ws",
        "SipDomain": "pbx.example.com",
        "IceTransportPolicy": "all",
        "WebRtcCodecs": "opus,g722,ulaw"
      }
    }
  ]
}
```

The import and the settings screen both refuse to disable the provider or change its ARI URL or Stasis application while calls are still bound to it.

## Default Asterisk from Configuration

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Asterisk": {
        "Default": {
          "BaseUrl": "http://pbx.example.com:8088/ari/",
          "UserName": "<ari-user>",
          "Password": "<ari-password>",
          "ApplicationName": "crestapps-telephony",
          "EndpointTemplate": "Local/{number}@default",
          "TimeoutSeconds": 30,
          "VoicemailContext": "crestapps-voicemail",
          "VoicemailExtensionTemplate": "{voicemailRecipientUserName}",
          "VoicemailPriority": 1
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__Asterisk__Default__BaseUrl=http://pbx.example.com:8088/ari/
OrchardCore__CrestApps__Asterisk__Default__Password=<ari-password>
```

- The browser WebRTC keys are available here too.
- Each tenant resolves the shared connection under `<ApplicationName>-<ShellName>` (for example `crestapps-telephony-Default`, `crestapps-telephony-blog1`) so Stasis event streams stay isolated. Inbound dialplans must route each tenant's calls to `Stasis(<ApplicationName>-<ShellName>)`.
- In the `Production` environment a tenant refuses to start when this section holds a development credential published in the repository.

### Coordination Options

Section `CrestApps:Asterisk:Coordination` binds `AsteriskCoordinationOptions`:

| Key | Default |
|---|---|
| `CredentialLockTimeout` | `00:00:05` |
| `CredentialLockExpiration` | `00:00:30` |
| `ChannelBindingCreateLockTimeout` | `00:00:10` |
| `PendingReclamationThreshold` | `00:05:00` |
| `HttpTotalRequestTimeout` | `00:00:30` |
| `HttpAttemptTimeout` | `00:00:10` |
| `RealtimeEventBufferCapacity` | `1000` (at most `100000`) |
| `RealtimeEventBackpressureTimeout` | `00:00:05` |
| `MaxRealtimeMessageBytes` | `1048576` |

The ARI HTTP client retries only safe methods, so a lost response to an originate can never place a second call.

## Browser WebRTC Audio

- Agent media flows directly between the browser (SIP.js) and Asterisk `chan_pjsip` over WSS, DTLS-SRTP and ICE; Orchard does not relay agent RTP.
- Browser audio is advertised only when the `wss://` URL, SIP domain, codecs, positive credential lifetime and contact expiration, and PJSIP Realtime provider and connection string are all set. Missing values make browser audio unavailable instead of failing startup.
- Orchard issues short-lived PJSIP credentials per authenticated session, records a durable lease in the tenant's YesSql store first, then writes the Realtime `ps_endpoints`, `ps_auths`, and `ps_aors` rows. Each user is capped at a few live credentials (the oldest is revoked first), and signing out revokes all of them.
- Asterisk advertises only the ICE servers you configure. Without a TURN server, agents behind strict NAT may get one-way or no audio. With a **TURN shared secret**, each registration gets an ephemeral coturn `use-auth-secret` credential (username `{unixExpiry}:{tenant}:{session}`, credential `Base64(HMAC-SHA1(secret, username))`); set the same value as coturn `static-auth-secret`.

```text
stun:turn.example.com:3478
turn:turn.example.com:3478?transport=udp
turns:turn.example.com:5349?transport=tcp
```

## Call Control and Real-Time State

- ARI operations used: `POST /channels` (originate), `DELETE /channels/{id}`, `POST /channels/{id}/answer`, `hold`, `mute?direction=both`, `continue` (Local-route blind transfer and voicemail), `variable`, `dtmf`, `POST /bridges` with `addChannel` (merge), and `GET /endpoints` (directory).
- A tenant-scoped ARI WebSocket listener per configured provider normalizes channel events into `ProviderVoiceEvent`, updates call history, and pushes state to the soft phone. It reconnects with jittered backoff and reconciles after each connect. Events from the Default Asterisk connection are tagged with the canonical `Asterisk` provider identity.
- Reconciliation reads the `CRESTAPPS_STATE_ONHOLD` and `CRESTAPPS_STATE_MUTED` channel variables. An ARI `404` is authoritative: the orphaned record is removed and the soft phone is told the call ended.
- An accepted originate starts in **Connecting**; the soft phone waits for the ARI event before showing **Connected**.

## Voicemail Routing

**Send to voicemail** copies the provider-neutral call metadata (`voicemailRecipientUserId`, `voicemailRecipientUserName`, `voicemailRecipientDisplayName`, `calledAddress`, `callerAddress`, `queueId`, `queueName`) into channel variables prefixed `CRESTAPPS_METADATA_` (for example `CRESTAPPS_METADATA_VOICEMAILRECIPIENTUSERNAME`) and continues the channel into the configured voicemail context, extension, and priority. Map those to mailboxes in the dialplan with a fallback.

## Contact Center Adapters

These adapters are integration glue described here from the provider side; queues, routing, recording governance, supervision, and the media session API are in `orchardcore-contact-center`.

- **Voice adapter** (with `CrestApps.OrchardCore.ContactCenter.Voice`) registers `AsteriskContactCenterVoiceProvider`. It advertises the `ServerSideAcd` delivery model, bridge recording, supervisor monitor, whisper and barge, blind transfer to another agent, attended consult phases, and multi-party conference on the canonical conversation bridge. It also registers recording ingest jobs and per-tenant channel bindings, and fails closed on a channel the current tenant does not own. Connecting a parked call to a browser agent currently returns `agent_bridge_unavailable` rather than a false success. Transfers to a queue, external number, or entry point return a confirmed failure.
- **Media adapter** (with `CrestApps.OrchardCore.ContactCenter.Voice.Media`, a dependency-only development foundation) registers `AsteriskContactCenterVoiceMediaProvider`. It uses ARI External Media over RTP/UDP with G.711 mu-law, 8 kHz mono, attached to the call bridge. The session request must carry `externalHost` (`AsteriskConstants.ExternalMediaHostMetadataKey`); optional `bindAddress` and `bindPort`. It is not production-certified; isolate the unencrypted RTP path on a private network and pin the session to its node.

## Development Hosts

- `src/Startup/CrestApps.Aspire.AppHost` provisions an Asterisk container (with coturn) and injects the Default Asterisk environment variables; the `CrestApps.OrchardCore.Cms.Web` launch profile injects the same values. The demo `Local/{number}@default` route answers, plays a tone, and echoes.
- `src/Startup/CrestApps.OrchardCore.Asterisk.Web` is a development-only dashboard and inbound simulator. It refuses to start outside `Development`; supply `AsteriskWeb:AsteriskUserName` and `AsteriskWeb:AsteriskPassword` through user secrets.
- Quick checks against ARI are `GET /ari/asterisk/info`, `GET /ari/channels`, and `GET /ari/bridges`.

## Upgrading from 2.x

- Remove `CrestApps.OrchardCore.Telephony.Admin` from recipes; the Asterisk tab is registered by the main Asterisk `Startup`, and `AsteriskAdminStartup` is gone.
- The Default Asterisk provider is now available on every tenant, not only the default shell. A default-shell deployment that routes inbound calls must change its dialplan to `Stasis(<ApplicationName>-Default)`.
- Code that reads `TelephonyProviderOptions` must use `IOptionsMonitor<TelephonyProviderOptions>`.

## Troubleshooting

| Symptom | Check |
|---|---|
| Provider missing from the default provider list | `IsEnabled`, base URL, user name, password, and application name are all required; for Default Asterisk all four configuration keys must be present |
| No inbound calls or events | The dialplan must hand calls to the exact Stasis application (per tenant for Default Asterisk) and Orchard must reach ARI over `ws` or `wss` |
| Calls stuck connected after a PBX hangup | Confirm the ARI listener connected (application logs) and only one node runs the provider |
| One-way or no browser audio | Configure TURN with a shared secret, or try `relay` to validate TURN |
| Transfer or voicemail button missing | Use a `Local/...@context` endpoint template, and set the voicemail context and extension template |
| Settings save refused | Calls are still bound to the old ARI identity; end or reconcile them first |
