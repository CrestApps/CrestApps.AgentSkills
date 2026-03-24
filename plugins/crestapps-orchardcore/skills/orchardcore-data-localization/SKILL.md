---
name: orchardcore-data-localization
description: Skill for managing translation data in Orchard Core. Covers the translation editor UI, translation statistics dashboard, permissions hierarchy, built-in translation providers, custom translation provider implementation, recipe steps for translations, API reference, and Liquid translation filters.
---

# Orchard Core Data Localization - Prompt Templates

## Configure and Manage Data Localization

You are an Orchard Core expert. Generate code and configuration for data localization including the translation editor, statistics dashboard, permissions, custom providers, recipe import/export, API usage, and Liquid filters.

### Guidelines

- Enable `OrchardCore.DataLocalization` for database-backed localization of dynamic content (content type names, permission descriptions, custom strings).
- Use Data Localization for dynamic strings that cannot be handled by static PO files.
- Use PO files (`OrchardCore.Localization`) for static UI strings in views and code.
- Use `OrchardCore.ContentLocalization` for localizing user-defined content items (not the same as data localization).
- Built-in providers cover content type display names, content field display names, and permission descriptions.
- Implement `ILocalizationDataProvider` to expose custom translatable strings.
- Localization keys must be human-readable display values, not technical identifiers, because untranslated keys are displayed as-is.
- Use `IDataLocalizer` (aliased as `D`) in Razor views to render translated dynamic strings.
- Use the `d` Liquid filter to localize dynamic data strings in Liquid templates.
- Translations are cached automatically; cache is invalidated when translations are updated through the admin UI.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling Data Localization

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.DataLocalization"
      ],
      "disable": []
    }
  ]
}
```

### Permissions

| Permission | Description |
|------------|-------------|
| `ViewTranslations` | View translations and statistics (read-only access) |
| `ManageTranslations` | Edit translations for all cultures |
| `ManageTranslations_{culture}` | Edit translations for a specific culture (e.g., `ManageTranslations_fr-FR`) |

#### Permission Hierarchy

```
ViewTranslations                      (Read-only access)
ManageTranslations                    (Full edit access - implies ViewTranslations)
├── ManageTranslations_fr-FR          (Edit French - implies ViewTranslations)
├── ManageTranslations_es-ES          (Edit Spanish - implies ViewTranslations)
└── ManageTranslations_{culture}      (Dynamically generated per supported culture)
```

Default role assignments:

- **Administrator**: `ManageTranslations` (full access)
- **Editor**: `ViewTranslations` (read-only)

### Built-in Data Providers

| Provider | Context | Strings Provided |
|----------|---------|-----------------|
| Content Type Provider | `Content Types` | Display names of all content types |
| Content Field Provider | `Content Fields` | Display names of all content fields |
| Permissions Provider | `Permissions` | Descriptions of all non-template permissions |

### Creating a Custom Localization Data Provider

Implement `ILocalizationDataProvider` to add your own translatable strings:

```csharp
using OrchardCore.Localization.Data;

public sealed class MenuItemLocalizationDataProvider : ILocalizationDataProvider
{
    public Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var strings = new List<DataLocalizedString>
        {
            new DataLocalizedString("Menu Items", "Dashboard", string.Empty),
            new DataLocalizedString("Menu Items", "User Settings", string.Empty),
            new DataLocalizedString("Menu Items", "Reports", string.Empty),
        };

        return Task.FromResult<IEnumerable<DataLocalizedString>>(strings);
    }
}
```

Register in your module's `Startup.cs`:

```csharp
using OrchardCore.Localization.Data;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, MenuItemLocalizationDataProvider>();
    }
}
```

### Localization Key Best Practices

Keys must be human-readable display values because untranslated keys are displayed as-is to users:

| Key Type | Example Key | Displayed if Untranslated | Result |
|----------|-------------|---------------------------|--------|
| ✅ Display value | `"Welcome Message"` | `Welcome Message` | Good |
| ❌ Technical ID | `"welcome_msg_001"` | `welcome_msg_001` | Bad |
| ❌ Code constant | `"CONTENT_TYPE_ARTICLE"` | `CONTENT_TYPE_ARTICLE` | Bad |

Use the context parameter to disambiguate keys that might conflict across categories.

### Import Translations via Recipe

```json
{
  "steps": [
    {
      "name": "Translations",
      "Translations": {
        "fr-FR": [
          { "Context": "Permissions", "Key": "Manage HTTPS", "Value": "Gérer HTTPS" },
          { "Context": "Content Types", "Key": "Article", "Value": "Article" }
        ],
        "es-ES": [
          { "Context": "Permissions", "Key": "Manage HTTPS", "Value": "Gestionar HTTPS" }
        ]
      }
    }
  ]
}
```

### Using IDataLocalizer in Razor Views

Inject `IDataLocalizer` to display translated dynamic strings:

```cshtml
@using OrchardCore.Localization.Data
@inject IDataLocalizer D

