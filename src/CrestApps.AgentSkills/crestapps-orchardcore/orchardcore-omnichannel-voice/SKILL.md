---
name: orchardcore-omnichannel-voice
description: Skill for the CrestApps Automated Voice module in Orchard Core, the provider-neutral AI voice conversation loop for automated phone activities and AI voice entry points. Covers the CrestApps.OrchardCore.Omnichannel.Voice feature and its dependencies, pairing it with Telnyx AI Voice Agent, automatic Phone activity loads, the voice settings cascade, realtime speech-to-speech versus the turn-based loop, silence, voicemail, handoff, the endCall tool, session-loss recovery, the voice-conclusion-analysis and voice-handoff-summary prompts, stranded call recovery, and adding automated voice to a telephony provider through IVoiceAgentMediaProvider, VoiceAgentEvent and IVoiceAgentConversationLoop. Use this skill when requests mention Orchard Core automated voice, AI phone calls, AI voice agent, outbound AI calling, or voice call conclusion. Strong matches include OmnichannelVoiceConstants, VoiceAgentConversationLoop, IVoiceAgentMediaProvider, EndCallTool, and StrandedVoiceCallRecoveryBackgroundTask.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Automated Voice

## Run AI Voice Conversations

You are an Orchard Core expert. Configure the Automated Voice feature so an AI
chat profile holds phone conversations for automated activities and AI voice
entry points, over whichever telephony provider carries the call.

### Guidelines

- Install `CrestApps.OrchardCore.Omnichannel.Voice` in the web/startup project.
  Enable `CrestApps.OrchardCore.Omnichannel.Voice` (**Automated Voice**). It
  depends on `CrestApps.OrchardCore.Telephony`, `CrestApps.OrchardCore.AI`,
  `CrestApps.OrchardCore.AI.Chat.Core` and
  `CrestApps.OrchardCore.Omnichannel.Managements`.
- The module supplies the conversation loop only. A provider feature supplies
  the audio and the `Phone` channel processor. Today that is **Telnyx AI Voice
  Agent** (`CrestApps.OrchardCore.Telnyx.AiVoice`), which depends on this
  feature and is covered by `orchardcore-telephony-telnyx`.
