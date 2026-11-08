---
name: crestapps-core-realtime-voice
description: Skill for realtime speech-to-speech voice in CrestApps.Core 2.0. Covers the realtime deployment capability, Conversation chat mode and ResolveConversationModeAsync, cascaded realtime deployments with CascadedRealtimeMetadata chaining speech-to-text (including Cartesia), chat and text-to-speech legs, profile speech settings in a cascade, the WebRTC server-relay transport (AddWebRtcRealtimeTransport) with WebSocket fallback, RealtimeTransportOptions, TURN and IRealtimeIceServerProvider, IRealtimeOrchestrator and IRealtimeConversation, switching profile during a call with SwitchResourceAsync and UpdateSessionAsync, keypad keys and supervisor notes with AddMessageAsync, voices the deployment does not offer, user transcription, the realtime hub contract, the CoreAIRealtime browser client, and upgrading. Use it when adding voice to a chat surface, configuring TURN, building a cascade, handing a call to another profile, or troubleshooting silent sessions, fallbacks, or missing transcripts.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Realtime Voice

A realtime session is a live, spoken conversation: audio in, audio out, with barge-in. It runs through the same orchestration as text chat, so the profile's or interaction's system message, persona, tools, data sources and turn persistence all apply. Audio travels over **WebRTC** (server relay) when available and falls back to **WebSocket** (PCM over SignalR) automatically.

## Guidelines

- Realtime is a model capability, not a chat mode. Declare the `realtime` feature on the deployment; it then fills the `realtime` slot. Never add `textGeneration` to a speech-to-speech-only model.
- A profile speaks when its chat mode is `Conversation` and a realtime deployment resolves. A chat interaction speaks when `ConversationDeploymentName` is set.
- Providers with a native speech-to-speech client in CrestApps.Core are OpenAI and Azure OpenAI (`gpt-realtime`). For anything else, build a cascaded realtime deployment. Cartesia's realtime client only transcribes, so it can hear for a cascade but not carry a session.
- Register `AddWebRtcRealtimeTransport()` for production voice, and configure TURN so users behind strict NATs are not pushed to WebSocket after an 8-second wait.
- Add a speech-to-text deployment on the realtime deployment's provider (for example `gpt-4o-transcribe`), or the user's turns are not transcribed.
- Keep a voice profile's tool list short; every tool sits in the model's context on every turn.
- The chat hubs and UI are covered by `crestapps-core-chat-interactions`, hub options by `crestapps-core-signalr`, provider configuration by `crestapps-core-openai` and `crestapps-core-azure-openai`, and ElevenLabs, Cartesia, the voice library and speech markup by `crestapps-core-speech-voices`.
- Profile speech settings (`AIProfileSpeechSettings`) and typed or list extraction fields are documented with the profile in `crestapps-core-ai-profiles`.

## Choosing the Model That Carries the Conversation

How a conversation is carried is resolved, never stored:

1. the conversation deployment named on the profile (`ChatModeProfileSettings.ConversationDeploymentName`) or the interaction (`ChatInteraction.ConversationDeploymentName`);
2. otherwise the site default, `DefaultAIDeploymentSettings.DefaultRealtimeDeploymentName`;
3. otherwise the first deployment that declares `realtime`.

If none resolves, `Conversation` mode falls back to the client-driven speech-to-text plus text-to-speech cascade (needs both slot defaults), then to dictation, then to typing. Because step 3 accepts any realtime-capable deployment, a site that has one uses it for every `Conversation` profile; to keep the client cascade, have no deployment declare `realtime` or carry `CascadedRealtimeMetadata`. A named conversation deployment that is not realtime-capable is reported (`ConversationModeResolution.IsMisconfigured`) instead of being silently replaced.

The views and hubs share one resolver, `IAIDeploymentManager.ResolveConversationModeAsync(configuredChatMode, conversationDeploymentName, chatDeploymentName, hasSpeechToText, hasTextToSpeech, chatModeIsSiteWide)` (namespace `CrestApps.Core.AI.Deployments`). Use it to decide whether to render the voice toggle. `IAIDeploymentCapabilityService.IsRealtimeDeploymentAsync(name)` is the single "is this deployment realtime" check.

