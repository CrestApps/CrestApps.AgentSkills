---
name: orchardcore-admin-dashboard
description: Skill for creating admin dashboard widgets in Orchard Core. Covers the DashboardWidget stereotype, DashboardPart position and sizing, the built-in HtmlDashboardWidget, ContentsMetadata and ContentsTags widget types, custom widget content types via recipe or migration, arranging widgets in Manage Dashboard mode, widget template customization, provisioning widgets with recipes, dashboard permissions, the DashboardBreadcrumbProvider Dashboard ancestor, and the AdminDashboardContent shape used when the feature is disabled. Use this skill when requests mention Orchard Core Admin Dashboard, Create and Configure Dashboard Widgets, Enabling Admin Dashboard, Widget Configuration Properties, dashboard widget recipes, Dashboard breadcrumb, or related implementation, setup, or troubleshooting work. Strong matches include OrchardCore.AdminDashboard, DashboardPart, DashboardBreadcrumbProvider, AccessAdminDashboard, ManageAdminDashboard.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Admin Dashboard - Prompt Templates

## Create and Configure Dashboard Widgets

You are an Orchard Core expert. Generate admin dashboard widgets, content types, and template configurations for Orchard Core.

### Guidelines

- Enable `OrchardCore.AdminDashboard` for admin dashboard widget support. It replaces the default `/admin` landing page with the widget dashboard.
- Dashboard widgets are content items with the `DashboardWidget` stereotype, so they can be edited, secured, versioned, and localized like any other content.
- Built-in widget types: `HtmlDashboardWidget` (an `HtmlBodyPart` body for links or notes), `ContentsMetadata`, and `ContentsTags`. The module also ships a `dashboard-widgets-samples` recipe.
- To create a widget content type, set its stereotype to `DashboardWidget`.
- `DashboardPart` is added automatically to every `DashboardWidget` type and stores `Position`, `Width`, and `Height`.
- `Position`, `Width`, and `Height` are `double` values (defaults `0`, `1.0`, `1.0`). The grid has 6 columns on large screens (3 on tablets, 1 on phones), so a `Width` of `1` is one sixth and `6` is the full width; use whole numbers from 1 to 6 for `Width` and `Height`.
- In **Manage Dashboard** mode, drag a widget by its handle to change its `Position` and resize it to change `Width` and `Height`; both the latest and published versions are updated.
- `ManageAdminDashboard` permission is required to manage (add/edit/arrange/remove) widgets; it implies `AccessAdminDashboard`.
- `AccessAdminDashboard` permission is required for users without `ManageAdminDashboard` to view the dashboard. By default it is granted to Editor, Moderator, Author, and Contributor; Administrator has both.
- Users with only `AccessAdminDashboard` also need `ViewContent` or the applicable per-content-type view permission to see a widget.
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
| `Width` | Double | 1 to 6 | Grid-column span; `1` is one sixth of a large screen, `6` the full width. Default `1.0`. Not validated server-side. |
| `Height` | Double | 1 to 6 | Grid-row span. Default `1.0`. Not validated server-side. |

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
          "DashboardPart": {
            "Position": 0.0,
            "Width": 2.0,
            "Height": 1.0
          },
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
| `ManageAdminDashboard` | Add, edit, arrange, and remove dashboard widgets. Implies `AccessAdminDashboard`. |
| `AccessAdminDashboard` | View the admin dashboard when the user does not manage it. Static member `OrchardCore.AdminDashboard.Permissions.AccessAdminDashboard`. |
| `ViewContent` | Required, with `AccessAdminDashboard`, unless a per-content-type view permission grants access. |

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

### Dashboard Breadcrumb Ancestor

The feature registers `OrchardCore.AdminDashboard.Services.DashboardBreadcrumbProvider` with `services.AddBreadcrumbProvider<DashboardBreadcrumbProvider>()`. On admin requests with a visible trail it inserts a localized **Dashboard** ancestor (ID `Dashboard`, URL `"~/" + AdminOptions.AdminUrlPrefix`, permission `AccessAdminDashboard`) at index `0`, unless the trail is named `Dashboard` or already has a `Dashboard` ancestor. So `Manage Content › Edit Article` becomes `Dashboard › Manage Content › Edit Article`. Users without access see the node as plain text; front-end trails and hidden admin trails (**Show breadcrumb** off) are not affected.

The dashboard page declares its own trail, which prevents a duplicate ancestor:

```razor
<breadcrumb name="Dashboard" title="@T["Dashboard"]" />
```

Restyle its current node with `BreadcrumbItem-Dashboard-Title.cshtml` (or `BreadcrumbItem-Dashboard-Title.DetailAdmin.cshtml`). See `orchardcore-breadcrumbs` for the provider contract.

### Admin Home Page Without This Feature

When `OrchardCore.AdminDashboard` is disabled, `/admin` renders the `AdminDashboard` shape of `OrchardCore.Admin`, whose `AdminDashboardContent` child shape collects positioned content from shape table providers (see `orchardcore-admin`).
