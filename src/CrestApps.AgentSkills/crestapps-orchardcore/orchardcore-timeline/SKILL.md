---
name: orchardcore-timeline
description: Skill for the CrestApps Orchard Core Timeline module (CrestApps.OrchardCore.Timeline), comments, internal notes and events on any record. Covers TimelineEntry on a RecordRef with kinds, Public and Internal visibility, authors, field changes, files and external ids, ITimelineService, ITimelineEntryHandler, ITimelineAccessProvider, the content item provider and AllowPublicTimelineEntries, the Timeline view component and composer kinds, mentions through notifications, the orphan file sweeper, the RedactTimelineEntries permission, the TimelineEntries recipe and deployment step and the data set. Use this skill when requests mention notes, comments, activity history on a record, a notes tab, mentions or redacting a note. Strong matches include TimelineEntry, TimelineVisibility, TimelineFieldChange, TimelineQuery, TimelineComposerKind, TimelineEntryIds, ITimelineFileService, AddTimelineAccessProvider and AddTimelineEntryHandler.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Timeline

## What Was Said and Done on a Record

You are an Orchard Core expert. Generate accurate setup and extension code for the CrestApps Timeline module. A timeline keeps comments, internal notes and events on any record (a `RecordRef` such as `WorkOrder:4abc...` or `Content:4xyz...`), newest first, with who wrote them, the field changes they record and their files. Ticket replies, notes and history, work order notes, order notes and the **Notes** tab of accounts and contacts all live in it.

### Guidelines

- Install `CrestApps.OrchardCore.Timeline` in the web/startup project. Tickets depends on it; enable `CrestApps.OrchardCore.Attachments` for files on entries.
- Reference `CrestApps.OrchardCore.Timeline.Abstractions` (models, `ITimelineService`, handlers, access providers, `TimelineConstants`, `TimelinePermissions`, registration helpers) or `CrestApps.OrchardCore.Timeline.Core` (`AddTimelineCore()`, store, default services) from your module.
- `ITimelineService` checks nothing about the user. A page acting for a user asks `ITimelineAccessService`, which asks the `ITimelineAccessProvider` registered for the record kind. **A kind without a provider is refused**, so register one for each record kind you show.
- Entries are kept as written: a body can only be redacted. Use `UpdateAsync` only for metadata (properties, channel, external id).
- Use `ImportAsync` with `TimelineEntryIds.FromSource(...)` ids for history from elsewhere, so re-running adds nothing and nothing is announced.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature

| Feature | ID | Depends on |
|---|---|---|
| Timeline | `CrestApps.OrchardCore.Timeline` | `CrestApps.OrchardCore.Resources` (the composer's file drop input) |

Category **Organization**. Optional integrations turn on with other features: `OrchardCore.Notifications` (mentions), `OrchardCore.Contents` (content item provider and cleanup), `CrestApps.OrchardCore.Attachments` (files on entries), `OrchardCore.Recipes.Core` / `OrchardCore.Deployment` (steps), `OrchardCore.DataPipelines.Core` (data set).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Timeline",
        "CrestApps.OrchardCore.Attachments"
      ],
      "disable": []
    }
  ]
}
```

The module adds no admin menu: each module shows a record's timeline on its own pages.

## Entries

`TimelineEntry` (a `CatalogItem` in the `Timeline` collection, indexed by `TimelineEntryIndex`):

| Member | Meaning |
|---|---|
| `Subject` | The record, `RecordRef(Kind, Id)` (`CrestApps.OrchardCore.Records`) |
| `Kind` | Open string: `Comment`, `Note`, `Event` (`TimelineConstants.Kinds`) or your own (Tickets adds `Reply`) |
| `Visibility` | `Public` (everyone who may read the record, customer included) or `Internal` |
| `Author` | `PartyRef`; empty for the site. `AuthorName` only for an author no party source knows (a guest who wrote by e-mail) |
| `Body` | Plain text, up to 20,000 characters (`TimelineConstants.MaxBodyLength`) |
| `Changes` | `TimelineFieldChange { Field, From, To }` as people read them |
| `AttachmentIds` | The entry's files (owner kind `TimelineEntry`) |
| `Channel` | Where it came in or went out (`Email`, `Portal`); empty for the admin |
| `ExternalId` | The id outside the site, such as an e-mail message id (indexed, up to 255 characters) |
| `CreatedUtc`, `RedactedUtc`, `RedactedBy` | Stamps |

Record, kind, channel and author are limited to 64 characters (`MaxIndexedLength`). An entry needs a subject, a kind, and a body, change or attachment.

## ITimelineService

| Method | Does |
|---|---|
| `AddAsync(entry)` | Checks, stamps, saves, runs `ITimelineEntryHandler.AddingAsync` (may refuse) and `AddedAsync` |
| `ImportAsync(entry)` | Same checks, keeps a long body whole, tells no handler; an entry whose id is stored is left as it is |
| `UpdateAsync(entryId, change)` | Changes only `TimelineEntryChanges` (properties, channel, external id), such as a delivery status; `TimelineConstants.Properties.Notice` shows a warning badge |
| `FindAsync(entryId)`, `FindByExternalIdAsync(externalId)` | Copies of stored entries |
| `ListAsync(TimelineQuery)` | Pages `{ Subject, Kinds, IncludeInternal, Skip, Take = 50 }` newest first; returns `TimelinePage(Entries, Total)` |
| `RedactAsync(entryId, by)` | Empties the body, keeps the entry and its changes |
| `DeleteAllAsync(subject)` | Removes a deleted record's timeline and tells each entry's handlers (so files go too) |
| `ScrubAsync(subject, scrub)` | Rewrites bodies, author names and change values for a personal data erasure |

```csharp
var result = await _timelineService.AddAsync(new TimelineEntry
{
    Subject = new RecordRef("Inspection", inspection.Id),
    Kind = TimelineConstants.Kinds.Event,
    Visibility = TimelineVisibility.Internal,
    Author = new PartyRef(PartyKinds.User, userId),
    Changes = [new TimelineFieldChange { Field = "Status", From = "Scheduled", To = "Done" }],
});

