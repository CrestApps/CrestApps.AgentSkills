---
name: orchardcore-audit-trail
description: Skill for configuring audit trail in Orchard Core. Covers audit event recording, the content and users audit trail events, AuditTrailPart for content tracking, the audit trail admin list with its Event, Category, Details and Actions columns, admin list filters and filter cards, settings and trimming recipes, custom audit event categories with AuditTrailOptions, recording events with IAuditTrailManager, and upgrading audit trail templates to Orchard Core 4.0. Use this skill when requests mention Orchard Core Audit Trail, Configure Audit Trail and Event Tracking, Enabling Audit Trail, Audit Trail Event Categories, AuditTrailEventName_SummaryAdmin, AuditTrailEventCategory_SummaryAdmin, AuditTrailEventTags_SummaryAdmin, IAuditTrailAdminListFilterProvider, AuditTrailIndexOptions, Attaching AuditTrailPart, or closely related setup or troubleshooting work. Strong matches include OrchardCore.AuditTrail, OrchardCore.Users.AuditTrail, OrchardCore.AuditTrail.Services, AuditTrailPermissions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Audit Trail - Prompt Templates

## Configure Audit Trail and Event Tracking

You are an Orchard Core expert. Generate audit trail configurations, custom event providers, and AuditTrailPart setups for Orchard Core.

### Guidelines

- Enable `OrchardCore.AuditTrail` for audit event recording. Content events are recorded automatically when the Contents feature is enabled; enable `OrchardCore.Users.AuditTrail` to record user events.
- The Audit Trail provides an immutable, auditable log of system events (content changes, user events, etc.).
- Events are logged only for event types that are registered in `AuditTrailOptions` and enabled in **Settings** → **Audit Trail**.
- The audit trail list supports filtering by date range, category, event, user, and correlation ID, and sorting.
- Content events show version history, diffs, and allow restoring previous versions or deleted items. Restored content items are created as drafts that must be manually published.
- Attach `AuditTrailPart` to content types to let editors add comments to audit trail entries on save.
- Client IP address logging is optional and may require GDPR/privacy compliance considerations.
- Trimming settings control how long events are retained in the database (default 10 days).
- Permissions are `AuditTrailPermissions.ViewAuditTrail` and `AuditTrailPermissions.ManageAuditTrailSettings`.
- The admin list renders through the shared `AdminList` shape under the list name `AuditTrail`, so it follows the site's List/Grid layout (see `orchardcore-admin-lists`).
- Always seal classes.

### Enabling Audit Trail

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.AuditTrail",
        "OrchardCore.Users.AuditTrail"
      ],
      "disable": []
    }
  ]
}
```

### Audit Trail Event Categories

| Category | Events | Description |
|----------|--------|-------------|
| **Content** | Created, Saved, Published, Unpublished, Removed, Cloned, Restored | Tracks content item lifecycle events. |
| **User** | LoggedIn, LogInFailed (off by default), LogInIsLockedOut, Enabled, Disabled, Created, Updated, Deleted, Confirmed | Tracks user authentication and account events (`OrchardCore.Users.AuditTrail`). |
| **User** | Registered, password reset events | Added when the Users Registration and Users Reset Password features are enabled. |

### Audit Trail Admin List

Each row shows four regions. In the `Grid` layout they are columns named `Event`, `Category`, `Details`, and `Actions`; in the `List` layout the same shapes are placed in zones.

| Shape | `SummaryAdmin` (List) zone | `SummaryAdminGrid` column | Content |
|-------|---------------------------|---------------------------|---------|
| `AuditTrailEventName_SummaryAdmin` | `EventName` | `Event` | Event name badge. |
| `AuditTrailEventCategory_SummaryAdmin` | `EventCategory` | `Category` | Category badge. |
| `AuditTrailEventMeta_SummaryAdmin` | `EventMeta` | `Details` | Time, user, and event summary. |
| `AuditTrailEventActions_SummaryAdmin` | `Actions` | `Actions` | Event-specific buttons (View, Restore, Details). |

Override a single shape with a template such as `AuditTrailEventCategory.SummaryAdmin.cshtml`, or a single grid cell with `AdminListCell-AuditTrail-Category.cshtml`.

### Audit Trail Event List Features

| Feature | Description |
|---------|-------------|
| **Date Range Filter** | Filter events to a specific time period (`date:` term). |
| **Category Filter** | Filter by event category (`category:` term, e.g. Content, User). |
| **Event Filter** | Filter by event name (`event:` term). |
| **User Filters** | Bare text searches the user name (`username` default term); `userid:` filters by user ID. |
| **Correlation ID** | `id:` lists every event of one correlation ID, such as one content item. |
| **Sorting** | Sort entries with the `sort:` term. |
| **Version Link** | Click to view the read-only editor of a content item at that version. |
| **Display Text Link** | Click to edit the latest version of the content item. |
| **View Button** | View the content item at the recorded version. |
| **Restore Button** | Restore a content item to a previous version (creates a draft). |
| **Details Link** | View detailed event information, including textual diffs for content events. |

### Adding a Custom Admin List Filter

Implement `IAuditTrailAdminListFilterProvider` for the logic, and a `DisplayDriver<AuditTrailIndexOptions>` returning `AuditTrailAdminFilters_Thumbnail__<Name>` (view `Views/Items/AuditTrailAdminFilters-<Name>.Thumbnail.cshtml`) to document it in the **Available Filters** dialog. The pattern is the same as `orchardcore-contents-admin-list-filters`.

```csharp
using OrchardCore.AuditTrail.Indexes;
using OrchardCore.AuditTrail.Models;
using OrchardCore.AuditTrail.Services;
using YesSql.Filters.Query;

