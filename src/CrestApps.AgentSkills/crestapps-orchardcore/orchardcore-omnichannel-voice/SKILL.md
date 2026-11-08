---
name: orchardcore-omnichannel-voice
description: Skill for the CrestApps Automated Voice module in Orchard Core, the provider-neutral AI voice conversation loop for automated phone activities and AI voice entry points. Covers the CrestApps.OrchardCore.Omnichannel.Voice feature, pairing it with Telnyx AI Voice Agent, automatic Phone loads, the voice settings cascade, realtime versus turn-based calls, silence, voicemail, handoff availability, the hidden call tools, VoiceCallInvocationItems, live-call supervision with IAIVoiceCallSupervision, session-loss recovery, the conclusion and handoff prompts, and adding automated voice to a telephony provider through IVoiceAgentMediaProvider, VoiceAgentEvent and IVoiceAgentConversationLoop. Use this skill when requests mention Orchard Core automated voice, AI phone calls, AI voice agent, outbound AI calling, or voice call conclusion. Strong matches include OmnichannelVoiceConstants, VoiceAgentConversationLoop, IVoiceAgentMediaProvider, EndCallTool, and StrandedVoiceCallRecoveryBackgroundTask.
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
  feature and is covered by `orchardcore-telephony-telnyx`. Asterisk cannot
  place or answer AI calls. An AI call is an automated activity, not a dialer
  mode, and uses no dialer profile.
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
business-hours calendar, cadence) are resolved by
`IAutomatedVoiceActivitySettingsResolver` from the activity, then the subject's
flow settings, then the default AI deployment site settings. In the admin, the
profile, update permissions and reply delay come from the load (an outbound
subject cannot hold a profile, so an outbound load must name one); the
speech-to-text and text-to-speech deployments and the voice are settings of an
**inbound** Phone subject only. When no voice is picked, the AI persona's voice
is used (when it was chosen on the speaking provider's deployment) before the
site default. An outbound subject shows only the **Live agent handoff** card.

Before each call the processor cancels the activity when the number is known
not in service or the contact opted out, then runs the do-not-call lists (with
the Outbound Dialer) and a calling window only when turned on for automated
calls; the refusing reason is written to the notes. A workflow's **Place Call
or Send Message** task skips these checks.

## Inbound AI Calls

Set **Route to** to **AI voice agent** on a **Voice calls** inbound entry point
and pick a Chat profile (`orchardcore-contact-center`). Optionally add an
inbound **Phone** subject whose address is that number to supply dispositions
and the handoff rules. A silent inbound caller is never treated as voicemail.

## Call Behavior

- **Silence**: after 8 seconds of silence the AI asks whether the caller is
  still there, twice, then says goodbye and hangs up. On a call the platform
  placed, silence from a person who never spoke is treated as a voicemail.
- **Recording notice**: with Contact Center Call Recording,
  `DiscloseOnAIVoiceCalls` (**Have automated voice agents say it first**) makes
  the AI say the recording disclosure before anything else.
- **Keys and phone menus**: the AI can press keys (`pressKeys`) to get through
  a phone menu or enter an extension, hears keys the caller presses, and with
  Secure Data Capture can take a card or account number through
  `collectSecureDigits`.
- **Voicemail**: when a machine answers (from the provider's answering machine
  detection, or recognized from the greeting), the AI waits for the tone,
  leaves one short message and hangs up; the activity takes the disposition with
  the *Answering machine* outcome.
- **Call tools**: `endCall` (`EndCallTool`), `pressKeys`, `collectSecureDigits`
  and `transferToLiveAgent` are registered **hidden**: a session gets a hidden
  tool only when it names it (`AICompletionContext.ToolNames`, plus
  `OrchestrationContext.MustIncludeTools`), so ordinary AI
  chats never see them and a call is offered the transfer only while someone
  can take it.
- **Handoff**: with **Live agent handoff** on the subject, the AI uses
  `transferToLiveAgent` to transfer to the handoff queue; the
  `voice-handoff-summary` prompt briefs the agent, and the briefing is saved on
  a fresh read of the activity so concurrent routing writes cannot drop it. A
  transferred call is not concluded by the AI. Before the conversation starts
  the loop asks `IOmnichannelHandoffAvailability` what a handoff would do: a
  closed queue that books a callback keeps the transfer and the model says
  someone will call back; one that books nothing removes the transfer and the
  model offers to take a message. The platform's fixed handoff lines are spoken
  only on English calls; otherwise the caller hears only the model.
- **Session loss**: a dropped realtime session is reopened with the transcript
  up to two times, then handed off if allowed, otherwise an apology ends the
  call. The activity takes the **Try Again** disposition and terminal reason
  `ai_session_lost`. An unexpected error before the session has sent anything
  else (a refused set-up, such as a voice the provider lacks) counts as lost.
- **Language**: a live session transcribes the caller in the AI persona's
  language and holds the model to it (`RealtimeOrchestrationRequest.SpeechLanguage`).
  The conclusion and handoff prompts take a `Language` variable, so the summary
  and briefing are written in the persona's language.
- **Conclusion**: the `voice-conclusion-analysis` prompt writes the summary and
  picks a disposition only from the subject's wired dispositions, based on what
  the customer said. A call nobody spoke on gets the *No answer* outcome; an
  undecided call gets the **Try Again** disposition, then *Disconnected*.
  `DispositionedBy` is `AIAgent`. A **New Activity** action still runs when the
  caller matched no contact, and the follow-up takes the call summary as its
  instructions.
- **Tools know the call**: on a live session the call's identity is put in
  `AIInvocationScope.Current.Items` under the `VoiceCallInvocationItems` keys
  (`ActivityId`, `CustomerNumber`, `ChannelEndpointId`, `ContactContentItemId`,
  `ContactContentType`, each prefixed `Omnichannel.Voice.`).
- **Supervision**: `IAIVoiceCallSupervision` sends the AI a note, takes the call
  over for an agent, transfers it to a number, switches its AI profile, or ends
  it; the Contact Center Live dashboard and the `api/ai/v1/live-calls` API use
  it. A live session runs on the node that answered it; with `OrchardCore.Redis`
  enabled, `IAIVoiceCallCommandRelay` carries commands to that node.
- **Voice choice**: the profile and template editors refuse a voice that the
  resolved speech-to-speech deployment does not list.
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
- The call tools are hidden rather than system tools. A custom realtime runner
  or chat that needs one must name it in `AICompletionContext.ToolNames` (and
  `OrchestrationContext.MustIncludeTools`); they are no longer given to every
  chat.
- `voice-conclusion-analysis` and `voice-handoff-summary` take a `Language`
  variable; override copies of these prompts should accept it.
