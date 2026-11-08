---
name: orchardcore-localization
description: Skill for localization and multi-language support in Orchard Core. Covers choosing PO, data or content localization, culture settings and recipes, CultureOptions, request culture providers, the S, H and T localizers, PO contexts and file locations, IStringLocalizerFactory contexts, pluralization with the Plural-Forms header and CLDR rules, translator notes and Arg names, build-time extraction into checked-in POT templates with OrchardCore.Localization.Build, OCLOC diagnostics, validation messages, IJSLocalizer, Liquid t and transliterate filters, and the admin culture picker. Use this skill when requests mention Orchard Core Localization, adding a culture, translating UI strings, PO or POT files, extracting strings for Crowdin, replacing PoExtractor, msgctxt, plural forms, or upgrading localization code to Orchard Core 4. Strong matches include OrchardCore.Localization, OrchardCore.Localization.Build, UpdateLocalizationCatalog, IStringLocalizer, IViewLocalizer, IPluralRuleProvider, and LocalizationSettings.
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

In Liquid, use the `t` filter, and `transliterate` to convert text to ASCII. Named arguments of `t` are format arguments in their order, and their names describe the placeholders to translators; `note` is a translator note, not an argument:

```liquid
{{ "Welcome to our site" | t }}
{{ "Hello {0}!" | t: User.Identity.Name }}
{{ "in {0} years" | t: years: time.years, note: "Relative date shown for future dates." }}
{{ "Ελληνικά" | transliterate }}
```

Keep keys compile-time constants (or Liquid literals) and pass values as format arguments, so the extraction can find every string.

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
- A translation from a PO file picks its form with that file's `Plural-Forms`
  header (gettext C expression over `n`, evaluated with the absolute count), so
  the `msgstr[n]` order always matches the file's own header. An out-of-range
  form selects `msgstr[0]`.
- Without a valid header, and for translations not loaded from PO files (data
  translations, untranslated `Plural()` forms), an `IPluralRuleProvider` rule
  for the culture applies. `DefaultPluralRuleProvider` walks `CultureInfo.Parent`
  (`zh-Hans-CN` → `zh-Hans` → `zh`) and implements the CLDR 49 integer
  cardinal rules (Arabic six forms, Russian three; French, Spanish, Italian and
  Portuguese gain `many` at index 2 for nonzero multiples of 1,000,000).
- Register a custom provider with `services.AddSingleton<IPluralRuleProvider, MyPluralRuleProvider>()`;
  a negative `Order` runs it before the default provider, a positive one makes it a fallback.
- `CultureDictionaryRecord.PluralRule` (set by `PoParser` from the header) and
  `CultureDictionary.GetPluralRule(key)` expose the rule applied to a translation.

```po
msgid ""
msgstr ""
"Language: cs\n"
"Plural-Forms: nplurals=3; plural=(n==1) ? 0 : (n>=2 && n<=4) ? 1 : 2;\n"
```

## Notes and Argument Names for Translators

Notes and argument names are written as comments of the message in the
translation template and are ignored at runtime. Use them only where a
translator could not otherwise understand a message (an ambiguous word such as
"Open", a format pattern, an unclear placeholder):

```csharp
using static OrchardCore.Localization.LocalizationMetadata; // Razor views inherit Arg() from the base page.

H.Plural(note: "Relative date shown for future dates.", count: Arg("years", years), singular: "in 1 year", plural: "in {0} years");
S.Note("Greeting shown on the dashboard.")["Hello {0}", Arg("user name", user.Name)];

// Bounded plural count, written as the gettext range flag (#, csharp-format, range: 0..23).
H.Plural(Arg("hours", time.Hours, 0, 23), "1 hour ago", "{0} hours ago");
```

- `Arg(name, value)` returns `value` unchanged; `Arg(name, count, min, max)`
  returns `count` and only applies to the count of a `Plural` call (constant
  bounds, `0 <= min <= max`). Only declare a range the code guarantees.
- `Note(note)` is an extension on `IStringLocalizer`/`IHtmlLocalizer` (and
  `IViewLocalizer`) that returns the same localizer; the `Plural` extensions
  also have overloads taking `note` first.
- A `// TRANSLATORS:` comment on the line above a C# call is extracted as a note too.
- For `LocalizationSource`, pass `note:` to `Create` (see `orchardcore-localization-source`).

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
(`https://crowdin.com/project/orchard-core`).

If a PO entry with a `msgctxt` has an empty `msgstr`, its context no longer
leaks onto the following entry without `msgctxt`; that entry is now loaded as a
context-free translation, as written.

## Translation Templates (POT) and Extraction

Orchard Core 4.0 replaces PoExtractor with build-time, Roslyn-based extraction.
Each project keeps a checked-in gettext template, `<AssemblyName>.pot`, next to
its project file; a normal build only embeds it as the
`<AssemblyName>.Localization.pot` resource and never extracts. Add the package
to every project whose strings should be extracted (referencing it from the web
project alone does not cover modules):

```xml
<PackageReference Include="OrchardCore.Localization.Build" Version="4.*" PrivateAssets="all" />
```

