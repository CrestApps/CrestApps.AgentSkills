---
name: orchardcore-crestapps-content-fields
description: Skill for adding CrestApps content fields to Orchard Core with PhoneField. Covers PhoneField storage, E.164 validation, country selection, editor settings, recipe schema support, display drivers, and content-definition migrations. Use this skill when requests mention CrestApps PhoneField, international telephone fields, country-aware phone input, E.164 phone storage, PhoneFieldSettings, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.ContentFields, PhoneField, PhoneFieldDisplayDriver, PhoneFieldSettingsDriver, PhoneFieldSchemaDefinition, IPhoneNumberService, and CrestApps.OrchardCore.Resources.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Content Fields

## Configure PhoneField

You are an Orchard Core expert. Generate focused code, migrations, and recipes for the CrestApps `PhoneField`. It is a custom content field, not a text field convention. It persists normalized phone data and uses the CrestApps phone-number service and the shared `intl-tel-input` resource.

### Guidelines

- Install `CrestApps.OrchardCore.ContentFields` in the web or startup project, never only in a class library.
- Enable the exact `CrestApps.OrchardCore.ContentFields` feature. Its manifest dependencies bring in `CrestApps.OrchardCore.Resources`, the CrestApps phone-number feature, `OrchardCore.ContentFields`, and `OrchardCore.ContentTypes`.
- The field type name is exactly `PhoneField`.
- Store `PhoneNumber` in E.164 form when it is valid, such as `+14155552671`.
- Persist `CountryCode` as an ISO 3166-1 alpha-2 code, such as `US`, as countries can share a calling code.
- `NationalNumber` is the local part without the calling code. Do not derive identity or uniqueness from it alone.
- The editor calls `IPhoneNumberService.IsValidNumber()` and normalizes successfully validated values through `TryFormatToE164()`.
- A required field rejects an empty submitted number. Invalid submitted numbers add a model-state error.
- Use `InitialCountryMode.CurrentCulture` only where the request culture reliably includes a region. It intentionally yields no initial country for neutral and invariant cultures.
- `InitialCountryMode.Specific` requires an ISO country code. The settings driver clears `SpecificCountryCode` for every other mode.
- `CrestApps.OrchardCore.Resources` registers the local and CDN-capable `intl-tel-input` script and style used by the editor. Do not add a duplicate CDN tag for it.
- Enable `CrestApps.OrchardCore.Recipes` too when recipe JSON schema support for this field is needed.
- Use file-scoped namespaces and sealed classes in C# examples. View Models are the exception when model binding needs inheritance.

### Feature overview

| Feature ID | Purpose |
|---|---|
| `CrestApps.OrchardCore.ContentFields` | Registers `PhoneField`, its display driver, and its settings driver |
| `CrestApps.OrchardCore.Resources` | Provides shared resources including `intl-tel-input` |
| `CrestApps.OrchardCore.Recipes` | Enables `PhoneFieldSchemaDefinition` when both features are enabled |

### Enable the feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContentFields",
        "CrestApps.OrchardCore.Recipes"
      ],
      "disable": []
    }
  ]
}
```

## PhoneField data and settings

`PhoneField` has three persisted properties:

| Property | Meaning |
|---|---|
| `PhoneNumber` | Normalized E.164 phone number |
| `CountryCode` | ISO 3166-1 alpha-2 region used by the editor |
| `NationalNumber` | Local number without the country calling code |

Configure `PhoneFieldSettings` on the content-part field definition:

| Setting | Meaning |
|---|---|
| `Hint` | Editor help text |
| `Required` | Rejects an empty number |
| `InitialCountryMode` | `Globe`, `CurrentCulture`, or `Specific` |
| `SpecificCountryCode` | Initial ISO country code for `Specific` mode |

### Attach PhoneField in a migration

```csharp
using CrestApps.OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;

namespace MyModule;

public sealed class ContactMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ContactMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("ContactPart", part => part
            .WithField("Phone", field => field
                .OfType("PhoneField")
                .WithDisplayName("Phone number")
                .WithPosition("1")
                .WithSettings(new PhoneFieldSettings
                {
                    Hint = "Enter an international phone number.",
                    Required = true,
                    InitialCountryMode = InitialCountryMode.Specific,
                    SpecificCountryCode = "US",
                })));

        return 1;
    }
}
```

Register the migration from the owning module startup:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<ContactMigrations>();
    }
}
```

## Define the field with a recipe

`PhoneFieldSchemaDefinition` validates the `PhoneFieldSettings` envelope in the `ContentDefinition` step when the Recipes feature is enabled. Keep the recipe root object and `steps` array.

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentParts": [
        {
          "Name": "ContactPart",
          "ContentPartFieldDefinitionRecords": [
            {
              "Name": "Phone",
              "DisplayName": "Phone number",
              "ContentFieldDefinition": {
                "Name": "PhoneField"
              },
              "Settings": {
                "PhoneFieldSettings": {
                  "Hint": "Enter an international phone number.",
                  "Required": true,
                  "InitialCountryMode": "Specific",
                  "SpecificCountryCode": "US"
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

### Content item payload

For the `content` recipe step, use all three properties where the country and national components are known. The schema permits the field payload alongside other content fields.

```json
{
  "steps": [
    {
      "name": "content",
      "data": [
        {
          "ContentType": "Contact",
          "DisplayText": "Example contact",
          "ContactPart": {
            "Phone": {
              "PhoneNumber": "+14155552671",
              "CountryCode": "US",
              "NationalNumber": "4155552671"
            }
          }
        }
      ]
    }
  ]
}
```

## Editor and display behavior

`PhoneFieldDisplayDriver` supplies editor and display shapes. On edit it first uses the stored `CountryCode`; for older records without it, it attempts to resolve a region from `PhoneNumber`. It derives a national number only when one has not already been stored.

Use the content-type editor to add `PhoneField` when no migration is appropriate:

1. Navigate to **Content Definition → Content Parts**.
2. Edit the target part and add a field of type **PhoneField**.
3. Set requiredness, hint text, and an initial-country mode.
4. Attach the part to a content type and save.
5. Create or edit an item and enter a full number with its country selection.

Do not manually alter a submitted E.164 value after `PhoneFieldDisplayDriver` has normalized it. If importing external records, populate a valid E.164 number and compatible uppercase ISO country code, then let normal editor updates retain the normalized structure.

## Troubleshooting

- Missing country flag or input behavior usually means the Content Fields feature is not enabled or its Resources dependency is unavailable.
- A number that looks valid locally can fail because `IPhoneNumberService` validates it using the selected country.
- `CurrentCulture` does not guess a country from a neutral culture such as `en`; choose `Specific` for deterministic behavior.
- If generated recipe tooling does not suggest `PhoneFieldSettings`, enable both `CrestApps.OrchardCore.ContentFields` and `CrestApps.OrchardCore.Recipes`.
- `PhoneNumber`, `CountryCode`, and `NationalNumber` are the supported content payload names. Do not substitute names such as `Value` or `Region`.
