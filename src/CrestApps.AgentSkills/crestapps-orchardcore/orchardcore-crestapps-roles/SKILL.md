---
name: orchardcore-crestapps-roles
description: Skill for the CrestApps Enhanced Roles module in Orchard Core and its RolePickerPart. Covers role selection on content types with the bootstrap-select editor, RolePickerPartSettings for required, single or multiple and excluded roles, the editor labels of a picker that restricts content, the shared ContentAccessRoles helper and ContentAccessRoleIndex in Roles.Core, migrations, recipe schema support, content item payloads, and the 3.0 dependency on OrchardCore.Resources. Use this skill when requests mention CrestApps RolePickerPart, Enhanced Roles, a content role picker, role selection content part, filtering a list by the reader's roles, RolePickerPartSettings or related setup and troubleshooting. Strong matches include CrestApps.OrchardCore.Roles, RolePickerPart, RolePickerPartDisplayDriver, RolePickerPartSettingsDisplayDriver, RolePickerPartSchemaDefinition, ContentAccessRoles, ContentAccessRoleIndex, RoleManager<IRole>, OrchardCore.Roles and OrchardCore.Resources.
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
- Enable the exact `CrestApps.OrchardCore.Roles` feature (**Enhanced Roles**). Its manifest depends on `OrchardCore.Roles` and `OrchardCore.Resources`.
- The reusable part name is `RolePickerPart`; its stored value property is `RoleNames`.
- The module's migration defines `RolePickerPart` as attachable and reusable (display name **Role Picker**, default position `999`), so a type can attach it more than once under different names.
- `RolePickerPart`, `RolePickerPartSettings` and `RolePickerPartContentAccessControlSettings` live in `CrestApps.OrchardCore.Roles.Core.Models`.
- The editor is Orchard Core's `bootstrap-select` dropdown with live search, plus select-all and deselect-all when multiple selection is allowed.
- Use `RolePickerPartSettings` only on a `RolePickerPart` content-type attachment, not on the reusable part definition or an unrelated field definition.
- Set `AllowSelectMultiple` to `false` for a single role. The display driver validates that at most one role was submitted.
- Set `Required` to `true` only when the item cannot be meaningful without a selected role.
- Populate `ExcludedRoles` for built-in or sensitive roles that should never be selectable in this context.
- Excluded roles are removed from submitted selections by the driver. They are not a security authorization mechanism.
- On its own, a role-picker value is content metadata, not an access-control grant. To restrict who can view an item by these roles, enable Content Access Control (see the `orchardcore-content-access-control` skill), which builds on this part.
- The editor lists role names from `RoleManager<IRole>`, removes excluded names, and orders the choices.
- When Content Access Control marks the picker as restricting the content (`RolePickerPartContentAccessControlSettings.IsContentRestricted`), the editor label defaults to **Which roles can see this content**, the empty text reads **Any role** and the hint defaults to **Leave empty so everyone can see it.**; a display name or hint set on the part attachment still wins. Other pickers use the part's display name as label and empty text.
- To filter lists by the reader's roles, use `ContentAccessRoles` and `ContentAccessRoleIndex` from `CrestApps.OrchardCore.Roles.Core`, never a per-item check over a large list.
- Enable `CrestApps.OrchardCore.Recipes` when recipe JSON Schema support for settings and the `RoleNames` payload is needed.
- Use sealed classes and file-scoped namespaces in generated C# examples.

### Feature overview

| Feature ID | Purpose |
|---|---|
| `CrestApps.OrchardCore.Roles` | Registers `RolePickerPart`, drivers, and its data migration |
| `OrchardCore.Roles` | Provides Orchard Core role management and identity role services |
| `OrchardCore.Resources` | Registers the `bootstrap-select` script and style the editors use |
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