```powershell
# One project (its references are not updated).
dotnet msbuild src/MyModule/MyModule.csproj -restore -t:UpdateLocalizationCatalog

# An application and its project references, or a whole solution.
dotnet build src/MyApp.Web/MyApp.Web.csproj -p:UpdateLocalizationCatalogs=true

# Copy the templates to one folder for Crowdin (absolute path; does not update them).
dotnet build MySolution.slnx -p:LocalizationCatalogOutputPath="$PWD/artifacts/localization"
```

- Extraction covers C# localizer calls with typed contexts, `LocalizationSource`
  declarations, plural calls, Razor views and pages, literal Liquid `t` inputs
  and constant DataAnnotations `ErrorMessage` values. It does not scan
  JavaScript/TypeScript; expose those strings through a C# `IJSLocalizer`.
- The worker uses the Roslyn and Razor compilers of the SDK driving the build
  (Roslyn 5.9+, SDK 10.0.4xx) and needs Orchard Core's target .NET runtime
  installed; its dependencies are build-only.
- Templates are deterministic (sorted, LF, no timestamps, no line numbers
  unless `LocalizationCatalogLineNumbers=true`); `DEBUG`/`TRACE` code is not
  extracted, and a multi-targeted project uses its first target framework.
  Commit them with the strings they describe, and keep their `msgstr` empty.
- Upload the POT files to the translation platform (preserving `msgctxt`,
  plurals and placeholders), then deploy the translated PO files to a location
  listed above. Embedded POT files do not change runtime translations.
- Properties: `GenerateLocalizationCatalog` (`false` disables embedding and
  updating), `UpdateLocalizationCatalogs`, `LocalizationCatalogPath`,
  `LocalizationStrict` (fail on extraction warnings), `LocalizationCatalogOutputPath`,
  `LocalizationViewPrefix`. Add extra Liquid inputs with
  `<LocalizationLiquid Include="Templates/**/*.liquid" />`.
- Mark helpers that forward caller-supplied keys with
  `[SkipLocalizationExtraction]` (namespace `OrchardCore.Localization`); it is
  not inherited, and callers of the helper are still extracted.

| Diagnostic | Meaning |
|---|---|
| `OCLOC101`-`OCLOC104` (IDE/build analyzers in `OrchardCore.Abstractions`) | `LocalizationSource` without context, foreign context (info), non-constant source value, non-constant localizer key. See `orchardcore-localization-source`. |
| `OCLOC001`-`OCLOC004` | Non-constant key, unresolved context, plural array without exactly two constant forms, empty key (warnings). |
| `OCLOC005` | Same context and key declared with conflicting plural forms (error). |
| `OCLOC006`-`OCLOC009` | Razor compiler, Liquid parse, C# compilation or worker failures. |
| `OCLOC010` / `OCLOC011` | Non-constant note or argument name / invalid `Arg` range; the message is extracted without it. |

Fix dynamic keys at the source instead of suppressing diagnostics; for a key
that is dynamic by design, disable `OCLOC104` around that call with a comment
saying where the values are declared.

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
descriptors use it in Orchard Core 4.0. Reuse `CommonLocalizationSources`
(for example `CommonLocalizationSources.ContentManagement`, PO context
`OrchardCore.Localization.CommonLocalizationSources`) for text several modules
share, and declare the rest in an internal `{Module}LocalizationSources` class.
The full guide (API, notes, analyzers, plurals, PO entries, Data Localization
interaction, migration) is `orchardcore-localization-source`.

```csharp
private static readonly LocalizationSource s_saved =
    LocalizationSource.Create<WidgetService>("Widget {0} saved.");

var text = stringLocalizerFactory.Localize(s_saved, widgetName); // msgctxt "MyModule.WidgetService"
```

## JavaScript Localization

Implement `IJSLocalizer` (register as scoped) to expose PO-backed strings for a
group, then pass `Orchard.GetJSLocalizations("my-group")` serialized as JSON to
the front-end component, which seeds the `@orchardcore/bloom` translations store
with `setTranslations()` and reads it with `getTranslations()`. The keys inside
`GetLocalizations` are ordinary C# localizer calls, so they are extracted into
the module's POT template; JavaScript files themselves are not scanned.

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
- An ordinary build does not refresh POT templates; run the update target and
  commit the result whenever strings or contexts change.
- A plural translation picks the wrong form: check the PO file's
  `Plural-Forms` header and that its `msgstr[n]` order matches it. Missing
  indexes throw `PluralFormNotFoundException`.

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
- PoExtractor is replaced by `OrchardCore.Localization.Build` and checked-in
  `<AssemblyName>.pot` templates. Upload those to Crowdin instead of running
  `extractpo`.
- PO files with a valid `Plural-Forms` header now use it instead of the
  culture's rule. Catalogs without a header follow the CLDR 49 rules, which
  changed several languages: add `many` at index 2 for `fr`, `pt`, `ca`, `es`,
  `gl`, `it`; `two` for `he`, `sat`, `se` (index 2) and `mt` (index 4); split
  single-form `fa`, `vi`, `ka`, `kk`, `ky`, `ug`, `cgg` catalogs; `jv` and `yo` now
  use one form; review zero for `hi`, `bn`, `gu`, `tr`, `uz` and others; negative
  counts use the positive magnitude. Or add a matching `Plural-Forms` header to keep a catalog's order.
- PO contexts of built-in deployment step categories/titles and workflow
  activity categories moved to shared `*LocalizationSources` classes; update the
  `msgctxt` of PO entries that translated them (see `orchardcore-localization-source`).
