---
name: orchardcore-localization
description: Skill for configuring localization and multi-language support in Orchard Core. Covers culture settings, the Localization CultureOptions configuration section, PO file contexts and discovery locations, IStringLocalizer, IHtmlLocalizer and view localizers, IStringLocalizerFactory contexts, deferred LocalizationSource with the Localize and Plural factory extensions, pluralization, HTML encoding of localized text, IJSLocalizer, the Liquid t and transliterate filters, the admin culture picker, and LocalizationPermissions. Use this skill when requests mention Orchard Core Localization, Configure Localization and Multi-Language Support, Enabling Localization Features, Localization Settings via Recipe, PO File Format, PO File Location Convention, LocalizationSource, deferring localization, translating permission descriptions, PoExtractor, upgrading localization code to Orchard Core 4, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Localization

Use `OrchardCore.Localization` for UI localization with PO files,
`OrchardCore.DataLocalization` for database-backed dynamic strings (see
`orchardcore-data-localization`), and `OrchardCore.ContentLocalization` for
translated content items (see `orchardcore-content-localization`).

| Feature | Purpose |
|---|---|
| `OrchardCore.Localization` | PO localization, culture settings (**Settings → Localization → Cultures**), request localization. |
| `OrchardCore.Localization.ContentLanguageHeader` | Adds the `Content-Language` response header. |
| `OrchardCore.Localization.AdminCulturePicker` | Admin navbar culture picker (admin culture cookie), shown when more than one culture is supported. |

## Culture Settings

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Localization",
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

The cultures page requires `OrchardCore.Localization.LocalizationPermissions.ManageCultures`.
The **Culture settings** deployment step exports `LocalizationSettings`. Read
the configured cultures through `ILocalizationService`
(`GetDefaultCultureAsync()`, `GetSupportedCulturesAsync()`,
`GetAllCulturesAndAliases()`).

Host-level culture behavior is configured in `OrchardCore:Localization:CultureOptions`:

```json
{
  "OrchardCore": {
    "Localization": {
      "CultureOptions": {
        "IgnoreSystemSettings": true
      }
    }
  }
}
```

`IgnoreSystemSettings` stops the OS user's custom date and number formats from
affecting cultures. Do not invent other localization configuration sections.

## UI Strings

Use the localizer typed to the class that owns the string, and `IHtmlLocalizer`
only for trusted translation markup:

```csharp
using Microsoft.Extensions.Localization;

namespace MyModule.Services;

public sealed class GreetingService
{
    internal readonly IStringLocalizer S;

    public GreetingService(IStringLocalizer<GreetingService> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Greet(string name, int count)
        => S["Hello {0}!", name] + " " + S.Plural(count, "{0} new message", "{0} new messages");
}
```

`Plural()` requires `OrchardCore.Localization.Abstractions`; `count` replaces
`{0}` and the culture's PO plural rule picks the form. Never hard-code a number
in the singular or plural text.

In Razor views, `T` is the view localizer and returns `LocalizedHtmlString`.
Write `@T["..."]` directly. When plain text is needed (an attribute set in code,
a list of strings, a `SelectListItem`), use `T.GetString("...")`, not
`T["..."].Value`, which double-encodes entities and drops the format arguments.

In Liquid, use the `t` filter, and `transliterate` to convert text to ASCII:

```liquid
{{ "Welcome to our site" | t }}
{{ "Hello {0}!" | t: User.Identity.Name }}
{{ "Ελληνικά" | transliterate }}
```

## PO Contexts and Locations

The PO `msgctxt` must exactly match the localizer scope:

- `{Namespace}.{Class}` for `IStringLocalizer<T>` (inner class `+` becomes `.`)
- `{Module}.Views.{ViewPath}` for a Razor view, e.g. `TheAdmin.Views.Layout`

```po
msgctxt "MyModule.Services.GreetingService"
msgid "Hello {0}!"
msgstr "Bonjour {0} !"

msgctxt "MyModule.Views.Home.Index"
msgid "Read more"
msgstr "Lire la suite"
```

To use another context, inject `IStringLocalizerFactory`:

```csharp
S = factory.Create(typeof(SharedMessages));            // msgctxt "MyModule.SharedMessages"
_shared = factory.Create("MySharedContext", string.Empty); // msgctxt "MySharedContext"
```

The PO localizer factory caches the localizers it creates, so resolving one per
lookup is cheap.

For `fr-CA`, PO files are loaded from these locations in order (the culture
name can also be the neutral `fr`, used for `fr-CA` when falling back to parent
cultures):

