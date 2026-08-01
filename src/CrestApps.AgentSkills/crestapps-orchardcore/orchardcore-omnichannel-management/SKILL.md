---
name: orchardcore-omnichannel-management
description: Skill for administering Omnichannel contacts, activities, batches, subject flows, campaigns, dispositions, and channel endpoints in Orchard Core using CrestApps modules. Covers the Interaction Center, contact content parts, activity dispatch, subject actions, permissions, and contact import integration. Use this skill when requests mention Orchard Core Omnichannel Management, Interaction Center, activity batches, subject flows, dispositions, channel endpoints, OmnichannelContactPart, or automated activity processing. Strong matches include work with CrestApps.OrchardCore.Omnichannel.Managements, AutomatedActivitiesProcessorBackgroundTask, SubjectFlowSettings, SubjectAction, OmnichannelActivity, OmnichannelActivityBatch, and IOmnichannelActivityManager.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Omnichannel Management

## Configure the Interaction Center

You are an Orchard Core expert. Build contact and activity operations around the
Omnichannel Management feature's existing Interaction Center rather than
inventing agent queues, routing rules, or a separate conversation-management
surface that this module does not provide.

### Guidelines

- Install `CrestApps.OrchardCore.Omnichannel.Managements` in the web/startup
  project. It depends on the base Omnichannel feature and required CrestApps
  contact, phone verification, user, time-zone, resource, and Orchard features.
- Enable the exact management feature ID
  `CrestApps.OrchardCore.Omnichannel.Managements`.
- The feature adds **Interaction Center** administration for Activities and a
  Management submenu containing Manage Activities, Load Inventory, Subject
  Flows, Campaigns, Campaign Groups, Dispositions, and Channel Endpoints.
- Contacts are ordinary Orchard content items with `OmnichannelContactPart`.
  The management module also registers `PhoneNumberInfoPart`, `EmailInfoPart`,
  and `OmnichannelContactInfoPart`.
- A subject is a content type with the `OmnichannelSubject` stereotype.
  `SubjectFlowSettings` associates each configured subject type with a campaign,
  interaction type, channel, and, for automated work, a channel endpoint.
- This module assigns activities to users but does not implement agent queues
  or routing algorithms. Use activity assignment and the bulk management UI;
  do not document queue or conversation-routing configuration that does not
  exist here.
- Install an `IOmnichannelProcessor` feature, such as SMS automation, before
  creating automated activity flows for that channel.
- Keep custom C# types `sealed`, use file-scoped namespaces, and keep View
  Models unsealed when model binding requires them.

## Feature and Package

| Item | Value |
|---|---|
| NuGet package | `CrestApps.OrchardCore.Omnichannel.Managements` |
| Base feature | `CrestApps.OrchardCore.Omnichannel` |
| Management feature | `CrestApps.OrchardCore.Omnichannel.Managements` |
| Admin area | **Interaction Center** |

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

## Interaction Center Surface

| Area | What the module manages |
|---|---|
| Activities | Review, create, edit, complete, and filter omnichannel work |
| Manage Activities | Bulk assignment, scheduling, subject, instruction, urgency, and purge operations |
| Load Inventory | Create and manage activity batches that define contact filters and generate activities |
| Subject Flows | Configure a flow per `OmnichannelSubject` content type |
| Campaigns | Maintain campaign catalog records |
| Campaign Groups | Maintain campaign-group catalog records |
| Dispositions | Maintain outcomes used by subject actions |
| Channel Endpoints | Maintain channel-specific service addresses |

Activities are represented by `OmnichannelActivity`; batches are represented by
`OmnichannelActivityBatch`. The management feature uses Orchard display managers for activity batches and
subject actions, alongside its activity and channel-endpoint stores and
managers. Extend those display-driver surfaces rather than replacing their
controllers with a separate queue UI.

## Model Contacts Correctly

Create a business-specific content type, attach `OmnichannelContactPart`, then
add the contact fields your organization needs.

1. Go to **Content Definition** and create or edit the contact type.
2. Attach `OmnichannelContactPart`.
3. Add phone, email, identity, and business fields as required.
4. Create or import content items of that type.
5. Configure only the communication-preference controls that apply to the type.

