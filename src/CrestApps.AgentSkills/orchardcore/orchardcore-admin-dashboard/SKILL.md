---
name: orchardcore-admin-dashboard
description: Skill for creating admin dashboard widgets in Orchard Core. Covers the DashboardWidget stereotype, widget position and sizing, custom dashboard widget content types, widget template customization, and dashboard layout configuration.
---

# Orchard Core Admin Dashboard - Prompt Templates

## Create and Configure Dashboard Widgets

You are an Orchard Core expert. Generate admin dashboard widgets, content types, and template configurations for Orchard Core.

### Guidelines

- Enable `OrchardCore.AdminDashboard` for admin dashboard widget support.
- Dashboard widgets are content items with the `DashboardWidget` stereotype.
- To create a widget content type, set its stereotype to `DashboardWidget`.
- Each widget supports `Position`, `Width` (1–6), and `Height` (1–6) settings.
- Width and Height values represent fractions of the screen (1 = 1/6 screen, 6 = full screen).
- `ManageAdminDashboard` permission is required to manage (add/edit/remove) widgets.
- `AccessAdminDashboard` permission is required for users without `ManageAdminDashboard` to view the dashboard.
- Users also need `ViewContent` permission to see widget content.
- Customize widget appearance with templates named `DashboardWidget-{ContentType}.DetailAdmin.cshtml`.
- Always seal classes.

### Enabling Admin Dashboard

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.AdminDashboard"
      ],
      "disable": []
    }
  ]
}
```

### Widget Configuration Properties

| Property | Type | Range | Description |
|----------|------|-------|-------------|
| `Position` | Numeric | Any | Controls the widget's order on the page. Lower values appear first. |
| `Width` | Integer | 1–6 | Widget width as a fraction of screen width. 1 = 1/6, 6 = full width. |
| `Height` | Integer | 1–6 | Widget height as a fraction of screen height. 1 = 1/6, 6 = full height. |

### Creating a Dashboard Widget Content Type via Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "{{WidgetTypeName}}",
          "DisplayName": "{{WidgetDisplayName}}",
          "Settings": {
            "ContentTypeSettings": {
              "Stereotype": "DashboardWidget",
              "Creatable": false,
              "Listable": false,
              "Draftable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "0"
                }
              }
            },
            {
              "PartName": "HtmlBodyPart",
              "Name": "HtmlBodyPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "1"
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

### Creating a Dashboard Widget Content Type via Migration

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace MyModule;

public sealed class DashboardMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public DashboardMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("{{WidgetTypeName}}", type => type
            .Stereotype("DashboardWidget")
            .DisplayedAs("{{WidgetDisplayName}}")
            .WithPart("TitlePart", part => part
                .WithPosition("0")
            )
            .WithPart("HtmlBodyPart", part => part
                .WithPosition("1")
            )
        );

        return 1;
    }
}
```

### Creating a Dashboard Widget Content Item via Recipe

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "{{WidgetContentItemId}}",
          "ContentType": "{{WidgetTypeName}}",
          "DisplayText": "{{WidgetTitle}}",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "{{WidgetTitle}}"
          },
          "HtmlBodyPart": {
            "Html": "<p>{{WidgetContent}}</p>"
          }
        }
      ]
    }
  ]
}
```

### Custom Widget Template

Create a template named `DashboardWidget-{{ContentType}}.DetailAdmin.cshtml` where `{{ContentType}}` is the technical name of the content type:

```html
<div class="card h-100 @string.Join(' ', Model.Classes.ToArray())">
    @if (Model.Header != null || Model.Leading != null || Model.ActionsMenu != null)
    {
        <div class="card-header">
            @await DisplayAsync(Model.Leading)
            @await DisplayAsync(Model.Header)
            @if (Model.ActionsMenu != null)
            {
                <div class="btn-group float-end" title="@T["Actions"]">
                    <button type="button" class="btn btn-sm" data-bs-toggle="dropdown"
                            aria-haspopup="true" aria-expanded="false">
                        <i class="fa-solid fa-ellipsis-v" aria-hidden="true"></i>
                    </button>
                    <div class="actions-menu dropdown-menu">
                        @await DisplayAsync(Model.ActionsMenu)
                    </div>
                </div>
            }
        </div>
    }
    <div class="dashboard-body-container card-body p-2 h-100">
        @if (Model.Tags != null || Model.Meta != null)
        {
            <div class="dashboard-meta">
                @await DisplayAsync(Model.Meta)
                @await DisplayAsync(Model.Tags)
            </div>
        }
        @await DisplayAsync(Model.Content)
    </div>
    @if (Model.Footer != null)
    {
        <div class="card-footer">
            @await DisplayAsync(Model.Footer)
        </div>
    }
</div>
```

### Permissions

| Permission | Description |
|------------|-------------|
| `ManageAdminDashboard` | Add, edit, and remove dashboard widgets. Includes dashboard access. |
| `AccessAdminDashboard` | View the admin dashboard (required for users without `ManageAdminDashboard`). |
| `ViewContent` | Required to see the widget content on the dashboard. |

### Dashboard Widget with Custom Part via Migration

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace MyModule;

public sealed class StatsDashboardMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public StatsDashboardMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("StatsWidgetPart", part => part
            .WithField("StatLabel", field => field
                .OfType("TextField")
                .WithDisplayName("Stat Label")
                .WithPosition("0")
            )
            .WithField("StatValue", field => field
                .OfType("NumericField")
                .WithDisplayName("Stat Value")
                .WithPosition("1")
            )
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync("StatsWidget", type => type
            .Stereotype("DashboardWidget")
            .DisplayedAs("Stats Widget")
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("StatsWidgetPart", part => part.WithPosition("1"))
        );

        return 1;
    }
}
```

### How to Add Widgets to the Dashboard

1. Log in with a user that has the `ManageAdminDashboard` permission.
2. Navigate to the admin dashboard (`/admin`).
3. Click the **Manage Dashboard** button.
4. Click **Add Widget** and select your dashboard widget content type.
5. Fill in the form, set Position, Width, and Height, then click **Publish**.
