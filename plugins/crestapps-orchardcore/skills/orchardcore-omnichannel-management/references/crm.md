# Omnichannel CRM - Leads, Accounts and Opportunities

| Item | Value |
|---|---|
| Feature | **Omnichannel CRM** `CrestApps.OrchardCore.Omnichannel.Crm` |
| Depends on | `CrestApps.OrchardCore.Omnichannel.Managements`, `OrchardCore.ContentFields`, `OrchardCore.Lists`, `OrchardCore.Title` |
| Starter recipe | `OmnichannelCrmStarter` (adds `Lead`, `Contact`, `SalesOpportunity` types and lead sources) |
| Menus | **Interaction Center > Leads**, **Accounts**, **Opportunities**; **Management > Lead Statuses**, **Lead Sources**, **Opportunity Stages** |

None of the records is a new storage model; each is an ordinary content type
recognized by its parts (`OmnichannelRecordKinds`). With the feature off, every
type with `OmnichannelContactPart` is a contact.

| Kind | Parts |
|---|---|
| Contact | `OmnichannelContactPart` without `LeadPart` |
| Lead | `OmnichannelContactPart` and `LeadPart` (reachable on every channel, listed under **Leads**) |
| Account | `AccountPart` plus Orchard's `ListPart` holding contacts and opportunities |
| Opportunity | `OpportunityPart` (the account is its `ContainedPart`) |

## What the Feature Creates

- `LeadPart`, `AccountPart`, `OpportunityPart`; an `Account` type if none exists.
- `LeadIndex` (with `IDX_LeadIndex_ContentType_Converted`) and `OpportunityIndex`;
  `ContentType` and `IsConverted` columns on `OmnichannelContactIndex`.
- Seeded **Lead Status** and **Opportunity Stage** catalogs.
- `LeadPart` settings: **Converts into** (`TargetContactContentType`) and
  **Opportunity type** (`DefaultOpportunityContentType`). `OpportunityPart`
  settings: **Stages** (`StageIds`).

## Leads List Count

`LeadContentsAdminListQueryService` wraps `IContentsAdminListQueryService` and
counts the Leads list from `LeadIndex` alone (through `LeadListCountQueryFactory`)
when the list names only lead types the user may view, uses only plain lead
terms (`lead-status:`, `converted:`, `source:`, `list:`, `owner:`, `rating:`),
an optional plain `status:`, and no free text or other query values. Otherwise
the list keeps its own count.

## Caller Matching

Inbound voice and SMS resolve a number to contacts first, then open leads;
converted leads never match.

## Conversion

`ILeadConversionService.ConvertAsync(LeadConversionRequest)` is used by the
Convert screen and the **Convert Lead** subject action (`ConvertLead`, handler
`Order` `-100` so it runs first). It merges into an existing contact (copying
only missing parts, fields and contact methods, combining opt-outs) or creates
one, resolves the account (none, new, existing, or automatic by company name),
optionally creates an opportunity, moves finished activities (keeping
`ConvertedFromLeadItemId`), moves or cancels open ones, runs re-pointers and
handlers, and closes the lead. A lead with a live activity cannot be converted.

| Extension point | Use |
|---|---|
| `ILeadConversionHandler` | `ConvertingAsync` and `ConvertedAsync(LeadConversionContext)` |
| `ILeadConversionRepointer` | `RepointAsync` to move your records from the lead id to the contact (messaging threads, callbacks and voicemails use it) |
| `IUnattendedLeadConverter` | The converter used by the subject action, the workflow task and AI conversion |

```csharp
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace MyCompany.OrchardCore.Crm;

public sealed class NotifyErpLeadConversionHandler : ILeadConversionHandler
{
    public Task ConvertedAsync(LeadConversionContext context)
    {
        // context.Lead, context.Contact, context.Account and context.Opportunity are available.
        return Task.CompletedTask;
    }
}
```

Register with `services.AddScoped<ILeadConversionHandler, NotifyErpLeadConversionHandler>();`.

### AI Conversion

An **Automatic** load of a lead type can store `LeadAIConversionSettings`
(`Enabled`, `CreateOpportunity`, `OpportunityContentType`,
`QualificationGuidance`). When an automated SMS or AI call concludes on an open
lead, the conclusion prompt may return `ConvertLead: true`, and the lead is
converted through `IUnattendedLeadConverter` before the subject actions run. A
load that creates an opportunity without a resolvable opportunity type is
refused on save.

### Workflows

With `OrchardCore.Workflows`, the feature adds the **Lead Converted** event
(`LeadConvertedEvent`; input `ContentItem` is the contact, plus `Lead`,
`LeadContentItemId`, `ContactContentItemId`, `AccountContentItemId`,
`OpportunityContentItemId`, `ContactCreated`) and the **Convert Lead** task
(`ConvertLeadTask`, outcomes **Converted** and **Failed**).

## Lead Statuses

A status is Open (no flags), Closed (`IsClosed`) or Converted (`IsClosed` and
`IsConverted`). Only an open status can be `IsDefault`; a recipe that sets
`IsDefault` with a closed flag is rejected.

## Import and Export

Through Content Transfer (`orchardcore-content-transfer`), lead files add
`LeadStatus`, `LeadSource`, `LeadList`, `Company`, `Rating`, `LeadOwner`, and
export-only `IsConverted`, `ConvertedUtc`, `ConvertedContactItemId`,
`LastScrubbedUtc`.

## Permissions

`ConvertLead` (implied by `ManageActivities`), `EditConvertedLead`,
`ManageLeadStatuses`, `ManageOpportunityStages`. Listing and editing records
uses the normal content permissions; the CRM menus need `ListContent`.

## Reports

**Lead funnel**, **Lead conversion by source and list** and **Opportunity
pipeline** (CRM and Campaigns category, `ViewOmnichannelReports`).

## Recipes

```json
{
  "steps": [
    {
      "name": "OmnichannelLeadStatus",
      "LeadStatuses": [
        { "Name": "Working - Contacted", "Order": 2 },
        { "Name": "Nurturing", "Order": 3 }
      ]
    },
    {
      "name": "OmnichannelOpportunityStage",
      "OpportunityStages": [
        { "Name": "Negotiation", "Order": 4, "Probability": 70 }
      ]
    }
  ]
}
```

Both catalogs are matched by name on import and have matching deployment steps.
