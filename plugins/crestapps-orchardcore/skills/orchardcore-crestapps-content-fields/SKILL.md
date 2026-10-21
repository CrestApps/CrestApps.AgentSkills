---
name: orchardcore-crestapps-content-fields
description: Skill for the CrestApps Content Fields module in Orchard Core, which adds PhoneField and AddressField. Covers PhoneField storage with E.164 validation, country selection and PhoneFieldSettings, the Address Field feature with country-aware editing through the Addresses module, AddressFieldSettings, the AddressFieldIndex for querying by place, recipe schema support, display drivers and content-definition migrations. Use this skill when requests mention CrestApps PhoneField, AddressField, international telephone fields, country-aware phone input, E.164 phone storage, postal address fields, or querying content by country or region. Strong matches include CrestApps.OrchardCore.ContentFields, CrestApps.OrchardCore.ContentFields.Address, PhoneField, PhoneFieldSettings, InitialCountryMode, AddressField, AddressFieldSettings, AddressFieldIndex, PhoneFieldDisplayDriver, AddressFieldDisplayDriver and IPhoneNumberService.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Content Fields

## Add CrestApps content fields

You are an Orchard Core expert. Generate focused code, migrations and recipes for the CrestApps content fields: `PhoneField` (international phone numbers stored in E.164 with their country) and `AddressField` (a postal address laid out and validated for its country).

### Guidelines

- Install `CrestApps.OrchardCore.ContentFields` in the web or startup project, never only in a class library.
- Enable `CrestApps.OrchardCore.ContentFields` for `PhoneField`. It depends on `CrestApps.OrchardCore.Resources`, `CrestApps.OrchardCore.PhoneNumbers`, `OrchardCore.ContentFields` and `OrchardCore.ContentTypes`.
- Enable `CrestApps.OrchardCore.ContentFields.Address` for `AddressField`. It depends on `CrestApps.OrchardCore.Addresses`, `OrchardCore.ContentFields` and `OrchardCore.ContentTypes`, and is separate so the phone field does not pull in Addresses.
- Field type names are exactly `PhoneField` and `AddressField`. Fields live in `CrestApps.OrchardCore.ContentFields.Fields`; settings and `InitialCountryMode` in `CrestApps.OrchardCore.ContentFields.Settings`.
- Store `PhoneNumber` in E.164 form (for example `+14155552671`) and `CountryCode` as ISO 3166-1 alpha-2, because countries can share a calling code.
- The phone editor validates with `IPhoneNumberService.IsValidNumber()` and normalizes with `TryFormatToE164()`; see the `orchardcore-phone-numbers` skill.
- Query addresses through `AddressFieldIndex` rather than loading every item.
- Do not add a duplicate CDN tag for `intl-tel-input`; the field's own `international-telephone-editor` resource depends on the shared one.
- Enable `CrestApps.OrchardCore.Recipes` for recipe JSON Schema support of both fields.

### Feature overview

| Feature ID | Purpose |
|---|---|
| `CrestApps.OrchardCore.ContentFields` | `PhoneField`, its display and settings drivers, the `international-telephone-editor` script and style |
| `CrestApps.OrchardCore.ContentFields.Address` | `AddressField`, its drivers, `AddressFieldIndex` and Liquid member access |
| `CrestApps.OrchardCore.Recipes` | Adds `PhoneFieldSchemaDefinition` and `AddressFieldSchemaDefinition` when enabled with the field features |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContentFields",
        "CrestApps.OrchardCore.ContentFields.Address",
        "CrestApps.OrchardCore.Recipes"
      ],
      "disable": []
    }
  ]
}
```

## PhoneField

| Property | Meaning |
|---|---|
| `PhoneNumber` | Normalized E.164 phone number |
| `CountryCode` | ISO 3166-1 alpha-2 region used by the editor |
| `NationalNumber` | Local number without the calling code |

| `PhoneFieldSettings` | Default | Meaning |
|---|---|---|
| `Hint` | `null` | Editor help text |
| `Required` | `false` | Rejects an empty number |
| `InitialCountryMode` | `Globe` | `Globe` (no preselection), `CurrentCulture` (region of the request culture, none for neutral cultures such as `en`), or `Specific` |
| `SpecificCountryCode` | `null` | ISO code preselected in `Specific` mode; the settings driver clears it for other modes |

```csharp
using CrestApps.OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace MyModule.Migrations;

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

Register it with `services.AddDataMigration<ContactMigrations>();` in the owning module's startup.

### Editor behavior

`PhoneFieldDisplayDriver` uses the stored `CountryCode` on edit; for older records without one, it resolves the region from `PhoneNumber` and derives a national number only when none is stored. A required field rejects an empty number, and an invalid number adds a model-state error. The editor uses the `intl-tel-input` library from `CrestApps.OrchardCore.Resources` (see `orchardcore-crestapps-resources`).