The part tracks `DoNotCall`, `DoNotSms`, `DoNotEmail`, and `DoNotChat` with UTC
timestamps. The management feature’s contact definition services enforce the
Omnichannel contact structure and support activity lookup and contact indexing.

## Configure Subject Flows

Create a content type with the `OmnichannelSubject` stereotype to model the
subject or data gathered by an interaction.

1. Create a campaign and the dispositions the flow needs.
2. Open **Interaction Center → Subject Flows**.
3. Select **Configure** for the subject content type.
4. Select the campaign, interaction type, and channel.
5. For an automated interaction, select a channel endpoint.
6. Save the flow and then select **Manage Flow** to define actions.

`SubjectFlowSettingsService` considers a flow configured only when it has:

- `SubjectContentType`
- `CampaignId`
- `Channel`
- `ChannelEndpointId` for `Automated` interactions

Only configured subjects are appropriate for loading activities. If the AI
feature is enabled, `AISubjectFlowSettingsDisplayDriver` adds AI-related fields
to the subject-flow editor.

## Define Disposition Actions

`SubjectAction` entries run after an activity is completed with the matching
subject type and disposition. The built-in action types are:

| Action | Behavior |
|---|---|
| Finish | Makes no additional activity changes |
| Try Again | Creates a retry activity with attempt, urgency, schedule, and optional assignee settings |
| New Activity | Creates a new activity and resolves target-subject flow settings when available |

Each action can also apply contact communication preferences. For example, use a
disposition action to set `DoNotSms` after a customer opts out.

The default `ISubjectActionExecutor` is `DefaultSubjectActionExecutor`. It
executes all actions matching the completed activity’s subject content type and
disposition; it does not execute a generic campaign-wide routing rule.

## Create and Process Activities

Use an activity batch to find contacts and create work at scale:

1. Create an `OmnichannelActivityBatch`.
2. Choose the contact criteria, subject type, and assignment details.
3. Load activities through the batch UI.
4. Resolve work manually in **Activities**, or enable a processor for automated
   work.
5. Complete an activity with a disposition to execute the matching subject
   actions.

`AutomatedActivitiesProcessorBackgroundTask` runs every five minutes with the
schedule `*/5 * * * *`. It selects scheduled automated activities in batches
of 100 and dispatches them to registered `IOmnichannelProcessor` instances
matched by channel. It only processes activities whose status is `NotStated` or `Scheduled`,
whose interaction type is `Automated`, and whose scheduled time has arrived.

## Add a Custom Processor

A channel feature can supply a processor for automated activity dispatch. Keep
the channel name aligned with the activity's selected channel.

```csharp
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace MyCompany.OrchardCore.Communications;

public sealed class ExampleOmnichannelProcessor : IOmnichannelProcessor
{
    public string Channel => "Example";

    public Task StartAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        // Send or start work for the selected activity.
        return Task.CompletedTask;
    }
}
```

Register the implementation in the owning feature as an
`IOmnichannelProcessor`. Do not register a processor for a channel until its
outbound provider credentials, endpoint behavior, and retry policy are ready.

## Permissions

The feature grants all of the following to the Administrator stereotype:

- `ListActivities`
- `ListContactActivities`
- `CompleteActivity`
- `CompleteOwnActivity`
- `ManageActivities`
- `PurgeActivity`
- `ManageDispositions`
- `ManageCampaigns`
- `ManageCampaignGroups`
- `ManageChannelEndpoints`
- `ManageActivityBatches`
- `ManageSubjectFlows`

The `Agent` stereotype receives `ListActivities`, `ListContactActivities`, and
`CompleteOwnActivity`. Grant management permissions deliberately; activity
purging and broad assignment affect operational data.

## Operational Checklist

- Configure a subject flow before loading activities for that subject.
- Create endpoints before using an automated flow that requires one.
- Enable a matching channel processor before scheduling automated work.
- Model opt-out preferences on contacts and apply them through disposition
  actions where appropriate.
- Review the five-minute processor schedule and monitor failed processor logs.
- Use Activity Batches for bulk generation rather than custom direct database
  writes.
- Treat activities as the module's work-management boundary; this feature has
  no native queue, routing-engine, or conversation-inbox configuration.
