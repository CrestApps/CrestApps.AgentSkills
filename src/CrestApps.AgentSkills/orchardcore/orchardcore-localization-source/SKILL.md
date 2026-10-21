---
name: orchardcore-localization-source
description: Skill for deferred localization with LocalizationSource in Orchard Core 4.0. Covers Create(value, type), Create of T, Value and Type, the IStringLocalizerFactory and IHtmlLocalizerFactory Localize and Plural extensions with format arguments, why deferred sources exist (PO context kept until display time, static readonly labels, extractor discovery), PO entries with msgctxt, the Orchard Core APIs that use it (Permission Description, DeploymentStep Category and Title, data source, file format, pipeline step and shared link descriptors), the Data Localization interaction, and migrating 3.x code. Use this skill when requests mention Orchard Core LocalizationSource, deferring localization, localizing static labels, translating permission descriptions or deployment step titles, or upgrading such code to Orchard Core 4. Strong matches include OrchardCore.Localization, LocalizationSource, StringLocalizerFactoryExtensions, Permission.Description, and DeploymentStep.Title.
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
- Declare sources with `LocalizationSource.Create<T>(value)` when `T` can be a generic argument, and `LocalizationSource.Create(value, typeof(T))` for static classes or runtime types. Prefer these factories over `new LocalizationSource(...)` or target-typed `new(...)`, so source-extraction tools can find them.
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
- An explicit `Create(...)` call gives extraction tools a recognizable declaration. Support for it depends on the external extractor (for example `OrchardCoreContrib.PoExtractor`); runtime PO keys and contexts are unchanged.

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
- `Plural()` always inserts `count` as `{0}`; your arguments follow as `{1}`, `{2}`, and so on. For a context-bearing source it delegates to the localizer's `Plural()`, so the culture's plural rule (from `IPluralRuleProvider`) picks the form, including zero forms and more than two forms.
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

### Where Orchard Core 4.0 Uses LocalizationSource

| API | Declared as | Translated by |
|---|---|---|
| `Permission.Description` | Constructor argument; built-in permissions use `LocalizationSource.Create(description, typeof(DeclaringType))` | Roles editor, admin menu link and placeholder node permission pickers, menu item permission picker |
| `DeploymentStep.Category`, `DeploymentStep.Title` | `static readonly` sources assigned in the step's parameterless constructor; `[JsonIgnore]`, never persisted | Deployment plan page (step picker groups) and the step add and edit screens |
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
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace MyModule.Deployment;

public sealed class WidgetsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource s_category = LocalizationSource.Create<WidgetsDeploymentStep>("Content");
    private static readonly LocalizationSource s_title = LocalizationSource.Create<WidgetsDeploymentStep>("Widgets");

    public WidgetsDeploymentStep()
    {
        Name = "Widgets";
        Category = s_category;
        Title = s_title;
    }
}
```

The PO context is `MyModule.Deployment.WidgetsDeploymentStep`, the same context a typed `IStringLocalizer<WidgetsDeploymentStep>` used, so existing PO translations keep working.

### Data Localization Interaction

- A permission description with a `Type` is translated only with PO files. `PermissionsLocalizationDataProvider` no longer lists it in Data Localization, and translations of it entered there are ignored.
- A context-free description is still listed in Data Localization (**Settings → Localization → Dynamic Translations**) and translated there. That covers `new Permission(name, "plain string")`, which wraps the string in a source without a `Type`, and the permissions built from templates such as "Edit Article". The templates themselves (values containing `{0}`) are not listed.
- Permission categories are plain strings and stay in Data Localization.
- To move a description from Data Localization to PO files, give it a `Type` and add a PO entry with the matching `msgctxt`. See `orchardcore-data-localization`.

### Upgrading from 3.x

- Permission descriptions:
  - Before: `new Permission("ManageWidgets", "Manage widgets")`. After: `new Permission("ManageWidgets", LocalizationSource.Create("Manage widgets", typeof(WidgetPermissions)))`.
  - Before: `permission.Description` was a `string`. After: read `permission.Description.Value` for the original text, or `stringLocalizerFactory.Localize(permission.Description)` for the translation.
  - Before: `string.Format(template.Description, typeName)`. After: `string.Format(template.Description.Value, typeName)` and keep the template context-free.
- Deployment steps:
  - Before: a constructor taking `IStringLocalizer<TStep>` assigned `Category = S["Content"]` and `Title = S["Widgets"]` as `LocalizedString`. After: assign `static readonly` sources in the parameterless constructor; the constructors that took a localizer were removed from the built-in steps.
  - Before: views rendered `step.Title` directly. After: render `StringLocalizerFactory.Localize(step.Title)`.
- Static labels: replace `LocalizedString` values cached in static fields or singletons (which captured one culture) with `LocalizationSource` values translated per request.
- Modules compiled against 3.x must be recompiled; the property types changed.

### Troubleshooting

| Symptom | Check |
|---|---|
| A permission description stays in English with a PO entry | The source has no `Type`, or `msgctxt` does not equal the type's full name. |
| A permission translation from Data Localization stopped working | The built-in description now has a `Type`; translate it with a PO file. |
| Placeholders appear unformatted | Pass the arguments to `Localize()`/`Plural()`, not to `Create()`. |
| Plural form is wrong in a language with several forms | The source has no `Type`, so the English rule applied. |
| HTML tags appear escaped | Use `IHtmlLocalizerFactory.Localize()` for trusted markup. |
