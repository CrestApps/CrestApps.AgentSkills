---
name: orchardcore-content-access-control
description: Skill for role-based content access control in Orchard Core with the CrestApps Content Access Control module. Covers restricting who can view a content item by the roles picked on a RolePickerPart, the Restrict content setting RolePickerPartContentAccessControlSettings, how RoleBasedContentItemAuthorizationHandler evaluates ViewContent, the Administrator bypass, named attachments, migrations, recipes, and the 3.0 change that makes restrictions apply even when View content is already granted. Use this skill when requests mention Orchard Core Content Access Control, restricting content by role, role-based content viewing, members-only content items, or RolePickerPart access settings. Strong matches include CrestApps.OrchardCore.ContentAccessControl, CrestApps.OrchardCore.Roles, RoleBasedContentItemAuthorizationHandler, RolePickerPartContentAccessControlSettings, IsContentRestricted and CommonPermissions.ViewContent.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Content Access Control

## Restrict content items by role

You are an Orchard Core expert. Content Access Control (`CrestApps.OrchardCore.ContentAccessControl`) restricts who may view individual content items. It adds a **Restrict content?** setting to each `RolePickerPart` attached to a content type; when it is on, the roles picked on an item decide who may view that item.

### Guidelines

- Install `CrestApps.OrchardCore.ContentAccessControl` in the web or startup project and enable the feature. It depends on `CrestApps.OrchardCore.Roles` (Enhanced Roles), which provides `RolePickerPart`; see the `orchardcore-crestapps-roles` skill.
- Attach a `RolePickerPart` to the type first; **Restrict content?** appears in that part's settings.
- Only `CommonPermissions.ViewContent` checked against a `ContentItem` resource is affected. Edit, publish and delete checks are not.
- Use `RolePickerPartSettings.ExcludedRoles` to keep roles such as `Administrator`, `Authenticated` and `Anonymous` out of the picker.
- Code that shows content without calling `IAuthorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, contentItem)` is not protected; authorize every item you render from custom queries, APIs or lists.
- Enable `CrestApps.OrchardCore.Recipes` for schema support of `RolePickerPartContentAccessControlSettings`.

### Feature overview

| Item | Value |
|---|---|
| Feature ID | `CrestApps.OrchardCore.ContentAccessControl` (category Content Management) |
| Dependency | `CrestApps.OrchardCore.Roles` |
| Authorization handler | `RoleBasedContentItemAuthorizationHandler` (scoped `IAuthorizationHandler` for `PermissionRequirement`) |
| Settings driver | `RolePickerPartContentAccessControlSettingsDisplayDriver` (adds **Restrict content?**) |
| Settings model | `RolePickerPartContentAccessControlSettings.IsContentRestricted` (namespace `CrestApps.OrchardCore.Roles.Core.Models`) |
| Recipe schema | `RolePickerPartContentAccessControlSchemaDefinition`, registered when `CrestApps.OrchardCore.Recipes` is enabled |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContentAccessControl"
      ],
      "disable": []
    }
  ]
}
```

## How authorization works

For a `ViewContent` check on a `ContentItem`, `RoleBasedContentItemAuthorizationHandler`:

1. Returns without acting for users in the `Administrator` role, so administrators see every item.
2. Loads the type definition and collects the role names from every `RolePickerPart` attachment (named or unnamed) whose `IsContentRestricted` is `true`.
3. Succeeds the requirement when the user is in any collected role.
4. Fails the requirement when roles were collected and the user is in none of them, even if another handler already succeeded it (Orchard Core grants **View content** to everyone, anonymous visitors included).
5. Does nothing when no roles were picked, so an item with an empty picker is not restricted.

Role names are compared case insensitively. Roles from several restricted attachments are combined.

## Configure a restricted type

### In a migration

```csharp
using CrestApps.OrchardCore.Roles.Core.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace MyModule.Migrations;

public sealed class MembersArticleMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public MembersArticleMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("MembersArticle", type => type
            .WithPart<RolePickerPart>(part => part
                .WithDisplayName("Limit access to selected roles")
                .WithSettings(new RolePickerPartContentAccessControlSettings
                {
                    IsContentRestricted = true,
                })
                .WithSettings(new RolePickerPartSettings
                {
                    AllowSelectMultiple = true,
                    Required = true,
                    Hint = "Select the roles that may view this article.",
                    ExcludedRoles = ["Administrator", "Authenticated", "Anonymous"],
                })));

        return 1;
    }
}
```

Register it with `services.AddDataMigration<MembersArticleMigrations>();`.

### In a recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "MembersArticle",
          "DisplayName": "Members Article",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true,
              "Draftable": true,
              "Versionable": true
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
                },
                "RolePickerPartSettings": {
                  "Required": true,
                  "AllowSelectMultiple": true,
                  "ExcludedRoles": [ "Administrator", "Authenticated", "Anonymous" ]
                }
              }
            }
          ]
        }
      ]
    },
    {
      "name": "content",
      "data": [
        {
          "ContentType": "MembersArticle",
          "DisplayText": "Members update",
          "Published": true,
          "Latest": true,
          "RolePickerPart": {
            "RoleNames": [ "Member" ]
          }
        }
      ]
    }
  ]
}
```

### In the admin

1. Enable **Content Access Control**.
2. Under **Content Definition > Content Types**, edit the type, add the **Role Picker** part, open its settings and tick **Restrict content?**.
3. Edit an item and pick the roles allowed to view it.

## Check access in custom code

```csharp
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace MyModule.Services;

public sealed class VisibleContentFilter
{
    private readonly IAuthorizationService _authorizationService;

    public VisibleContentFilter(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task<IReadOnlyList<ContentItem>> FilterAsync(System.Security.Claims.ClaimsPrincipal user, IEnumerable<ContentItem> items)
    {
        var visible = new List<ContentItem>();

        foreach (var item in items)
        {
            if (await _authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, item))
            {
                visible.Add(item);
            }
        }

        return visible;
    }
}
```

## Upgrading from 2.x

- The handler no longer stops when another handler already succeeded the requirement. In 2.x a restriction had no effect on anyone who held **View content** (including anonymous visitors), so restricted items were effectively public. In 3.0 a restricted item is refused to whoever is not in its roles. Review restricted types after upgrading: visitors and users outside the picked roles lose access.
- Users in the `Administrator` role bypass the restriction.

## Troubleshooting

- **Restrict content?** is missing: the type has no `RolePickerPart`, or the feature is disabled.
- Everyone can still see an item: no roles are picked on it, the restriction is off for that attachment, or the page renders the item without a `ViewContent` authorization check.
- An editor cannot open an item they should see: confirm their role is picked and is not in `ExcludedRoles`.
- Recipe tooling does not know `RolePickerPartContentAccessControlSettings`: enable `CrestApps.OrchardCore.Recipes`.
