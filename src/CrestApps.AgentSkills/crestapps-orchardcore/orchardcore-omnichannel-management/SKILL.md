---
name: orchardcore-omnichannel-management
description: Skill for the CrestApps Omnichannel Management (CRM) module in Orchard Core. Covers CrestApps.OrchardCore.Omnichannel.Activities, ChannelEndpoints, Managements and Crm features; contacts with OmnichannelContactPart, IContactMethodsAccessor, IContactMethodsProvider and IContactMethodsWriter, OmnichannelContactPhoneNumberIndex and IOmnichannelContactPhoneLookup; omnichannel addresses and capabilities; subjects, dispositions, campaigns, subject actions, activity loads, cadences, leads, accounts and opportunities; the automated activity processor, DispositionedBy, CRM reports, permissions, admin lists and the Omnichannel recipe steps. Use this skill when requests mention Omnichannel Management, Interaction Center, CRM contacts, subject flows, activity loads, dispositions, cadences, leads or omnichannel addresses. Strong matches include OmnichannelActivity, OmnichannelActivityBatch, ISubjectActionHandler, IActivityBatchLoader, ILeadConversionService, and AddContactMethodsProvider.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Omnichannel Management (CRM)

## Configure the Interaction Center

You are an Orchard Core expert. Build contact and activity work on the
Omnichannel Management module's existing model and extension points. Queues,
routing, agents and the dialer belong to the Contact Center
(`orchardcore-contact-center`); the human inbox belongs to
`orchardcore-omnichannel-messaging`.

### Guidelines

- Install `CrestApps.OrchardCore.Omnichannel.Managements` in the web/startup
  project and enable `CrestApps.OrchardCore.Omnichannel.Managements`; it brings
  the headless and address features with it.
- The module depends on `OrchardCore.Resources` (it uses Orchard Core's
  `bootstrap-select` resource), `OrchardCore.Flows`, `OrchardCore.ContentTypes`
  and the CrestApps Users, ContentFields, PhoneNumbers and TimeZones features.
- Read contact phone numbers and emails only through `IContactMethodsAccessor`.
- Configure a subject before loading activities for it, and enable a channel
  processor (`orchardcore-omnichannel-sms`, `orchardcore-omnichannel-voice`)
  before scheduling automated work.
- Every admin list renders through Orchard Core's `AdminList` shape (see the
  framework skill `orchardcore-admin-lists`).
- C# samples use `sealed` classes and file-scoped namespaces.

## Features

| Feature | ID | Notes |
|---|---|---|
| Omnichannel Activities | `CrestApps.OrchardCore.Omnichannel.Activities` | Headless: catalogs, stores, managers, parts, indexes, migrations, permissions, recipe and deployment steps, `api/omnichannel/subject-disposition-actions`. |
| Omnichannel Channel Endpoints | `CrestApps.OrchardCore.Omnichannel.ChannelEndpoints` | Dependency only. **Interaction Center > Management > Omnichannel Addresses**. Enabled by Management, Messaging Workspace, Contact Center Voice and Inbound Entry Points. |
| Omnichannel Management | `CrestApps.OrchardCore.Omnichannel.Managements` | Admin screens, display drivers, menus, reports. |
| Omnichannel CRM | `CrestApps.OrchardCore.Omnichannel.Crm` | Leads, accounts, opportunities. See [references/crm.md](references/crm.md). |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Omnichannel.Managements"
      ],
      "disable": []
    }
  ]
}
```

The `OmnichannelCrmStarter` recipe enables Management and CRM and adds `Lead`,
`Contact` and `SalesOpportunity` types.

## Admin Surface

| Menu | Purpose | Permission |
|---|---|---|
| Interaction Center > Activities | Your own **Not started** manual activities (`Admin/omnichannel/activities`) | `ListActivities` |
| Interaction Center > Contacts | Content list filtered to every type with `OmnichannelContactPart` | `ListContent` |
| Management > Manage Activities | Bulk assign, reschedule, change subject, source, dialer profile, purge (`Admin/omnichannel/manage-activities`) | `ManageActivities` |
| Management > Load Activities | Activity batches (Manual, Automatic, Dialer sources) | `ManageActivityBatches` |
| Management > Subject Flows | Read-only overview with **Edit Content Type**, **Edit Settings**, **Manage Flow** | `ManageSubjectFlows` |
| Management > Campaigns, Campaign Groups, Dispositions | Catalogs | `ManageCampaigns`, `ManageCampaignGroups`, `ManageDispositions` |
| Management > Cadences | Re-engagement follow-ups | `ManageCadences` |
| Management > Numbers Not In Service | Dead numbers | `ManageActivities` |
| Management > Omnichannel Addresses | Owned numbers and their capabilities | `ManageChannelEndpoints` |

Admin list names, for `IAdminListColumnsProvider` or
`AdminListCell-{List}-{Column}.cshtml` overrides, include
`OmnichannelActivities`, `OmnichannelManageActivities`,
`OmnichannelContactScheduledActivities`, `OmnichannelContactCompletedActivities`,
`OmnichannelActivityBatches`, `OmnichannelSubjectFlows`,
`OmnichannelSubjectActions`, `OmnichannelCampaigns`, `OmnichannelCampaignGroups`,
`OmnichannelDispositions`, `OmnichannelCadences`, `OmnichannelChannelEndpoints`
and `OmnichannelNotInServiceNumbers`.

## Contacts and Contact Methods

A contact is any content item with `OmnichannelContactPart`, which stores
`TimeZoneId` and `DoNotCall`, `DoNotSms`, `DoNotEmail` with UTC timestamps
(set them with `SetDoNotCall`, `SetDoNotSms`, `SetDoNotEmail`). Unless a
provider is registered for the type, a fixed `ContactMethods` bag of
`ContactMethod` stereotype items (`PhoneNumber`, `EmailAddress`) is injected;
do not rename or replace it.

`OmnichannelContactPartSettings`: **Auto detect time zone**
(`AutoDetectTimeZone`), **Require time zone** (`RequireTimeZone`, enforced only
when auto detect is off), **Use Do not call**, **Use Do not SMS**, **Use Do not
email**.

### Keep Numbers and Emails in Your Own Fields

Every reader (contact index, inbound call and SMS matching, `phone:` search,
lead matching, opt-outs, import duplicates, activity destinations, the contact
header, messaging, time zone detection, phone verification) goes through
`IContactMethodsAccessor`, which reads the bag by default.

```csharp
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement;

