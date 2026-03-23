---
name: orchardcore-audit-trail
description: Skill for configuring audit trail in Orchard Core. Covers audit event recording, AuditTrailPart for content tracking, event filtering and sorting, audit settings configuration, custom audit event providers, and audit trail feature setup.
---

# Orchard Core Audit Trail - Prompt Templates

## Configure Audit Trail and Event Tracking

You are an Orchard Core expert. Generate audit trail configurations, custom event providers, and AuditTrailPart setups for Orchard Core.

### Guidelines

- Enable `OrchardCore.AuditTrail` for audit event recording.
- The Audit Trail provides an immutable, auditable log of system events (content changes, user events, etc.).
- Events are logged automatically for supported actions (create, publish, delete content items; user login failures, etc.).
- The audit trail list supports filtering by date range, category (Content, User, etc.), and sorting.
- Content events show version history, diffs, and allow restoring previous versions or deleted items.
- Restored content items are created as drafts that must be manually published.
- Attach `AuditTrailPart` to content types to let editors add comments to audit trail entries on save.
- Configure which events to record via **Settings** → **Audit Trail**.
- Client IP address logging is optional and may require GDPR/privacy compliance considerations.
- Trimming settings control how long events are retained in the database.
- Custom modules can provide their own audit trail event handlers.
- Always seal classes.

### Enabling Audit Trail

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.AuditTrail"
      ],
      "disable": []
    }
  ]
}
```

### Audit Trail Event Categories

| Category | Events | Description |
|----------|--------|-------------|
| **Content** | Created, Published, Unpublished, Removed, Cloned, Restored | Tracks content item lifecycle events. |
| **User** | LoggedIn, LogInFailed, PasswordReset, PasswordChanged | Tracks user authentication and account events. |

### Audit Trail Event List Features

| Feature | Description |
|---------|-------------|
| **Date Range Filter** | Filter events to a specific time period. |
| **Category Filter** | Filter by event category (e.g., Content, User). |
| **Sorting** | Sort entries by various parameters (date, category, user). |
| **Version Link** | Click to view the read-only editor of a content item at that version. |
| **Display Text Link** | Click to edit the latest version of the content item. |
| **View Button** | View the content item at the recorded version. |
| **Restore Button** | Restore a content item to a previous version (creates a draft). |
| **Details Link** | View detailed event information, including textual diffs for content events. |

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

### Creating a Custom Audit Trail Event Provider

Implement `IAuditTrailEventHandler` to record custom events:

```csharp
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace MyModule.AuditTrail;

public sealed class CustomAuditTrailEventHandler : AuditTrailEventHandlerBase
{
    public override Task CreateAsync(AuditTrailCreateContext context)
    {
        // Add custom data to the audit trail event context.
        return Task.CompletedTask;
    }
}
```

### Recording a Custom Audit Trail Event

```csharp
using OrchardCore.AuditTrail.Services;

namespace MyModule.Services;

public sealed class MyService
{
    private readonly IAuditTrailManager _auditTrailManager;

    public MyService(IAuditTrailManager auditTrailManager)
    {
        _auditTrailManager = auditTrailManager;
    }

    public async Task PerformActionAsync()
    {
        // Perform your action...

        // Record the audit trail event.
        await _auditTrailManager.RecordEventAsync(
            new AuditTrailContext(
                name: "CustomAction",
                category: "MyModule",
                correlationId: "{{CorrelationId}}",
                userId: "{{UserId}}",
                userName: "{{UserName}}"
            )
        );
    }
}
```

### Registering Custom Audit Trail Event Handler

```csharp
using OrchardCore.AuditTrail.Services;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAuditTrailEventHandler, CustomAuditTrailEventHandler>();
    }
}
```

### Content Event Diff Tracking

The Audit Trail module tracks diffs between content versions:

- **Current version values** are shown in green.
- **Previous version values** are shown in red.
- Access diffs via the **Details** link → **Diff** tab in the audit trail list.

### Audit Trail Trimming Configuration

```json
{
  "steps": [
    {
      "name": "Settings",
      "AuditTrailTrimmingSettings": {
        "RetentionDays": 90,
        "Disabled": false
      }
    }
  ]
}
```

### Enabling Content-Specific Audit Trail

To track specific content type events:

1. Navigate to **Settings** → **Audit Trail** → **Content** tab.
2. Select the content types to track.
3. Save the settings.

Only events for selected content types will be recorded, reducing noise in the audit log.

### Best Practices

- Attach `AuditTrailPart` to critical content types (e.g., pages, policies) for edit comments.
- Enable IP logging only when required and ensure privacy compliance.
- Set a reasonable trimming period (e.g., 90–365 days) to manage database size.
- Use the Details/Diff view to investigate content changes before restoring.
- Custom event providers should use descriptive category and event names for easy filtering.
