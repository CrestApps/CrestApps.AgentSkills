---
name: orchardcore-ai-realtime-voice
description: Skill for realtime speech-to-speech voice in Orchard Core with the CrestApps AI Chat and AI Chat Interactions modules. Covers deployments that declare the realtime capability, the realtime slot, Conversation chat mode with the Conversation deployment (ConversationDeploymentName on profiles and chat interactions), the resolution order and the speech-to-text plus text-to-speech fallback, the removal of ChatMode.Realtime and RealtimeDeploymentName, cascaded realtime deployments with the CascadedRealtime provider and CascadedRealtimeMetadata, WebRTC with WebSocket fallback, RealtimeTransportOptions (turn detection, session limits, knowledge grounding, STUN and TURN), per-tenant Cloudflare TURN credentials, realtime voices, and the realtime hub methods. Use this skill when requests mention Orchard Core realtime voice, speech-to-speech, WebRTC, TURN, barge-in, or cascaded realtime. Strong matches include IRealtimeOrchestrator, RealtimeTransportOptions, CloudflareTurnOptions, and StartRealtimeWebRtc.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Realtime Voice - Prompt Templates

## Configure Realtime Voice

You are an Orchard Core expert. Generate configuration and recipes for live spoken AI conversations in the CrestApps AI Chat and AI Chat Interactions surfaces.

### Guidelines

- Realtime voice is part of `CrestApps.OrchardCore.AI.Chat` (AI Chat sessions and widgets) and `CrestApps.OrchardCore.AI.Chat.Interactions`. Both register the WebRTC transport (`AddWebRtcRealtimeTransport`, package `CrestApps.Core.AI.WebRTC`) and per-tenant Cloudflare TURN (`AddTenantCloudflareRealtimeTurn`). No extra feature is needed.
- A realtime deployment is one whose model declares the `realtime` capability in `Properties.AIDeploymentMetadata.Features`, or a **Cascaded Realtime** deployment. See `orchardcore-ai-model-capabilities`.
- One chat thread carries both typed and spoken turns. In **Conversation** chat mode the surface shows a voice toggle beside a working message box.
- The **Chat deployment** is only the text model. It lists text-capable deployments and answers typed messages, including those typed during a voice session. The speech-to-speech model goes in the **Conversation deployment**.
- How a conversation is carried is resolved, never stored. There is no per-profile switch back to the cascade.
- Tools, data sources, the system message and turn persistence all apply to the spoken conversation, because turns run through `IRealtimeOrchestrator`.
- Keep TURN secrets and Cloudflare tokens in a secret store, not in committed configuration.
- Recipe JSON uses `{ "steps": [...] }`.

### Resolution Order

For a surface in **Conversation** mode:

1. the conversation deployment named on the profile or interaction;
2. otherwise the site's default realtime deployment (**Settings → Artificial Intelligence → Default Deployments**);
3. otherwise the first deployment whose model declares `realtime`;
4. otherwise the client-driven speech-to-text plus text-to-speech cascade, which needs both deployments;
5. otherwise microphone dictation, then plain typing.

Because step 3 accepts any realtime-capable deployment, a site with one uses it even with nothing named. To keep the cascade, have no deployment declare `realtime`. A named conversation deployment that cannot run realtime is reported rather than silently replaced.

