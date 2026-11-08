---
name: orchardcore-omnichannel-messaging
description: Skill for the CrestApps Omnichannel Messaging Workspace in Orchard Core, a human-operated inbox for every non-voice channel, and its SMS Messaging Channel. Covers CrestApps.OrchardCore.Omnichannel.Messaging, Messaging.Sms, Messaging.RoutedDistribution and Messaging.Azure, SMS setup with per-number providers, STOP START HELP keywords, quiet hours, retries, MMS pictures and their Azure Blob storage, the unread-only filter, AI handoff, templates, broadcasts, the OmnichannelMessageTemplate and MessagingBroadcasts recipe steps, MessagingWorkspaceOptions, the messaging permissions, AuthorizeConversationAsync, the MessagingHub, and building a channel with IMessagingChannel. Use this skill when requests mention the messaging workspace, messaging inbox, two-way SMS for agents, messaging permissions, adding a channel to the inbox, or upgrading from the SMS Portal. Strong matches include IMessagingChannel, IMessagingInboundProcessor, SmsMessagingChannel, and MessagingConstants.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Omnichannel Messaging Workspace

## Configure the Messaging Workspace

You are an Orchard Core expert. Set up the human messaging inbox, its SMS
channel and permissions, and extend it with new channels through its contracts
instead of changing the workspace.

### Guidelines

- Install `CrestApps.OrchardCore.Omnichannel.Messaging` and
  `CrestApps.OrchardCore.Omnichannel.Messaging.Sms` in the web/startup project.
- The workspace carries no channel. Enable a channel feature; enabling **SMS
  Messaging Channel** enables the workspace and everything it needs.
- It does not need Contact Center Voice or the Omnichannel Management CRM
  screens; addresses come from the dependency-only Omnichannel Channel
  Endpoints feature.
- Where a number's texts go (queue or agent, hours, auto-replies) is set on its
  **Text messages** inbound entry point, covered by `orchardcore-contact-center`.
- The workspace is the human counterpart to `orchardcore-omnichannel-sms`; both
  run on the same numbers and an automated conversation hands off into it.
- Keep provider secrets in Orchard Core SMS settings or configuration, never in
  recipes or source. C# samples use `sealed` classes.

## Features

| Feature | ID | Depends on |
|---|---|---|
| Omnichannel Messaging Workspace | `CrestApps.OrchardCore.Omnichannel.Messaging` | `CrestApps.OrchardCore.Omnichannel.ChannelEndpoints`, `CrestApps.OrchardCore.ContactCenter.AgentServices`, `CrestApps.OrchardCore.ContactCenter.EntryPoints`, `OrchardCore.SignalR`, `CrestApps.OrchardCore.Outreach` (sends broadcasts) |
| SMS Messaging Channel | `CrestApps.OrchardCore.Omnichannel.Messaging.Sms` | the workspace, `CrestApps.OrchardCore.ContactCenter.ProviderInbox` (the **Provider Webhook Inbox** of the Webhooks module), `OrchardCore.Sms` |
| Omnichannel Messaging Routed Distribution | `CrestApps.OrchardCore.Omnichannel.Messaging.RoutedDistribution` | the workspace, `CrestApps.OrchardCore.ContactCenter.Queues` |
| Omnichannel Messaging - Azure Blob Storage | `CrestApps.OrchardCore.Omnichannel.Messaging.Azure` | the workspace. Keeps pictures in Azure Blob Storage (see Pictures below) |