namespace MyCompany.OrchardCore.Crm;

public sealed class PersonContactMethodsProvider : IContactMethodsProvider
{
    public ContactMethodSet GetContactMethods(ContentItem contact)
    {
        if (!contact.TryGet<PersonPart>(out var person))
        {
            return ContactMethodSet.Empty;
        }

        return new ContactMethodSet(
            [
                new ContactPhoneNumber { PhoneNumber = person.Mobile?.PhoneNumber, CountryCode = person.Mobile?.CountryCode, Type = ContactPhoneTypes.Cell },
                new ContactPhoneNumber { PhoneNumber = person.Office?.PhoneNumber, CountryCode = person.Office?.CountryCode, Type = ContactPhoneTypes.Office },
            ],
            [
                new ContactEmailAddress { Email = person.Email?.Text },
            ]);
    }
}
```

```csharp
services.AddContactMethodsProvider<PersonContactMethodsProvider>("Person");
services.AddContactMethodsWriter<PersonContactMethodsWriter>("Person");
```

- A provider is a singleton that reads only the given content item (no queries,
  no scoped services); the contact index is built from it on save.
- `ContactPhoneTypes` are `Cell`, `Home`, `Office`, `Work`, `Other`; `Mobile`
  normalizes to `Cell`, case and spaces are ignored.
- A type with a provider is not given the bag (an existing bag is left intact
  but no longer read), and imports leave its Email, Cell Phone and Phone columns
  to the type's own fields.
- What the product learns (an email given in a conversation, a converted lead's
  numbers) is written through `IContactMethodsWriter`
  (`TrySetEmailAddressAsync`, `TryAddEmailAddressAsync`, `TryAddPhoneNumberAsync`).
  A type with a provider and no writer is read-only.
- Register `services.AddContactMethods()` when you construct the readers
  outside a startup that already does.

### Match a Number to a Contact

`OmnichannelContactPhoneNumberIndex` holds one row per distinct E.164 number of
every published or latest contact and lead (type, content type, converted flag).
Find records by number with `IOmnichannelContactPhoneLookup.FindByNumbersAsync`
or list a record's numbers with `GetNumbersAsync` (batches of 50). This covers
inbound calls and texts, shared-number opt-outs, lead matching, import
duplicates and the dialer's lead check, so a contact calling from a Work number
is recognized. On upgrade, the **Omnichannel Contact Phone Number Backfill**
task indexes older records (up to 2,000 a minute, resumable); until it finishes,
lookups also read the first Cell and Home numbers.

The `phone:`, `phone-exact:`, `phone-starts:` and `phone-ends:` terms and the
activity phone filters search the primary Cell and Home numbers. On
contact-only content lists the search box also matches a number that begins
with the digits typed (at least three digits, no letters).

## Omnichannel Addresses

`OmnichannelChannelEndpoint` is an owned address with an `AddressType`
(`OmnichannelAddressTypes.PhoneNumber` or `EmailAddress`), a normalized `Value`
(E.164 for numbers), and `Capabilities` named by channel (`Phone`, `SMS`). One
number used for calls and texts is one address with both capabilities. Features
contribute capabilities:

```csharp
services.AddOmnichannelAddressCapability(OmnichannelAddressTypes.PhoneNumber, "WhatsApp", capability =>
{
    capability.DisplayName = S["WhatsApp"];
    capability.Description = S["Messages sent and received on this number."];
});
```

- `Phone` comes from Contact Center Voice, `SMS` (with the provider picker) from
  the SMS Messaging Channel. A capability whose feature is disabled stays on the
  address and returns with the feature.
- Add capability settings with a `DisplayDriver<OmnichannelChannelEndpoint>`
  whose shape root carries `data-address-capability="<capability>"`.
- Check `endpoint.HasCapability("SMS")`; `IOmnichannelChannelEndpointManager.GetByServiceAddressAsync(channel, address)`
  only returns addresses with that capability.
- Register `AddOmnichannelAddressType` for a new type and an
  `IChannelEndpointAddressPolicy` to normalize and validate its values.
- Addresses cannot be deleted and their type is fixed after creation.

## Subjects, Dispositions, Campaigns

- A subject is any content type with `OmnichannelSubjectPart`. The
  `OmnichannelSubject` stereotype is no longer recognized.
- Subject configuration lives in `OmnichannelSubjectPartSettings` on the content
  type: `Direction` (`Outbound` default, `Inbound`), `InteractionType` and
  `Channel` (inbound only), `ChannelEndpointId`, `DefaultCampaignId`,
  `RequireDisposition` (default `true`). It travels with the
  `ContentDefinition` recipe step.
- With AI enabled, an **AI configuration** card (`OmnichannelSubjectAISettings`)
  holds the profile, subject goal, AI update permissions, speech and voice
  defaults, SMS timeout, delay and opt-out keywords, and the **Live agent
  handoff** settings (`EnableAgentHandoff`, `HandoffQueueId`,
  `HandoffOnUserRequest`, `HandoffOnQualifiedLead`, `HandoffOnFrustration`).
  Outbound AI settings come from the **Automatic** load instead.
- `ISubjectFlowSettingsService` resolves the effective `SubjectFlowSettings`.
- Dispositions have unique, fixed names and an optional `Outcome`
  (`DispositionOutcome`) used when a call ends without a choice (`NotInService`,
  `NoAnswer`, `Busy`, `AnsweringMachine`, `Rejected`, `Failed`, `Disconnected`).
- Campaigns and campaign groups are grouping and reporting records only; they
  cannot be deleted.

## Subject Actions

`SubjectAction` rows map a subject type and disposition to an action type
(`Source`): `Finish`, `TryAgain`, `NewActivity` (and `ConvertLead` with CRM).
Each can set `SetDoNotCall`, `SetDoNotSms`, `SetDoNotEmail`, and carries
`DispositionGuidance` that the AI reads when it dispositions.
`DefaultSubjectActionExecutor` runs every action matching the completed
activity's subject and disposition, ordered by the handler's `Order`.

Add an action type:

```csharp
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace MyCompany.OrchardCore.Crm;

