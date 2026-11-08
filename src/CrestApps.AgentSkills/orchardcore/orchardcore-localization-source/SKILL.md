---
name: orchardcore-localization-source
description: Skill for deferred localization with LocalizationSource in Orchard Core 4.0. Covers Create(value, type), Create of T, translator notes, the IStringLocalizerFactory and IHtmlLocalizerFactory Localize and Plural extensions with Arg names, why deferred sources exist, CommonLocalizationSources and per-module LocalizationSources classes, the OCLOC101 to OCLOC104 analyzers, PO entries with msgctxt, the APIs that use it (Permission Description, PermissionTemplate, DeploymentStep Category and Title, data pipeline descriptors), the Data Localization interaction, and migrating 3.x code. Use this skill when requests mention LocalizationSource, deferring localization, localizing static labels, translating permission descriptions or deployment step titles, or upgrading such code to Orchard Core 4. Strong matches include OrchardCore.Localization, LocalizationSource, CommonLocalizationSources, StringLocalizerFactoryExtensions, SkipLocalizationExtraction, OCLOC101, Permission.Description, and DeploymentStep.Title.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Deferred Localization with LocalizationSource - Prompt Templates

## Declare Now, Translate at Display Time

You are an Orchard Core expert. Generate code that declares translatable text where no localizer or culture is available and translates it when it is shown.

### Guidelines

- `LocalizationSource` (namespace `OrchardCore.Localization`, package `OrchardCore.Abstractions`) is an immutable `sealed record` holding an untranslated `Value` and an optional source `Type`. It never translates, formats, or encodes anything.
- Declare sources with `LocalizationSource.Create<T>(value)` when `T` can be a generic argument, and `LocalizationSource.Create(value, typeof(T))` for static classes or runtime types. Prefer these factories over `new LocalizationSource(...)` or target-typed `new(...)`, so the build-time extraction (`OrchardCore.Localization.Build`, see `orchardcore-localization`) finds them.
- Reuse a shared source before declaring a new one: `CommonLocalizationSources` for text shared by several modules, otherwise an `internal static class {Module}LocalizationSources` at the module root (see below).
- Add `note: "..."` to `Create` only when translators could not otherwise understand the text; name format arguments with `Arg(name, value)` where the source is formatted.
- `Value` is the PO `msgid`. `Type.FullName` is the PO `msgctxt` (an inner class `+` becomes `.`), exactly like `IStringLocalizer<T>`.
- Keep the value a literal. Never build it with string interpolation or concatenation; pass dynamic parts as format arguments at display time.
- Store sources in `static readonly` fields (or static get-only properties). They are safe to share across instances, tenants, and cultures.
- Translate with `IStringLocalizerFactory.Localize()` for text and attributes, and `IHtmlLocalizerFactory.Localize()` only for trusted translation markup. The display call site picks the encoding, not the declaration.
- Do not use a generic type as `Type`; its `FullName` does not make a usable PO context.
- All C# classes must use the `sealed` modifier, except View Models.

### Why Deferred Sources Exist

- Permissions, deployment step labels, and descriptors are created in static fields, constructors, or singletons, before a tenant, request, or culture is known. Resolving a localizer there either translates with the wrong culture or cannot be done at all.
- A plain `string` loses the PO context, so it could only be translated through Data Localization. A source keeps both the key and the context until display time, so PO files translate it.
- A `static readonly` source is allocated once, instead of resolving a typed localizer and allocating a `LocalizedString` per instance or per tenant.
- An explicit `Create(...)` call with a constant value and a context is extracted into the project's `.pot` translation template by `OrchardCore.Localization.Build`. The analyzers shipped with `OrchardCore.Abstractions` flag declarations that cannot be extracted (see below).

### Declaring and Translating

```csharp
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;

namespace MyModule.Services;

public static class OrderMessages
{
    // Static classes cannot be generic arguments, so use typeof().
    public static readonly LocalizationSource Shipped =
        LocalizationSource.Create("Order {0} was shipped to {1}.", typeof(OrderMessages));

    public static readonly LocalizationSource Banner =
        LocalizationSource.Create("<strong>Free shipping</strong> on orders over {0}", typeof(OrderMessages));
}

public sealed class OrderNotifier
{
    private static readonly LocalizationSource s_pendingItems =
        LocalizationSource.Create<OrderNotifier>("{0} item is waiting in order {1}");

    private readonly IStringLocalizerFactory _stringLocalizerFactory;
    private readonly IHtmlLocalizerFactory _htmlLocalizerFactory;

    public OrderNotifier(
        IStringLocalizerFactory stringLocalizerFactory,
        IHtmlLocalizerFactory htmlLocalizerFactory)
    {
        _stringLocalizerFactory = stringLocalizerFactory;
        _htmlLocalizerFactory = htmlLocalizerFactory;
    }

    public LocalizedString ShippedText(string orderNumber, string city)
        => _stringLocalizerFactory.Localize(OrderMessages.Shipped, orderNumber, city);

    public LocalizedHtmlString BannerHtml(decimal threshold)
        => _htmlLocalizerFactory.Localize(OrderMessages.Banner, threshold.ToString("C"));

    public LocalizedString PendingItems(int count, string orderNumber)
        => _stringLocalizerFactory.Plural(count, s_pendingItems, "{0} items are waiting in order {1}", orderNumber);
}
```

