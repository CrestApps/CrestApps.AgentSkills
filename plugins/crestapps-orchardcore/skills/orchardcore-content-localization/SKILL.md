---
name: orchardcore-content-localization
description: Skill for configuring content localization in Orchard Core. Covers LocalizationPart for multi-language content, ContentCulturePicker for culture selection, culture cookie configuration, Liquid localization filters (switch_culture_url, localization_set), and localization set management.
---

# Orchard Core Content Localization - Prompt Templates

## Configure Content Localization

You are an Orchard Core expert. Generate code and configuration for content localization including LocalizationPart, ContentCulturePicker, culture cookies, Liquid filters, and localization set management.

### Guidelines

- Enable `OrchardCore.ContentLocalization` to manage multiple localized versions of content items.
- Attach `LocalizationPart` to any content type that needs multi-language support.
- Enable `OrchardCore.ContentLocalization.ContentCulturePicker` to let users switch cultures on the frontend.
- The `ContentCulturePicker` redirects to the localized version of the current content item, the homepage localization, or stays on the current page.
- `ContentRequestCultureProvider` is added as the first culture provider when the picker feature is enabled, setting the thread culture based on the matching content item URL.
- Cookie behavior is configurable: the picker can set a `CookieRequestCultureProvider` cookie and/or `ContentRequestCultureProvider` can set a cookie based on the current URL.
- Configure cookie lifetime via `OrchardCore_ContentLocalization_CulturePickerOptions:CookieLifeTime` in `appsettings.json`.
- Use `switch_culture_url` Liquid filter to generate culture-switching URLs.
- Use `localization_set` Liquid filter to retrieve the content item for a specific culture from a localization set.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Enabling Content Localization Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.ContentLocalization",
        "OrchardCore.ContentLocalization.ContentCulturePicker"
      ],
      "disable": []
    }
  ]
}
```

### Attaching LocalizationPart to a Content Type

Attach `LocalizationPart` to a content type so it can be translated into multiple cultures:

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;

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

### Attaching LocalizationPart via Recipe

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

### ContentCulturePicker Cookie Settings via Recipe

Configure whether the culture picker sets a cookie and whether `ContentRequestCultureProvider` sets a cookie based on the current URL:

```json
{
  "steps": [
    {
      "name": "settings",
      "ContentCulturePickerSettings": {
        "SetCookie": true
      },
      "ContentRequestCultureProvider": {
        "SetCookie": true
      }
    }
  ]
}
```

### Cookie Lifetime Configuration

Configure the culture picker cookie lifetime (in days) via `appsettings.json`:

```json
{
  "OrchardCore": {
    "OrchardCore_ContentLocalization_CulturePickerOptions": {
      "CookieLifeTime": 14
    }
  }
}
```

### ContentCulturePicker Redirect Rules

The `ContentCulturePicker` determines the redirect URL using these rules in order:

1. If the current content item has a related localization for the selected culture, redirect to that item.
2. Otherwise, if a HomePage is configured, find and redirect to its localization for the selected culture.
3. Otherwise, redirect to the current page.

### Liquid Filters

#### `switch_culture_url`

Returns the URL of the action that switches the current culture. Use this to build culture selector links:

```liquid
{{ Model.Culture.Name | switch_culture_url }}
```

Output example:

```text
/Loc1/RedirectToLocalizedContent?targetculture=fr&contentItemUrl=%2Fblog
```

Build a culture switcher in Liquid:

```liquid
{% for culture in Model.Cultures %}
  <a href="{{ culture.Name | switch_culture_url }}">{{ culture.DisplayName }}</a>
{% endfor %}
```

#### `localization_set`

Returns the content item in the specified culture from a localization set. Defaults to the current request culture if no culture argument is provided:

```liquid
{% assign localizedItem = Model.ContentItem.Content.LocalizationPart.LocalizationSet | localization_set: "en" %}
{{ localizedItem.DisplayText }}
```

Use without a culture argument to get the item in the current request culture:

```liquid
{% assign localizedItem = Model.ContentItem.Content.LocalizationPart.LocalizationSet | localization_set %}
{{ localizedItem.DisplayText }}
```

### Rendering Culture Picker Widget

Add the culture picker shape to your layout or template:

```liquid
{% shape "ContentCulturePicker" %}
```

### Complete Culture Switcher Example

Build a full culture switcher dropdown in a Liquid template:

```liquid
<div class="culture-switcher">
  <span>{{ "Language" | t }}:</span>
  <ul>
    {% for culture in Model.Cultures %}
      <li>
        <a href="{{ culture.Name | switch_culture_url }}"
           class="{% if culture.Name == Model.CurrentCulture.Name %}active{% endif %}">
          {{ culture.DisplayName }}
        </a>
      </li>
    {% endfor %}
  </ul>
</div>
```
