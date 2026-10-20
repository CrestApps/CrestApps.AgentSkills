---
name: orchardcore-data-localization
description: Skill for managing translation data in Orchard Core with the OrchardCore.DataLocalization module. Covers the Dynamic Translations editor UI, translation statistics dashboard, ViewDynamicTranslations and ManageTranslations permissions with per-culture permissions, built-in translation providers and their contexts, custom ILocalizationDataProvider implementation, the Translations recipe step and deployment steps, IDataLocalizer, TranslationsManager, and the Liquid d filter. Use this skill when requests mention Orchard Core Data Localization, Dynamic Translations, Enabling Data Localization, Permissions, Built-in Data Providers, Creating a Custom Localization Data Provider, Data Localization Filters, translating content type names or permission categories, upgrading data localization to Orchard Core 4, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include OrchardCore.DataLocalization and OrchardCore.Localization.Data.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Data Localization

## Guidelines

- Enable `OrchardCore.DataLocalization` (depends on `OrchardCore.Localization`)
  for database-backed translations of dynamic strings such as content type and
  field display names, admin menu text, and permission categories.
- Use PO files (`OrchardCore.Localization`, see `orchardcore-localization`) for
  static UI strings in views and code, including permission descriptions
  declared with `LocalizationSource.Create(value, typeof(DeclaringType))`.
- Use `OrchardCore.ContentLocalization` for translating content items.
- Implement `ILocalizationDataProvider` to expose custom translatable strings.
- Keys must be human-readable display values, because untranslated keys are
  displayed as-is.
- Use `IDataLocalizer` (aliased as `D`) in Razor and the `d` filter in Liquid.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

## Enabling Data Localization

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.DataLocalization"
      ]
    }
  ]
}
```

## Admin UI

**Settings → Localization → Dynamic Translations** opens the translation
editor: pick a culture, search, filter by category, and edit each string;
auto-save (on by default) saves after 2 seconds of inactivity. The
**Statistics** page shows overall, per-culture, and per-category progress
(green ≥75%, yellow 25–74%, red <25%).

## Permissions

`OrchardCore.Localization.Data.DataLocalizationPermissions` declares:

| Permission | Description |
|---|---|
| `ViewDynamicTranslations` | View translations and statistics (read-only). Implied by `ManageTranslations`. |
| `ManageTranslations` | Edit translations for all cultures. |
| `ManageTranslations_{culture}` | Edit one culture, e.g. `ManageTranslations_fr-FR` ("Manage French (France) translations"), generated for each supported culture with `CreateCulturePermission()`. Implied by `ManageTranslations`. |
| `ManageLocalization` | Legacy, kept for compatibility; implied by `ManageTranslations`. |

Defaults: **Administrator** gets `ManageTranslations`, **Editor** gets
`ViewDynamicTranslations`. The admin menu entry and the strings API require
`ViewDynamicTranslations`, so grant it together with a culture permission to a
role that should edit only one culture (under **Data Localization** in the role
editor).

## Built-in Data Providers

| Provider | Context | Strings Provided |
|----------|---------|-----------------|
| Content types (`OrchardCore.Contents`) | `Content Types` | Content type display names |
| Content fields (`OrchardCore.Contents`) | `Content Fields:{PartName}` | Content field display names, grouped by part |
| Permissions (`OrchardCore.Roles`) | `Permissions` or `Permissions:{Group}` | Permission categories, and descriptions that are not templates and have no PO context |
| Admin menus (`OrchardCore.AdminMenu`) | `Admin Menus` or `Admin Menus:{MenuName}` | Admin menu and admin node text |
| Search (`OrchardCore.Search`) | `Search` | Search-related text such as the placeholder |

Built-in permission descriptions now carry a PO context, so they are translated
with PO files and are not listed here; template-based descriptions such as
"Edit Article" still are.

## Creating a Custom Localization Data Provider

```csharp
using OrchardCore.Localization.Data;

public sealed class MenuItemLocalizationDataProvider : ILocalizationDataProvider
{
    public Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var strings = new List<DataLocalizedString>
        {
            new("Menu Items", "Dashboard", string.Empty),
            new("Menu Items", "User Settings", string.Empty),
            new("Menu Items", "Reports", string.Empty),
        };

