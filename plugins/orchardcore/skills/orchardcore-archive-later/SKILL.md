---
name: orchardcore-archive-later
description: Skill for scheduling published Orchard Core content to be automatically unpublished (archived) at a future date. Covers ArchiveLaterPart, scheduled UTC dates, ArchiveLaterPartIndex, editor behavior, admin list (List and Grid layout) display, and the Content Items Archiver ScheduledArchivingBackgroundTask. Use this skill when requests mention Orchard Core Archive Later, ArchiveLaterPart, scheduled archiving, scheduled unpublish, content expiration, delayed archiving, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.ArchiveLater, ArchiveLaterPart, ArchiveLaterPartIndex, ArchiveLaterPartDisplayDriver, ScheduledArchivingBackgroundTask, IBackgroundTask, and IClock. It also helps with migrations, recipes, index behavior, and the code patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Archive Later - Prompt Templates

## Schedule Future Unpublishing

You are an Orchard Core expert. Generate content definitions and integration code for automatically unpublishing (archiving) a published content item when its scheduled time arrives.

### Guidelines

- Enable the `OrchardCore.ArchiveLater` feature. Its module dependency is `OrchardCore.Contents`.
- Attach `ArchiveLaterPart` to each content type whose items editors may schedule for archiving.
- `ArchiveLaterPart.ScheduledArchiveUtc` is a nullable UTC `DateTime`. The editor (rendered in the `Actions` zone) converts the editor's local date and time to UTC with `ILocalClock`; clearing the date or using the cancel button removes the schedule.
- Editors need the normal `PublishContent` permission (`CommonPermissions.PublishContent`) for the item to set or cancel the schedule; otherwise the posted value is ignored.
- The feature registers `ScheduledArchivingBackgroundTask` (title **Content Items Archiver**) as an `IBackgroundTask`. Its schedule is `* * * * *`, so due content is checked every minute, and only while the tenant is running.
- The task queries `ArchiveLaterPartIndex` for rows that are `Latest` and `Published` and whose `ScheduledArchiveDateTimeUtc` is earlier than `IClock.UtcNow`.
- The scheduled date is shown in the admin content list by the `ArchiveLaterPart_SummaryAdmin` shape: `Meta:25` in the `List` layout (`SummaryAdmin`) and `Modified:25` in the `Grid` layout (`SummaryAdminGrid`). Override placement for each display type separately.
- The task clears `ScheduledArchiveUtc`, applies the part, then calls `IContentManager.UnpublishAsync` so the item is removed from the published set while the draft is retained.
- "Archive Later" is the counterpart of "Publish Later" — Publish Later publishes a draft in the future, Archive Later unpublishes a published item in the future.
- Do not create an independent timer or unpublish job for attached content types. The index and background task handle tenancy and content versioning.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling Archive Later

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.ArchiveLater"
      ],
      "disable": []
    }
  ]
}
```

### Attaching ArchiveLaterPart via Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "Article",
          "DisplayName": "Article",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Draftable": true,
              "Versionable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "ArchiveLaterPart",
              "Name": "ArchiveLaterPart",
              "Settings": {}
            }
          ]
        }
      ]
    }
  ]
}
```

### Attaching ArchiveLaterPart via Migration

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;

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
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Article", type => type
            .WithPart("ArchiveLaterPart"));

        return 1;
    }
}
```

### Reading or Setting the Schedule in Code

```csharp
// Schedule an item to be unpublished 30 days from now (IClock and IContentManager are injected).
var part = contentItem.As<ArchiveLaterPart>();

part.ScheduledArchiveUtc = _clock.UtcNow.AddDays(30);

contentItem.Apply(part);

await _contentManager.UpdateAsync(contentItem);
```

To find items scheduled for archiving, query the index:

```csharp
using OrchardCore.ArchiveLater.Indexes;
using YesSql;

var due = await _session
    .QueryIndex<ArchiveLaterPartIndex>(x => x.Published && x.ScheduledArchiveDateTimeUtc != null)
    .ListAsync();
```

### How the Background Task Works

- `ScheduledArchivingBackgroundTask` runs every minute (`* * * * *`).
- It uses `IClock` to read the current UTC time, then queries `ArchiveLaterPartIndex` for latest, published items whose `ScheduledArchiveDateTimeUtc` is due.
- For each due item it clears `ScheduledArchiveUtc`, applies the part, and calls `IContentManager.UnpublishAsync`.
- Because it uses an index and the content manager, it is tenant-aware and respects draft/published versioning.

### Notes

- Archiving here means "unpublish": the latest draft remains editable, only the published version is retracted.
- Pair with `OrchardCore.PublishLater` when editors need both scheduled publish and scheduled archive on the same content type.
- The `ArchiveLaterPartDisplayDriver` renders the scheduling editor; the part registers a `TemplateOptions` member-access strategy for `ArchiveLaterPartViewModel` so it can be used from Liquid.