```json
{
  "CrestApps": {
    "AI": {
      "Deployments": [
        {
          "ClientName": "OpenAI",
          "ConnectionName": "openai",
          "Name": "gpt-realtime",
          "Features": [ "realtime" ]
        },
        {
          "ClientName": "OpenAI",
          "ConnectionName": "openai",
          "Name": "gpt-4o-transcribe",
          "Features": [ "speechToText" ]
        }
      ]
    }
  }
}
```

## Cascaded Realtime Deployments

A cascade chains three existing deployments into one `IRealtimeClient`, possibly from different vendors:

```text
mic -> realtime speech-to-text -> chat (tools, data sources) -> text-to-speech -> speaker
```

```csharp
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Realtime;

// The source is a registered deployment provider name. A cascade needs no connection of its own.
var deployment = await deploymentManager.NewAsync("cascaded-voice", "OpenAI");

deployment.ModelName = "cascaded-voice";

deployment.Alter<CascadedRealtimeMetadata>(cascade =>
{
    cascade.SpeechToTextDeploymentName = "gpt-4o-transcribe";
    cascade.ChatDeploymentName = "gpt-4.1";
    cascade.TextToSpeechDeploymentName = "azure-tts";
    cascade.SpeechSpeed = 1.2;
    cascade.SpeakingStyle = "Speak at a brisk, natural phone pace.";
});

deployment.Alter<AIDeploymentMetadata>(metadata =>
{
    metadata.Features = [AIDeploymentFeatureNames.Realtime];
});

var validation = await deploymentManager.ValidateAsync(deployment);

if (!validation.Succeeded)
{
    throw new InvalidOperationException("The cascaded deployment is not valid.");
}

await deploymentManager.CreateAsync(deployment);
```

A configured deployment can carry the same metadata under `Properties`, keyed by type name: `"Features": [ "realtime" ]` plus `"Properties": { "CascadedRealtimeMetadata": { "SpeechToTextDeploymentName": "...", "ChatDeploymentName": "...", "TextToSpeechDeploymentName": "..." } }`.

