---
name: orchardcore-omnichannel
description: Skill for configuring Omnichannel communication in Orchard Core using CrestApps modules. Covers subject flows, subject actions, campaigns as grouping/reporting records, activity batches, dispositions, multi-channel messaging (SMS, email, phone, chat), AI-powered automation, Azure Event Grid webhooks, and Twilio integration. Use this skill when requests mention Orchard Core Omnichannel, Subject Flows, Manage Flow, Omnichannel SMS, Contact Management, Channel Endpoints, Communication Preferences, Activity Batches, Campaigns, Dispositions, or related setup and troubleshooting. Strong matches include CrestApps.OrchardCore.Omnichannel, CrestApps.OrchardCore.Omnichannel.Managements, CrestApps.OrchardCore.Omnichannel.Sms, CrestApps.OrchardCore.Omnichannel.EventGrid, OmnichannelContactPart, SubjectFlowSettings, SubjectAction, IOmnichannelProcessor, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Omnichannel - Prompt Templates

## Configure Omnichannel

You are an Orchard Core expert. Generate code, configuration, and recipes for adding omnichannel communication capabilities to an Orchard Core application using CrestApps modules.

### Guidelines

- The base Omnichannel feature (`CrestApps.OrchardCore.Omnichannel`) provides shared message storage, contact communication-preference indexing, and communication contracts. It does not itself send SMS or email, place calls, expose an activity UI, or implement channel routing.
- The Managements feature (`CrestApps.OrchardCore.Omnichannel.Managements`) adds an admin UI for contacts, activities, activity batches, campaigns, subject flows, dispositions, and channel endpoints under the **Interaction Center** menu.
- The SMS feature (`CrestApps.OrchardCore.Omnichannel.Sms`) enables AI-powered SMS automation. It integrates with the AI Chat module to run AI-driven conversations over SMS using Twilio webhooks.
- The Event Grid feature (`CrestApps.OrchardCore.Omnichannel.EventGrid`) receives inbound events through a webhook endpoint. Prefer Microsoft Entra bearer-token delivery; a configured `aeg-sas-key` is a supported shared-secret alternative.
- The Azure Communication Services feature (`CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices`) only declares the dependency bridge to Orchard Core Azure Email and SMS features. It does not add an Omnichannel channel processor or its own ACS settings and routing implementation.
- Omnichannel domain data (messages, activities, batches, AI chat sessions) is stored in a dedicated `Omnichannel` YesSql collection.
- Communication preferences (`DoNotCall`, `DoNotSms`, `DoNotEmail`, `DoNotChat`) are tracked per contact with UTC timestamps.
- The SMS module validates inbound Twilio requests using HMAC-SHA1 signature verification against the Twilio AuthToken.
- A background task runs every 5 minutes to process automated activities that are scheduled and ready for dispatch.
- Always secure API keys, SAS keys, and Twilio credentials using user secrets or environment variables; never hardcode them.
- Install CrestApps packages in the web/startup project.

### Available Omnichannel Features

| Feature | Feature ID | Description |
|---------|-----------|-------------|
| Omnichannel | `CrestApps.OrchardCore.Omnichannel` | Shared message storage, preferences, indexes, and contracts |
| Azure Communication Services | `CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices` | Dependency bridge for Orchard Core Azure Email and SMS features; not an Omnichannel processor |
| Azure Event Grid | `CrestApps.OrchardCore.Omnichannel.EventGrid` | Webhook endpoint for receiving inbound messages from Azure Event Grid |
| Omnichannel Management | `CrestApps.OrchardCore.Omnichannel.Managements` | Admin UI for contacts, activities, activity batches, campaigns, subject flows, dispositions, and channel endpoints |
| SMS Automation | `CrestApps.OrchardCore.Omnichannel.Sms` | AI-powered SMS channel automation via Twilio with AI chat session integration |

### NuGet Packages

| Package | Description |
|---------|-------------|
| `CrestApps.OrchardCore.Omnichannel` | Base omnichannel module |
| `CrestApps.OrchardCore.Omnichannel.EventGrid` | Azure Event Grid webhook handler |
| `CrestApps.OrchardCore.Omnichannel.Managements` | Contact and activity management UI |
| `CrestApps.OrchardCore.Omnichannel.Sms` | SMS automation with Twilio and AI |

### Supported Channels

The omnichannel system supports the following communication channels:

- **SMS** - Text messaging via Twilio; the Azure Communication Services feature is currently a placeholder and does not yet provide ACS SMS wiring
- **Email** - Email communication with contact email tracking
- **Phone** - Voice call tracking with do-not-call preferences
- **Chat** - Chat messaging with do-not-chat preferences

### Content Types and Parts

| Content Type / Part | Stereotype | Description |
|---------------------|-----------|-------------|
| `OmnichannelContactPart` | Content part | Attachable contact part; it is not a content-type stereotype |
| `PhoneNumber` | `ContactMethod` | Contact method with Number, Extension, and Type fields |
| `EmailAddress` | `ContactMethod` | Contact method with an Email field |
| `PhoneNumberInfoPart` | — | Reusable part with phone number, extension, and type fields |
| `EmailInfoPart` | — | Reusable part with an email field |
| `OmnichannelContactInfoPart` | — | Contact information part |

### Enabling Omnichannel Features via Recipe