### AI Profile in Conversation Mode

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "Deployments": [
        {
          "Name": "gpt-realtime",
          "ClientName": "OpenAI",
          "ConnectionName": "openai",
          "Properties": {
            "AIDeploymentMetadata": {
              "Features": [ "realtime" ]
            }
          }
        }
      ]
    },
    {
      "name": "AIProfile",
      "Profiles": [
        {
          "Name": "voice-concierge",
          "DisplayText": "Voice Concierge",
          "Type": "Chat",
          "ChatDeploymentName": "gpt-4o",
          "Settings": {
            "ChatModeProfileSettings": {
              "ChatMode": "Conversation",
              "ConversationDeploymentName": "gpt-realtime",
              "VoiceName": "alloy"
            }
          }
        }
      ]
    }
  ]
}
```

- Leave `ConversationDeploymentName` empty to inherit the site default. The same field names the model of phone calls the profile answers or places, whatever its chat mode.
- The **Voice** picker lists the resolved realtime model's voices, or the text-to-speech deployment's when the cascade carries the conversation. Without a voice, the persona voice (see `orchardcore-ai-personas`), then the default voice, then the provider default is used.
- Profile templates use `ProfileTemplateMetadata.ConversationDeploymentName`; naming one puts the generated profile in Conversation mode unless the template sets its own chat mode.

### Chat Interactions

An interaction has no chat mode. Set the **Chat Interactions** site setting `ChatInteractionChatModeSettings.ChatMode` to `Conversation`, then name a **Conversation deployment** on the interaction (`ChatInteraction.ConversationDeploymentName`); naming one is the opt-in to speak. See `orchardcore-ai-chat-interactions`.

### Cascaded Realtime

When no provider ships a speech-to-speech model, create a deployment under the **Cascaded Realtime** provider (`ClientName` `CascadedRealtime`, registered by `CrestApps.OrchardCore.AI`) that chains three deployments, which may come from different providers:

```
mic -> speech-to-text -> chat (tools, data sources) -> text-to-speech -> speaker
```

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "Deployments": [
        {
          "Name": "cascaded-voice",
          "ClientName": "CascadedRealtime",
          "Properties": {
            "AIDeploymentMetadata": {
              "Features": [ "realtime" ]
            },
            "CascadedRealtimeMetadata": {
              "SpeechToTextDeploymentName": "scribe",
              "ChatDeploymentName": "gpt-4o",
              "TextToSpeechDeploymentName": "eleven-tts"
            }
          }
        }
      ]
    }
  ]
}
```

- The speech-to-text deployment must itself declare `realtime` (it transcribes continuously). All three names are required.
- The cascade owns no connection; the chained deployments carry the credentials. It declares `realtime` itself and its **Voice** picker lists the text-to-speech deployment's voices.
- Expect more delay before the first word. Replies are spoken a sentence at a time, and barge-in is driven by the first partial transcript.

### Transport and Configuration

Audio uses **WebRTC** when available and falls back to **WebSocket** (PCM over SignalR) automatically at connect time. The browser peers with the application's hub, not the provider, so any realtime provider works. Transcripts and errors stay on SignalR.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "RealtimeTransport": {
          "EnableWebRtc": true,
          "TurnDetectionType": "semantic_vad",
          "TurnDetectionEagerness": "auto",
          "IdleTimeoutSeconds": 30,
          "MaxSessionDurationSeconds": 300,
          "EnableKnowledgeGrounding": true,
          "StunUrls": [ "stun:stun.l.google.com:19302" ],
          "TurnUrls": [ "turns:turn.example.com:5349" ],
          "TurnSecret": "<coturn-shared-secret>",
          "TurnCredentialTtlSeconds": 3600,
          "IceTransportPolicy": "All"
        }
      }
    }
  }
}
```

| Key | Purpose |
|---|---|
| `EnableWebRtc` | Offer WebRTC (default `true`). Turn off on hosts with no inbound UDP and no TURN, or every session waits out the connect timeout |
| `TurnDetectionType` | `semantic_vad` (default) or `server_vad`; a deployment that rejects semantic detection is switched automatically |
| `TurnDetectionEagerness` | `low`, `medium`, `high` or `auto` (default) |
| `IdleTimeoutSeconds` | End a session with no user speech (default `30`) |
| `MaxSessionDurationSeconds` | Hard session ceiling (default `300`); the chat UI shows a distinct "limit reached" message |
| `EnableKnowledgeGrounding` | Retrieve knowledge before the model replies (default `true`) |
| `GroundingAcknowledgementDelayMs` | Delay before one spoken filler while retrieval runs (default `700`) |
| `GroundingResponseWatchdogSeconds` | Answer anyway after this wait (default `15`) |
| `StunUrls`, `TurnUrls` | ICE servers; TURN is needed behind strict NATs or blocked UDP |
| `TurnSecret`, `TurnCredentialTtlSeconds` | coturn `use-auth-secret` ephemeral credentials (recommended) |
| `TurnUsername`, `TurnCredential` | Static TURN credentials, used only without `TurnSecret` |
| `IceTransportPolicy` | `All` (default) or `Relay` |

The section is read through `IShellConfiguration`, so each tenant can set its own values. A tenant that declares nothing inherits the host's values; one that declares `StunUrls` or `TurnUrls` replaces the host list instead of appending. In a tenant's own `App_Data/Sites/{tenant}/appsettings.json`, start at `CrestApps` without the `OrchardCore` wrapper.

### Cloudflare Realtime TURN

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "RealtimeTransport": {
          "Cloudflare": {
            "TokenId": "<turn-token-id>",
            "ApiToken": "<api-token>",
            "TtlSeconds": 86400
          }
        }
      }
    }
  }
}
```