- Live speech-to-speech needs **Contact Center Voice Media**
  (`CrestApps.OrchardCore.ContactCenter.Voice.Media`) and a realtime deployment
  (the profile's **Conversation deployment**, see `orchardcore-ai-chat`).
  Without it, the turn-based loop speaks with text-to-speech and listens with
  transcription.
- Calls are screened for opt-outs, do-not-call registries and the calling
  window before dialing; the calling window needs the Contact Center Outbound
  Dialer (`orchardcore-contact-center`, `orchardcore-dnc-registry`).

## Enable via Recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Telnyx.AiVoice"
      ],
      "disable": []
    }
  ]
}
```

Enabling the provider feature enables Automated Voice and its dependencies.

## Outbound AI Calls

1. Build the CRM: contact type, subject with `OmnichannelSubjectPart`,
   dispositions, subject actions with **When to choose this disposition**, and a
   campaign (see `orchardcore-omnichannel-management`).
2. Create a Chat profile with **Start the conversation automatically**; its
   opening message is the call greeting. The **Phone calls** starting points
   (**Answer calls at the front desk**, **Qualify leads by phone**, **Confirm
   appointments by phone**) ship with this module (see
   `orchardcore-ai-profile-templates`).
3. Add the caller number as an omnichannel address with **Voice calls**.
4. Create an **Automatic** load with channel **Phone**, the address, the AI
   profile, reply delay and optionally **Play background office sound on
   calls** (realtime profiles only).
5. `AutomatedActivitiesProcessorBackgroundTask` dispatches due activities to the
   provider's `Phone` processor, which dials and starts the loop.

Voice settings (`AutomatedVoiceActivitySettings`: profile, speech-to-text and
text-to-speech deployments, voice, ambience, AI update permissions, reply delay,
business-hours calendar, cadence) resolve from the activity load, then the
subject's AI settings, then the global AI site settings.

## Inbound AI Calls

Set **Route to** to **AI voice agent** on a **Voice calls** inbound entry point
and pick a Chat profile (`orchardcore-contact-center`). Optionally add an
inbound **Phone** subject whose address is that number to supply dispositions
and the handoff rules. A silent inbound caller is never treated as voicemail.

## Call Behavior

- **Silence**: after 8 seconds of silence the AI asks whether the caller is
  still there, twice, then says goodbye and hangs up.
- **Voicemail**: when a machine answers (from the provider's answering machine
  detection, or recognized from the greeting), the AI waits for the tone,
  leaves one short message and hangs up; the activity takes the disposition with
  the *Answering machine* outcome.
- **Ending**: the always-on `endCall` tool (`EndCallTool`) lets the AI hang up
  when the conversation is over.
- **Handoff**: with **Live agent handoff** on the subject, the AI uses
  `transferToLiveAgent` to transfer to the handoff queue; the
  `voice-handoff-summary` prompt briefs the agent. A transferred call is not
  concluded by the AI.
- **Session loss**: a dropped realtime session is reopened with the transcript
  up to two times, then handed off if allowed, otherwise an apology ends the
  call. The activity takes the **Try Again** disposition and terminal reason
  `ai_session_lost`.
- **Conclusion**: the `voice-conclusion-analysis` prompt writes the summary and
  picks a disposition only from the subject's wired dispositions, based on what
  the customer said. A call nobody spoke on gets the *No answer* outcome; an
  undecided call gets the **Try Again** disposition, then *Disconnected*.
  `DispositionedBy` is `AIAgent`.
- **Shutdown**: when a node stops, live calls are ended and callers handed on
  (`AIVoiceCallShutdown`).
- **Stranded calls**: `StrandedVoiceCallRecoveryBackgroundTask` (**Stranded
  Automated Voice Call Recovery**, `*/5 * * * *`) concludes calls whose hangup
  the provider never reported.
- With AI Usage Analytics, each call leaves a voice session summary (talk time,
  silence, barge-ins, outcome).

## Add Automated Voice to a Telephony Provider

A provider integrates in three pieces, all in its own feature that depends on
`CrestApps.OrchardCore.Omnichannel.Voice`:

1. Implement `IVoiceAgentMediaProvider` (namespace
   `CrestApps.OrchardCore.Telephony.Services`): `TechnicalName`,
   `DefaultVoice`, `SpeakAsync`, `StartTranscriptionAsync`,
   `StopTranscriptionAsync`, `GatherAsync` and `HangupAsync`. Register it scoped;
   `IVoiceAgentMediaProviderResolver` finds it by technical name.
2. Register an `IOmnichannelProcessor` whose `Channel` is `Phone` that dials the
   contact for an automated activity (see `orchardcore-omnichannel`).
3. Translate the provider's call webhooks into `VoiceAgentEvent`
   (`Kind`: `Answered`, `SpeechStarted`, `SpeechEnded`, `Transcription`,
   `AnswererDetected`, `MachineGreetingEnded`, `Hangup`; plus `ProviderCallId`,
   `ProviderName`, `ActivityId`, `TranscriptionText`, `TranscriptionIsFinal`,
   `Answerer`, `HangupCause`) and pass them to
   `IVoiceAgentConversationLoop.HandleAsync`.

```csharp
using CrestApps.OrchardCore.Telephony.Services;

namespace MyCompany.OrchardCore.Telephony.Acme;

public sealed class AcmeVoiceAgentMediaProvider : IVoiceAgentMediaProvider
{
    public string TechnicalName => "Acme";

    public string DefaultVoice => "acme-neural-1";

    public Task<bool> SpeakAsync(string providerCallId, string text, string voice = null, string language = null, CancellationToken cancellationToken = default)
        => Task.FromResult(true); // Call the provider's text-to-speech command.

    public Task<bool> StartTranscriptionAsync(string providerCallId, string language = null, string commandId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> StopTranscriptionAsync(string providerCallId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> GatherAsync(string providerCallId, string text, string validDigits, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> HangupAsync(string providerCallId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
```

```csharp
services.AddScoped<IVoiceAgentMediaProvider, AcmeVoiceAgentMediaProvider>();
```

Use the Telnyx provider (`TelnyxVoiceAgentMediaProvider`,
`TelnyxAiVoiceConversationHandler`, `VoiceOmnichannelProcessor`) as the
reference. For bidirectional audio and realtime sessions, the provider must
also integrate with Contact Center Voice Media.

## Upgrading from 2.x

- AI calling requires both this provider-neutral feature and a provider's AI
  voice feature (Telnyx AI Voice Agent enables it for you). Dialpad is removed.
- Telephony provider options are read live through
  `IOptionsMonitor<TelephonyProviderOptions>`; custom code that read them with
  `IOptions<T>` must switch (see `orchardcore-telephony`).