1. `{Extension}/Localization/fr-CA.po`
2. `/Localization/fr-CA.po`
3. `App_Data/Sites/{tenant}/Localization/fr-CA.po`
4. `/Localization/{ExtensionId}/fr-CA.po`
5. `/Localization/{ExtensionId}-fr-CA.po`
6. `/Localization/fr-CA/{ExtensionId}.po`, then every `.po` in `/Localization/fr-CA/`

Restart the application after editing PO files. Include the web project's
files in publish output:

```xml
<ItemGroup>
  <Content Include="Localization\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

Generate PO templates with `dotnet tool install --global OrchardCoreContrib.PoExtractor`
and `extractpo <INPUT_PATH> <OUTPUT_PATH>`.

## Deferring Localization with LocalizationSource

When a string is declared where no localizer or culture is available (static
fields, permissions, deployment step labels, descriptors), declare a
`LocalizationSource` (`OrchardCore.Localization`, in `OrchardCore.Abstractions`)
and translate it at display time:

```csharp
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;

namespace MyModule;

public static class MyMessages
{
    public static readonly LocalizationSource Welcome =
        LocalizationSource.Create("Welcome {0}", typeof(MyMessages));
}

public sealed class WelcomeRenderer
{
    private static readonly LocalizationSource s_item =
        LocalizationSource.Create<WelcomeRenderer>("{0} item for {1}");

    private readonly IStringLocalizerFactory _stringLocalizerFactory;
    private readonly IHtmlLocalizerFactory _htmlLocalizerFactory;

    public WelcomeRenderer(
        IStringLocalizerFactory stringLocalizerFactory,
        IHtmlLocalizerFactory htmlLocalizerFactory)
    {
        _stringLocalizerFactory = stringLocalizerFactory;
        _htmlLocalizerFactory = htmlLocalizerFactory;
    }

    public LocalizedString Text(string userName)
        => _stringLocalizerFactory.Localize(MyMessages.Welcome, userName);

    public LocalizedHtmlString Html(string userName)
        => _htmlLocalizerFactory.Localize(MyMessages.Welcome, userName);

    public LocalizedString Items(int count, string owner)
        => _stringLocalizerFactory.Plural(count, s_item, "{0} items for {1}", owner);
}
```

- `Value` is the PO `msgid`; `Type` supplies the `msgctxt`
  (`MyModule.MyMessages`). Use `Create<T>(value)` when `T` can be a generic
  argument and `Create(value, type)` for static classes or runtime types.
  Prefer these factories over `new(...)` so extraction tools can find them.
- The source never translates, formats, or encodes; pass format arguments to
  `Localize()`/`Plural()`. `Plural()` prepends `count` as `{0}`.
- A source without a `Type` has no PO context: the value is returned (formatted)
  and `Plural()` falls back to the English rule. A `null` source returns `null`.
- Use HTML localization only for trusted markup; use string localization for
  labels, permission descriptions, and attributes.

`Permission.Description`, `DeploymentStep.Category`, and `DeploymentStep.Title`
are `LocalizationSource` values; translate them with
`IStringLocalizerFactory.Localize()`. Built-in permission descriptions are
translated with PO files, not Data Localization.

## JavaScript Localization

Implement `IJSLocalizer` (register as scoped) to expose PO-backed strings for a
group, then pass `Orchard.GetJSLocalizations("my-group")` serialized as JSON to
the front-end component, which seeds the `@orchardcore/bloom` translations store
with `setTranslations()` and reads it with `getTranslations()`.

## Content and Admin Culture Pickers

`LocalizationPart` links translations with `LocalizationSet` and stores each
item's `Culture`. The front-end content culture picker is a separate
content-localization feature. `OrchardCore.Localization.AdminCulturePicker`
only affects the admin area and uses the admin culture cookie provider.

## Upgrading from 3.x

- `OrchardCore_Localization_CultureOptions` → `OrchardCore:Localization:CultureOptions`
  (the old name still binds but is deprecated).
- `OrchardCore.Localization.Permissions.ManageCultures` was removed; use
  `OrchardCore.Localization.LocalizationPermissions.ManageCultures`.
- `Permission.Description` is a `LocalizationSource` instead of a `string`:
  create descriptions with `LocalizationSource.Create(value, typeof(DeclaringType))`,
  display them with `IStringLocalizerFactory.Localize(permission.Description)`,
  and read `Description.Value` for the original text. Recompile modules.
- `DeploymentStep.Category`/`Title` are `LocalizationSource`; assign static
  sources in the parameterless constructor instead of resolving a localizer.
- `IDataResourceStringProvider` was removed; use `ILocalizationDataProvider`.
