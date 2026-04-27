---
name: orchardcore-omnichannel
description: Skill for configuring Omnichannel communication in Orchard Core using CrestApps modules. Covers multi-channel messaging (SMS, email, phone, chat), contact management, campaigns, AI-powered SMS automation, Azure Event Grid webhooks, and Twilio integration. Use this skill when requests mention Orchard Core Omnichannel, Omnichannel SMS, Contact Management, Channel Endpoints, Communication Preferences, Activity Batches, Campaigns, Dispositions, or related setup and troubleshooting. Strong matches include CrestApps.OrchardCore.Omnichannel, CrestApps.OrchardCore.Omnichannel.Managements, CrestApps.OrchardCore.Omnichannel.Sms, CrestApps.OrchardCore.Omnichannel.EventGrid, OmnichannelContactPart, IOmnichannelProcessor, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Omnichannel - Prompt Templates

## Configure Omnichannel

You are an Orchard Core expert. Generate code, configuration, and recipes for adding omnichannel communication capabilities to an Orchard Core application using CrestApps modules.

### Guidelines

- The CrestApps Omnichannel module (`CrestApps.OrchardCore.Omnichannel`) provides a unified multi-channel communication layer supporting SMS, email, phone, and chat channels.
- The Managements feature (`CrestApps.OrchardCore.Omnichannel.Managements`) adds an admin UI for contacts, activities, campaigns, dispositions, channel endpoints, and activity batches under the **Interaction Center** menu.
- The SMS feature (`CrestApps.OrchardCore.Omnichannel.Sms`) enables AI-powered SMS automation. It integrates with the AI Chat module to run AI-driven conversations over SMS using Twilio webhooks.
- The Event Grid feature (`CrestApps.OrchardCore.Omnichannel.EventGrid`) receives inbound messages from Azure Event Grid via a webhook endpoint, validated with a SAS key or AAD bearer token.
- The Azure Communication Services feature (`CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices`) provides integration points for Azure Communication Services.
- Omnichannel domain data (messages, activities, batches, AI chat sessions) is stored in a dedicated `Omnichannel` YesSql collection.
- Communication preferences (`DoNotCall`, `DoNotSms`, `DoNotEmail`, `DoNotChat`) are tracked per contact with UTC timestamps.
- The SMS module validates inbound Twilio requests using HMAC-SHA1 signature verification against the Twilio AuthToken.
- A background task runs every 5 minutes to process automated activities that are scheduled and ready for dispatch.
- Always secure API keys, SAS keys, and Twilio credentials using user secrets or environment variables; never hardcode them.
- Install CrestApps packages in the web/startup project.

### Available Omnichannel Features

| Feature | Feature ID | Description |
|---------|-----------|-------------|
| Omnichannel | `CrestApps.OrchardCore.Omnichannel` | Base omnichannel layer with message indexing and contact communication preferences |
| Azure Communication Services | `CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices` | Azure Communication Services integration for multi-channel messaging |
| Azure Event Grid | `CrestApps.OrchardCore.Omnichannel.EventGrid` | Webhook endpoint for receiving inbound messages from Azure Event Grid |
| Omnichannel Management | `CrestApps.OrchardCore.Omnichannel.Managements` | Admin UI for contacts, activities, campaigns, dispositions, and channel endpoints |
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

- **SMS** - Text messaging via Twilio or Azure Communication Services
- **Email** - Email communication with contact email tracking
- **Phone** - Voice call tracking with do-not-call preferences
- **Chat** - Chat messaging with do-not-chat preferences

### Content Types and Parts

| Content Type / Part | Stereotype | Description |
|---------------------|-----------|-------------|
| `OmnichannelContactPart` | — | Attachable part that marks a content item as an omnichannel contact |
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

The webhook endpoint is available at `POST Omnichannel/webhook/AzureEventGrid`. It validates requests using either a SAS key (`aeg-sas-key` header) or an AAD bearer token.

### Webhook Endpoints

| Method | Route | Module | Authentication |
|--------|-------|--------|---------------|
| POST | `Omnichannel/webhook/AzureEventGrid` | Event Grid | SAS key or AAD bearer token |
| POST | `Omnichannel/webhook/Twilio` | SMS | Twilio HMAC-SHA1 signature |
| POST | `Omnichannel/webhook/TwilioEventGrid` | SMS | Twilio HMAC-SHA1 signature |

All webhook endpoints are anonymous and do not require antiforgery tokens. They validate authenticity using their respective authentication mechanisms.

### AI-Powered SMS Automation

The SMS module integrates with the CrestApps AI Chat module to enable automated SMS conversations:

1. **Outbound** - The `SmsOmnichannelProcessor` creates AI chat sessions, renders initial messages using Liquid templates from the campaign configuration, and sends them via `ISmsService`.
2. **Inbound** - The `SmsOmnichannelEventHandler` receives customer SMS replies, feeds them into the AI chat session as user prompts, runs AI completion, and sends the AI response back as SMS.
3. **Conclusion Analysis** - A deferred task uses AI with the `sms-conclusion-analysis` prompt template to determine if the conversation has concluded. When concluded, it auto-sets the disposition and triggers the `CompletedActivityEvent` workflow event.

### Admin UI - Interaction Center

The Managements feature adds an **Interaction Center** menu in the admin dashboard with the following sections:

1. **Activities** - View and manage omnichannel activities (calls, SMS, emails). Filter by status, channel, campaign, and assignee.
2. **Activity Batches** - Group and manage activities in batches for bulk operations.
3. **Campaigns** - Define campaign configurations including AI profiles, deployment names, and message templates.
4. **Dispositions** - Configure activity outcome categories (e.g., completed, no answer, callback requested).
5. **Channel Endpoints** - Manage communication channel endpoints (phone numbers, SMS numbers, email addresses).

### Workflow Integration

The Managements feature provides workflow tasks and events for automation:

**Workflow Events:**
- `CompletedActivityEvent` - Fires when an activity is completed, providing Activity, Contact, Subject, and Disposition data.

**Workflow Tasks:**
- `TryAgainActivityTask` - Creates a retry activity with configurable max attempts, urgency level, and schedule delay.
- `NewActivityTask` - Creates a new activity for a different campaign or subject.
- `SetContactCommunicationPreferenceActivityTask` - Updates a contact's DoNotCall, DoNotSms, DoNotEmail, or DoNotChat preferences.

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