- `IAIClientFactory.CreateRealtimeClientAsync` composes `CascadedRealtimeClient` whenever the deployment carries a complete `CascadedRealtimeMetadata`; providers never see the cascade. A leg that is itself cascaded, or a leg name that does not exist, throws.
- **Speech-to-text leg**: must expose a realtime client with a transcription session: an OpenAI or Azure OpenAI transcription deployment, or a Cartesia deployment on `ink-2` or `ink-preview` (Cartesia's turns endpoint decides where each user turn ends; `ink-whisper` or no model hears with `ink-2`). It transcribes with its own model. Azure AI Speech has no realtime client, and ElevenLabs transcribes whole recordings only (its realtime client throws `NotSupportedException`), so neither can be this leg.
- **Chat leg**: built with function invocation, so tools, data sources and the system message work; it must declare `toolCalling` or the tools are stripped. A reply's tool calls and results stay in the cascade's history; an interrupted, unanswered call is left out. A tool call ends the line being spoken: the text before the call is finished as a line of its own, and what the model says after the tool's answer is a new line of the same response, as a speech-to-speech provider finishes its message item when the model calls a function.
- **Text-to-speech leg**: must return raw PCM (`audio/L16` or `audio/pcm`) at the session's output rate; the cascade asks for `Pcm<rate>` (for example `Pcm24000`) and refuses MP3 or WAV. OpenAI (resampled from 24 kHz), Azure AI Speech (`Pcm8000` to `Pcm48000`), ElevenLabs and Cartesia all qualify. The voice picker lists this leg's voices.
- `SpeechSpeed` is sent as `TextToSpeechOptions.Speed` (OpenAI 0.25 to 4, Azure Speech 0.5 to 2); `SpeakingStyle` as the `TextToSpeechOptionsProperties.Instructions` property (ignored by `tts-1`, `tts-1-hd` and Azure Speech). A listener's speed multiplies on top.
- Replies are spoken a sentence at a time; a partial transcript while the assistant speaks cancels the turn and flushes client audio. Latency is higher than a native model.
- `response.create` (`RequestUnpromptedResponseAsync`) is written and spoken by the cascade, so an outbound call can open with a greeting. When the assistant spoke last (system items added after its line do not count), the chat leg ends the history with a short user turn, because some models (Claude 5.x) refuse to continue an assistant message. Instructions passed with the request replace the session's for that reply only. When grounding is on, utterances join the history and are answered once the host requests a reply.

### Profile Speech Settings in a Cascade

When the session's resource is an `AIProfile` with `AIProfileSpeechSettings`, `DefaultRealtimeOrchestrator` hands the cascade a `CascadedRealtimeProfileOptions` built with `CascadedRealtimeProfileOptions.From(settings, fallbackClient)`:

| Setting | Effect in the cascade |
|---|---|
| `SpeakingRate` | Clamped to 0.25 to 4; multiplies `CascadedRealtimeMetadata.SpeechSpeed` |
| `SpeakingEmotion` | Sent as `TextToSpeechOptionsProperties.Emotion`; Cartesia uses it, other providers ignore it |
| `PronunciationEntries` | Applied to the text before synthesis, whole words, longest phrase first |
| `FallbackTextToSpeechDeploymentName` | Speaks when the text-to-speech leg throws before producing audio |
| `BoostedKeywords` | Added to the transcription request and prompt (`Vocabulary: a, b.`) |
| `RecognitionLanguages` | The first is the speech language when `RealtimeOrchestrationRequest.SpeechLanguage` is not set |
| `InterruptionSensitivity` | Words of caller speech that cut the assistant off (`0` never; unset one word); on a native session it sets the VAD threshold |
| `EnableBackchanneling` / `BackchannelPhrases` | A short spoken "mm-hm" while the caller talks; cascade only, never stored |

An explicit `VadThreshold` or `AllowInterruption = false` on the request wins over the profile.

## Messages That Do Not Come from the Microphone

`IRealtimeConversation.AddMessageAsync(role, text)` adds a user message (keys pressed on a phone keypad) or a system message (a supervisor's note) to the live conversation without asking for a reply. To have it answered now, follow it with `RequestUnpromptedResponseAsync()` with no instructions, once no response is in flight:

```csharp
await conversation.AddMessageAsync(ChatRole.User, "[Pressed on the keypad: 1234]", cancellationToken);
await conversation.RequestUnpromptedResponseAsync(cancellationToken: cancellationToken);
```

Do not pass such a message as a response's instructions: those replace the session's instructions for that response, so the assistant loses its persona and language, and the message is gone by the next turn. Speech-to-speech providers receive a `conversation.item.create`; a cascade adds it to the chat leg's history. The default interface implementation throws `NotSupportedException`; `DefaultRealtimeConversation` and cascaded deployments support it.

## Switching Profile During a Call

```csharp
// Inside the same AIInvocationScope the call was started in.
await orchestrator.SwitchResourceAsync(
    conversation,
    new RealtimeOrchestrationRequest { Resource = otherProfile },
    cancellationToken);
```

`IRealtimeOrchestrator.SwitchResourceAsync` builds the new profile's system message and tools as `StartAsync` does and applies them with `IRealtimeConversation.UpdateSessionAsync(new RealtimeSessionUpdate { Instructions = ..., Tools = ... })`. A host that has its own instructions and tools can call `UpdateSessionAsync` directly; `null` leaves that part unchanged and an empty tool list removes every tool.

- The change applies from the next response; a response in progress finishes as it started. What was said stays in the conversation.
- Only the new tools run: a call to a tool the previous profile offered is refused.
- Speech-to-speech providers get a partial `session.update`; a cascade swaps its chat leg's options for the next reply.
- The voice, realtime deployment and turn detection do not change; the request's `Voice` and `RealtimeDeploymentName` are ignored.
- `IRealtimeConversation.SupportsSessionUpdate` says whether the conversation can do this; the default implementations of `UpdateSessionAsync` and `SwitchResourceAsync` throw `NotSupportedException`.

## Voice Selection

The voice reaches the provider in the same set-up message as the instructions and tools, and a provider refuses that message whole over one value it does not accept. `DefaultRealtimeOrchestrator` therefore checks the requested voice against `IRealtimeVoiceResolver.GetVoicesAsync(deployment)`: a voice the deployment does not list is dropped with a warning and the session opens with the provider's default voice. A cascaded deployment's voice, and any voice when the list is empty or cannot be read, is passed on unchecked. Keep the profile's voice one of the realtime deployment's voices, or none.

## Transport Registration

```csharp
builder.Services.AddWebRtcRealtimeTransport();
```

`AddWebRtcRealtimeTransport()` (package `CrestApps.Core.AI.WebRTC`, namespace `Microsoft.Extensions.DependencyInjection`) registers the SIPSorcery peer factory (`IWebRtcRealtimePeerFactory`, Opus through Concentus) and binds `RealtimeTransportOptions` to `CrestApps:AI:RealtimeTransport`. It is idempotent. Without it voice still works, over WebSocket only, and `RealtimeTransportOptions` keeps its defaults unless the host binds the section itself:

```csharp
builder.Services.AddOptions<RealtimeTransportOptions>().BindConfiguration("CrestApps:AI:RealtimeTransport");
```

Both chat hubs (`AIChatHubCore<TClient>` and `ChatInteractionHubBase`) support WebRTC. `RealtimeTransportSettings.IsWebRtcEnabled(services)` is what a view uses to advertise it (`data-coreai-chat-realtime-webrtc-enabled`). The hubs must run with `ConfigureCrestAppsChatHubOptions<THub>()`; with one parallel invocation per client the WebRTC handshake deadlocks.

The browser peers with the hub, not the provider; the hub keeps talking to the provider over WebSocket, so WebRTC works with any realtime provider. Audio crosses the boundary as PCM16 at 24 kHz mono. Transcripts, errors and lifecycle events stay on SignalR.

## Configuration

```json
{
  "CrestApps": {
    "AI": {
      "RealtimeTransport": {
        "EnableWebRtc": true,
        "IdleTimeoutSeconds": 30,
        "MaxSessionDurationSeconds": 300,
        "TurnDetectionType": "semantic_vad",
        "TurnDetectionEagerness": "auto",
        "EnableKnowledgeGrounding": true,
        "GroundingAcknowledgementDelayMs": 700,
        "GroundingResponseWatchdogSeconds": 15,
        "StunUrls": [ "stun:turn.example.com:3478" ],
        "TurnUrls": [ "turn:turn.example.com:3478", "turns:turn.example.com:5349" ],
        "TurnSecret": "the-same-secret-configured-in-coturn",
        "TurnCredentialTtlSeconds": 3600,
        "IceTransportPolicy": "All"
      }
    }
  }
}
```

| Option | Default | Meaning |
|---|---|---|
| `EnableWebRtc` | `true` | Turn off on hosts with no inbound UDP and no reachable TURN, so sessions do not wait out the connect timeout |
| `IdleTimeoutSeconds` | `30` | Ends a session after this long with neither side speaking (`session_ended: idle`); queued playback and an uncommitted utterance (up to 2 minutes) hold it off. `0` disables |
| `MaxSessionDurationSeconds` | `300` | Hard cap per session (`session_ended: max_duration`). `0` disables |
| `TurnDetectionType` / `TurnDetectionEagerness` | `semantic_vad` / `auto` | `server_vad` is a silence timer; a deployment that rejects semantic detection is switched to server VAD |
| `EnableKnowledgeGrounding` | `true` | Per-turn retrieval for voice; also requires preemptive RAG to be enabled |
| `GroundingAcknowledgementDelayMs` | `700` | After this, the assistant says a short out-of-band "let me look that up" line. `0` never speaks one |
| `GroundingResponseWatchdogSeconds` | `15` | Requests a reply anyway when a committed turn goes unanswered. `0` disables |
| `StunUrls` | public Google STUN when empty | STUN servers |
| `TurnUrls` + `TurnSecret` + `TurnCredentialTtlSeconds` | | coturn `use-auth-secret` ephemeral credentials (username is a UNIX expiry, credential is Base64 HMAC-SHA1) |
| `TurnUsername` / `TurnCredential` | | Static TURN credentials, used only without `TurnSecret`; unsuitable for hosted TURN whose credentials expire |
| `IceTransportPolicy` | `All` | `Relay` forces TURN-only candidates |

`TurnUrls` without a secret or static credentials produce no TURN entry.

### Cloudflare Realtime TURN

```csharp
using CrestApps.Core.AI.Chat;

builder.Services.AddCloudflareRealtimeTurn();
```

```json
{
  "CrestApps": {
    "AI": {
      "RealtimeTransport": {
        "Cloudflare": {
          "TokenId": "<TURN Token ID>",
          "ApiToken": "<API token>",
          "TtlSeconds": 86400
        }
      }
    }
  }
}
```

Credentials are minted on demand and reused until half their lifetime. While `TokenId`/`ApiToken` are unset, the configured STUN/TURN servers apply; while Cloudflare is configured they are ignored. If Cloudflare is unreachable, already issued credentials keep being served.

### Any other TURN service

Both the browser's list and the server peer resolve ICE servers through `IRealtimeIceServerProvider`. See [realtime-voice-examples.md](references/realtime-voice-examples.md) for a complete provider, a direct `IRealtimeOrchestrator` session, and the client wiring.

## Transcribing the User

The user's side of the transcript (history, grounding, post-call analysis) comes from a separate model. The orchestrator picks, in order: `RealtimeOrchestrationRequest.InputTranscriptionModel` (empty string disables it); a cascade's own speech-to-text leg; the `speechToText` slot deployment when it is on the realtime deployment's provider; the first speech-to-text deployment on that provider. With none, a warning is logged and grounding falls back to the search tool. For OpenAI, `gpt-4o-transcribe` is the accurate choice; `whisper-1` invents phrases from short sounds.

## Client Contract

The hubs own the session. Interaction hub methods take `itemId`; `AIChatHubCore` takes `profileId` and `sessionId`:

| Hub method | Purpose |
|---|---|
| `GetRealtimeIceServers()` | Called right before the browser creates its `RTCPeerConnection`, so TURN credentials are fresh |
| `StartRealtimeWebRtc(itemId, offerSdp, voice, language, silenceDurationMs, vadThreshold, allowInterruption)` | WebRTC session; the answer arrives on `ReceiveRealtimeAnswer(sdp)` |
| `AddRealtimeIceCandidate(candidate, sdpMid, sdpMLineIndex)` | Trickled ICE; server candidates arrive on `ReceiveRealtimeIceCandidate` |
| `StartRealtimeConversation(itemId, audioChunks, voice, language, silenceDurationMs, vadThreshold, allowInterruption)` | WebSocket session; `audioChunks` is a stream of base64 PCM16 24 kHz frames, assistant audio returns as `ReceiveAudioChunk(id, base64, "audio/pcm")` |
| `UpdateRealtimeSettings(allowInterruption, silenceDurationMs, vadThreshold)` | Applies barge-in and turn detection to the running session (browser gate, server pump and provider) |
| `UpdateRealtimeSpeechSpeed(speed)` | Speaking speed from the next reply (next sentence on a cascade) |
| `StopConversation()` | Ends the session |

`ReceiveRealtimeEvent(identifier, type, payload)` carries `RealtimeClientEventTypes`: `speech_speed`, `session_ready`, `speech_started`, `playback_flush`, `user_turn_pending`, `user_turn_dropped`, and `session_ended` with a `RealtimeSessionEndReasons` value (`completed`, `cancelled`, `error`, `idle`, `max_duration`). Transcripts use `ReceiveTranscript`, `ReceiveConversationUserMessage`, `ReceiveConversationAssistantToken` and `ReceiveConversationAssistantComplete`.

The shared browser client is `realtime-audio.js` (`@crestapps/ai-chat-ui`, global `CoreAIRealtime`). `CoreAIRealtime.attach({ connection, sendStart, sendStartWebRtc, webRtcEnabled, voiceName, selectors, onSessionStateChanged, ... })` returns a controller with `start`, `stop`, `toggle`, `isActive`, `getState`, `getGateLevel` and `getTransportStats`. It handles transport selection and fallback, the AudioWorklet microphone gate with learned echo level, playback, and the short settings popover (microphone, speaker, volume, speaking speed, language, interruptions, push-to-talk). A host that keeps typing available between sessions passes only `realtimeButton` and `micButton` in `selectors` and drives its own controls from `onSessionStateChanged(active)`.

## Troubleshooting

- **Session sits at Listening and never answers**: check that the provider reports user speech (a warning is logged when it never does), the microphone gate level (`getGateLevel()`), and that turn detection is supported.
- **Every user falls back to WebSocket after about 8 seconds**: no TURN relay, blocked UDP, or the hub is missing per-hub options. Configure TURN, or set `EnableWebRtc` to `false`.
- **The assistant answers itself**: use a headset or turn off interruptions (half duplex); the gate learns the echo level during the first reply.
- **Knowledge questions answered as "not available"**: the chat deployment (or cascade chat leg) lacks `toolCalling`, the vector store connection is not configured, or grounding is off. Raise `CrestApps.Core.AI.Services.DefaultRealtimeTurnGrounding` and `CrestApps.Core.AI.Chat.Realtime` to `Debug`.
- **"resolved N tool(s), above the 30 a session carries well"**: curate the profile's tools or select fewer MCP tools; the session was trimmed to about 20 at open.
- **Choppy playback**: read `CoreAIRealtime.activeController.getTransportStats()` and the peer's closing log line under `CrestApps.Core.AI.WebRTC`.

## Upgrading Within 2.0 Previews

- `DefaultRealtimeOrchestrator` takes `IRealtimeVoiceResolver` as a new constructor dependency (no change when resolved from the container). A voice the native realtime deployment does not list is now dropped, so a session that used to fail its set-up now opens with the provider's default voice.
- A cascaded reply that calls a tool now produces two finished assistant lines (before and after the tool call) instead of one. Hosts that read "what was said" from finished lines see a goodbye spoken before an end-call tool as said.
- Earlier previews described ElevenLabs as a possible cascade speech-to-text leg, which never worked; use Cartesia (`ink-2`), OpenAI or Azure OpenAI for that leg and keep ElevenLabs for speaking.
- Custom `IRealtimeConversation` implementations inherit throwing defaults for `AddMessageAsync` and `UpdateSessionAsync` (`SupportsSessionUpdate` is `false`); override them to support keypad input and profile switching.

## Upgrading from 1.x

- `ChatMode.Realtime` and `AIProfile.RealtimeDeploymentName` are removed. Set `ChatMode.Conversation` and, optionally, `ChatModeProfileSettings.ConversationDeploymentName`; on interactions set `ConversationDeploymentName`. Stored profiles are folded when read, and `ProfileTemplateMetadata.RealtimeDeploymentName` is `[Obsolete]` but still supplies the deployment.
- A profile in `Conversation` mode now prefers any resolvable realtime deployment over the speech-to-text plus text-to-speech cascade.
- `RealtimeSessionConfiguratorContext.InputTranscriptionModel` no longer defaults to `whisper-1`; add a speech-to-text deployment on the realtime provider.
- `DefaultRealtimeVoiceResolver` takes an extra `IAIDeploymentStore` constructor argument (no change when resolved from the container).
- `IRealtimeConversation.RequestStandaloneMessageAsync(instructions)` is new; its default implementation falls back to `RequestUnpromptedResponseAsync`, so override it to get the out-of-band, tools-off behavior.
- No voice is substituted when none is chosen; the provider's default voice is used.
- The voice settings popover was simplified; presets, the echo self-test, gate modes and turn-detection sliders are gone.