<h1>@D["Article", "Content Types"]</h1>
<p>@D["Manage HTTPS", "Permissions"]</p>
```

The output is automatically HTML-encoded by Razor, making it safe against XSS. Translated values should contain plain text only.

#### IDataLocalizer with Arguments

```cshtml
@using OrchardCore.Localization.Data
@inject IDataLocalizer D

<p>@D["Hello {0}", "My Context", userName]</p>
```

### Comparison: IStringLocalizer vs IDataLocalizer

| Feature | `IStringLocalizer` (T) | `IDataLocalizer` (D) |
|---------|------------------------|----------------------|
| Source | PO files (static) | Database (dynamic) |
| HTML encoding | Auto-encoded by Razor | Auto-encoded by Razor |
| Pluralization | Supported | Not supported |
| Arguments | `T["Hello {0}", name]` | `D["Hello {0}", "Context", name]` |
| Use case | Static UI strings | Dynamic data (content types, permissions) |

### Liquid Filter: `d`

Localizes a dynamic data string using the current culture:

```liquid
{{ "Blog" | d: "Content Types" }}
```

For `ar` culture, it returns `مدونة`. For unsupported cultures or missing translations, it returns `Blog.Content Types`.

### When to Use Each Localization Approach

| Use Case | Recommended Approach |
|----------|---------------------|
| Static UI strings in views/code | PO files (`OrchardCore.Localization`) |
| Content type/field display names | Data Localization (`OrchardCore.DataLocalization`) |
| Permission descriptions | Data Localization (`OrchardCore.DataLocalization`) |
| Admin menu items | Data Localization with custom provider |
| User-defined content | Content Localization (`OrchardCore.ContentLocalization`) |
| Database-stored dynamic strings | Data Localization with custom provider |

### API Reference

#### ILocalizationDataProvider

```csharp
public interface ILocalizationDataProvider
{
    Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync();
}
```

#### DataLocalizedString

```csharp
public class DataLocalizedString
{
    public DataLocalizedString(string context, string name, string value);

    public string Context { get; }  // Category/group name
    public string Name { get; }     // Original string (key)
    public string Value { get; }    // Translated value
}
```

#### ITranslationsManager

```csharp
public interface ITranslationsManager
{
    Task<TranslationsDocument> LoadTranslationsDocumentAsync();
    Task<TranslationsDocument> GetTranslationsDocumentAsync();
    Task RemoveTranslationAsync(string name);
    Task UpdateTranslationAsync(string name, IEnumerable<Translation> translations);
}
```

### Translation Editor Admin UI

Navigate to **Settings → Localization → Dynamic Translations** to use the translation editor:

- **Culture Selector**: Choose which culture to edit translations for.
- **Search**: Filter strings by original text or translation.
- **Category Filter**: Focus on a specific category (e.g., Content Types, Permissions).
- **Auto-save**: Enabled by default, saves after 2 seconds of inactivity.
- **Save Button**: Manually save all changes.

### Statistics Dashboard

The statistics dashboard at **Settings → Localization → Dynamic Translations → Statistics** shows:

- **Overall Progress**: Total translation progress across all cultures.
- **By Culture**: Progress bar and completion count for each supported culture.
- **By Category**: Detailed breakdown per category for a selected culture.

Progress bars are color-coded: 🟢 Green (≥75%), 🟡 Yellow (25–74%), 🔴 Red (<25%).

### Deployment Step

Use the **All Data Translations** deployment step to export all translations for backup or transfer between environments.
