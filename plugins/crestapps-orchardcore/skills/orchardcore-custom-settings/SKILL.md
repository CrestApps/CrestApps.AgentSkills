---
name: orchardcore-custom-settings
description: Skill for creating custom site settings in Orchard Core using the CustomSettings stereotype. Covers creating settings content types, defining permissions, accessing settings via ISiteService, Liquid property access, and recipe-based configuration of custom settings.
---

# Orchard Core Custom Settings - Prompt Templates

## Create Custom Site Settings

You are an Orchard Core expert. Generate code and configuration for creating custom site-wide settings sections using the `CustomSettings` stereotype.

### Guidelines

- Enable the `OrchardCore.CustomSettings` feature to use custom site settings.
- Custom settings are organized in sections, each represented by a content type with the `CustomSettings` stereotype.
- When creating a custom settings content type, disable `Creatable`, `Listable`, `Draftable`, and `Securable` metadata — they do not apply.
- Do **not** mark existing content types with the `CustomSettings` stereotype; this will break existing content items of that type.
- Custom settings sections appear in the _Settings_ menu alongside module-provided settings.
- Each section gets a dedicated permission under the `OrchardCore.CustomSettings` feature group in the Roles editor.
- Access custom settings in code via `ISiteService.GetCustomSettingsAsync("TypeName")`, which returns a `ContentItem`.
- Access custom settings in Liquid via `{{ Site.Properties.TypeName.PartName }}`.
- Custom settings are stored as named JSON properties inside the site document.
- All C# classes must use the `sealed` modifier, except View Models.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.

### Enabling Custom Settings

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.CustomSettings"
      ],
      "disable": []
    }
  ]
}
```

### Creating a Custom Settings Type via Code

#### Step 1: Define the Content Part

```csharp
public sealed class {{SettingsPartName}} : ContentPart
{
    public string {{PropertyName}} { get; set; }

    public bool {{BoolPropertyName}} { get; set; }
}
```

#### Step 2: Register the Content Type with CustomSettings Stereotype

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("{{SettingsTypeName}}", type => type
            .DisplayedAs("{{Settings Display Name}}")
            .Stereotype("CustomSettings")
            .WithPart("{{SettingsPartName}}", part => part
                .WithPosition("0")
            )
        );

        return 1;
    }
}
```

The `CustomSettings` stereotype tells Orchard Core this content type represents a site settings section rather than a regular content item.

#### Step 3: Register Services in Startup

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<{{SettingsPartName}}>();
    }
}
```

### Accessing Custom Settings in Code

Use `ISiteService.GetCustomSettingsAsync()` to retrieve a custom settings section as a `ContentItem`:

```csharp
public sealed class MyService
{
    private readonly ISiteService _siteService;

    public MyService(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public async Task<{{SettingsPartName}}> GetSettingsAsync()
    {
        ContentItem settingsItem = await _siteService.GetCustomSettingsAsync("{{SettingsTypeName}}");

        return settingsItem.As<{{SettingsPartName}}>();
    }
}
```

Alternatively, access settings without the extension method:

```csharp
var siteSettings = await _siteService.GetSiteSettingsAsync();
var settingsItem = siteSettings.As<ContentItem>("{{SettingsTypeName}}");
```

### Accessing Custom Settings in Controllers

```csharp
public sealed class MyController : Controller
{
    private readonly ISiteService _siteService;

    public MyController(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public async Task<IActionResult> Index()
    {
        ContentItem settingsItem = await _siteService.GetCustomSettingsAsync("{{SettingsTypeName}}");
        var settings = settingsItem.As<{{SettingsPartName}}>();

        return View(settings);
    }
}
```

### Accessing Custom Settings in Liquid

Custom settings are available via the `Site.Properties` object. Each section is accessible by its content type name:

```liquid
{{ Site.Properties.{{SettingsTypeName}}.{{SettingsPartName}}.{{PropertyName}} }}
```

For example, accessing an `HtmlBodyPart` on a `BlogSettings` section:

```liquid
{{ Site.Properties.BlogSettings.HtmlBodyPart.Html | raw }}
```

### Permissions

Each custom settings section automatically receives a dedicated permission. To manage access:

1. Navigate to _Security → Roles_ in the admin dashboard.
2. Edit the desired role.
3. Under the `OrchardCore.CustomSettings` feature group, check the permission for the settings section.

### Custom Settings with Content Fields

Custom settings parts can include content fields for richer configuration:

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("BrandingSettings", type => type
            .DisplayedAs("Branding Settings")
            .Stereotype("CustomSettings")
            .WithPart("BrandingSettings", part => part
                .WithPosition("0")
            )
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync("BrandingSettings", part => part
            .WithField("Logo", field => field
                .OfType("MediaField")
                .WithDisplayName("Site Logo")
                .WithPosition("0")
            )
            .WithField("FooterText", field => field
                .OfType("HtmlField")
                .WithDisplayName("Footer Text")
                .WithPosition("1")
                .WithEditor("Wysiwyg")
            )
            .WithField("SocialLink", field => field
                .OfType("LinkField")
                .WithDisplayName("Social Media Link")
                .WithPosition("2")
            )
        );

        return 1;
    }
}
```

When using content fields, the fields are rendered automatically by content field display drivers — no manual handling in the display driver is needed.

### Configuring Custom Settings via Recipe

Use the `custom-settings` recipe step to set values for a custom settings section:

```json
{
  "steps": [
    {
      "name": "custom-settings",
      "{{SettingsTypeName}}": {
        "ContentItemId": "[js:uuid()]",
        "ContentType": "{{SettingsTypeName}}",
        "{{SettingsPartName}}": {
          "{{PropertyName}}": "{{value}}",
          "{{BoolPropertyName}}": true
        }
      }
    }
  ]
}
```

### Creating Custom Settings Content Type via Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "{{SettingsTypeName}}",
          "DisplayName": "{{Settings Display Name}}",
          "Settings": {
            "ContentTypeSettings": {
              "Stereotype": "CustomSettings"
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "{{SettingsPartName}}",
              "Name": "{{SettingsPartName}}",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "0"
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

### Important Notes

- Use `GetCustomSettingsAsync()` for read-only access (returns cached instance).
- Custom settings content types must not be confused with the `DisplayDriver<ISite>` pattern used for module-specific site settings; `CustomSettings` is a content-based approach managed entirely through the content type system.
- Content types with `CustomSettings` stereotype should not have `Creatable`, `Listable`, `Draftable`, or `Securable` enabled.
