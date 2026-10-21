---
name: orchardcore-telephony-telnyx
description: Skill for integrating Telnyx with CrestApps Orchard Core as a browser WebRTC telephony provider, Contact Center voice provider, SMS provider, and AI voice agent. Covers the CrestApps.OrchardCore.Telnyx, CrestApps.OrchardCore.Telnyx.Sms and CrestApps.OrchardCore.Telnyx.AiVoice features, the API key and Connect Telnyx provisioning, TelnyxOptions read through IOptionsMonitor, Ed25519-signed call and messaging webhooks, the telnyx-webrtc adapter and bridged keypad dialing, STUN and TURN, noise suppression, recording ingest, the media stream WebSocket, the TelnyxSettings and TelnyxSmsSettings recipe steps, and the OrchardCore_Sms_Telnyx section. Use this skill when requests mention Telnyx, Telnyx Call Control, Telnyx WebRTC, Telnyx SMS, Telnyx webhooks, or a Telnyx AI phone agent in CrestApps Orchard Core. Strong matches include TelnyxConstants, TelnyxSettings, TelnyxOptions, TelnyxSmsOptions, TelnyxTelephonyProvider, TelnyxContactCenterVoiceProvider, TelnyxSmsProvider, and VoiceOmnichannelProcessor.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Telephony Telnyx - Prompt Templates

## Connect Telnyx

You are an Orchard Core expert. Generate accurate configuration, recipes, and troubleshooting guidance for the CrestApps Telnyx module. Telnyx exposes a SIP-over-WebSocket registrar and a server-side Call Control API, so the browser soft phone carries the call audio itself (WebRTC) and the Contact Center can bridge live calls to agents server-side (`ServerSideAcd`). The provider-neutral soft phone, hub, and contracts are covered by `orchardcore-telephony`.

### Guidelines

- Install `CrestApps.OrchardCore.Telnyx` in the web/startup project.
- Enable `CrestApps.OrchardCore.Telnyx`; it enables `CrestApps.OrchardCore.Telephony` and `CrestApps.OrchardCore.WebSockets` (see `orchardcore-websockets`).
- Telnyx voice has no configuration section; configure it on the **Telnyx** tab of **Settings → Communication → Telephony** with `ManageTelephonySettings`.
- Create the API key as a Telnyx account Owner or Admin. A restricted member's key fails **Connect Telnyx** with `Not authorized` (Telnyx code 10006).
- Paste the Telnyx account Ed25519 **public key** into **Webhook public key**; webhooks are rejected until it is set.
- Present Telnyx-owned caller ids so calls earn STIR/SHAKEN attestation.
- Read `TelnyxOptions` and `TelephonyProviderOptions` through `IOptionsMonitor<T>`; saving the settings refreshes them without a tenant restart.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.

### Features

| Feature | Feature ID | Depends on | Purpose |
|---|---|---|---|
| Telnyx | `CrestApps.OrchardCore.Telnyx` | Telephony, WebSockets | Telephony provider, browser WebRTC soft phone, signed call webhooks. Category Telephony |
| Telnyx SMS | `CrestApps.OrchardCore.Telnyx.Sms` | Telnyx, `OrchardCore.Sms` | Orchard Core SMS/MMS provider and signed messaging webhook. Category Communication |
| Telnyx AI Voice Agent | `CrestApps.OrchardCore.Telnyx.AiVoice` | Telnyx, `CrestApps.OrchardCore.Omnichannel.Voice`, `CrestApps.OrchardCore.AI`, `CrestApps.OrchardCore.AI.Chat.Core`, `CrestApps.OrchardCore.Omnichannel.Managements` | Phone omnichannel processor that runs an AI conversation over Telnyx. Category Contact Center |