| `RolePickerPartSettings` property | Admin label | Meaning |
|---|---|---|
| `ExcludedRoles` | **Exclude roles** | Role names removed from the choices and stripped from the saved value |
| `Required` | **Required?** | Requires at least one allowed selected role |
| `AllowSelectMultiple` | **Allow multiple?** | Allows more than one role; when off, saving more than one fails validation |
| `Hint` | **Hint** | Help text under the picker |

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
2. Navigate to **Design → Content Definition → Content Types** and edit the target type.
3. Add the **Role Picker** part.
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

## Reader roles for restricted content

`CrestApps.OrchardCore.Roles.Core` (namespace `CrestApps.OrchardCore.Roles.Core`) holds the building blocks Content Access Control and modules such as Knowledge Base share:

| Member | Purpose |
|---|---|
| `ContentAccessRoles.GetReaderRoles(user)` | `Anonymous` for a visitor; `Authenticated` plus the role claims for a signed-in user (Orchard Core never puts those two roles in claims) |
| `ContentAccessRoles.IsInAnyRole(user, roleNames)` | Whether the reader holds any of the roles, `Authenticated` and `Anonymous` included |
| `ContentAccessRoles.SeesEverything(user)` | `true` for the Administrator role; skip role filtering for them |
| `ContentAccessControlConstants.Feature` / `.RolesFeature` | `CrestApps.OrchardCore.ContentAccessControl` / `CrestApps.OrchardCore.Roles` |
| `ContentAccessRoleIndex` (`.Indexes`) | One row (`ContentItemId`, `ContentType`, `RoleName`) per picked role of each published item of a restricted type; an item with no role picked gets `Anonymous` and `Authenticated` rows. Drafts and unrestricted types get none |

The Content Access Control module owns the index provider and table (it declares `.ExclusiveToDocumentType()` only, since an item has one row per role); see `orchardcore-content-access-control`. Filter a published-item query in SQL:

```csharp
using CrestApps.OrchardCore.Roles.Core;
using CrestApps.OrchardCore.Roles.Core.Indexes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using System.Security.Claims;
using YesSql;
using YesSql.Services;

namespace MyModule.Services;

public sealed class RestrictedArticleQuery
{
    private readonly ISession _session;

    public RestrictedArticleQuery(ISession session)
    {
        _session = session;
    }

    public Task<IEnumerable<ContentItem>> ListAsync(ClaimsPrincipal reader)
    {
        var query = _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == "Article" && index.Published);

        if (!ContentAccessRoles.SeesEverything(reader))
        {
            var roles = ContentAccessRoles.GetReaderRoles(reader).ToArray();

            query = query.Where(index => index.ContentItemId.IsIn<ContentAccessRoleIndex>(
                row => row.ContentItemId,
                row => row.RoleName.IsIn(roles)));
        }

        return query.ListAsync();
    }
}
```

## Upgrading from 2.x

- The editors no longer use the removed `crestapps-bootstrap-select` resource. They request Orchard Core's `bootstrap-select`, and the module now depends on `OrchardCore.Resources`. Theme overrides of `RolePickerPart.Edit.cshtml` or `RolePickerPartSettings.Edit.cshtml` must switch to `<style asp-name="bootstrap-select">` and `<script asp-name="bootstrap-select" at="Foot">`.

## Troubleshooting

- If the part does not appear in Content Definition, enable `CrestApps.OrchardCore.Roles` and confirm `OrchardCore.Roles` is available.
- If the picker renders as a plain list box, confirm `OrchardCore.Resources` is enabled so `bootstrap-select` resolves.
- If single-select has no options, create roles first in the Orchard Core role administration UI and ensure they are not in `ExcludedRoles`.
- If excluded values appear in imported content JSON, review settings and application logic. The normal editor strips them, but imports should only use allowed roles.
- A missing `RolePickerPartSettings` recipe schema means the Roles and Recipes features were not enabled together.
- A restricted picker shows **Which roles can see this content** instead of the part name: give the part attachment a display name to override it.