public sealed class NotifyCrmSubjectActionHandler : ISubjectActionHandler
{
    public string ActionType => "NotifyCrm";

    public Task ExecuteAsync(SubjectAction action, SubjectActionExecutionContext context)
    {
        // Push the completed activity to the external CRM.
        return Task.CompletedTask;
    }
}
```

```csharp
services.AddScoped<ISubjectActionHandler, NotifyCrmSubjectActionHandler>();
services.Configure<SubjectActionOptions>(options =>
{
    options.AddActionType("NotifyCrm", entry =>
    {
        entry.DisplayName = S["Notify CRM"];
        entry.Description = S["Sends the outcome to the external CRM."];
    });
});
```

## Activities and Loads

- `OmnichannelActivity` is the work item. Completed activities record
  `DispositionedBy` (`ActivityDispositionActor`: `Unknown`, `User`, `AIAgent`,
  `Dialer`, `System`) and `DispositionedByAIProfileId`; dialer work records
  `DialerProfileId`. Completion goes through `IActivityDispositionService`
  (pass `ActivityDispositionRequest.DispositionedBy` when the caller knows it).
- Activity rows show the interaction type and source, AI profile, dialer profile
  and campaign, and assignee (resolved once per page by `ActivityHandlerDescriber`).
- An `OmnichannelActivityBatch` (load) pages contacts and creates activities in
  the background; it starts only on **Load activities**. **Automatic** loads
  create `Automated` activities driven by a Chat profile with **Start the
  conversation automatically**, plus an optional cadence and business-hours
  calendar (see [references/cadences.md](references/cadences.md)).
- `AutomatedActivitiesProcessorBackgroundTask` (`*/5 * * * *`) skips numbers
  known not in service, then dispatches due `NotStated` or `Scheduled`
  automated activities to the `IOmnichannelProcessor` for their channel.
  `OmnichannelAutomationOptions` (section `CrestApps:Omnichannel:Automation`,
  registered when AI is enabled) sets `ProcessorBatchSize` (100),
  `MaxActivitiesPerInvocation` (1,000), `MaxProcessingAttempts` (5) and
  `RetryDelayMinutes` (5).

### Extend Loads, Sources and Channels

```csharp
services.Configure<ActivityBatchSourceOptions>(options =>
{
    options.AddSource("Webinar", entry =>
    {
        entry.DisplayName = S["Webinar attendees"];
        entry.Description = S["Loads the attendees of a webinar."];
        entry.RequiresUserAssignment = false;
    });
});