The Contact Center voice adapter is not a feature: it activates when `CrestApps.OrchardCore.ContactCenter.Voice` is enabled, and the media-stream adapter when `CrestApps.OrchardCore.ContactCenter.Voice.Media` is enabled.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Telephony.SoftPhone",
        "CrestApps.OrchardCore.Telnyx"
      ],
      "disable": []
    }
  ]
}
```

## Connect Telnyx and Settings

1. Enter the v2 **API key** and save.
2. Click **Connect Telnyx** (admin route `telnyx/connect`). It idempotently finds or creates a Call Control application with webhook `https://<tenant-host>/api/telnyx/webhook/call`, a Credential SIP connection, and an outbound voice profile, discovers numbers, suggests a caller id, and assigns an unassigned number to the Call Control application. The resulting ids are stored and shown read-only; a plain **Save** never overwrites them.
3. Paste the **Webhook public key** and confirm the **Default outbound caller id**.
4. **Disconnect** (`telnyx/connect/disconnect`) deletes the created resources and clears the ids.

`TelnyxSettings` (site settings) and the projected `TelnyxOptions` carry these members:

| Member | Default | Notes |
|---|---|---|
| `IsEnabled` | `false` | Enabling with no default provider makes `Telnyx` the default |
| `ApiKey` | | Bearer token, stored encrypted |
| `ConnectionId`, `SipConnectionId`, `OutboundVoiceProfileId` | | Written by Connect |
| `DefaultOutboundCallerId` | | E.164, used when a call carries no caller id of its own |
| `WebhookPublicKey` | | Ed25519 public key, stored encrypted |
| `CredentialLifetimeMinutes` | `180` | Browser SIP credential lifetime; renewal waits for a live call to end |
| `EchoTestDestination` | | Audio test destination for diagnostics and the health canary |
| `OrphanedCallHandling` | `Report` | `Report` or `EndCall` for a connected call with no local record |
| `AnsweringMachineDetection` | `Premium` | `Premium`, `Standard`, or `Disabled` |
| `TtsVoice`, `TtsLanguage` | `female`, `en-US` | Voice for prompts, for example `AWS.Polly.Joanna-Neural` |
| `NoiseSuppressionEngine` | `Off` | `Krisp`, `DeepFilterNet`, `Denoiser`, or `AiCoustics` (ai-coustics) |
| `NoiseSuppressionAgentVoice`, `NoiseSuppressionCallerVoice` | `true`, `false` | Which voices Telnyx cleans; each is billed |
| `NoiseSuppressionStrength` | `Balanced` | `Light`, `Balanced`, `Strong` |
| `SipWebSocketUrl`, `SipDomain` | `wss://sip.telnyx.com:7443`, `sip.telnyx.com` | SIP-over-WSS is on port 7443, not 443 |
| `WebRtcCodecs`, `WebRtcRegion` | SDK default, Automatic | Preferred codecs and signaling region |
| `IceUrls`, `TurnUsername`, `TurnCredential`, `IceTransportPolicy` | SDK default, `all` | See STUN and TURN below |
| `ApiBaseUrl` | `https://api.telnyx.com/v2/` | Override for testing |

### Recipe

Voice settings export through the **Telnyx Settings** step and import with `TelnyxSettings`; SMS settings use **Telnyx SMS Settings** and `TelnyxSmsSettings`. API keys, webhook public keys, and the TURN credential are never exported; supply them in clear text in a hand-written recipe and they are encrypted on import. Connection ids belong to one Telnyx account, so run **Connect Telnyx** again in an environment that uses another account.

```json
{
  "steps": [
    {
      "name": "TelnyxSettings",
      "Settings": {
        "IsEnabled": true,
        "ApiKey": "supply-in-clear-text-or-omit",
        "DefaultOutboundCallerId": "+15551230000",
        "CredentialLifetimeMinutes": 180,
        "OrphanedCallHandling": "Report",
        "AnsweringMachineDetection": "Premium",
        "NoiseSuppressionEngine": "Krisp",
        "NoiseSuppressionAgentVoice": true,
        "NoiseSuppressionCallerVoice": false,
        "TtsVoice": "AWS.Polly.Joanna-Neural",
        "TtsLanguage": "en-US"
      }
    },
    {
      "name": "TelnyxSmsSettings",
      "Settings": {
        "IsEnabled": true,
        "MessagingProfileId": "40017a1b-0000-0000-0000-000000000000"
      }
    }
  ]
}
```

An unknown noise suppression engine imports as off, the region is normalized, and options refresh without a restart. Members the step omits keep their stored values.