All are in the **Contact Center** category; the admin menu is **Messaging**
(**Inbox**, **Broadcasts**, **Templates**).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Omnichannel.Messaging.Sms",
        "CrestApps.OrchardCore.Omnichannel.Messaging.RoutedDistribution"
      ],
      "disable": []
    }
  ]
}
```

Operators are Contact Center agent profiles; a bare profile is created the
first time a permitted user opens the inbox, which is what ties them to their
queues.

## Set Up SMS

1. Configure an SMS provider (Twilio, Azure from `orchardcore-sms-azure`, or
   Telnyx from `orchardcore-telephony-telnyx`) and the tenant default at
   **Settings > Communication > SMS**.
2. Add each number at **Interaction Center > Management > Omnichannel
   Addresses** with **Text messages (SMS)** ticked. The SMS settings pick the
   **Provider** that owns the number (empty uses the default) and the **Agents
   who text from this number**.
3. Add a **Text messages** entry point under **Interaction Center > Management
   > Inbound entry points**: route to an agent or a queue, choose **Shared
   pool** or **Routed** distribution, and set the auto-reply (at most once a
   day per conversation) and closed auto-reply.
4. Grant the permissions below.
5. Point the provider webhook at the site: Twilio `api/twilio/webhook/sms`
   (mapped by this channel, shown as **Webhook URL** on the Twilio tab), Telnyx
   `api/telnyx/webhook/sms`, ACS through `api/azure/webhook/eventgrid`
   (`orchardcore-omnichannel-event-grid`).
6. Open **Messaging > Inbox**.

Inbound deliveries are committed to the durable provider webhook inbox (keyed on
the provider's message id, handler `sms-inbound`) before the provider is
answered, so retries are de-duplicated on every node and a restart loses
nothing; a text that cannot be stored gets `503` so the provider resends it. Outbound SMS goes through the
per-number `ISmsDispatcher` (`SendAsync`, `ResolveProviderNameAsync`), so
different numbers can use different carriers. A **Send SMS** button appears
beside phone-number fields on admin pages.

### SMS Compliance

| Keyword | Synonyms | Effect |
|---|---|---|
| `STOP` | STOPALL, UNSUBSCRIBE, CANCEL, END, QUIT | Closes the conversation, sets **Do not SMS**, sends one confirmation. |
| `START` | UNSTOP, YES | Reverses an opt-out (ignored when not opted out). |
| `HELP` | INFO | Sends the help reply. |

The message is trimmed, trailing `. ! ? , ; :` removed, and compared without
case (`SmsKeywordPolicy.Classify`). An opt-out wins when the message is an
opt-out keyword or starts with one followed by a space or punctuation ("STOP",
"stop." and "STOP please" opt out; "stopping by" and "please stop" do not).
HELP and START must be the whole message. SMS automation uses the same rule
(`OmnichannelSmsComplianceHelper.IsOptOutRequest`). A STOP from a number no
contact holds is recorded against the number (`ContactAddressOptOut`), and every
SMS send path refuses it until it texts START.

A keyword suppresses the entry point's auto-reply for that message. Customize
replies with `SmsKeywordReplySettings`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Omnichannel": {
        "Messaging": {
          "Sms": {
            "KeywordReplies": {
              "StopMessage": "You are unsubscribed. Reply START to resubscribe.",
              "HelpMessage": "Reply STOP to unsubscribe.",
              "StartMessage": "You are resubscribed. Reply STOP to unsubscribe."
            }
          }
        }
      }
    }
  }
}
```

- **Quiet hours**: outside the destination queue's business hours in the
  contact's local time, a banner warns; sending is never blocked.
  `SendMessagesDuringQuietHours` only softens the banner.
- **Retries**: a refused outbound message is retried after 1, 5, 15 and 60
  minutes (five attempts). An opt-out refusal is never retried; the contact is
  marked **Do not SMS** and the message failed.
- **Pictures (MMS)**: JPEG, PNG, GIF, WebP, up to 10 per message; the composer
  shrinks still images to fit about 1 MB. Telnyx sends media directly, Twilio
  numbers use the workspace's Twilio sender, other providers refuse picture
  messages. Providers download pictures from signed links built from **Base
  URL**, so set it to the public address. Inbound pictures are copied into the
  tenant's `App_Data` (`MessagingAttachments`), encrypted with data protection.
  On hosts whose file system is replaced on deployment, enable
  `CrestApps.OrchardCore.Omnichannel.Messaging.Azure` and configure
  `CrestApps:Omnichannel:Messaging:AzureAttachments` (`ConnectionString`,
  `ContainerName` and `BasePath`, both Liquid such as `{{ ShellSettings.Name }}`,
  and `CreateContainer`). Pictures stay encrypted before they reach Azure, so
  persist the data protection keys too; incomplete settings log an error and
  keep the local store; existing local files are not moved.

## The Inbox

