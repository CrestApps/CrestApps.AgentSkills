---
name: orchardcore-omnichannel-sms
description: Skill for AI-driven SMS automation in Orchard Core with the CrestApps SMS Omnichannel Automation feature CrestApps.OrchardCore.Omnichannel.Sms. Covers dependencies, automatic SMS activity loads driven by a Chat AI profile, SmsOmnichannelProcessor and SmsOmnichannelEventHandler, the Twilio webhook api/twilio/webhook/sms with signature and Base URL validation, Telnyx and ACS inbound texts, per-number providers, no-response timeout, response delay, opt-out keywords, cadences and business hours, owed-reply recovery and re-engagement tasks, the sms-conclusion-analysis prompt, AI lead conversion, handoff into the messaging workspace, and the AI agent route on text entry points. Use this skill when requests mention Omnichannel SMS automation, AI SMS conversations, automated SMS activities, the Twilio inbound SMS webhook, or SMS opt-out keywords. Strong matches include SmsOmnichannelProcessor, SmsOmnichannelEventHandler, SmsReEngagementBackgroundTask, and SmsEntryPointAIConversationStarter.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Omnichannel SMS Automation

## Configure AI-Driven SMS

You are an Orchard Core expert. Configure the SMS Omnichannel Automation
feature so an AI chat profile carries SMS conversations for automated
activities, and connect the SMS provider's inbound webhook so customer replies
reach the AI.

### Guidelines

- Install `CrestApps.OrchardCore.Omnichannel.Sms` in the web/startup project and
  enable `CrestApps.OrchardCore.Omnichannel.Sms`.
- Its manifest depends on `CrestApps.OrchardCore.AI`,
  `CrestApps.OrchardCore.AI.Chat.Core`,
  `CrestApps.OrchardCore.Omnichannel.Managements`, `OrchardCore.Sms` and
  `CrestApps.OrchardCore.ContactCenter.BusinessHours` (business-hours calendars
  gate background follow-ups).
- An AI provider connection and a chat deployment must exist (see
  `orchardcore-ai` and `orchardcore-ai-connections`).
- Configure at least one Orchard Core SMS provider (Twilio, Azure, or Telnyx from
  `orchardcore-telephony-telnyx`) and the tenant default at
  **Settings > Communication > SMS**. Sends go through `ISmsProviderRouter`, so
  an SMS address that pins a provider sends through that provider.
- Only **Chat** profiles with **Start the conversation automatically** can drive
  automated SMS, because the opening message is rendered and sent first.
- Twilio secrets live in Orchard Core's Twilio settings (protected at rest);
  never place tokens in recipes or source.