        return Task.FromResult<IEnumerable<DataLocalizedString>>(strings);
    }
}
```

```csharp
using OrchardCore.Localization.Data;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, MenuItemLocalizationDataProvider>();
    }
}
```

The `DataLocalizedString(context, name, value)` constructor takes the context
(category), the key, and the value (empty for descriptors). Use the context
to disambiguate keys; `Group:SubGroup` contexts are shown as sub-groups.

| Key Type | Example Key | Displayed if Untranslated | Result |
|----------|-------------|---------------------------|--------|
| Display value | `"Welcome Message"` | `Welcome Message` | Good |
| Technical ID | `"welcome_msg_001"` | `welcome_msg_001` | Bad |
| Code constant | `"CONTENT_TYPE_ARTICLE"` | `CONTENT_TYPE_ARTICLE` | Bad |

## Import Translations via Recipe

The `Translations` step takes a flat `translations` array; entries are merged
into the existing translations of each culture by context and key.

```json
{
  "steps": [
    {
      "name": "Translations",
      "translations": [
        { "culture": "fr-FR", "context": "Menu Items", "key": "Dashboard", "value": "Tableau de bord" },
        { "culture": "fr-FR", "context": "Content Types", "key": "Article", "value": "Article" },
        { "culture": "es-ES", "context": "Content Types", "key": "Article", "value": "Artículo" }
      ]
    }
  ]
}
```

The same step also accepts the legacy `DynamicDataTranslations` step name.

## Deployment Steps

- **Translations** exports the `Translations` format above, with all or
  selected cultures (`IncludeAll`, `Cultures`) and optional `Categories`.
- **All Data Translations** exports every translation as a
  `DynamicDataTranslations` step.

## Using IDataLocalizer in Razor Views

```cshtml
@using OrchardCore.Localization.Data
@inject IDataLocalizer D

<h1>@D["Article", "Content Types"]</h1>
<p>@D["Hello {0}", "My Context", userName]</p>
```

`DataLocalizedString` converts implicitly to `string`, and Razor HTML-encodes
it. Translated values should contain plain text only. `ResourceNotFound` tells
whether a translation exists. When a breadcrumb or other API needs
`IHtmlContent`, wrap `.Value ?? string.Empty` in an `HtmlContentString` rather
than using `Html.Raw`.

| Feature | `IStringLocalizer` (T) | `IDataLocalizer` (D) |
|---------|------------------------|----------------------|
| Source | PO files (static) | Database (dynamic) |
| HTML encoding | Auto-encoded by Razor | Auto-encoded by Razor |
| Pluralization | Supported | Not supported |
| Arguments | `T["Hello {0}", name]` | `D["Hello {0}", "Context", name]` |
| Use case | Static UI strings | Dynamic data (content types, menus, categories) |

## Liquid Filter: `d`

The `d` filter is documented under **Data Localization Filters** in the Liquid
reference:

```liquid
{{ "Blog" | d: "Content Types" }}
```

It returns the translation for the current culture, for example `مدونة` for
`ar`. For missing translations or unsupported cultures it returns
`Blog.Content Types`.

## Managing Translations in Code

Inject the scoped `TranslationsManager` (`OrchardCore.DataLocalization.Services`;
the `ITranslationsManager` interface is not registered). `UpdateTranslationAsync(culture, translations)`
replaces the translations of a culture, `RemoveTranslationAsync(culture)`
removes them, and `GetTranslationsDocumentAsync()` returns the cached
`TranslationsDocument`. The localization cache is refreshed when the document
changes.

## When to Use Each Localization Approach

| Use Case | Recommended Approach |
|----------|---------------------|
| Static UI strings and declared permission descriptions | PO files (`OrchardCore.Localization`) |
| Content type/field display names | Data Localization |
| Permission categories and template-based descriptions | Data Localization |
| Admin menu items | Data Localization |
| User-defined content | Content Localization (`OrchardCore.ContentLocalization`) |
| Database-stored dynamic strings | Data Localization with a custom provider |

## Upgrading from 3.x

- Built-in permission descriptions are translated with PO files and no longer
  appear in Data Localization; translations entered for them in Data
  Localization are ignored. Move them to PO files under the declaring type's
  context (for example `OrchardCore.Email.EmailPermissions`).
- `IDataResourceStringProvider` was removed; implement `ILocalizationDataProvider`.
- The Liquid documentation section "Localization Filters" for `d` is now
  "Data Localization Filters".