| Extension | Namespace and package | Returns |
|---|---|---|
| `IStringLocalizerFactory.Localize(source, params object[] arguments)` | `Microsoft.Extensions.Localization`, `OrchardCore.Abstractions` | `LocalizedString` |
| `IHtmlLocalizerFactory.Localize(source, params object[] arguments)` | `Microsoft.AspNetCore.Mvc.Localization`, `OrchardCore.Abstractions` | `LocalizedHtmlString` |
| `IStringLocalizerFactory.Plural(count, source, plural, params object[] arguments)` | `Microsoft.Extensions.Localization`, `OrchardCore.Localization.Abstractions` | `LocalizedString` |
| `IHtmlLocalizerFactory.Plural(count, source, plural, params object[] arguments)` | `Microsoft.AspNetCore.Mvc.Localization`, `OrchardCore.Localization.Abstractions` | `LocalizedHtmlString` |

Behavior:

- A source with a `Type` resolves `factory.Create(source.Type)` (the PO localizer factory caches it) and looks up `Value`, formatted with the arguments.
- A source without a `Type` (for example `LocalizationSource.Create("Hello")`) has no PO context. `Localize()` returns `Value`, formatted with any arguments; `Plural()` uses the English rule (singular only when `count == 1`).
- A `null` source returns `null` without resolving a localizer, so `factory.Localize(source)?.Value ?? fallback` is the safe pattern. A `null` `Value` throws `ArgumentNullException` when the source is created.
- `Plural()` always inserts `count` as `{0}`; your arguments follow as `{1}`, `{2}`, and so on. For a context-bearing source it delegates to the localizer's `Plural()`, so the plural rule picks the form (the `Plural-Forms` header of the PO file that supplied the translation, otherwise the culture's `IPluralRuleProvider` rule), including zero forms and more than two forms.
- `LocalizedString` is HTML encoded by Razor when rendered. `LocalizedHtmlString` keeps the markup of the translation and encodes the arguments; escape literal braces as `{{` and `}}` in HTML sources.

### PO File Entries

For the sources above (`MyModule.Services` namespace):

```po
msgctxt "MyModule.Services.OrderMessages"
msgid "Order {0} was shipped to {1}."
msgstr "La commande {0} a été expédiée à {1}."

msgctxt "MyModule.Services.OrderNotifier"
msgid "{0} item is waiting in order {1}"
msgid_plural "{0} items are waiting in order {1}"
msgstr[0] "{0} article attend dans la commande {1}"
msgstr[1] "{0} articles attendent dans la commande {1}"
```

The singular `Value` is the `msgid`; the `plural` argument is the `msgid_plural`. PO file locations are covered by `orchardcore-localization`.

### Displaying in Razor

```cshtml
@using Microsoft.Extensions.Localization
@inject IStringLocalizerFactory StringLocalizerFactory

<span class="badge text-bg-secondary">@StringLocalizerFactory.Localize(Model.Step.Category)</span>
<strong>@(StringLocalizerFactory.Localize(Model.Step.Title)?.Value ?? Model.Step.Name)</strong>
```

### Sharing Sources

A source is immutable and culture-independent, so declare it once and reuse it wherever the text has the same meaning; it is then translated with a single PO entry.

- `CommonLocalizationSources` (`OrchardCore.Abstractions`, namespace `OrchardCore.Localization`) declares text shared by several modules or general to all of them: `Configuration`, `Content`, `ContentManagement`, `Development`, `Infrastructure`, `Search`, `Security`, `Settings`. Its PO context is `OrchardCore.Localization.CommonLocalizationSources`.
- Declare the module's other sources in an `internal static class` at the module root named after the module, for example `LuceneLocalizationSources` in `OrchardCore.Lucene`. Its PO context is the class's full name.
- Keep permission descriptions in their `*Permissions` class, and text shown by a single view in the view with `T`.

```csharp
using OrchardCore.Localization;

namespace MyModule;

internal static class MyModuleLocalizationSources
{
    public static readonly LocalizationSource ExportMyData = LocalizationSource.Create("Export My Data", typeof(MyModuleLocalizationSources));
}
```