- The list is customer-centric and filters by **All**, **Mine**, **Favorites**
  or **Unassigned**, and by channel. The **Unread only** toggle (route value
  `unread=true`, `MessagingWorkspaceBuilder.BuildInboxAsync(..., unreadOnly, ...)`)
  narrows any filter and its counts to conversations whose `IsRead` is off, the
  same rule as the **Messaging** menu badge; the open customer keeps their row.
- The thread header shows the customer and their number; when the conversation
  is linked to a contact or a lead, the name opens it with a **Contact** or
  **Lead** badge, and **Our number** shows the service address. The **Customer**
  card lists every matching contact and lead (**View account**, **View lead**);
  the **AI summary** appears only after an AI hand-off.
- Agents are notified when a conversation is assigned or transferred to them.

## Hand-off From AI

While an automated activity handles a contact on an address, the workspace
leaves that contact's texts to the AI. On hand-off the whole transcript is
copied into the conversation once (provider message ids prevent duplicates),
and the workspace owns the thread from then on. The workspace implements
`IOmnichannelHandoffService` for messaging channels.

## Permissions

| Permission | Grants |
|---|---|
| `UseMessagingWorkspace` | Open the inbox; visibility comes from the next three |
| `ViewOwnMessagingConversations` | Conversations assigned to you or sent to an address you own that no colleague claimed |
| `ViewQueueMessagingConversations` | Also unclaimed conversations of your queues, within your agent entitlements |
| `ViewAllMessagingConversations` | Every conversation, including unrouted ones; transfer any |
| `SendGroupMessages` | Broadcasts and multi-recipient messages |
| `SendMessagesDuringQuietHours` | Plain quiet-hours notice |
| `ManageMessaging` | **Messaging > Templates** |

View-all implies queue, which implies own. **Agent** gets
`UseMessagingWorkspace` and `ViewQueueMessagingConversations`; **Supervisor**
gets `UseMessagingWorkspace`, `ViewAllMessagingConversations`,
`SendGroupMessages`, `SendMessagesDuringQuietHours`, `ManageMessaging`. There
is no per-channel permission.

Authorize a conversation in code against `ViewAllMessagingConversations`; the
`MessagingConversationAuthorizationHandler` grants narrower access:

```csharp
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

if (!await _authorizationService.AuthorizeConversationAsync(User, conversation, ConversationOperation.Send))
{
    return Forbid();
}
```

`ConversationOperation` is `View`, `Send`, `Claim`, `Close`, `Snooze` or
`Transfer`.

## Templates, Broadcasts and Workflows

- **Templates** (`ManageMessaging`) are canned responses with a **Name** and
  **Body**, exported by the **Messaging Templates** deployment step.
- **Broadcasts** (`SendGroupMessages`) send one message to many recipients
  (contacts and leads reachable on the channel, plus typed addresses) as
  individual 1:1 threads from one sending address, in the background, on any
  channel whose capabilities allow broadcasts. The Outreach engine sends them,
  50 recipients per batch (`OutreachRunId` tracks the run); opted-out
  recipients are skipped (`SkippedCount`), a refused send counts as failed (it
  is not retried), a batch that throws is retried with back-off from 1 minute
  doubling to 30 (five attempts) and then stops the run, and a number listed
  twice gets one message. They cannot be
  cancelled once queued.
- Broadcasts travel as drafts through the **Messaging broadcasts** deployment
  step and the `MessagingBroadcasts` recipe step: delivery state is never
  exported, an import never queues a message, and a queued or sent broadcast on
  the target is left alone. Recipients are personal data.
- With `OrchardCore.Workflows`, the SMS channel adds the **Send Text Message**
  task (`SendTextMessageTask`: `From`, `To`, `Body`, optional `UserName`;
  outcomes **Done** and **Failed**), which sends into the customer's conversation.
- With `OrchardCore.DataPipelines.Core`, a **Messaging** data source exposes
  conversations and messages to report builders (`orchardcore-reports`).

