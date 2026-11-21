---
name: orchardcore-crestapps-content-fields
description: Skill for the CrestApps Content Fields module in Orchard Core, which adds PhoneField, AddressField and Conditional Fields. Covers PhoneField storage with E.164 validation and PhoneFieldSettings, AddressField with country-aware editing, AddressFieldSettings and AddressFieldIndex, Conditional Fields with ConditionalFieldSettings.ShowWhen rules, server-side hiding of field values, IContentConditionSource, IContentFieldValueReader, IContentFieldVisibilityProvider and IContentFieldStateService, the TaxonomyCascadingPicker shape and recipe schemas. Use this skill when requests mention CrestApps PhoneField, AddressField, postal address fields, querying content by country, showing a field only when another field has a value, conditional fields or a cascading category picker. Strong matches include CrestApps.OrchardCore.ContentFields, CrestApps.OrchardCore.ContentFields.Address, CrestApps.OrchardCore.ContentFields.ConditionalFields, PhoneField, AddressField, ConditionalFieldSettings and ConditionRule.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Content Fields

## Add CrestApps content fields

You are an Orchard Core expert. Generate focused code, migrations and recipes for the CrestApps content fields: `PhoneField` (international phone numbers stored in E.164 with their country), `AddressField` (a postal address laid out and validated for its country) and Conditional Fields (show any content field only when other answers say so).

### Guidelines

- Install `CrestApps.OrchardCore.ContentFields` in the web or startup project, never only in a class library.
- Enable `CrestApps.OrchardCore.ContentFields` for `PhoneField`. It depends on `CrestApps.OrchardCore.Resources`, `CrestApps.OrchardCore.PhoneNumbers`, `OrchardCore.ContentFields` and `OrchardCore.ContentTypes`.
- Enable `CrestApps.OrchardCore.ContentFields.Address` for `AddressField`. It depends on `CrestApps.OrchardCore.Addresses`, `OrchardCore.ContentFields` and `OrchardCore.ContentTypes`, and is separate so the phone field does not pull in Addresses.
- Field type names are exactly `PhoneField` and `AddressField`. Fields live in `CrestApps.OrchardCore.ContentFields.Fields`; settings and `InitialCountryMode` in `CrestApps.OrchardCore.ContentFields.Settings`.
- The field and settings classes are in the `CrestApps.OrchardCore.ContentFields.Abstractions` package (namespaces unchanged). A class library that only reads or writes `PhoneField`/`AddressField` values references the abstractions package instead of the module.
- Store `PhoneNumber` in E.164 form (for example `+14155552671`) and `CountryCode` as ISO 3166-1 alpha-2, because countries can share a calling code.
- The phone editor validates with `IPhoneNumberService.IsValidNumber()` and normalizes with `TryFormatToE164()`; see the `orchardcore-phone-numbers` skill.
- Query addresses through `AddressFieldIndex` rather than loading every item.
- Do not add a duplicate CDN tag for `intl-tel-input`; the field's own `international-telephone-editor` resource depends on the shared one.
- Enable `CrestApps.OrchardCore.ContentFields.ConditionalFields` for **Show this field when** rules on any field. It depends only on `OrchardCore.Contents` and `OrchardCore.ContentTypes`. Feature IDs are in `ContentFieldsConstants.Features` (ContentFields.Abstractions).
- Treat the browser's show and hide as convenience only: the server drops values posted for hidden fields and ignores their validation errors. Never rely on client script for conditional data.
- Enable `CrestApps.OrchardCore.Recipes` for recipe JSON Schema support of both fields and of `ConditionalFieldSettings`.

### Feature overview

| Feature ID | Purpose |
|---|---|
| `CrestApps.OrchardCore.ContentFields` | `PhoneField`, its display and settings drivers, the `international-telephone-editor` script and style |
| `CrestApps.OrchardCore.ContentFields.Address` | `AddressField`, its drivers, `AddressFieldIndex` and Liquid member access |
| `CrestApps.OrchardCore.ContentFields.ConditionalFields` | `ConditionalFieldSettings` on every field, server-side hiding, the `crestapps-conditional-fields` and `crestapps-cascading-picker` scripts, value readers |
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

`AddressFieldIndex` (namespace `CrestApps.OrchardCore.ContentFields.Indexes`, a `ContentFieldIndex`) has one row per address field with `Country`, `Region`, `City` and `PostalCode`. Its provider (`AddressFieldIndexProvider : ContentFieldIndexProvider`) maps published and latest versions, so it is never single-record per document.

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

## Conditional Fields

Every field's settings gain **Show this field when**, stored as `ConditionalFieldSettings.ShowWhen` (a `ConditionRule` from `CrestApps.OrchardCore.Conditions`: `Match` `All` or `Any`, and `Conditions` of `Source`, `Operator`, `Values`). A source is another field as `{TypePartName}.{FieldName}` or a module-provided value name (such as the Tickets `TicketCategory`). Operators: `Is`, `IsNot`, `IsOneOf`, `IsNoneOf`, `IsEmpty`, `IsNotEmpty`, `Contains`, `GreaterThan`, `LessThan`, `IsOrUnder`.

```csharp
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace MyModule.Migrations;

public sealed class DeviceRequestMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public DeviceRequestMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("DeviceRequest", part => part
            .WithField("IsLaptop", field => field
                .OfType("BooleanField")
                .WithDisplayName("Is it a laptop?"))
            .WithField("LaptopModel", field => field
                .OfType("TextField")
                .WithDisplayName("Laptop model")
                .WithSettings(new ConditionalFieldSettings
                {
                    ShowWhen = new ConditionRule
                    {
                        Match = ConditionMatch.All,
                        Conditions =
                        [
                            new Condition { Source = "DeviceRequest.IsLaptop", Operator = ConditionOperator.Is, Values = ["true"] },
                        ],
                    },
                })));

        return 1;
    }
}
```

- On post, a snapshot driver and a later display handler restore the stored value of every hidden field and remove its validation errors; displays leave hidden fields out. On edit, a field hidden by a new answer keeps its stored value.
- A field under a hidden field is hidden too; cycles are refused by the settings editor.
- Extend with `IContentConditionSource` (non-field values), `IContentFieldValueReader` (your field type), `IContentFieldVisibilityProvider` (hide fields for your own reason) and read the outcome with `IContentFieldStateService`, all in `CrestApps.OrchardCore.ContentFields.Conditions`.
- The `TaxonomyCascadingPicker` shape picks a taxonomy term level by level; `TaxonomyTermTree.From(taxonomy)` walks terms without the Taxonomies module.

Read [conditional fields](references/conditional-fields.md) for the server pipeline, the built-in readers, a custom reader, the recipe shape and the cascading picker.

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
- A conditional field never hides: check that the source key is `{TypePartName}.{FieldName}` (the part name on the type, often the type name), that the feature is enabled, and that the source field type has a value reader.
- A value typed into a hidden field disappears after saving: that is by design; the server keeps only what applies.
- A rule on an `AddressField` source never matches: the built-in reader reads `Address.CountryCode`, while `Address` stores the country in `Country`, so it currently reads an empty value.
- Field hints are missing on a front-end form: enable Conditional Fields, which ships a `Hint_Admin` fallback for non-admin themes, or add your own.