```po
msgctxt "OrchardCore.Localization.CommonLocalizationSources"
msgid "Content Management"
msgstr "Gestion du contenu"

msgctxt "MyModule.MyModuleLocalizationSources"
msgid "Export My Data"
msgstr "Exporter mes données"
```

### Translator Notes and Argument Names

A source takes a note for translators where it is declared; the note is written to the translation template as a `#.` comment, is not stored in the source, and does not change its value or equality. Name format arguments with `Arg(name, value)` (static `OrchardCore.Localization.LocalizationMetadata`, returns its value unchanged) where the source is formatted. `Arg(name, count, min, max)` also declares the range of a plural count, written as the gettext `range` flag.

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using static OrchardCore.Localization.LocalizationMetadata;

public static class DashboardMessages
{
    public static readonly LocalizationSource Greeting = LocalizationSource.Create(
        "Hello {0}",
        typeof(DashboardMessages),
        note: "Greeting shown on the dashboard.");

    public static readonly LocalizationSource DayLeft =
        LocalizationSource.Create("{0} day left", typeof(DashboardMessages));
}

// At display time:
var text = stringLocalizerFactory.Localize(DashboardMessages.Greeting, Arg("user name", user.Name));
var days = stringLocalizerFactory.Plural(Arg("days", count, 1, 7), DashboardMessages.DayLeft, "{0} days left");
```

The overloads are `Create(value, type, note)`, `Create<T>(value, note)` and the matching constructor `LocalizationSource(value, type, note)`. Notes, names and range bounds must be compile-time constants to be extracted.

### Analyzer Rules

`OrchardCore.Abstractions` ships Roslyn analyzers, with a code fix, for every project referencing it:

| Rule | Severity | Reported when |
|---|---|---|
| `OCLOC101` | Warning | A constant value has no context, for example `LocalizationSource.Create("Manage widgets")`. The code fix adds the containing type (`Create<T>`, or `typeof(T)` for a static class). |
| `OCLOC102` | Info | The context is not the declaring type or one of its containing types. Fine when strings deliberately share a context. |
| `OCLOC103` | Warning | The value is not a compile-time constant, so it cannot be extracted. |
| `OCLOC104` | Warning | A localizer indexer, `GetString`, `GetHtml` or `Plural` key is not a compile-time constant. |

Members and types marked `[SkipLocalizationExtraction]` (namespace `OrchardCore.Localization`) are not analyzed or extracted; reserve it for helpers that forward keys declared elsewhere. Suppress a rule around an intentionally dynamic call with `#pragma warning disable OCLOC104` and a comment saying where the keys come from, or set its severity in `.editorconfig` (`dotnet_diagnostic.OCLOC102.severity = none`). A context-free source whose value is not a constant is not reported, since it forwards a value declared elsewhere.

### Where Orchard Core 4.0 Uses LocalizationSource