## AddressField

`AddressField.Address` holds the shared `Address` model of the Addresses module (`CrestApps.OrchardCore.Addresses.Models`), or `null` when nothing was entered: `Name`, `Company`, `AddressLine1`, `AddressLine2`, `DependentLocality`, `City`, `Region` (ISO 3166-2 subdivision code without the country prefix, when the country lists regions), `County`, `District`, `PostalCode`, `SortingCode`, `Country` (ISO 3166-1 alpha-2) and `Phone`. A type can have several, such as Home and Work.

| `AddressFieldSettings` | Default | Meaning |
|---|---|---|
| `Hint` | `null` | Help text above the address |
| `Required` | `false` | An address must be entered; an optional blank one is stored as `null` |
| `ShowName`, `ShowCompany`, `ShowPhone` | `true`, `true`, `false` | Which extra lines the editor asks for |
| `Countries` | `null` | ISO codes offered; empty offers every country `ICountryService` lists |
| `DefaultCountry` | `null` | Country a new address starts in |

The editor is the Addresses module's `AddressEditor` shape bound under the field prefix and validated by `IAddressEditorService`, so required parts, postal code patterns, region lists and postal-code-in-region checks follow the chosen country. The `AddressField` display shape prints through `IAddressFormatter`. `AddressField` and `Address` are registered for Liquid member access. See the `orchardcore-addresses` skill for the address services.

```csharp
using CrestApps.OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace MyModule.Migrations;

public sealed class VenueMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public VenueMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("Venue", part => part
            .WithField("Location", field => field
                .OfType("AddressField")
                .WithDisplayName("Location")
                .WithSettings(new AddressFieldSettings
                {
                    Required = true,
                    ShowCompany = false,
                    Countries = ["US", "CA"],
                    DefaultCountry = "CA",
                })));

        return 1;
    }
}
```

### Query content by place

`AddressFieldIndex` (namespace `CrestApps.OrchardCore.ContentFields.Indexes`, a `ContentFieldIndex`) has one row per address field with `Country`, `Region`, `City` and `PostalCode`.

```csharp
using CrestApps.OrchardCore.ContentFields.Indexes;
using OrchardCore.ContentManagement;
using YesSql;

namespace MyModule.Services;

public sealed class VenueQueries
{
    private readonly ISession _session;

    public VenueQueries(ISession session)
    {
        _session = session;
    }

    public Task<IEnumerable<ContentItem>> GetOntarioVenuesAsync()
        => _session
            .Query<ContentItem, AddressFieldIndex>(index =>
                index.ContentType == "Venue" &&
                index.Country == "CA" &&
                index.Region == "ON" &&
                index.Published)
            .ListAsync();
}
```

## Recipes

With CrestApps Recipes enabled, both fields have a content-definition settings schema and a content payload schema.

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
                  "Required": true,
                  "InitialCountryMode": "Specific",
                  "SpecificCountryCode": "US"
                }
              }
            },
            {
              "Name": "Mailing",
              "DisplayName": "Mailing address",
              "ContentFieldDefinition": {
                "Name": "AddressField"
              },
              "Settings": {
                "AddressFieldSettings": {
                  "Required": false,
                  "ShowPhone": false,
                  "DefaultCountry": "US"
                }
              }
            }
          ]
        }
      ]
    },
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
            },
            "Mailing": {
              "Address": {
                "Name": "Jane Doe",
                "AddressLine1": "1 Market St",
                "City": "San Francisco",
                "Region": "CA",
                "PostalCode": "94105",
                "Country": "US"
              }
            }
          }
        }
      ]
    }
  ]
}
```

The schemas validate shape only. Imported phone numbers should already be valid E.164 with a matching uppercase country code, and imported addresses are not run through the country rules until an editor saves them.

## Troubleshooting

- Missing country flags or input behavior: the Content Fields feature or its Resources dependency is not enabled.
- A number that looks valid locally can fail because `IPhoneNumberService` validates it for the selected country.
- `CurrentCulture` does not guess a country from a neutral culture; use `Specific` for deterministic behavior.
- **AddressField** missing from the field list: enable `CrestApps.OrchardCore.ContentFields.Address` (and with it Addresses).
- Address queries return nothing for older items: the index fills when items are saved; resave or reindex affected items.
- Recipe tooling does not suggest the settings: enable the field feature together with `CrestApps.OrchardCore.Recipes`.
- Use only the documented payload names (`PhoneNumber`, `CountryCode`, `NationalNumber`, `Address`). Do not substitute `Value` or `Region` at the phone level.
