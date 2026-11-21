---
name: orchardcore-content-access-control
description: Skill for role-based content access control in Orchard Core with the CrestApps Content Access Control module. Covers restricting who can read a content item by the roles picked on a RolePickerPart, the Restrict content setting RolePickerPartContentAccessControlSettings, how RoleBasedContentItemAuthorizationHandler evaluates every reading permission (view, preview, list, securable types), Authenticated and Anonymous as picked roles, the Administrator bypass, ContentAccessRoles, filtering lists and searches in SQL with ContentAccessRoleIndex, keeping restricted items out of AI knowledge bases, and recipes. Use this skill when requests mention Content Access Control, restricting content by role, members-only content, filtering restricted content in a query, or RolePickerPart access settings. Strong matches include CrestApps.OrchardCore.ContentAccessControl, RoleBasedContentItemAuthorizationHandler, RolePickerPartContentAccessControlSettings, IsContentRestricted, ContentAccessRoles and ContentAccessRoleIndex.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Content Access Control

## Restrict content items by role

You are an Orchard Core expert. Content Access Control (`CrestApps.OrchardCore.ContentAccessControl`) restricts who may read individual content items. It adds a **Restrict content?** setting to each `RolePickerPart` attached to a content type; when it is on, the roles picked on an item decide who may view, preview and list that item.

### Guidelines

- Install `CrestApps.OrchardCore.ContentAccessControl` in the web or startup project and enable the feature. It depends on `CrestApps.OrchardCore.Roles` (Enhanced Roles), which provides `RolePickerPart`; see the `orchardcore-crestapps-roles` skill.
- Attach a `RolePickerPart` to the type first; **Restrict content?** appears in that part's settings.
- Every permission that reads a `ContentItem` resource is affected: `ViewContent`, `ViewOwnContent`, `PreviewContent`, `PreviewOwnContent` and `ListContent`, and a securable type's own versions (`ViewContent_Handbook`), matched by name. Edit, publish and delete checks are not.
- **Authenticated** means anyone signed in and **Anonymous** a visitor, though neither is ever a role claim. Keep them in the picker so an editor can open an item to every signed-in user or to everyone; exclude only `Administrator` (who sees everything anyway) with `RolePickerPartSettings.ExcludedRoles`.
- Code that shows content without calling `IAuthorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, contentItem)` is not protected; authorize every item you render, or filter the query with `ContentAccessRoleIndex`.
- Use `ContentAccessRoles` (`CrestApps.OrchardCore.Roles.Core`: `GetReaderRoles`, `IsInAnyRole`, `SeesEverything`) in custom filters so they match the handler.
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
| Index | `ContentAccessRoleIndex` (`CrestApps.OrchardCore.Roles.Core.Indexes`: `ContentItemId`, `ContentType`, `RoleName`), scoped provider `ContentAccessRoleIndexProvider`, `.ExclusiveToDocumentType()` only (several rows per item) |
| AI visibility | `ContentAccessIndexedDocumentVisibility` (`IIndexedDocumentVisibility`) |
| Constants | `ContentAccessControlConstants.Feature`, `ContentAccessControlConstants.RolesFeature` (`CrestApps.OrchardCore.Roles.Core`) |

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

For a reading permission checked on a `ContentItem`, `RoleBasedContentItemAuthorizationHandler`:

1. Returns without acting for users in the `Administrator` role (`ContentAccessRoles.SeesEverything`), so administrators see every item.
2. Returns for any permission that does not read content (edit, publish, delete and others).
3. Loads the type definition and collects the role names from every `RolePickerPart` attachment (named or unnamed) whose `IsContentRestricted` is `true`.
4. When the user is in any collected role (`ContentAccessRoles.IsInAnyRole`, which treats **Authenticated** as any signed-in user and **Anonymous** as a visitor), succeeds a view permission (`ViewContent` or `ViewContent_{Type}`); listing and previewing still need their own permission.
5. Fails every reading permission when roles were collected and the user is in none of them, even if another handler already succeeded it (Orchard Core grants **View content** to everyone, anonymous visitors included). A securable type's own permissions cannot leak a restricted item.
6. Does nothing when no roles were picked, so an item with an empty picker is not restricted.

Role names are compared case insensitively. Roles from several restricted attachments are combined. An item restricted to **Authenticated** opens for every signed-in user, to **Anonymous** only for visitors, and to both for everyone.