## Webhooks

| Endpoint | Method | Feature | Events |
|---|---|---|---|
| `/api/telnyx/webhook/call` | `POST` | Telnyx | Call Control events, committed to the durable provider webhook inbox and normalized into `ProviderVoiceEvent` |
| `/api/telnyx/webhook/sms` | `POST` | Telnyx SMS | `message.received` onto the Omnichannel `SmsReceived` bus, `message.finalized` delivery receipts |
| `/api/telnyx/media/stream` | WebSocket `GET` | Contact Center Voice Media adapter | Bidirectional G.711 mu-law (`PCMU`) media stream |

Telnyx signs `{timestamp}|{raw_body}` with Ed25519 and sends `telnyx-signature-ed25519` and `telnyx-timestamp`. The call endpoint rejects unsigned, stale, or bodies over 1 MiB, and answers service-unavailable when a stored public key cannot be decrypted instead of accepting unsigned traffic. Webhook and media endpoints must be reachable at the tenant's public base URL over HTTPS and WSS.

## Browser WebRTC Soft Phone

- Telnyx advertises `TelephonyAudioCapabilities.Browser` with the `telnyx-webrtc` adapter (the vendored `@telnyx/webrtc` SDK). The server mints a short-lived telephony credential on the SIP connection per session; credentials are capped per user, expire on their own, and are deleted at Telnyx on sign-out.
- Capabilities: `Dial`, `Hangup`, `Hold`, `Resume`, `Mute`, `Transfer`, `AttendedTransfer`, `Merge`, `SendDigits`, `Voicemail`, `ReceiveCalls`, `ExtensionDial`, `ExtensionConference`, and `BridgedDial`.
- **Bridged dialing**: a keypad dial rings the agent's own registered browser leg (auto-answered) and bridges it to the number on the server, so the call can be transferred, merged, and monitored. When no bridge is possible the provider returns `TelephonyConstants.ErrorCodes.BridgeUnavailable` without creating a leg and only then does the browser dial directly.
- Hold and mute run in the browser adapter; Telnyx never reports a call muted, so the soft phone keeps mute state itself.
- Outbound dials carry a Telnyx `command_id` for idempotency.
- **Leave** and **End for all** in a conference are honored through `RequestMetadata.ConferenceLeave` and `ConferenceEnd`.

### STUN and TURN

The `@telnyx/webrtc` SDK ships its own STUN and TURN relays.

- Leave **ICE URLs** empty to use them.
- A STUN-only list is ignored on purpose, because replacing the SDK defaults with STUN only causes one-way audio behind strict NAT.
- To use your own relay, include a `turn:` or `turns:` URL (with **TURN username** and **TURN credential** when required).
- Set **ICE transport policy** to `relay` to force TURN.

Telnyx does not mint ephemeral coturn credentials the way the Asterisk provider does.

### Noise Suppression

Choose an engine, which voices to clean, and a strength. Suppression starts on the agent's leg when the agent connects and lasts through hold, transfers, and conferences; a change applies to the next call. It is a billed Telnyx beta, and a refused command leaves the call running without it. The application log records lines such as `Telnyx noise suppression Krisp started on Agent leg ...`.

## Contact Center Adapter (Provider Side)

With Contact Center Voice enabled, `TelnyxContactCenterVoiceProvider` advertises `ServerSideAcd` and supports dialer dialing, agent connect (originate `sip:{agent}@sip.telnyx.com`, auto-answered, then `actions/bridge`), blind and consult transfers, supervisor listen, whisper, barge and takeover through Telnyx conferences, and recording. It also registers the inbound router, IVR and queue-treatment providers, the own-number source, and `TelnyxWebhookInboxHandler`. Routing, entry points, queues, supervision, and recording governance are described in `orchardcore-contact-center`.

Recording runs `record_start`, `record_pause`, `record_resume`, and `record_stop` under Contact Center governance. On `call.recording.saved` a durable ingest job streams the recording into the encrypted `IRecordingMediaStore` and then deletes the Telnyx-hosted copy; failures retry with backoff and dead-letter. Erased interactions are never re-ingested. Storage location is covered by `orchardcore-telephony`.

