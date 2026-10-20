---
name: orchardcore-custom-settings
description: Skill for creating admin-defined custom site settings in Orchard Core using the CustomSettings stereotype. Covers settings content types in code or recipes, the per-section ManageCustomSettings permission, GetCustomSettingsAsync, Liquid access through Site.Properties and the PermittedSiteProperties allow list, rendering stored HTML safely with sanitize_html, the custom-settings recipe step, the Custom Settings deployment step, and when to use a code-defined SiteDisplayDriver instead. Use this skill when requests mention Orchard Core Custom Settings, Create Custom Site Settings, Accessing Custom Settings in Code or Liquid, Site.Properties is empty, or closely related Orchard Core implementation, upgrade, or troubleshooting work. Strong matches include OrchardCore.CustomSettings, CustomSettings stereotype, ISiteService, GetCustomSettingsAsync, ContentPart, DataMigration, IContentDefinitionManager, SettingsLiquidOptions, PermittedSiteProperties.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Custom Settings - Prompt Templates

## Create Custom Site Settings

You are an Orchard Core expert. Generate code and configuration for creating site-wide settings sections using the `CustomSettings` stereotype.

### Guidelines

- Enable the `OrchardCore.CustomSettings` feature to use custom site settings.
- Each custom settings section is a content type with the `CustomSettings` stereotype, made of parts and fields like any other content type.
- For this stereotype the type editor hides the `Creatable`, `Listable`, `Draftable`, and `Versionable` options; also leave `Securable` off, since each section gets its own permission.
- Do **not** mark an existing content type with the `CustomSettings` stereotype; this breaks existing content items of that type.
- Sections appear under **Settings** in the admin, next to module-provided settings.
- Each section gets a `ManageCustomSettings_{TypeName}` permission under the `OrchardCore.CustomSettings` group of the Roles editor; it has no default role stereotype.
- The section is stored as a `ContentItem` named after the type in the site document `Properties`.
- Read it in code with `ISiteService.GetCustomSettingsAsync("TypeName")` (from `OrchardCore.Contents.Core`).
- In Orchard Core 4.0, `{{ Site.Properties }}` is redacted in Liquid. List each section in `OrchardCore:Liquid:PermittedSiteProperties` before reading it from Liquid.
- `HtmlBodyPart`, `HtmlField`, `MarkdownBodyPart`, and `MarkdownField` no longer execute Liquid; use `LiquidPart` or `LiquidField` for templated content.
- When the settings are owned by a module (fixed schema, validation, secrets, typed options), prefer a code-defined `SiteDisplayDriver<TSettings>`; see `orchardcore-site-settings`.
- All C# classes must use the `sealed` modifier, except view models.
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

#### Step 2: Register the Content Type with the CustomSettings Stereotype

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

#### Step 3: Register Services in Startup

```csharp
[RequireFeatures("OrchardCore.CustomSettings")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<{{SettingsPartName}}>()
            .UseDisplayDriver<{{SettingsPartName}}DisplayDriver>();

        services.AddDataMigration<Migrations>();
    }
}
```

Registering a typed part only supplies its CLR model. Add a display driver when its CLR properties need an editor:

```csharp
public sealed class {{SettingsPartName}}DisplayDriver : ContentPartDisplayDriver<{{SettingsPartName}}>
{
    public override IDisplayResult Edit({{SettingsPartName}} part, BuildPartEditorContext context)
        => Initialize<{{SettingsPartName}}ViewModel, {{SettingsPartName}}>("{{SettingsPartName}}_Edit", static (model, part) =>
        {
            model.{{PropertyName}} = part.{{PropertyName}};
            model.{{BoolPropertyName}} = part.{{BoolPropertyName}};
        }, part);

    public override async Task<IDisplayResult> UpdateAsync({{SettingsPartName}} part, UpdatePartEditorContext context)
    {
        var model = new {{SettingsPartName}}ViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.{{PropertyName}} = model.{{PropertyName}};
        part.{{BoolPropertyName}} = model.{{BoolPropertyName}};

        return Edit(part, context);
    }
}

public class {{SettingsPartName}}ViewModel
{
    public string {{PropertyName}} { get; set; }

    public bool {{BoolPropertyName}} { get; set; }
}
```