namespace MyModule.AuditTrail;

public sealed class ModuleAuditTrailAdminListFilterProvider : IAuditTrailAdminListFilterProvider
{
    public void Build(QueryEngineBuilder<AuditTrailEvent> builder)
    {
        builder
            .WithNamedTerm("mymodule", builder => builder
                .OneCondition((val, query) =>
                    query.With<AuditTrailEventIndex>(i => i.Category == "MyModule" && i.Name == val)));
    }
}
```

```csharp
services.AddTransient<IAuditTrailAdminListFilterProvider, ModuleAuditTrailAdminListFilterProvider>();
```

The card template renders only the card's inner content (title and capability icons, token, description):

```html
@model ShapeViewModel<AuditTrailIndexOptions>
@{
    var term = Model.Value.FilterResult.FirstOrDefault(x => x.TermName == "mymodule");
}

<div class="d-flex justify-content-between align-items-center gap-2">
    <h6 class="card-title fw-semibold mb-0">@T["My module event"]</h6>
    <span class="text-primary text-nowrap">
        <i class="fa-solid fa-sm fa-minus" title="@T["Accepts a single value"]" aria-hidden="true"></i>
    </span>
</div>
<div class="mt-1"><code class="small text-nowrap">@(term?.ToString() ?? "mymodule:...")</code></div>
<p class="card-text small text-body-secondary mt-1 mb-0">@T["Filters on an event of My Module."]</p>
```

### Attaching AuditTrailPart via Migration

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace MyModule;

public sealed class AuditTrailMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public AuditTrailMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentTypeName}}", type => type
            .WithPart("AuditTrailPart", part => part
                .WithPosition("20")
            )
        );

        return 1;
    }
}
```

### Attaching AuditTrailPart via Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "{{ContentTypeName}}",
          "DisplayName": "{{DisplayName}}",
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "AuditTrailPart",
              "Name": "AuditTrailPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "20"
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

### Audit Trail Settings

Configure at **Settings** → **Audit Trail**:

| Setting | Description |
|---------|-------------|
| **Event Recording** | Enable or disable recording for specific event types. |
| **Client IP Logging** | When enabled, the client IP address is included in audit trail events. Requires GDPR/privacy compliance. |
| **Trimming** | Configure retention period for audit events. Disable trimming to keep events indefinitely. |
| **Content Tab** | Select which content types should have their events recorded. |

### Audit Trail Settings Recipe

```json
{
  "steps": [
    {
      "name": "Settings",
      "AuditTrailSettings": {
        "ClientIpAddressAllowed": false,
        "Categories": [
          {
            "Name": "Content",
            "Events": [
              {
                "Name": "Published",
                "Category": "Content",
                "IsEnabled": true
              }
            ]
          }
        ]
      },
      "AuditTrailTrimmingSettings": {
        "RetentionDays": 90,
        "Disabled": false
      },
      "ContentAuditTrailSettings": {
        "AllowedContentTypes": [
          "Article",
          "Page"
        ]
      }
    }
  ]
}
```

### Registering a Custom Audit Trail Category and Events

