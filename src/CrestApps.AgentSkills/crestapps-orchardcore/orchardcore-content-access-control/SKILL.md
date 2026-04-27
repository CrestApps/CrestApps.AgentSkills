---
name: orchardcore-content-access-control
description: Skill for configuring role-based content access control in Orchard Core using the CrestApps Content Access Control module. Covers restricting content view permissions by role, the RoleBasedContentItemAuthorizationHandler, RolePickerPart settings, and enabling the feature via recipe. Use this skill when requests mention Orchard Core Content Access Control, restricting content by role, role-based content viewing, RolePickerPart access settings, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.ContentAccessControl, CrestApps.OrchardCore.Roles, RoleBasedContentItemAuthorizationHandler, RolePickerPartContentAccessControlSettings. It also helps with enabling content restriction on a content type, configuring which roles can view content items, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Content Access Control - Prompt Templates

## Configure Content Access Control

You are an Orchard Core expert. Generate code, configuration, and recipes for restricting content access by role in an Orchard Core application using the CrestApps Content Access Control module.

### Overview

The **Content Access Control** module (`CrestApps.OrchardCore.ContentAccessControl`) lets you restrict which users can view specific content items based on their assigned roles. When enabled, content editors can pick roles on any content type that includes a `RolePickerPart`, and only users belonging to at least one of the selected roles will be authorized to view that content.

- **Feature ID** - `CrestApps.OrchardCore.ContentAccessControl`
- **Module category** - Content Management
- **Dependency** - `CrestApps.OrchardCore.Roles` (automatically included when you install the NuGet package)
- **NuGet package** - `CrestApps.OrchardCore.ContentAccessControl`

### How It Works

1. The module registers a `RoleBasedContentItemAuthorizationHandler` as a scoped `IAuthorizationHandler`.
2. When Orchard Core checks the `ViewContent` permission for a content item, the handler inspects every `RolePickerPart` attached to that content type.
3. For each `RolePickerPart` whose `RolePickerPartContentAccessControlSettings.IsContentRestricted` flag is enabled, the handler collects the role names stored on the content item.
4. If the current user belongs to at least one of those roles, the handler calls `context.Succeed(requirement)` and grants access.
5. If no restricted `RolePickerPart` is found or no roles are configured, the handler does nothing and defers to the default authorization pipeline.

### Key Services

| Service | Lifetime | Purpose |
|---------|----------|---------|
| `RoleBasedContentItemAuthorizationHandler` | Scoped | Intercepts `ViewContent` permission checks and enforces role-based restrictions |
| `RolePickerPartContentAccessControlSettingsDisplayDriver` | Scoped | Provides the "Restrict content?" checkbox in the content type editor for each `RolePickerPart` |

### Guidelines

- Install the NuGet package in your web/startup project.
- The module depends on `CrestApps.OrchardCore.Roles`, which provides the `RolePickerPart`. You do not need to install it separately; it is included as a package dependency.
- The access control check only applies to the `ViewContent` permission. Other permissions (edit, delete, publish) are not affected.
- A content type must have a `RolePickerPart` attached before the "Restrict content?" option appears in the content type editor.
- When the "Restrict content?" checkbox is enabled for a `RolePickerPart` on a content type, editors will pick roles when creating or editing content items of that type.
- If multiple `RolePickerPart` instances are attached (named parts), each one is evaluated independently and all selected roles across all restricted parts are combined.

### Enabling the Feature via Recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContentAccessControl"
      ]
    }
  ]
}
```

### Attaching RolePickerPart and Enabling Content Restriction

To use content access control on a content type, first attach a `RolePickerPart` (provided by `CrestApps.OrchardCore.Roles`) and then enable the "Restrict content?" setting on that part. Below is a recipe that creates an `Article` content type with content restriction enabled.

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
              "Listable": true,
              "Draftable": true,
              "Versionable": true,
              "Securable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart",
              "Settings": {}
            },
            {
              "PartName": "RolePickerPart",
              "Name": "RolePickerPart",
              "Settings": {
                "RolePickerPartContentAccessControlSettings": {
                  "IsContentRestricted": true
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

With this configuration, every `Article` content item will display a role picker. Only users who belong to at least one of the selected roles will be able to view the article.

### Programmatic Registration

The module registers its services in the `Startup` class.

```csharp
using CrestApps.OrchardCore.ContentAccessControl.Drivers;
using CrestApps.OrchardCore.ContentAccessControl.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContentAccessControl;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContentTypePartDefinitionDisplayDriver, RolePickerPartContentAccessControlSettingsDisplayDriver>()
            .AddScoped<IAuthorizationHandler, RoleBasedContentItemAuthorizationHandler>();
    }
}
```

### Summary

| Task | How |
|------|-----|
| Install the package | `dotnet add reference CrestApps.OrchardCore.ContentAccessControl` in the web project |
| Enable the feature | Recipe step with `"enable": ["CrestApps.OrchardCore.ContentAccessControl"]` |
| Attach role picker to a content type | Add `RolePickerPart` via content type editor or recipe |
| Turn on restriction | Check "Restrict content?" in the `RolePickerPart` settings or set `IsContentRestricted` to `true` in the recipe |
| Assign roles to a content item | Edit the content item and select roles in the role picker field |
