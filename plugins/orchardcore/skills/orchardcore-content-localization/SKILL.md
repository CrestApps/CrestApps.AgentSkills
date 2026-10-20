---
name: orchardcore-content-localization
description: Skill for configuring content localization in Orchard Core. Covers LocalizationPart for multi-language content, IContentLocalizationManager, the ContentCulturePicker feature and shape, ContentRequestCultureProvider, culture cookie settings, the ContentLocalization CulturePickerOptions configuration section, Liquid localization filters (switch_culture_url, localization_set), localized sitemaps, the contents admin list culture filter, GraphQL localizations, and ContentLocalizationPermissions. Use this skill when requests mention Orchard Core Content Localization, Attaching LocalizationPart to a Content Type, Attaching LocalizationPart via Recipe, ContentCulturePicker Cookie Settings via Recipe, Cookie Lifetime Configuration, translating content items, culture switcher, upgrading content localization to Orchard Core 4, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Content Localization

## Guidelines

- Enable `OrchardCore.ContentLocalization` (depends on Content Types and
  Localization) to manage localized versions of content items. Configure the
  site cultures first (see `orchardcore-localization`).
- Attach `LocalizationPart` to every content type that needs translations. Each
  item stores its `Culture`; all translations share a `LocalizationSet`.
- Enable `OrchardCore.ContentLocalization.ContentCulturePicker` (requires
  Autoroute) for a front-end culture picker and URL-based culture detection.
- Enable `OrchardCore.ContentLocalization.Sitemaps` for localized sitemaps with
  `hreflang` alternates.
- Use `switch_culture_url` and `localization_set` Liquid filters for custom
  culture switchers and lookups.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

## Enabling Content Localization Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.ContentLocalization",
        "OrchardCore.ContentLocalization.ContentCulturePicker"
      ]
    }
  ]
}
```

## Attaching LocalizationPart to a Content Type

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Article", type => type
            .WithPart("LocalizationPart")
        );

        return 1;
    }
}
```

## Attaching LocalizationPart via Recipe

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
              "Draftable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "LocalizationPart",
              "Name": "LocalizationPart"
            },
            {
              "PartName": "TitlePart",
              "Name": "TitlePart"
            }
          ]
        }
      ]
    }
  ]
}
```

Localized content items in a `content` step share the same `LocalizationSet`:

```json
{
  "steps": [
    {
      "name": "content",
      "data": [
        {
          "ContentItemId": "[js:uuid()]",
          "ContentType": "Article",
          "DisplayText": "About",
          "Published": true,
          "TitlePart": { "Title": "About" },
          "LocalizationPart": { "LocalizationSet": "about-set", "Culture": "en-US" }
        },
        {
          "ContentItemId": "[js:uuid()]",
          "ContentType": "Article",
          "DisplayText": "À propos",
          "Published": true,
          "TitlePart": { "Title": "À propos" },
          "LocalizationPart": { "LocalizationSet": "about-set", "Culture": "fr-FR" }
        }
      ]
    }
  ]
}
```

## Localizing Content in Code

`IContentLocalizationManager` clones an item into another culture and reads
localization sets:

```csharp
using OrchardCore.ContentLocalization;
using OrchardCore.ContentManagement;

public sealed class TranslationService
{
    private readonly IContentLocalizationManager _contentLocalizationManager;

    public TranslationService(IContentLocalizationManager contentLocalizationManager)
    {
        _contentLocalizationManager = contentLocalizationManager;
    }

    public Task<ContentItem> CreateFrenchVersionAsync(ContentItem contentItem)
        => _contentLocalizationManager.LocalizeAsync(contentItem, "fr-FR");

