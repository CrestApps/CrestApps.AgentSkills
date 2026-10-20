---
name: orchardcore-publish-later
description: Skill for scheduling unpublished Orchard Core content for automatic publication. Covers PublishLaterPart, scheduled UTC dates, time zones and daylight-saving conversion, programmatic scheduling, PublishLaterPartIndex, editor behavior, admin list (List and Grid layout) display, and the Scheduled Content Items Publisher ScheduledPublishingBackgroundTask. Use this skill when requests mention Orchard Core Publish Later, PublishLaterPart, scheduled publishing, delayed publishing, scheduled content release, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.PublishLater, PublishLaterPart, PublishLaterPartIndex, PublishLaterPartDisplayDriver, ScheduledPublishingBackgroundTask, IBackgroundTask, and ILocalClock. It also helps with migrations, recipes, index behavior, and the code patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Publish Later - Prompt Templates

## Schedule Future Publication

You are an Orchard Core expert. Generate content definitions and integration code for publishing an unpublished latest version when its scheduled time arrives.

### Guidelines

- Enable the `OrchardCore.PublishLater` feature. Its module dependency is `OrchardCore.Contents`.
- Attach `PublishLaterPart` to each content type that editors may schedule.
- `PublishLaterPart.ScheduledPublishUtc` is a nullable UTC `DateTime`. The editor converts the editor's local date and time through `ILocalClock` (user time zone when **User Time Zone** is enabled, else site time zone, else server time zone). Local times skipped or repeated by a daylight-saving transition fail conversion and the schedule is not saved.
- Selecting **Publish Later** saves the latest version as a draft with its schedule; an existing published version stays live until then. Past times are accepted and publish on the next run.
- Editors need the normal `PublishContent` permission (with owner and per-type variants) to set or cancel the schedule. There is no dedicated permission, and custom endpoints must authorize themselves.
- The feature registers `ScheduledPublishingBackgroundTask` (title **Scheduled Content Items Publisher**) as an `IBackgroundTask`. Its schedule is `* * * * *`, so due content is checked every minute. Publication can happen after, never before, the scheduled time, and only while the tenant is running.
- Because it uses `IContentManager.PublishAsync`, handlers, workflows, indexing, and notifications run as for a manual publish. A handler can still cancel publication.
- The schedule is shown in the admin content list by `PublishLaterPart_SummaryAdmin`: `Meta:25` in the `List` layout (`SummaryAdmin`) and `Modified:25` in the `Grid` layout (`SummaryAdminGrid`). The editor shape `PublishLaterPart_Edit` is placed at `Actions:10`.
- The task queries `PublishLaterPartIndex` for latest, unpublished items whose scheduled UTC time is earlier than the current UTC clock value.
- The task clears `ScheduledPublishUtc`, applies the part, then calls `IContentManager.PublishAsync`.
- Do not create an independent timer or publish job for attached content types. The index and background task handle tenancy and content versioning.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling Publish Later

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.PublishLater"
      ],
      "disable": []
    }
  ]
}
```

### Attaching PublishLaterPart with a Migration

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;
using OrchardCore.PublishLater.Models;

namespace MyModule;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("NewsArticle", type => type
            .Creatable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart")
            .WithPart(nameof(PublishLaterPart), part => part.WithPosition("10")));

        return 1;
    }
}
```

### Content Definition Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "NewsArticle",
          "DisplayName": "News Article",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Draftable": true,
              "Versionable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart"
            },
            {
              "PartName": "PublishLaterPart",
              "Name": "PublishLaterPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "10"
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

### Scheduling Content in a Recipe

Persist the scheduled instant as UTC. A scheduled item should normally be latest and unpublished.

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js:uuid()]",
          "ContentType": "NewsArticle",
          "DisplayText": "Conference announcement",
          "Latest": true,
          "Published": false,
          "TitlePart": {
            "Title": "Conference announcement"
          },
          "PublishLaterPart": {
            "ScheduledPublishUtc": "2026-09-01T09:00:00Z"
          }
        }
      ]
    }
  ]
}
```

### Scheduling Programmatically

```csharp
// draft must be the latest unpublished version of a type with PublishLaterPart.
draft.Alter<PublishLaterPart>(part => part.ScheduledPublishUtc = scheduledUtc);

await _contentManager.SaveDraftAsync(draft);
```

Convert a local value with `ILocalClock.ConvertToUtcAsync` first. Set the property to `null` to cancel.

### Editor Behavior

The built-in editor shape appears in the content editor action zone and uses `PublishLaterPartViewModel`:

| Property | Purpose |
|---|---|
| `ScheduledPublishLocalDateTime` | Local value bound from the editor. |
| `ScheduledPublishUtc` | Stored UTC value for display and scheduling. |
| `ContentItem` | The content item being edited. |

Submitting the cancel action or an empty local date clears the schedule. Do not set a local time directly on `PublishLaterPart`; convert it to UTC first.

### Inspecting Scheduled Items

Use the map index for administrative or reporting queries. Restrict the query to the latest unpublished version.

```csharp
using OrchardCore.PublishLater.Indexes;
using YesSql;

namespace MyModule.Services;

public sealed class ScheduledPublicationQuery
{
    private readonly ISession _session;

    public ScheduledPublicationQuery(ISession session)
    {
        _session = session;
    }

    public Task<IEnumerable<PublishLaterPartIndex>> GetDueItemsAsync(DateTime utcNow)
    {
        return _session
            .QueryIndex<PublishLaterPartIndex>(x =>
                x.Latest &&
                !x.Published &&
                x.ScheduledPublishDateTimeUtc < utcNow)
            .ListAsync();
    }
}
```

### Background Task Behavior

`ScheduledPublishingBackgroundTask` publishes each due latest version independently. It obtains the latest item, clears its schedule, logs at debug level, and publishes it. The `PublishLaterPartIndexProvider` excludes already published or non-latest versions and removes stale indexes when the part is removed from a type definition.

### Troubleshooting

| Symptom | Check |
|---|---|
| No scheduling controls | Enable the feature and attach `PublishLaterPart` to the content type. |
| Content is not published | Verify it is latest, unpublished, and its UTC time has passed. |
| Time is offset | Store and compare UTC values; let the built-in editor use `ILocalClock`. |
| Old item is published | Confirm an editor did not create a newer draft after scheduling the item. |
| Some due items stay drafts | An exception while publishing one item stops the current run; fix the failing handler, the next run retries. With the Background Tasks feature enabled, check the task is enabled in the `/Admin/BackgroundTasks` list. |
| Schedule not saved near a DST change | Choose an unambiguous local time. |
