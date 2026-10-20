# Localization Examples

## PO File

`Localization/fr-CA.po` in an extension:

```po
msgctxt "MyModule.Services.NotificationService"
msgid "Notification sent"
msgstr "Notification envoyée"

msgctxt "MyModule.Views.Shared._Pager"
msgid "One item"
msgid_plural "{0} items"
msgstr[0] "Un élément"
msgstr[1] "{0} éléments"
```

The `msgctxt` values must match the localizer's namespace and class or view
path exactly.

## Liquid

```liquid
<h1>{{ "Welcome to our site" | t }}</h1>
<p>{{ "Published on {0}" | t: Model.ContentItem.PublishedUtc }}</p>
```

## Content Translation

```csharp
var translated = await _contentLocalizationManager.LocalizeAsync(contentItem, "fr-CA");
```

## Permission Description Translated with PO Files

```csharp
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace MyModule;

public static class WidgetPermissions
{
    public static readonly Permission ManageWidgets = new(
        "ManageWidgets",
        LocalizationSource.Create("Manage widgets", typeof(WidgetPermissions)));
}
```

```po
msgctxt "MyModule.WidgetPermissions"
msgid "Manage widgets"
msgstr "Gérer les widgets"
```

Display it with `stringLocalizerFactory.Localize(permission.Description)`.

## Plain Text from a View Localizer

```cshtml
@{
    var tooltip = T.GetString("Toggle {0}", Model.Title);
}
<button type="button" title="@tooltip">@T["Toggle {0}", Model.Title]</button>
```

`T.GetString()` returns translated, formatted plain text that Razor encodes
once; `T["..."].Value` would be the unformatted HTML template.

## Context-free Shared Localizer

```csharp
public sealed class ReportLabels
{
    private readonly IStringLocalizer _labels;

    public ReportLabels(IStringLocalizerFactory factory)
    {
        _labels = factory.Create("Reports", string.Empty);
    }

    public string Total => _labels["Total"];
}
```

```po
msgctxt "Reports"
msgid "Total"
msgstr "Total général"
```