    public Task<ContentItem> FindAsync(string localizationSet, string culture)
        => _contentLocalizationManager.GetContentItemAsync(localizationSet, culture);
}
```

Other members: `GetItemsForSetAsync()`, `GetItemsForSetsAsync(sets, culture)`,
`DeduplicateContentItemsAsync()`, and `GetFirstItemIdForSetsAsync()` (current
culture, then default culture, then the first item of each set). In Razor,
`await Orchard.GetContentCultureAsync(contentItem)` returns an item's culture.
Hook into localization with `IContentLocalizationHandler` or
`ContentLocalizationPartHandlerBase<TPart>`.

## Permissions

`OrchardCore.ContentLocalization.ContentLocalizationPermissions` declares
`LocalizeContent` (localize content for others), `LocalizeOwnContent`, and
`ManageContentCulturePicker` (the settings pages). Administrators and Editors
get all three; Authors and Contributors get `LocalizeOwnContent`.

## ContentCulturePicker

With the picker feature enabled:

- `ContentRequestCultureProvider` is added as the first request culture
  provider; it sets the culture from the content item matching the current URL.
- The `ContentCulturePicker` shape is added to the front-end `Navbar` when more
  than one culture is supported. Its model exposes `CurrentCulture` and
  `SupportedCultures` (`CultureInfo`). Override `ContentCulturePicker.cshtml`
  in a theme to restyle it.
- The `RedirectToLocalizedContent` route redirects to the item's localization
  for the selected culture, otherwise (when `RedirectToHomepage` is on) the
  home page localization, otherwise back to the current page.

Settings live under **Settings → Localization → Content Culture Picker**
(`ContentCulturePickerSettings`) and **Content Culture**
(`ContentRequestCultureProviderSettings`):

```json
{
  "steps": [
    {
      "name": "settings",
      "ContentCulturePickerSettings": {
        "SetCookie": true,
        "RedirectToHomepage": false
      },
      "ContentRequestCultureProviderSettings": {
        "SetCookie": true
      }
    }
  ]
}
```

`ContentCulturePickerSettings.SetCookie` (default `true`) writes the
`CookieRequestCultureProvider` cookie when a culture is picked;
`ContentRequestCultureProviderSettings.SetCookie` writes it from the culture
of the content item matching the URL.

### Cookie Lifetime Configuration

```json
{
  "OrchardCore": {
    "ContentLocalization": {
      "CulturePickerOptions": {
        "CookieLifeTime": 14
      }
    }
  }
}
```

`CookieLifeTime` is in days (default `14`) and can also be set per tenant under
`OrchardCore:{TenantName}:ContentLocalization:CulturePickerOptions`.

## Liquid Filters

### `switch_culture_url`

Returns the `RedirectToLocalizedContent` URL for a target culture and the
current path and query string:

```liquid
{{ "fr" | switch_culture_url }}
```

```text
/RedirectToLocalizedContent?targetCulture=fr&contentItemUrl=%2Fblog
```

### `localization_set`

Returns the content item of a localization set in the given culture (the
request culture when omitted). An array of sets returns one item per set.

```liquid
{% assign localizedItem = Model.ContentItem.Content.LocalizationPart.LocalizationSet | localization_set: "en" %}
{{ localizedItem.DisplayText }}

{% assign current = Model.ContentItem.Content.LocalizationPart.LocalizationSet | localization_set %}
```

### Culture Switcher in Liquid

The Liquid `Culture` object lists the site cultures:

```liquid
<ul class="culture-switcher">
  {% for culture in Culture.SupportedCultures %}
    <li class="{% if culture.Name == Culture.Name %}active{% endif %}">
      <a href="{{ culture.Name | switch_culture_url }}">{{ culture.NativeName }}</a>
    </li>
  {% endfor %}
</ul>
```

## Admin, Sitemaps, and GraphQL

- The contents admin list adds a culture filter (`culture:fr-FR` in the search
  box) and shows each item's culture and translations.
- `OrchardCore.ContentLocalization.Sitemaps` replaces the sitemap content items
  query to emit localized URLs with alternates.
- GraphQL exposes `localizationPart { culture localizationSet localizations { contentItemId } }`
  on content types with the part.

## Upgrading from 3.x

- `OrchardCore_ContentLocalization_CulturePickerOptions` →
  `OrchardCore:ContentLocalization:CulturePickerOptions` (the old name still
  binds but is deprecated).
- `OrchardCore.ContentLocalization.Permissions.LocalizeContent`,
  `LocalizeOwnContent`, and `ManageContentCulturePicker` were removed; use the
  same members of `OrchardCore.ContentLocalization.ContentLocalizationPermissions`.