## Enable via Recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Omnichannel.Sms"
      ],
      "disable": []
    }
  ]
}
```

Add `CrestApps.OrchardCore.Omnichannel.Messaging.Sms` as well when the AI must
be able to hand SMS conversations to people (see Live Agent Handoff).

## Set Up an Automated SMS Campaign

1. Build the CRM: a contact type with `OmnichannelContactPart`, a subject type
   with `OmnichannelSubjectPart`, dispositions, a campaign, and subject actions
   with **When to choose this disposition** guidance on each (see
   `orchardcore-omnichannel-management`).
2. Create a Chat AI profile with **Start the conversation automatically**. The
   **Qualify leads by text** and **Customer care by text** starting points ship
   with this module (see `orchardcore-ai-profile-templates`). The opening
   message can use Liquid such as `{{ Contact.DisplayText }}`.
3. Add the sending number as an omnichannel address with the **Text messages
   (SMS)** capability.
4. Connect the provider webhook (below).
5. Create an **Automatic** activity load: subject, campaign, AI profile (or
   leave empty to use the subject's), channel **SMS**, the sending address,
   reply delay, optional cadence and business-hours calendar. Run **Actions >
   Load activities**.

`AutomatedActivitiesProcessorBackgroundTask` (every five minutes) hands each due
automated `SMS` activity to `SmsOmnichannelProcessor`, which checks opt-outs,
renders and sends the opening message, and sets the activity to
`AwaitingCustomerAnswer`.

## Inbound Webhooks

| Provider | Endpoint | Validation |
|---|---|---|
| Twilio | `POST ~/api/twilio/webhook/sms` | `X-Twilio-Signature` against the Twilio auth token |
| Telnyx | `POST ~/api/telnyx/webhook/sms` | Ed25519 signature (see `orchardcore-telephony-telnyx`) |
| Azure Communication Services | via `POST ~/api/azure/webhook/eventgrid` | Event Grid SAS key or Entra token (see `orchardcore-omnichannel-event-grid`) |

The Twilio webhook is registered by this feature and by the SMS Messaging
Channel; with both on, it is mapped once. Its full address shows as a read-only
**Webhook URL** on the Twilio tab of **Settings > Communication > SMS**; set the
Twilio number's *A message comes in* to that URL with HTTP POST.

- Returns `404` while the Twilio provider is disabled, `400` when the auth
  token is missing, and refuses an invalid signature.
- Twilio signs the public URL. Behind a TLS-terminating proxy, set **Base URL**
  in **Settings > General** so the signature is checked against it.
- Each receiver raises `OmnichannelConstants.Events.SmsReceived` on the `SMS`
  channel, which `SmsOmnichannelEventHandler` and the messaging workspace both
  consume.

## Conversation Behavior

- Each inbound reply is appended to the activity's AI chat session; the profile
  generates the next reply, which is sent after the reply delay with natural
  pacing. A reply delay set on the load is stored on each activity and wins over
  the subject's **Response delay**.
- An opt-out keyword (default `STOP`, configurable on the subject) cancels the
  activity and sets the contact's **Do not SMS**, even when the AI may not update
  the contact.
- **No-response timeout** fails the activity when the customer stops answering.
- After the conversation ends, the `sms-conclusion-analysis` prompt picks a
  disposition from the subject's wired dispositions, writes the summary to the
  activity notes, and runs the subject actions exactly as an agent completion
  would. With the CRM feature and AI conversion on the load, a qualified open
  lead is converted first (see `orchardcore-omnichannel-management`).
- The subject's AI settings decide whether the AI may update the contact or the
  subject.

### Background Tasks

| Task | Schedule | Purpose |
|---|---|---|
| `SmsReEngagementBackgroundTask` (**Automated SMS Re-Engagement**) | `*/5 * * * *` | Sends due cadence follow-ups while the load's business-hours calendar is open in the contact's time zone. |
| `SmsOwedReplyRecoveryBackgroundTask` (**Automated SMS Owed-Reply Recovery**) | `*/10 * * * *` | Sends a reply that was owed but lost, for example after a restart, when the contact's last message is recent. |

## Live Agent Handoff

Configure **Live agent handoff** on the subject (handoff queue plus at least one
**Escalate when** condition). The SMS handoff is carried out by the messaging
workspace, so enable `CrestApps.OrchardCore.Omnichannel.Messaging.Sms`. The
thread, with its full automated transcript, moves into the queue's inbox. See
`orchardcore-omnichannel-messaging`.

## Let the AI Answer Incoming Texts

With the SMS Messaging Channel also enabled, `EntryPointAIAgentStartup` adds an
**AI agent** routing choice to text entry points (`SmsEntryPointAIConversationStarter`).
Set **Route to** to **AI agent** on the number's **Text messages** entry point
and pick a Chat profile. Texts go to people instead when the entry point is
closed, a person already holds an open conversation with the customer, the
customer's last AI conversation ended less than an hour ago, or the profile was
deleted. Entry points are covered by `orchardcore-contact-center`.

## Extend

- Add a supplementary `IOmnichannelEventHandler` only for behavior this module
  does not cover; filter by event type and channel (see `orchardcore-omnichannel`).
- To send through another carrier, implement an Orchard Core `ISmsProvider`
  (see `orchardcore-sms`) and pin it on the SMS address.

## Upgrading from 2.x

- The Twilio webhook moved from `Omnichannel/webhook/Twilio` to
  `api/twilio/webhook/sms`; the `Omnichannel/webhook/TwilioEventGrid` route is
  removed (deliver ACS texts through `api/azure/webhook/eventgrid`).
- Subject flows are configured on the `OmnichannelSubjectPart` content-type
  part settings, and the per-run campaign, channel and address are chosen on the
  activity load (see `orchardcore-omnichannel-management`).
- Enabling the feature now also enables **Contact Center Business Hours**.