Cloudflare issues short-lived TURN credentials through an API (`CloudflareTurnOptions`, section `CrestApps:AI:RealtimeTransport:Cloudflare`). Credentials are refreshed halfway through `TtlSeconds` (default `86400`), which is not a session limit. Each tenant can use its own Cloudflare account. Without a token, or during a Cloudflare outage, the configured `StunUrls` and `TurnUrls` are used.

### Hub Methods and Endpoints

| Method | AIChatHub | ChatInteractionHub |
|---|---|---|
| Start over WebSocket | `StartRealtimeConversation(profileId, sessionId, audioChunks, voice, language, ...)` | `StartRealtimeConversation(itemId, audioChunks, voice, language, ...)` |
| Start over WebRTC | `StartRealtimeWebRtc(profileId, sessionId, offerSdp, ...)` | `StartRealtimeWebRtc(itemId, offerSdp, ...)` |
| ICE | `GetRealtimeIceServers`, `AddRealtimeIceCandidate` | Same |
| Live settings | `UpdateRealtimeSettings(allowInterruption, ...)`, `UpdateRealtimeSpeechSpeed(speed)` | Same |
| Stop | `StopConversation` | Same |

`~/CrestApps.OrchardCore.AI.Chat/Realtime/Voices?deploymentName=` returns the voices of a realtime deployment (empty name resolves the site default). The browser side is the vendored `realtime-audio` script from `@crestapps/ai-chat-ui`; its settings popover holds only per-device preferences (microphone, speaker, volume, language, **Allow interruptions**, **Push-to-talk**).

### Upgrading from 2.x

- `ChatMode.Realtime` is removed. A stored `"Realtime"` chat mode reads as `TextInput`. Set `ChatMode` to `Conversation` and name the model in `ConversationDeploymentName`.
- The separate realtime deployment field is removed. A profile that named `RealtimeDeploymentName`, or used a speech-to-speech model as its chat deployment, is shown with that model as its **Conversation deployment** and Conversation mode when read; saving persists the new shape. `ProfileTemplateMetadata.RealtimeDeploymentName` is obsolete but still supplies `ConversationDeploymentName`.
- Speech-to-speech models no longer appear in the chat deployment picker.
- Realtime deployments are found by the `realtime` capability, not by a purpose.

### Troubleshooting

- **Always WebSocket**: UDP blocked and no TURN; add `TurnUrls` with credentials or Cloudflare TURN, or set `EnableWebRtc` to `false` to skip the wait.
- **Conversation uses an unexpected model**: some deployment declares `realtime` and step 3 picked it; name the conversation deployment or set the site default.
- **Sessions end after five minutes**: raise `MaxSessionDurationSeconds`.
- **Long silent pauses before answers**: tune `GroundingAcknowledgementDelayMs`, or set `EnableKnowledgeGrounding` to `false`.
