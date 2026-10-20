# Permission Provider Examples

## Orchard Core Style Example

Orchard Core keeps the static permissions in a `*Permissions` class of a Core or Abstractions project and the provider in the module:

```csharp
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace OrchardCore.Roles;

public static class RolesPermissions
{
    public static readonly Permission ManageRoles = new(
        "ManageRoles",
        LocalizationSource.Create("Manage Roles", typeof(RolesPermissions)),
        isSecurityCritical: true);

    public static readonly Permission ViewRoles = new(
        "ViewRoles",
        LocalizationSource.Create("View Roles", typeof(RolesPermissions)),
        [ManageRoles]);
}
```

```csharp
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace OrchardCore.Roles;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        RolesPermissions.ManageRoles,
        RolesPermissions.ViewRoles,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
```

## Dynamic Provider Example

A provider can compute permissions at runtime, for example one per content type with a given stereotype. Keep the template description context-free and format it through `Description.Value`:

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Security.Permissions;

namespace MyModule;

internal sealed class ReportPermissionProvider : IPermissionProvider
{
    private static readonly Permission s_viewReportTemplate =
        new("ViewReport_{0}", "View report - {0}", [ReportPermissions.ManageReports]);

    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ReportPermissionProvider(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
        => (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.GetStereotype() == "Report")
            .Select(CreatePermission);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() => [];

    public static Permission CreatePermission(ContentTypeDefinition type) =>
        new(
            string.Format(s_viewReportTemplate.Name, type.Name),
            string.Format(s_viewReportTemplate.Description.Value, type.DisplayName),
            s_viewReportTemplate.ImpliedBy);
}
```

## CrestApps Placement Example

```text
src/Core/CrestApps.Sports.Teams.Core/
  Models/TeamPart.cs
  Permissions/TeamPermissions.cs

src/Modules/CrestApps.Sports.Teams/
  PermissionProvider.cs
  Startup.cs
  Drivers/TeamPartDisplayDriver.cs
```

## Removed Obsolete Permission Members (Orchard Core 4.0)

Only the C# members were removed; stored permission names are unchanged.

| Removed member | Replacement |
| --- | --- |
| `OrchardCore.Admin.PermissionsAdminSettings.ManageAdminSettings` | `OrchardCore.Admin.AdminPermissions.ManageAdminSettings` |
| `OrchardCore.AdminMenu.Permissions.ManageAdminMenu` | `OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu` (3.x member returned `ViewAdminMenuAll`) |
| `OrchardCore.AdminMenu.Permissions.ViewAdminMenuAll` | `OrchardCore.AdminMenu.AdminMenuPermissions.ViewAdminMenuAll` (3.x member returned `ManageAdminMenu`) |
| `OrchardCore.Apis.GraphQL.CommonPermissions` (class and members) | `OrchardCore.Apis.GraphQL.GraphQLPermissions` |
| `OrchardCore.Autoroute.Permissions.SetHomepage` | `OrchardCore.Autoroute.AutoroutePermissions.SetHomepage` |
| `OrchardCore.ContentLocalization.Permissions.*` | `OrchardCore.ContentLocalization.ContentLocalizationPermissions.*` |
| `OrchardCore.ContentManagement.GraphQL.Permissions.ApiViewContent` | `OrchardCore.Apis.GraphQL.GraphQLPermissions.ApiViewContent` |
| `OrchardCore.ContentTypes.Permissions.EditContentTypes` / `ViewContentTypes` | `OrchardCore.Contents.ContentTypesPermissions.EditContentTypes` / `ViewContentTypes` |
| `OrchardCore.Deployment.CommonPermissions` (class and members) | `OrchardCore.Deployment.DeploymentPermissions` |
| `OrchardCore.Deployment.Remote.Permissions.Export` | `OrchardCore.Deployment.DeploymentPermissions.ExportRemoteInstances` |
| `OrchardCore.Deployment.Remote.Permissions.ManageRemoteClients` / `ManageRemoteInstances` | `OrchardCore.Deployment.DeploymentPermissions.ManageRemoteClients` / `ManageRemoteInstances` |
| `OrchardCore.Elasticsearch.ElasticsearchIndexPermissionHelper.GetElasticIndexPermission(string)` | `OrchardCore.Indexing.Core.IndexingPermissions.CreateDynamicPermission(IndexProfile)` |
| `OrchardCore.Email.Permissions.ManageEmailSettings` | `OrchardCore.Email.EmailPermissions.ManageEmailSettings` |
| `OrchardCore.Features.Permissions.ManageFeatures` | `OrchardCore.Features.FeaturesPermissions.ManageFeatures` |
| `OrchardCore.Google.GoogleAnalyticsPermissionsProvider.ManageGoogleAnalytics` | `OrchardCore.Google.Permissions.ManageGoogleAnalytics` |
| `OrchardCore.Localization.Permissions.ManageCultures` | `OrchardCore.Localization.LocalizationPermissions.ManageCultures` |
| `OrchardCore.Lucene.LuceneIndexPermissionHelper.ManageLuceneIndexes`, `OrchardCore.Lucene.Permissions.*` | `OrchardCore.Lucene.LuceneSearchPermissions.*` |
| `OrchardCore.Media.Permissions` (class and members), `MediaCachePermissions.ManageAssetCache`, `SecureMediaPermissions.*` | `OrchardCore.Media.MediaPermissions.*` |
| `OrchardCore.OpenId.Permissions.*` | `OrchardCore.OpenId.OpenIdPermissions.*` |
| `OrchardCore.Queries.Sql.Permissions.ManageSqlQueries` | `OrchardCore.Queries.QueriesPermissions.ManageSqlQueries` |
| `OrchardCore.ReCaptcha.Permissions.ManageReCaptchaSettings` | `OrchardCore.ReCaptcha.ReCaptchaPermissions.ManageReCaptchaSettings` |
| `OrchardCore.Recipes.RecipesPermissionProvider.ManageReCaptchaSettings` | `OrchardCore.Recipes.RecipePermissions.ManageRecipes` |
| `OrchardCore.Search.Permissions.ManageSearchSettings` | `OrchardCore.Search.SearchPermissions.ManageSearchSettings` |
| `OrchardCore.Search.Permissions.QuerySearchIndex` | `OrchardCore.Indexing.Core.IndexingPermissions.QuerySearchIndex` |
| `OrchardCore.Security.StandardPermissions` (class), `StandardPermissions.SiteOwner` | None; assign users to the `Administrator` system role |
| `OrchardCore.Seo.SeoPermissionProvider.ManageSeoSettings` | `OrchardCore.Seo.SeoPermissions.ManageSeoSettings` |
| `OrchardCore.Settings.Permissions.ManageSettings` / `ManageGroupSettings` | `OrchardCore.Settings.SettingsPermissions.ManageSettings` / `ManageGroupSettings` |
| `OrchardCore.Shortcodes.Permissions.ManageShortcodeTemplates` | `OrchardCore.Shortcodes.ShortcodesPermissions.ManageShortcodeTemplates` |
| `OrchardCore.Sitemaps.Permissions.ManageSitemaps` | `OrchardCore.Sitemaps.SitemapsPermissions.ManageSitemaps` |
| `OrchardCore.Sms.SmsPermissionProvider.ManageSmsSettings` | `OrchardCore.Sms.SmsPermissions.ManageSmsSettings` |
| `OrchardCore.Users.CommonPermissions` (class and members) | `OrchardCore.Users.UsersPermissions` (same member names) |
| `OrchardCore.Users.CommonPermissions.CreateAssignRoleToUsersPermission(string)` | `OrchardCore.Users.UsersPermissions.CreateAssignRoleToUsersPermission(string)` (3.x member returned the edit-users-in-role permission) |
| `OrchardCore.Workflows.Permissions.*` | `OrchardCore.Workflows.WorkflowsPermissions.*` |

## Best Practices

- Keep `PermissionProvider` internal and sealed; keep static permissions in a `static` `*Permissions` class.
- Keep reusable static permission instances in the matching `*.Core` project.
- Use `LocalizationSource.Create(value, typeof(DeclaringType))` for descriptions.
- Use `OrchardCoreConstants.Roles.Administrator` for the administrator stereotype.
- Keep role-specific stereotypes focused on the minimum permissions each role needs.
