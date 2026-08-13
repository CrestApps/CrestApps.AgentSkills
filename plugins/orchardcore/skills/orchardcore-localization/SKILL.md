---
name: orchardcore-localization
description: Skill for configuring localization and multi-language support in Orchard Core. Covers culture settings, content localization, PO file contexts and discovery locations, IStringLocalizer, the Liquid t filter, and the admin culture picker. Use this skill when requests mention Orchard Core Localization, Configure Localization and Multi-Language Support, Enabling Localization Features, Localization Settings via Recipe, PO File Format, PO File Location Convention, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Localization

Use `OrchardCore.Localization` for UI localization and
`OrchardCore.ContentLocalization` for translated content items. Configure the
default and supported cultures in the site settings UI or with a root recipe:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Localization",
        "OrchardCore.ContentLocalization",
        "OrchardCore.Localization.AdminCulturePicker"
      ]
    },
    {
      "name": "Settings",
      "LocalizationSettings": {
        "DefaultCulture": "en-US",
        "SupportedCultures": ["en-US", "fr-FR"],
        "FallBackToParentCulture": true
      }
    }
  ]
}
```

The localization module uses these settings to configure request
localization. Do not invent an `OrchardCore_Localization_CultureProvider`
configuration section.

In Orchard Core 3.0, `ILocalizationService` exposes culture lookup through
an instance service method. Do not call a static default implementation.
`PoParser` is static; use `PoParser.Parse` or `PoParser.ParseAsync` for PO
file parsing.

## UI Strings

Use the localizer typed to the class that owns the string:

```csharp
private readonly IStringLocalizer<MyController> _localizer;

public MyController(IStringLocalizer<MyController> localizer)
{
    _localizer = localizer;
}
```

In Liquid, use the `t` filter:

```liquid
{{ "Welcome to our site" | t }}
{{ "Hello {0}!" | t: User.Identity.Name }}
```

## PO Contexts and Locations

The PO `msgctxt` must exactly match the localizer scope:

- `{Namespace}.{Class}` for `IStringLocalizer<T>`
- `{Namespace}.{ViewPath}` for a Razor view

```po
msgctxt "MyModule.Controllers.HomeController"
msgid "Welcome to our site"
msgstr "Bienvenue sur notre site"

msgctxt "MyModule.Views.Home.Index"
msgid "Read more"
msgstr "Lire la suite"
```

For `fr-CA`, the provider checks these locations in order:

1. `{Extension}/Localization/fr-CA.po`
2. `/Localization/fr-CA.po`
3. `App_Data/Sites/{tenant}/Localization/fr-CA.po`
4. `/Localization/{ExtensionId}/fr-CA.po`
5. `/Localization/{ExtensionId}-fr-CA.po`
6. `/Localization/fr-CA/{ExtensionId}.po`, then each `.po` in that culture folder

## Content and Admin Culture Pickers

`LocalizationPart` links translations with `LocalizationSet` and stores each
item's `Culture`. The content culture picker is a separate content-localization
feature. `OrchardCore.Localization.AdminCulturePicker` instead adds a navbar
shape in the admin area when more than one supported culture is configured; it
uses the admin culture cookie provider and does not render a front-end picker.