Content fields attached to the part (Media, Html, Link, ...) are rendered by their own drivers; no extra driver code is needed for them.

### Accessing Custom Settings in Code

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

        return settingsItem.GetOrCreate<{{SettingsPartName}}>();
    }
}
```

Without the extension method:

```csharp
var siteSettings = await _siteService.GetSiteSettingsAsync();
var settingsItem = siteSettings.GetOrCreate<ContentItem>("{{SettingsTypeName}}");
```

`GetCustomSettingsAsync()` returns the cached instance; do not modify it. To change a section in code, use `LoadSiteSettingsAsync()`, update `site.Properties["{{SettingsTypeName}}"]`, and call `UpdateSiteSettingsAsync()`.

Custom settings types can be edited in the admin by anyone with the `EditContentTypes` permission, so treat their schema as part of your deployment and avoid relying on parts that administrators can remove.

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
        var settingsItem = await _siteService.GetCustomSettingsAsync("{{SettingsTypeName}}");
        var settings = settingsItem.GetOrCreate<{{SettingsPartName}}>();

        return View(settings);
    }
}
```

### Accessing Custom Settings in Liquid

First allow the section names (application or tenant configuration):

```json
{
  "OrchardCore": {
    "Liquid": {
      "PermittedSiteProperties": [
        "{{SettingsTypeName}}",
        "BlogSettings"
      ]
    }
  }
}
```

Then read them by type name and part name:

```liquid
{{ Site.Properties.{{SettingsTypeName}}.{{SettingsPartName}}.{{PropertyName}} }}
```

HTML stored in a custom settings section is persisted exactly as authored and is only sanitized when rendered through its display shape. When outputting raw HTML from Liquid, sanitize it explicitly:

```liquid
{{ Site.Properties.BlogSettings.HtmlBodyPart.Html | sanitize_html | raw }}
```

Liquid syntax inside that HTML is not executed. Add a `LiquidPart` (or a `LiquidField`) to the section when editors need templated content.

### Permissions

1. Navigate to **Security → Roles** and edit a role.
2. Under the `OrchardCore.CustomSettings` group, check **Manage Custom Settings - {Display Name}**.

The permission name is `ManageCustomSettings_{TypeName}`; grant it to the roles that must see and save the section.

### Custom Settings with Content Fields

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

```liquid
{% assign branding = Site.Properties.BrandingSettings.BrandingSettings %}
{{ branding.FooterText.Html | sanitize_html | raw }}
<a href="{{ branding.SocialLink.Url }}">{{ branding.SocialLink.Text }}</a>
```

(Requires `"BrandingSettings"` in `PermittedSiteProperties`.)

### Configuring Custom Settings via Recipe

The `custom-settings` step copies each named entry into the site `Properties`:

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

### Creating a Custom Settings Content Type via Recipe

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

### Deployment

The **Custom Settings** deployment step (in the **Settings** category, requires `OrchardCore.Deployment`) exports all sections, or the selected `SettingsTypeNames`, as a `custom-settings` recipe step. Only sections the exporting user may manage are exported.

### Upgrading from 3.x

- Liquid templates reading `{{ Site.Properties.MySettings... }}` render nothing until `MySettings` is added to `OrchardCore:Liquid:PermittedSiteProperties`.
- Liquid embedded in `HtmlBodyPart`/`HtmlField` of a section is no longer executed (the `RenderLiquid` setting is gone); move it to a `LiquidPart` or `LiquidField`.
- Raw HTML read from `Site.Properties` is the authored source; pipe it through `sanitize_html` before `raw`.
