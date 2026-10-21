---
name: orchardcore-localization
description: Skill for configuring localization and multi-language support in Orchard Core. Covers choosing between PO, data and content localization, culture settings and the LocalizationSettings recipe, the Localization CultureOptions configuration section, request culture providers, the S, H and T localizer conventions, PO file contexts and discovery locations, IStringLocalizerFactory custom contexts, pluralization and IPluralRuleProvider, validation message translation, a short LocalizationSource guide, IJSLocalizer, Liquid t and transliterate filters, the admin culture picker, and PoExtractor. Use this skill when requests mention Orchard Core Localization, adding a culture, translating UI strings, PO files, msgctxt, plural forms, translating validation messages, upgrading localization code to Orchard Core 4, or related setup or troubleshooting work. Strong matches include OrchardCore.Localization, IStringLocalizer, IViewLocalizer, IPluralRuleProvider, ILocalizationService, and LocalizationSettings.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Localization

## Choose the Layer

| Goal | Use |
|---|---|
| Translate a label or message in C#, Razor or Liquid | PO localization (`OrchardCore.Localization`) with `S`, `H` or `T` and a PO file |
| Count-dependent text ("1 item" / "{0} items") | `Plural()` and a PO `msgid_plural` entry |
| Text declared where no localizer or culture exists (static fields, permissions, deployment steps, descriptors) | `LocalizationSource`, see `orchardcore-localization-source` |
| Translate data-driven values (content type names, context-free permission descriptions, custom dynamic strings) | Data Localization (`OrchardCore.DataLocalization`), see `orchardcore-data-localization` |
| Translate content items (a blog post in French) | Content Localization (`LocalizationPart`), see `orchardcore-content-localization` |
| Add a supported culture or change the default one | Culture settings below |

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

| Property | Description |
|---|---|
| `DefaultCulture` | Default request culture, for example `en-US`. |
| `SupportedCultures` | Cultures the site accepts, used for both culture and UI culture. A culture must be listed before content can be translated into it. |
| `FallBackToParentCulture` | Fall back from `fr-CA` to `fr` when looking up translations and cultures. |

The module applies these settings with `app.UseRequestLocalization()` when the
tenant starts. The cultures page requires
`OrchardCore.Localization.LocalizationPermissions.ManageCultures`. The
**Culture settings** deployment step exports `LocalizationSettings`. Read the
configured cultures through `ILocalizationService`
(`GetDefaultCultureAsync()`, `GetSupportedCulturesAsync()`,
`GetAllCulturesAndAliases()`, `FallBackToParentCultures`).

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

### Request Culture Providers

| Provider | Picks the culture from |
|---|---|
| `ContentRequestCultureProvider` (Content Localization) | The culture of the localized content item matched by the route; added first. |
| `AdminCookieCultureProvider` (Admin Culture Picker) | The `admin_culture_{tenant VersionId}` cookie, on admin paths only. |
| ASP.NET Core defaults | Query string, cookie, then `Accept-Language`. |

## UI Strings

| Symbol | Type | Use in | Output |
|---|---|---|---|
| `S` | `IStringLocalizer<T>` field | Services, drivers, handlers, controllers | `LocalizedString`, encoded by Razor when rendered |
| `H` | `IHtmlLocalizer<T>` field | Messages whose translation contains trusted markup | `LocalizedHtmlString`, markup kept, arguments encoded |
| `T` | `IViewLocalizer` property | Razor views (already on the Orchard Core `RazorPage`) | `LocalizedHtmlString` |

Inject the localizer typed to the class that owns the string, and name the
field `S` (or `H`) so extraction tools find the calls:

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

In Razor views write `@T["..."]` directly. When plain text is needed (an
attribute set in code, a list of strings, a `SelectListItem`), use
`T.GetString("...")`, not `T["..."].Value`, which double-encodes entities and
drops the format arguments.

In Liquid, use the `t` filter, and `transliterate` to convert text to ASCII:

```liquid
{{ "Welcome to our site" | t }}
{{ "Hello {0}!" | t: User.Identity.Name }}
{{ "Ελληνικά" | transliterate }}
```

## Pluralization

`Plural()` is an extension from `OrchardCore.Localization.Abstractions` on
`IStringLocalizer`, `IHtmlLocalizer` and `IViewLocalizer`:

```csharp
S.Plural(count, "{0} book", "{0} books");
H.Plural(count, "{0} <b>new</b> book", "{0} <b>new</b> books");
S.Plural(count, ["{0} book on {1}", "{0} books on {1}"], shelfName); // forms array; shelfName is {1}
```

- `count` replaces `{0}`; extra arguments are `{1}`, `{2}`, and so on.
- The singular text is the PO `msgid`, the plural text the `msgid_plural`.
- Never hard-code a number in the singular or plural text; languages differ
  in when each form applies (French uses the singular for 0 and 1).
- The form is chosen by an `IPluralRuleProvider` for the culture's neutral
  language. `DefaultPluralRuleProvider` ships CLDR-based rules for many
  languages (Arabic has six forms, Russian three). The PO `Plural-Forms`
  header is not read, so add every `msgstr[n]` the language's rule produces.
- Register a custom provider with `services.AddSingleton<IPluralRuleProvider, MyPluralRuleProvider>()`;
  a negative `Order` runs it before the default provider, a positive one makes it a fallback.

## PO Contexts and Locations

The PO `msgctxt` must exactly match the localizer scope, or the `msgid` is
shown untranslated:

- `{Namespace}.{Class}` for `IStringLocalizer<T>` and `IHtmlLocalizer<T>`
  (the type's full name; an inner class `+` becomes `.`)
- the view path up to the module or theme folder for a Razor view, e.g.
  `TheAdmin.Views.Layout` or `MyModule.Views.Home.Index`

```po
msgctxt "MyModule.Services.GreetingService"
msgid "Hello {0}!"
msgstr "Bonjour {0} !"

msgctxt "MyModule.Views.Home.Index"
msgid "Read more"
msgstr "Lire la suite"
```

To use another context, inject `IStringLocalizerFactory` (or
`IHtmlLocalizerFactory`):

```csharp
S = factory.Create(typeof(SharedMessages));            // msgctxt "MyModule.SharedMessages"
_shared = factory.Create("MySharedContext", string.Empty); // msgctxt "MySharedContext"
```

The PO localizer factory caches the localizers it creates, so resolving one per
lookup is cheap.

For `fr-CA`, `ModularPoFileLocationProvider` loads PO files from these
locations in order (the culture name can also be the neutral `fr`, used for
`fr-CA` when falling back to parent cultures):

1. `{Extension}/Localization/fr-CA.po` for each module and theme, in extension order
2. `/Localization/fr-CA.po` in the web project
3. `App_Data/Sites/{tenant}/Localization/fr-CA.po`
4. For each extension, overrides for modules consumed as NuGet packages: `/Localization/{ExtensionId}/fr-CA.po`, `/Localization/{ExtensionId}-fr-CA.po`, `/Localization/fr-CA/{ExtensionId}.po`
5. Every `.po` file in `/Localization/fr-CA/`

- Restart the application after editing PO files.
- Prefer `/Localization/` over `App_Data` in Docker: mounting a volume at
  `App_Data` hides the files that were in the image.
- PO files in a module's `Localization` folder are embedded in the module
  assembly automatically. Include the web project's files in publish output:

```xml
<ItemGroup>
  <Content Include="Localization\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

Community translations of Orchard Core itself are on Crowdin
(`https://crowdin.com/project/orchard-core`). Generate PO templates for your
own code with the `OrchardCoreContrib.PoExtractor` tool:

```bash
dotnet tool install --global OrchardCoreContrib.PoExtractor
extractpo <INPUT_PATH> <OUTPUT_PATH> [-l|--language {"C#"|"VB"}] [-t|--template {"razor"|"liquid"}]
```

## Validation Messages

The Localization module registers `AddDataAnnotationsPortableObjectLocalization()`,
so `ValidationAttribute` error messages are translated with PO files. All of
them share the `OrchardCore.Localization.DataAnnotations.LocalizedDataAnnotationsMvcOptions`
context. The lookup tries the formatted message (with the `[Display(Name)]` or
property name), then the message with that name replaced by `{0}`:

```po
msgctxt "OrchardCore.Localization.DataAnnotations.LocalizedDataAnnotationsMvcOptions"
msgid "The {0} field is required."
msgstr "Le champ {0} est obligatoire."
```

## Deferring Localization with LocalizationSource

When a string is declared where no localizer or culture is available, declare
a `LocalizationSource` and translate it at display time. `Permission.Description`,
`DeploymentStep.Category`/`Title`, and the Data Sources and Data Pipelines
descriptors use it in Orchard Core 4.0. The full guide (API, plurals, PO
entries, Data Localization interaction, migration) is `orchardcore-localization-source`.

```csharp
private static readonly LocalizationSource s_saved =
    LocalizationSource.Create<WidgetService>("Widget {0} saved.");

var text = stringLocalizerFactory.Localize(s_saved, widgetName); // msgctxt "MyModule.WidgetService"
```

## JavaScript Localization

Implement `IJSLocalizer` (register as scoped) to expose PO-backed strings for a
group, then pass `Orchard.GetJSLocalizations("my-group")` serialized as JSON to
the front-end component, which seeds the `@orchardcore/bloom` translations store
with `setTranslations()` and reads it with `getTranslations()`.

## Content and Admin Culture Pickers

`LocalizationPart` links translations with `LocalizationSet` and stores each
item's `Culture`; `IContentLocalizationManager.LocalizeAsync(item, culture)`
creates a translation. The front-end content culture picker, the
`localization_set` and `switch_culture_url` Liquid filters, and the
`ContentCulturePickerSettings` recipe settings belong to Content Localization
(see `orchardcore-content-localization`).
`OrchardCore.Localization.AdminCulturePicker` only affects the admin area and
uses the admin culture cookie provider.

## Gotchas

- A wrong `msgctxt` is the most common cause of an untranslated string; check
  the namespace and class of the localizer's type, or the view path.
- Inject `IStringLocalizer<T>` typed to the class that owns the strings, not a
  base class or a shared type, unless you deliberately want that context.
- Pass dynamic values as format arguments; never concatenate or interpolate
  them into the `msgid`.
- Use `H`/`IHtmlLocalizer` only for trusted translation markup.
- Do not cache `LocalizedString` values in static fields or singletons; they
  capture one culture. Use `LocalizationSource` instead.

## Upgrading from 3.x

- `OrchardCore_Localization_CultureOptions` → `OrchardCore:Localization:CultureOptions`
  (the old name still binds but is deprecated).
- `OrchardCore.Localization.Permissions.ManageCultures` was removed; use
  `OrchardCore.Localization.LocalizationPermissions.ManageCultures`.
- `Permission.Description` is a `LocalizationSource` instead of a `string`, and
  `DeploymentStep.Category`/`Title` are `LocalizationSource` instead of
  `LocalizedString`. Built-in permission descriptions are now translated with
  PO files instead of Data Localization. See `orchardcore-localization-source`
  for the before/after code. Recompile modules.
- `IDataResourceStringProvider` was removed; use `ILocalizationDataProvider`.