```json
{
  "steps": [
    {
      "name": "OmnichannelMessageTemplate",
      "Templates": [
        {
          "ItemId": "2hb7c4x9m1q6z3v8k5r0t2n7y",
          "Name": "Opening hours",
          "Body": "We are open Monday to Friday, 8am to 6pm."
        }
      ]
    },
    {
      "name": "MessagingBroadcasts",
      "Broadcasts": [
        {
          "ItemId": "4bc00000000000000000000001",
          "Name": "Holiday hours",
          "Channel": "sms",
          "ServiceAddress": "+12025550100",
          "Body": "We close at noon on Friday.",
          "Recipients": [ "+12025550101", "+12025550102" ]
        }
      ]
    }
  ]
}
```

Routing travels with entry points through the `ContactCenterEntryPoint`
deployment step (`orchardcore-contact-center`).

## Configuration

| Section | Options class | Settings |
|---|---|---|
| `CrestApps:Omnichannel:Messaging` | `MessagingWorkspaceOptions` | `InboxPageSize` (50), `ConversationLockTimeoutSeconds` (10), `ConversationLockExpirationSeconds` (30), `OutboxBatchSize` (200), `MaxMessagesPerPassPerEndpoint` (20), `MaxInboundAttachmentBytes` (10 MB), `MaxInboundAttachments` (10), `AttachmentLinkLifetimeHours` (72) |
| `CrestApps:Omnichannel:Messaging:RoutedDistribution` | `MessagingRoutedDistributionOptions` | `PickupGraceMinutes` (5), `MaxReassignmentAttempts` (2), `VoiceCapacityWeight` (3) |
| `CrestApps:Omnichannel:Messaging:Sms:KeywordReplies` | `SmsKeywordReplySettings` | `StopMessage`, `HelpMessage`, `StartMessage` |

In the root `appsettings.json` put these under the `OrchardCore` key; a tenant
file starts at `CrestApps`. Routed distribution pushes conversations only to
agents who are **Available** with the inbox open, and re-pools unpicked ones.

## Real Time

The workspace maps its own `MessagingHub` with
`SignalRHubRoutes.GetHubPath<MessagingHub>()` on Orchard Core SignalR (see
`orchardcore-signalr`). New messages, receipts and assignment changes are
pushed; missed pushes are caught up by polling. Other admin pages show a notice
when a customer writes or a conversation is transferred, and **Messaging >
Inbox** shows the waiting count. Each notification is logged at `Information`.

## Add a Channel

Implement `IMessagingChannel` and register it with
`services.AddMessagingChannel<TChannel>()`, plus an address capability and an
entry point channel. Inbound receivers call
`IMessagingInboundProcessor.ProcessAsync`; channel-only rules are
`IMessagingInboundHandler`s; delivery receipts go to
`IMessagingConversationService.ApplyDeliveryReceiptAsync`. See
[references/adding-a-channel.md](references/adding-a-channel.md).

## Upgrading from 2.x

- The SMS Portal (`CrestApps.OrchardCore.Omnichannel.Sms.Portal`) is replaced.
  Enable **SMS Messaging Channel**; the first workspace migration imports
  conversations, templates, broadcasts, each SMS endpoint's routing (as a text
  entry point) and role permissions. Agents turn **Available** back on.
- `SmsNumberRoute` is retired: routing lives on inbound entry points and the
  provider on the SMS omnichannel address.
- Rename configuration: `CrestApps:Sms:Workspace` to
  `CrestApps:Omnichannel:Messaging`, `CrestApps:Sms:RoutedDistribution` to
  `CrestApps:Omnichannel:Messaging:RoutedDistribution`,
  `CrestApps:Sms:Portal:KeywordReplies` to
  `CrestApps:Omnichannel:Messaging:Sms:KeywordReplies`.
- `UseMessagingWorkspace` no longer implies the other permissions. A migration
  grants `ViewQueueMessagingConversations` to every role that could use the
  workspace; review roles that should see everything and grant
  `ViewAllMessagingConversations` explicitly.
- `IMessagingConversationAuthorizationService` is removed; use
  `IAuthorizationService.AuthorizeConversationAsync`.
- `SmsMessagingChannel` takes `IContactMethodsAccessor` and
  `IOmnichannelContactPhoneLookup`; register `services.AddContactMethods()` when
  you build it by hand.
- The Broadcasts and Templates lists render through Orchard Core's `AdminList`
  (list names `MessagingBroadcasts`, `MessagingTemplates`; see
  `orchardcore-admin-lists`).