On the item editor, a restricting picker is labelled *Which roles can see this content* (unless the part has a display name of its own), shows *Any role* while none is picked, and hints *Leave empty so everyone can see it.* (unless the part has a hint).

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
                .WithSettings(new RolePickerPartContentAccessControlSettings
                {
                    IsContentRestricted = true,
                })
                .WithSettings(new RolePickerPartSettings
                {
                    AllowSelectMultiple = true,
                    Required = true,
                    Hint = "Select the roles that may view this article.",
                    ExcludedRoles = ["Administrator"],
                })));

        return 1;
    }
}
```

Register it with `services.AddDataMigration<MembersArticleMigrations>();`. Without a display name, editors see the default label *Which roles can see this content*; the custom `Hint` replaces *Leave empty so everyone can see it.*, which would contradict `Required`.

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
                  "ExcludedRoles": [ "Administrator" ]
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

1. Enable **Content Access Control** under **Tools > Features**.
2. Under **Design > Content Definition > Content Types**, edit the type, add the **Role Picker** part, open its settings and tick **Restrict content?**.
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

## Filter lists and searches in SQL

Authorizing each item is fine for one page, but a list or search of a restricted type would load every item to drop the ones its reader can't open. `ContentAccessRoleIndex` holds a row per role allowed to read each **published** item of a restricted type; an item with no role picked gets two rows, `Anonymous` and `Authenticated`. Drafts and items of unrestricted types get no row. When the feature is first enabled, a deferred task indexes the published items restricted before, 100 at a time.

```csharp
using CrestApps.OrchardCore.Roles.Core;
using CrestApps.OrchardCore.Roles.Core.Indexes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace MyModule.Services;

public sealed class MembersArticleQuery
{
    private readonly ISession _session;

    public MembersArticleQuery(ISession session)
    {
        _session = session;
    }

    public Task<IEnumerable<ContentItem>> ListVisibleAsync(System.Security.Claims.ClaimsPrincipal user)
    {
        var query = _session.Query<ContentItem, ContentItemIndex>(index => index.Published && index.ContentType == "MembersArticle");

        if (!ContentAccessRoles.SeesEverything(user))
        {
            var roles = ContentAccessRoles.GetReaderRoles(user).ToArray();

            query = query.Where(index => index.ContentItemId.IsIn<ContentAccessRoleIndex>(
                row => row.ContentItemId,
                row => row.RoleName.IsIn(roles)));
        }

        return query.OrderByDescending(index => index.PublishedUtc).Take(20).ListAsync();
    }
}
```

Use it only for a type whose `RolePickerPart` restricts content: items of other types have no row and would never be found. The Knowledge Base filters the help center, its search and its suggestions this way. A module that relies on the index depends on `ContentAccessControlConstants.Feature`.

## AI knowledge bases

An AI data source built from a content index answers anyone allowed to use its profile, without this module's checks. `ContentAccessIndexedDocumentVisibility` (an `IIndexedDocumentVisibility`) makes the AI data source sync leave out every item whose allowed roles do not include `Anonymous`, and remove one restricted after it was indexed. Items with no role picked, and items of types without a restricting picker, stay in. See `orchardcore-ai-data-sources`.

## Upgrading from 2.x

- The handler no longer stops when another handler already succeeded the requirement. In 2.x a restriction had no effect on anyone who held **View content** (including anonymous visitors), so restricted items were effectively public. In 3.0 a restricted item is refused to whoever is not in its roles. Review restricted types after upgrading: visitors and users outside the picked roles lose access.
- Users in the `Administrator` role bypass the restriction.
- The restriction now covers every reading permission (view, view own, preview, preview own, list) and a securable type's own permissions, not only `ViewContent`.
- **Authenticated** and **Anonymous** are honored when picked. Remove them from `ExcludedRoles` if you excluded them before, so editors can open an item to all signed-in users or to everyone.

## Troubleshooting

- **Restrict content?** is missing: the type has no `RolePickerPart`, or the feature is disabled.
- Everyone can still see an item: no roles are picked on it, the restriction is off for that attachment, or the page renders the item without a `ViewContent` authorization check.
- An editor cannot open an item they should see: confirm their role is picked and is not in `ExcludedRoles`.
- Recipe tooling does not know `RolePickerPartContentAccessControlSettings`: enable `CrestApps.OrchardCore.Recipes`.
- A list filtered with `ContentAccessRoleIndex` misses items: the type is not restricted (no rows), the items are drafts, or the backfill after enabling the feature has not finished.
- A restricted item still answers in AI chat: it lists `Anonymous` among its roles, has no role picked, or the data source has not synced since.