## Telnyx AI Voice Agent

`CrestApps.OrchardCore.Telnyx.AiVoice` registers the Phone-channel `VoiceOmnichannelProcessor`. An automated Phone activity dials the contact (`POST /v2/calls`, caller id from the activity's channel endpoint when set), speaks a greeting, and runs a speak-and-listen loop with Telnyx text-to-speech and real-time transcription driven by an AI chat profile, then settles the activity with a summary and a disposition chosen from the dispositions the subject's actions are wired to. A call where the customer never spoke takes the *No answer* outcome, and a call handed to a live agent is concluded by that agent. Inbound calls an entry point routes to an AI voice agent are answered by the same conversation.

The AI profile, speech deployments, voice, and reply delay are the automated voice settings on the subject flow (batch, then subject flow, then AI site settings). See `orchardcore-omnichannel-management` and `orchardcore-ai-chat`.

## Telnyx SMS

Two providers, like Orchard Core's Twilio pattern, both read through `IOptionsMonitor<TelnyxSmsOptions>`:

- **Configuration**. Section `OrchardCore_Sms_Telnyx` with `ApiKey`, `MessagingProfileId`, `WebhookPublicKey`, and `ApiBaseUrl`. The provider is enabled whenever `ApiKey` has a value.
- **UI**. The **Telnyx** tab of **Settings → Communication → SMS** (`/Admin/Settings/sms`). Values entered there win while that provider is enabled.

```json
{
  "OrchardCore": {
    "OrchardCore_Sms_Telnyx": {
      "ApiKey": "<telnyx-api-key>",
      "MessagingProfileId": "<messaging-profile-id>",
      "WebhookPublicKey": "<base64-ed25519-public-key>"
    }
  }
}
```

```text
OrchardCore__OrchardCore_Sms_Telnyx__ApiKey=<telnyx-api-key>
```

The source reads `OrchardCore_Sms_Telnyx` exactly; it does not use the Orchard Core 4.0 grouped `Sms:*` naming. In a tenant's own `App_Data/Sites/{tenant}/appsettings.json`, omit the `OrchardCore` wrapper. Set Telnyx as the default SMS provider on the SMS settings screen, and point the messaging profile webhook at `https://<your-site>/api/telnyx/webhook/sms`. See `orchardcore-sms` and `orchardcore-omnichannel-sms`.

## Upgrading from 2.x

- Telnyx replaces the removed Dialpad provider; set `TelephonySettings.DefaultProviderName` to `Telnyx`.
- Remove `CrestApps.OrchardCore.Telephony.Admin` from recipes; the Telnyx tab is registered by the Telnyx feature.
- Read `TelnyxOptions` through `IOptionsMonitor<TelnyxOptions>.CurrentValue`; `IOptions<TelnyxOptions>` keeps the first value and never sees a settings change.
- `TelnyxIvrProvider` and `TelnyxQueueTreatmentProvider` take `IVoiceMediaProvisioningService` instead of `IVoiceMediaItemManager`; resolve clips with `IVoiceMediaProvisioningService.ResolveAsync` rather than `VoiceMediaItem.MediaReference`.

## Troubleshooting

| Symptom | Check |
|---|---|
| Connect fails with `Not authorized` (10006) | Create the API key from an Owner or Admin member, or grant Voice and Connections permissions |
| Inbound calls never arrive | Webhook public key missing or wrong, number not assigned to the Call Control application, or `/api/telnyx/webhook/call` not publicly reachable over HTTPS |
| Soft phone socket closes with `1006` | **SIP WebSocket URL** must use port `7443` |
| One-way audio | Leave **ICE URLs** empty or include a TURN URL; a STUN-only list is ignored |
| Calls flagged as spam | Use a Telnyx-owned **Default outbound caller id** |
| Settings change not picked up by custom code | Switch from `IOptions<TelnyxOptions>` to `IOptionsMonitor<TelnyxOptions>` |
| SMS not received | Check the messaging profile webhook URL, `WebhookPublicKey`, and that the Telnyx SMS provider is enabled |
