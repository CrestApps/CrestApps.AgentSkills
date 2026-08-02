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