| API | Declared as | Translated by |
|---|---|---|
| `Permission.Description` | Constructor argument; built-in permissions use `LocalizationSource.Create(description, typeof(DeclaringType))` | Roles editor, admin menu link and placeholder node permission pickers, menu item permission picker |
| `DeploymentStep.Category`, `DeploymentStep.Title` | Read-only virtual properties overridden to return shared sources (`CommonLocalizationSources` or the module's `*LocalizationSources` class); `[JsonIgnore]` on each override, never persisted | Deployment plan page (step picker groups) and the step add and edit screens |
| `PermissionTemplate.Description` | A source with `{0}`, formatted into a context-free description by `CreateDynamicPermission(...)` | Roles editor (through Data Localization) |
| `IDataSource.DisplayName`, `Description` (and `DataSourceDescriptor`) | `static abstract` interface properties, for example `public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<MyDataSource>("Orders");` | Data source pickers |
| `IDataFileFormat.DisplayName` (and `DataFileFormatDescriptor`) | Same pattern | File format pickers |
| `IDataPipelineStepType.DisplayName`, `Description` (and `DataPipelineStepTypeDescriptor`) | Same pattern | Data Pipelines designer and run views |
| `ISharedLinkHandler.DisplayName` (and `SharedLinkHandlerDescriptor`) | Same pattern | Shared download links admin |

See `orchardcore-permission-providers`, `orchardcore-deployments`, `orchardcore-data-sources`, `orchardcore-data-pipeline-steps` and `orchardcore-data-pipelines` for the rest of those APIs.

### Permission Descriptions

```csharp
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace MyModule;

public static class WidgetPermissions
{
    public static readonly Permission ManageWidgets = new(
        "ManageWidgets",
        LocalizationSource.Create("Manage widgets", typeof(WidgetPermissions)),
        isSecurityCritical: true);
}
```

```po
msgctxt "MyModule.WidgetPermissions"
msgid "Manage widgets"
msgstr "Gérer les widgets"
```

### Deployment Step Labels

```csharp
using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace MyModule.Deployment;

public sealed class WidgetsDeploymentStep : DeploymentStep
{
    public override string Name => "Widgets";

    // [JsonIgnore] is not inherited by overrides, and a LocalizationSource can't be serialized.
    [JsonIgnore]
    public override LocalizationSource Category => CommonLocalizationSources.Content;

    [JsonIgnore]
    public override LocalizationSource Title => MyModuleLocalizationSources.Widgets;
}
```

`MyModuleLocalizationSources.Widgets` is declared like `ExportMyData` in **Sharing Sources** above.

The category is translated with `msgctxt "OrchardCore.Localization.CommonLocalizationSources"` and the title with `msgctxt "MyModule.MyModuleLocalizationSources"`. Translations keyed by the old step-type context (`MyModule.Deployment.WidgetsDeploymentStep`) must be moved to the new contexts.

### Data Localization Interaction

- A permission description with a `Type` is translated only with PO files. `PermissionsLocalizationDataProvider` no longer lists it in Data Localization, and translations of it entered there are ignored.
- A context-free description is still listed in Data Localization (**Settings → Localization → Dynamic Translations**) and translated there. That covers the obsolete `new Permission(name, "plain string")`, which wraps the string in a source without a `Type`, and the permissions created by a `PermissionTemplate`, such as "Edit Article" (`CreateDynamicPermission` formats the template into a context-free source). Values still containing `{0}` are not listed.
- Permission categories are plain strings and stay in Data Localization.
- To move a description from Data Localization to PO files, give it a `Type` and add a PO entry with the matching `msgctxt`. See `orchardcore-data-localization`.

### Upgrading from 3.x

- Permission descriptions:
  - Before: `new Permission("ManageWidgets", "Manage widgets")`. After: `new Permission("ManageWidgets", LocalizationSource.Create("Manage widgets", typeof(WidgetPermissions)))`.
  - Before: `permission.Description` was a `string`. After: read `permission.Description.Value` for the original text, or `stringLocalizerFactory.Localize(permission.Description)` for the translation.
  - The `Permission` constructors taking a `string` description are `[Obsolete]`.
  - Before: a template `Permission` formatted with `string.Format(template.Description, typeName)`. After: declare a `PermissionTemplate` and call `template.CreateDynamicPermission(name, displayName)` (see `orchardcore-permission-providers`).
- Deployment steps:
  - Before: a constructor taking `IStringLocalizer<TStep>` assigned `Name`, `Category = S["Content"]` and `Title = S["Widgets"]`. After: override `Name`, `Category` and `Title` (read-only virtual) to return shared sources, with `[JsonIgnore]` on the two labels; the constructors that took a localizer were removed from the built-in steps.
  - The built-in step labels changed PO context: shared categories use `OrchardCore.Localization.CommonLocalizationSources`, titles the module's `*LocalizationSources` class (for example `OrchardCore.Lucene.LuceneLocalizationSources`) instead of the step type. Workflow activity categories moved the same way to `OrchardCore.Workflows.Activities.ActivityCategoryLocalizationSources`. Update the `msgctxt` of affected PO entries; entries without `msgctxt` still apply.
  - Before: views rendered `step.Title` directly. After: render `StringLocalizerFactory.Localize(step.Title)`.
- Static labels: replace `LocalizedString` values cached in static fields or singletons (which captured one culture) with `LocalizationSource` values translated per request.
- Replace `PoExtractor` runs with `OrchardCore.Localization.Build` checked-in templates (see `orchardcore-localization`), and fix the `OCLOC1xx` warnings it surfaces.
- Modules compiled against 3.x must be recompiled; the property types changed.

### Troubleshooting

| Symptom | Check |
|---|---|
| A permission description stays in English with a PO entry | The source has no `Type`, or `msgctxt` does not equal the type's full name. |
| A permission translation from Data Localization stopped working | The built-in description now has a `Type`; translate it with a PO file. |
| Placeholders appear unformatted | Pass the arguments to `Localize()`/`Plural()`, not to `Create()`. |
| Saving a deployment plan throws a serialization error | A `Category` or `Title` override lacks `[JsonIgnore]`. |
| A deployment step category or title lost its translation after upgrading | Its PO entry still uses the step-type `msgctxt`; use `OrchardCore.Localization.CommonLocalizationSources` or the module's `*LocalizationSources` class. |
| `OCLOC101` on a declaration | Add the context: `Create<T>(value)` or `Create(value, typeof(T))`. |
| Plural form is wrong in a language with several forms | The source has no `Type`, so the English rule applied. |
| HTML tags appear escaped | Use `IHtmlLocalizerFactory.Localize()` for trusted markup. |
