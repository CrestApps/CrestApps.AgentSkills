---
name: orchardcore-crestapps-roles
description: Skill for CrestApps role extensions in Orchard Core using RolePickerPart. Covers role selection on content types, part settings, required and single or multiple role validation, excluded roles, migrations, recipe schema support, and content item payloads. Use this skill when requests mention CrestApps RolePickerPart, content role picker, role selection content part, role-based content metadata, RolePickerPartSettings, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.Roles, RolePickerPart, RolePickerPartDisplayDriver, RolePickerPartSettingsDisplayDriver, RolePickerPartSchemaDefinition, RoleManager<IRole>, and OrchardCore.Roles.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Roles

## Add roles to content with RolePickerPart

You are an Orchard Core expert. Use the CrestApps Roles module when a content item must persist selected Orchard Core role names. `RolePickerPart` is a reusable content part with settings that control requiredness, selection cardinality, and which roles editors cannot select.

### Guidelines

- Install `CrestApps.OrchardCore.Roles` in the web or startup project.
- Enable the exact `CrestApps.OrchardCore.Roles` feature. Its manifest depends on `OrchardCore.Roles`.
- The reusable part name is `RolePickerPart`; its stored value property is `RoleNames`.
- Use `RolePickerPartSettings` on the content-type part attachment, not on an unrelated field definition.
- Set `AllowSelectMultiple` to `false` for a single role. The display driver validates that at most one role was submitted.
- Set `Required` to `true` only when the item cannot be meaningful without a selected role.
- Populate `ExcludedRoles` for built-in or sensitive roles that should never be selectable in this context.
- Excluded roles are removed from submitted selections by the driver. They are not a security authorization mechanism.
- Use Orchard Core permissions and authorization policies to enforce access. A role-picker value is content metadata, not an access-control grant.
- The single-select editor retrieves role names through `RoleManager<IRole>`, excludes configured names, and orders the choices.
- Enable `CrestApps.OrchardCore.Recipes` when recipe JSON Schema support for settings and the `RoleNames` payload is needed.
- Use sealed classes and file-scoped namespaces in generated C# examples.

### Feature overview

| Feature ID | Purpose |
|---|---|
| `CrestApps.OrchardCore.Roles` | Registers `RolePickerPart`, drivers, and its data migration |
| `OrchardCore.Roles` | Provides Orchard Core role management and identity role services |
| `CrestApps.OrchardCore.Recipes` | Activates `RolePickerPartSchemaDefinition` when combined with the Roles feature |

### Enable RolePickerPart

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Roles",
        "CrestApps.OrchardCore.Recipes"
      ],
      "disable": []
    }
  ]
}
```

## Configure the part

| Setting | Meaning |
|---|---|
| `Required` | Requires at least one allowed selected role |
| `AllowSelectMultiple` | Allows multiple selections when `true` |
| `ExcludedRoles` | Role names that are removed from the available and submitted values |
| `Hint` | Help text for the editor |

`RolePickerPart.RoleNames` is an array of role-name strings. Keep names aligned with role records managed through Orchard Core Roles.

### Attach it in a migration

```csharp
using CrestApps.OrchardCore.Roles.Core.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;

namespace MyModule;

public sealed class ProductMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ProductMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Product", type => type
            .WithPart<RolePickerPart>(part => part
                .WithDisplayName("Visible to roles")
                .WithSettings(new RolePickerPartSettings
                {
                    Required = true,
                    AllowSelectMultiple = true,
                    Hint = "Select the roles that can see this product.",
                    ExcludedRoles = ["Anonymous", "Authenticated"],
                })));

        return 1;
    }
}
```

Register the migration from the owning module:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<ProductMigrations>();
    }
}
```

## Configure it through the admin UI

1. Enable **Enhanced Roles**.
2. Navigate to **Content Definition → Content Types** and edit the target type.
3. Add **RolePickerPart**.
4. Set its display name, hint, required setting, selection mode, and excluded role names.
5. Save the content definition.
6. Edit a content item and select its role values.

The item editor strips excluded roles even if a stale form submission includes them. If single selection is configured, it also reports a model-state error when more than one allowed role is submitted.

## Define the part in a recipe

With CrestApps Recipes enabled, the content-definition schema recognizes `RolePickerPartSettings`.

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentParts": [
        {
          "Name": "RolePickerPart",
          "Settings": {
            "RolePickerPartSettings": {
              "Required": true,
              "AllowSelectMultiple": true,
              "ExcludedRoles": [
                "Anonymous",
                "Authenticated"
              ],
              "Hint": "Select allowed roles."
            }
          },
          "ContentPartFieldDefinitionRecords": []
        }
      ],
      "ContentTypes": [
        {
          "Name": "Product",
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "RolePickerPart",
              "Name": "RolePickerPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "5"
                },
                "RolePickerPartSettings": {
                  "Required": true,
                  "AllowSelectMultiple": true,
                  "ExcludedRoles": [
                    "Anonymous",
                    "Authenticated"
                  ],
                  "Hint": "Select allowed roles."
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

### Set selected roles in a content item recipe

```json
{
  "steps": [
    {
      "name": "content",
      "data": [
        {
          "ContentType": "Product",
          "DisplayText": "Partner catalogue",
          "RolePickerPart": {
            "RoleNames": [
              "Partner",
              "Administrator"
            ]
          }
        }
      ]
    }
  ]
}
```

The schema validates the shape only. Before importing, ensure each `RoleNames` value names a role that exists for the tenant. The content part does not create missing roles.

## Read the selected role names

Use the part data from the loaded content item. Keep authorization decisions explicit rather than using an unvalidated content property as a permission check.

```csharp
using CrestApps.OrchardCore.Roles.Core.Models;
using OrchardCore.ContentManagement;

namespace MyModule;

public sealed class ProductRoleReader
{
    public IReadOnlyList<string> GetRoleNames(ContentItem product)
        => product.As<RolePickerPart>()?.RoleNames ?? [];
}
```

## Troubleshooting

- If the part does not appear in Content Definition, enable `CrestApps.OrchardCore.Roles` and confirm `OrchardCore.Roles` is available.
- If single-select has no options, create roles first in the Orchard Core role administration UI and ensure they are not in `ExcludedRoles`.
- If excluded values appear in imported content JSON, review settings and application logic. The normal editor strips them, but imports should only use allowed roles.
- A missing `RolePickerPartSettings` recipe schema means the Roles and Recipes features were not enabled together.