An event is only recorded when its category and name are registered in `AuditTrailOptions`. The last `WithEvent` arguments are `enableByDefault` and `isMandatory`.

```csharp
using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;

namespace MyModule.AuditTrail;

public sealed class MyModuleAuditTrailEventConfiguration : IConfigureOptions<AuditTrailOptions>
{
    public const string Category = "MyModule";
    public const string Exported = nameof(Exported);

    public void Configure(AuditTrailOptions options)
    {
        options.For<MyModuleAuditTrailEventConfiguration>(Category, S => S["My Module"])
            .WithEvent(Exported, S => S["Exported"], S => S["Data was exported."], true);
    }
}

public sealed class CustomAuditEvent
{
    public string Action { get; set; }
}
```

### Recording a Custom Audit Trail Event

```csharp
using MyModule.AuditTrail;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace MyModule.Services;

public sealed class MyService
{
    private readonly IAuditTrailManager _auditTrailManager;

    public MyService(IAuditTrailManager auditTrailManager)
    {
        _auditTrailManager = auditTrailManager;
    }

    public async Task PerformActionAsync(string userId, string userName)
    {
        // Perform your action...

        await _auditTrailManager.RecordEventAsync(
            new AuditTrailContext<CustomAuditEvent>(
                name: MyModuleAuditTrailEventConfiguration.Exported,
                category: MyModuleAuditTrailEventConfiguration.Category,
                correlationId: "{{CorrelationId}}",
                userId: userId,
                userName: userName,
                auditTrailEventItem: new CustomAuditEvent { Action = "Export" }
            )
        );
    }
}
```

### Altering Events Before They Are Saved

Implement `IAuditTrailEventHandler` (or derive from `AuditTrailEventHandlerBase`) to change the create context or the built `AuditTrailEvent`:

```csharp
using OrchardCore.AuditTrail.Models;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace MyModule.AuditTrail;

public sealed class CustomAuditTrailEventHandler : AuditTrailEventHandlerBase
{
    public override Task CreateAsync(AuditTrailCreateContext context)
    {
        if (context is AuditTrailCreateContext<CustomAuditEvent> customContext)
        {
            customContext.AuditTrailEventItem.Action ??= "Unknown";
        }

        return Task.CompletedTask;
    }
}
```

### Registering the Custom Services

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures("OrchardCore.AuditTrail")]
public sealed class AuditTrailStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, MyModuleAuditTrailEventConfiguration>();
        services.AddScoped<IAuditTrailEventHandler, CustomAuditTrailEventHandler>();
    }
}
```

To render custom details for your event on the **Details** page, derive a display driver from `AuditTrailEventSectionDisplayDriver<CustomAuditEvent>` and place a shape in the `DetailAdmin` display type.

### Content Event Diff Tracking

The Audit Trail module tracks diffs between content versions:

- **Current version values** are shown in green.
- **Previous version values** are shown in red.
- Access diffs via the **Details** link → **Diff** tab in the audit trail list.

### Enabling Content-Specific Audit Trail

To track specific content type events:

1. Navigate to **Settings** → **Audit Trail** → **Content** tab.
2. Select the content types to track.
3. Save the settings.

Only events for selected content types will be recorded, reducing noise in the audit log.

### Upgrading from 3.x

- The single `AuditTrailEventTags_SummaryAdmin` shape (event and category badges together) was replaced by `AuditTrailEventName_SummaryAdmin` in the `EventName` zone and `AuditTrailEventCategory_SummaryAdmin` in the `EventCategory` zone. Move overrides of `AuditTrailEventTags.SummaryAdmin.cshtml` to the two new templates.
- The list now has a separate **Category** column; rows read `Event`, `Category`, `Details`, and `Actions`.
- Rows of the `Grid` layout use the `SummaryAdminGrid` display type and cell templates `AdminListCell-AuditTrail-{Column}.cshtml`; `SummaryAdmin` placement only applies to the `List` layout.
- Filter card templates (`AuditTrailAdminFilters-*.Thumbnail.cshtml`) must use the new card markup shown above instead of the old `ocat-wrapper` layout.

### Best Practices

- Attach `AuditTrailPart` to critical content types (e.g., pages, policies) for edit comments.
- Enable IP logging only when required and ensure privacy compliance.
- Set a reasonable trimming period (e.g., 90–365 days) to manage database size.
- Use the Details/Diff view to investigate content changes before restoring.
- Custom event providers should use descriptive category and event names for easy filtering.