services.AddScoped<IActivityBatchLoader, WebinarActivityBatchLoader>();
services.Configure<ActivitySourceOptions>(options => options.AddSource("Webinar", entry => entry.DisplayName = S["Webinar"]));
services.Configure<ActivityChannelOptions>(options => options.AddChannel("WhatsApp", entry => entry.DisplayName = S["WhatsApp"]));
```

`IActivityBatchLoader.Source` must match the registered source; without a
loader, `DefaultContactActivityBatchLoader` (not sealed, so you can inherit it)
is used. `ActivitySourceOptions` and `ActivityChannelOptions` drive the Source
and Channel filters; only sources with `CanBeSetManually` are offered by
**Change Source**.

## Reports

With `CrestApps.OrchardCore.Reports` enabled, the module contributes CRM
reports (Operations, Queue and Routing, Agent Performance, CRM and Campaigns,
Compliance and Audit, Technical and IT), gated by `ViewOmnichannelReports`
(implied by `ManageActivities`, granted to Administrator and Supervisor).

- Every report filters by campaign group, campaign, channel, source, status and
  several dispositions (`OmnichannelReportCriteria.DispositionIds`).
- **Dialer call detail** (`omnichannel-dialer-call-detail`,
  `DialerCallDetailReportProvider`) lists one row per dialed activity, pages 100
  rows in SQL on screen and streams exports in batches of 500. It presets
  **Source** to **Dialer** through `IOmnichannelReportFilterDefaults`.
- A report that needs the page picker names
  `OmnichannelReportFilterDisplayDriver.PageFilterName` in
  `IReportFilterMetadata.FilterNames`. See `orchardcore-reports`.

## Permissions

Registered by the headless feature. Administrator gets all; **Agent** gets
`ListActivities`, `ListContactActivities`, `CompleteOwnActivity`,
`EditActivity`; **Supervisor** gets `ViewOmnichannelReports`.

`ListActivities`, `ListContactActivities` (implied by `ListActivities`),
`CompleteActivity`, `CompleteOwnActivity`, `EditActivity` (implied by
`ManageActivities`), `ManageActivities`, `PurgeActivity` (implied by
`ManageActivities`), `ManageDispositions`, `ManageCampaigns`,
`ManageCampaignGroups`, `ManageCadences`, `ManageChannelEndpoints`,
`ManageActivityBatches`, `DeleteLoadedActivityBatches`, `ManageSubjectFlows`,
`ViewOmnichannelReports`. CRM adds `ConvertLead`, `EditConvertedLead`,
`ManageLeadStatuses`, `ManageOpportunityStages`.

## Recipes and Deployment

Each catalog has a deployment step (category **Omnichannel**) and a recipe step.
Entries are matched by `ItemId` (updated in place, or created with that id).
Import in order: dispositions, channel endpoints and cadences, then campaign
groups, then campaigns, then subject actions.

```json
{
  "steps": [
    {
      "name": "OmnichannelDisposition",
      "Dispositions": [
        { "ItemId": "4k2q8m1x7c3v9b5n6t0h2y7za", "Name": "Interested", "Description": "The customer wants a follow-up.", "Outcome": "None" },
        { "ItemId": "9p3r7s2t6u1v5w8x0y4z2a7bc", "Name": "No Answer", "Outcome": "NoAnswer" }
      ]
    },
    {
      "name": "OmnichannelChannelEndpoint",
      "ChannelEndpoints": [
        { "ItemId": "1a2b3c4d5e6f7g8h9j0k1m2nq", "DisplayText": "Main line", "AddressType": "PhoneNumber", "Value": "+17025550100", "Capabilities": [ "Phone", "SMS" ] }
      ]
    },
    {
      "name": "OmnichannelCampaignGroup",
      "CampaignGroups": [
        { "ItemId": "5d6e7f8g9h0j1k2m3n4p5q6rs", "DisplayText": "Sales" }
      ]
    },
    {
      "name": "OmnichannelCampaign",
      "Campaigns": [
        { "ItemId": "6e7f8g9h0j1k2m3n4p5q6r7st", "DisplayText": "Spring promotion", "CampaignGroupId": "5d6e7f8g9h0j1k2m3n4p5q6rs" }
      ]
    },
    {
      "name": "OmnichannelSubjectAction",
      "SubjectActions": [
        { "ItemId": "7f8g9h0j1k2m3n4p5q6r7s8tu", "Source": "Finish", "DisplayText": "Done", "SubjectContentType": "SalesCall", "DispositionId": "4k2q8m1x7c3v9b5n6t0h2y7za" }
      ]
    }
  ]
}
```

`OmnichannelCadence` is shown in [references/cadences.md](references/cadences.md);
`OmnichannelLeadStatus` and `OmnichannelOpportunityStage` in
[references/crm.md](references/crm.md). The full schemas live in the
`orchardcore-recipes` skill's recipe-schemas reference.

## Data at Rest

Message bodies and customer and service addresses are stored unencrypted in the
tenant database (and addresses in `OmnichannelMessageIndex`). **Purge** marks an
activity `Purged` but deletes no message or contact data. There is no automated
per-contact erasure; handle erasure requests with audited database operations.

## Upgrading from 2.x

- Replace `OmnichannelSubjectWriter.GetContactEmail(contact)` with
  `IContactMethodsAccessor.Get(contact).GetPrimaryEmailAddress()`;
  `OmnichannelSubjectWriter.TryApplyContactEmailAsync` takes an
  `IContactMethodsWriter`.
- `SmsMessagingChannel`, `DefaultContactActivityBatchLoader`,
  `OmnichannelContactPartContentImportHandler` and `ActivitiesController` take an
  `IContactMethodsAccessor`; `ContactOptOutResolver`, `SmsMessagingChannel`,
  `LeadMatchFinder`, `OmnichannelContactDuplicateLookupService` and
  `DefaultContactActivityBatchLoader` take an `IOmnichannelContactPhoneLookup`.
  `IOmnichannelContactDuplicateLookupService.GetPhoneNumberOwnersAsync` and
  `OmnichannelContactMatches` take `IOmnichannelContactMatch`.
- Admin list view models expose `AdminListContext` (`List`, or
  `ScheduledList`/`CompletedList`); row view models moved to `Models`
  (`SubjectFlowEntry`, `SubjectActionEntry`, `NotInServiceNumberEntry`), and
  `ActivitiesController` no longer takes `IDisplayManager<OmnichannelActivityContainer>`.
  A driver adding a shape to a row needs a `SummaryAdminGrid` location too, and
  `ActionsMenu` items are bare `<a class="dropdown-item">` elements.
- Replace the `OmnichannelSubject` stereotype with `OmnichannelSubjectPart`;
  subject flows are part settings, not a separate configure screen.
- Contacts no longer have `DoNotChat`.
- Addresses gained `AddressType` and `Capabilities`; the upgrade merges
  per-channel duplicates and keeps the old ids in `MergedItemIds`, and old
  recipes still import.
- `crestapps-bootstrap-select` is gone; use Orchard Core's `bootstrap-select`.
- Migration fixes: the contact index migration no longer stalls on PostgreSQL,
  and a repair step restores missing `Source` and `CreatedUtc` columns on
  `OmnichannelActivityBatchIndex`.