Enable the base omnichannel and management features:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Omnichannel",
        "CrestApps.OrchardCore.Omnichannel.Managements"
      ],
      "disable": []
    }
  ]
}
```

### Enabling SMS Automation via Recipe

Enable SMS automation with AI chat integration. This requires the AI and AI Chat features to be enabled alongside the SMS feature:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.Omnichannel",
        "CrestApps.OrchardCore.Omnichannel.Managements",
        "CrestApps.OrchardCore.Omnichannel.Sms"
      ],
      "disable": []
    }
  ]
}
```

### Enabling Azure Event Grid via Recipe

Enable the Event Grid webhook endpoint for receiving inbound messages from Azure:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Omnichannel",
        "CrestApps.OrchardCore.Omnichannel.EventGrid"
      ],
      "disable": []
    }
  ]
}
```

### Azure Event Grid Configuration

Configure Event Grid webhook authentication in your shell configuration (`appsettings.json`):

```json
{
  "CrestApps": {
    "Omnichannel": {
      "EventGrid": {
        "EventGridSasKey": "<!-- Your Event Grid SAS Key -->",
        "AADIssuer": "<!-- Your AAD Issuer URL -->",
        "AADAudience": "<!-- Your AAD Audience -->",
        "AADMetadataAddress": "<!-- Your AAD OpenID Metadata Address -->"
      }
    }
  }
}
```

The webhook endpoint is available at `POST Omnichannel/webhook/AzureEventGrid`. Prefer Entra bearer-token delivery by configuring `AADIssuer`, `AADAudience`, and `AADMetadataAddress`; it also accepts a configured SAS key in the `aeg-sas-key` header.

### Webhook Endpoints

| Method | Route | Module | Authentication |
|--------|-------|--------|---------------|
| POST | `Omnichannel/webhook/AzureEventGrid` | Event Grid | SAS key or AAD bearer token |
| POST | `Omnichannel/webhook/Twilio` | SMS | Twilio HMAC-SHA1 signature |
| POST | `Omnichannel/webhook/TwilioEventGrid` | SMS | Twilio HMAC-SHA1 signature |

All webhook endpoints are anonymous and do not require antiforgery tokens. They validate authenticity using their respective authentication mechanisms.

### AI-Powered SMS Automation

The SMS module integrates with the CrestApps AI Chat module to enable automated SMS conversations:

1. **Outbound** - The `SmsOmnichannelProcessor` creates AI chat sessions, renders initial messages using the configured subject flow and AI profile, and sends them via `ISmsService`.
2. **Inbound** - The `SmsOmnichannelEventHandler` receives customer SMS replies, feeds them into the AI chat session as user prompts, runs AI completion, and sends the AI response back as SMS.
3. **Conclusion Analysis** - A deferred task uses AI with the `sms-conclusion-analysis` prompt template to determine if the conversation has concluded. When concluded, it auto-sets the disposition and triggers the `CompletedActivityEvent` workflow event.

### Admin UI - Interaction Center

The Managements feature adds an **Interaction Center** menu in the admin dashboard with the following sections:

1. **Activities** - View and manage omnichannel activities (calls, SMS, emails). Filter by status, channel, campaign, and assignee.
2. **Activity Batches** - Group and manage activities in batches for bulk operations. Batches choose a subject and contacts, then resolve campaign, interaction type, channel, and endpoint from the subject flow when activities are loaded.
3. **Campaigns** - Define campaign names and descriptions used for grouping and reporting.
4. **Subject Flows** - Configure campaign association, interaction type, channel, channel endpoint, and AI settings per `OmnichannelSubject` content type.
5. **Dispositions** - Configure unique activity outcome categories (e.g., completed, no answer, callback requested) used by subject actions.
6. **Channel Endpoints** - Manage communication channel endpoints (phone numbers, SMS numbers, email addresses).

### Subject Flows and Manage Flow

- A subject is any content type with the `OmnichannelSubject` stereotype.
- Subject flow settings live at the subject-content-type level, not the campaign level.
- Campaigns are used for grouping and reporting only; they no longer define channel, interaction type, endpoint, or action logic.
- The **Configure** screen stores the campaign, interaction type, channel, endpoint, and AI-related settings for the subject.
- The **Manage Flow** screen stores disposition-driven **Subject Actions** for that subject.
- Subjects with no actions show a **Missing flow** badge in the Subject Flows list.
- When the AI feature is enabled, automated subject flows expose a chat AI profile selector plus subject goal and initial outbound prompt pattern fields.

### Permissions

| Permission | Description |
|-----------|-------------|
| `ListActivities` | List all omnichannel activities |
| `ListContactActivities` | List activities for a specific contact |
| `CompleteActivity` | Complete any activity |
| `CompleteOwnActivity` | Complete only own assigned activities |
| `EditActivity` | Edit activity details |
| `ManageDispositions` | Manage disposition categories |
| `ManageCampaigns` | Manage campaigns |
| `ManageActivityBatches` | Manage activity batches |
| `ManageChannelEndpoints` | Manage channel endpoints |

By default, the **Administrator** role has all permissions. The **Agent** role has `ListActivities` and `ListContactActivities`.

### Background Processing

The `AutomatedActivitiesProcessorBackgroundTask` runs on a cron schedule (`*/5 * * * *`, every 5 minutes). It queries activities with `Status = NotStarted` and `InteractionType = Automated` that are scheduled for dispatch (`ScheduledUtc <= now`), then routes them to the appropriate `IOmnichannelProcessor` implementation (e.g., `SmsOmnichannelProcessor`) in batches of 100.