if (!result.Succeeded)
{
    // result.Errors says why.
}
```

`ITimelineFileService.ListAsync(TimelineFileQuery { Subjects, InternalSubjects, Skip, Take })` pages the files of one or more records newest first from `TimelineAttachmentIndex`, internal entries' files only for `InternalSubjects`, never those of redacted entries.

## Access

| Provider | Covers |
|---|---|
| `ContentItemTimelineAccess` (with `OrchardCore.Contents`) | Record kind `Content`. Staff (internal entries) are people who may edit the item and every item of its type; public entries only on types opted in with `services.AllowPublicTimelineEntries("Project")`. Anonymous visitors read nothing. |
| Tickets, Work Orders, Orders | Their own providers for `Ticket`, `WorkOrder` and order records |

`ITimelineAccessService.GetVisibleAsync(user, subjects, visibility)` checks many records with one call per kind; override `ITimelineAccessProvider.GetVisibleAsync` with one query for a large store.

| Permission | Display name | Default role |
|---|---|---|
| `RedactTimelineEntries` | Redact timeline entries | Administrator |

Redacting also needs the right to read the entry.

## The Timeline Card

```cshtml
@await Component.InvokeAsync("Timeline", new { subject = $"Inspection:{Model.InspectionId}", title = T["Notes"].Value })
```

- Arguments: `subject` (required, `Kind:Id`), `title`, `returnUrl`, `kinds` (comma-separated kinds to show).
- Renders nothing when the user may not read the timeline; shows 50 entries a page with **Older**/**Newer**, the author's current name from the party directory, times in the user's time zone, changes and an **Internal** badge.
- The composer posts to `Admin/timeline/add` (route `TimelineAdd`); **Redact** asks through the confirm dialog, then posts to `Admin/timeline/redact/{id}` (`TimelineRedact`) and deletes the entry's files.
- `Admin/timeline/entry/{id}?kinds=` (`TimelineEntry`) opens the record at the page that shows the entry; each entry has the element id `timeline-entry-{id}`.
- Default composer buttons (`TimelineOptions.ComposerKinds`): **Add note** (internal `Note`) and **Add comment** (public `Comment`). Add a button for your kind with `services.AddTimelineComposerKind(new TimelineComposerKind { ... })`, or override the `TimelineComposer` shape (alternate `TimelineComposer__{Kind}`) and post to your own action.

## Mentions and Notifications

With `OrchardCore.Notifications` on, `@username` in an entry sends the `TimelineMention` notification type (**Mentions**) with a link to the record, unless the user may not read the entry. The notification doesn't carry the text. An e-mail address is not a mention.

## Background Work

| Task or step | Does |
|---|---|
| **Remove files no timeline entry kept** (`TimelineOrphanFilesBackgroundTask`, hourly at minute 17) | Removes files of record kinds with a timeline access provider that no entry keeps, a day after upload |
| `Timeline.DeleteRecords` resumable step | Deletes a content item's (and a ticket's) timeline when the item is removed for good, retried until done |
| `Timeline.IndexFiles` resumable step | Indexed the files of existing entries when the files index shipped |

A file is stored before its entry; a module that attaches files to such records outside a timeline must keep them in an entry, or the sweeper removes them.

## Recipes, Deployment and Data Set

The `TimelineEntries` step (`TimelineConstants.RecipeStepName`) adds entries through `ImportAsync`: checked, stamped, nothing announced, an existing `ItemId` left as it is.

```json
{
  "steps": [
    {
      "name": "TimelineEntries",
      "Entries": [
        {
          "ItemId": "4tl0000000000000000000000a",
          "Subject": "WorkOrder:4abc0000000000000000000000",
          "Kind": "Note",
          "Visibility": "Internal",
          "Author": { "Kind": "User", "Id": "4user000000000000000000000" },
          "Body": "Customer asked for a morning visit.",
          "CreatedUtc": "2026-11-02T14:30:00Z"
        }
      ]
    }
  ]
}
```

The **Timeline entries** deployment step (category **Content**) exports every entry of the record kinds in `SubjectKinds` (comma-separated, such as `WorkOrder,Order`), oldest first. With CrestApps Recipes on, both have schemas.

With `OrchardCore.DataPipelines.Core` on, the **Timeline** data source has the `TimelineEntries` data set (record, kind, visibility, channel, text, author, attachments and changes count), filtered 500 rows at a time through each kind's access provider; redacted text is empty.

## Extension Points

| Seam | Register with |
|---|---|
| `ITimelineAccessProvider` (`SubjectKind`, `CanViewAsync`, `CanAddAsync`, optional batched `GetVisibleAsync`) | `services.AddTimelineAccessProvider<T>()` |
| `ITimelineEntryHandler` (`AddingAsync` with an error list, `AddedAsync`, `RedactedAsync`, `UpdatedAsync`, `DeletedAsync`) | `services.AddTimelineEntryHandler<T>()` |
| Public entries on a content type | `services.AllowPublicTimelineEntries("TypeName")` |
| Composer button | `services.AddTimelineComposerKind(...)` |

Access provider and entry handler examples are in [references/timeline-extensions.md](references/timeline-extensions.md).

## Troubleshooting

| Symptom | Check |
|---|---|
| The card renders nothing | No access provider for the record kind, or the user may not view the record. |
| Customers don't see comments on a content type | Public entries on content types need `AllowPublicTimelineEntries`. |
| `AddAsync` refused | Missing subject or kind, empty entry, body over 20,000 characters, or kind/channel/author over 64 characters. |
| No file picker on the composer | `CrestApps.OrchardCore.Attachments` is not enabled. |
| Files vanished a day after upload | They weren't kept in a timeline entry of a kind with an access provider. |
| No mention notifications | `OrchardCore.Notifications` is off, or the user can't read the entry. |
| Re-running an import duplicated history | Give imported entries stable ids with `TimelineEntryIds.FromSource(...)` or a fixed `ItemId`. |

Related skills: `orchardcore-attachments`, `orchardcore-tickets`, `orchardcore-work-orders`, `orchardcore-accounts`, `orchardcore-orders`, `orchardcore-omnichannel-management`, `orchardcore-notifications`.
