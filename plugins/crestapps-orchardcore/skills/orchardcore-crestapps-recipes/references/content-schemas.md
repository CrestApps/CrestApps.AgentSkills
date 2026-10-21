# Content Definition and Content Item Schemas

## The contracts

| Type | Namespace | Role |
|---|---|---|
| `IContentSchemaDefinition` | `CrestApps.OrchardCore.Recipes.Core` | `Type` (`ContentDefinitionSchemaType.Part` or `Field`), `Name`, `GetSettingsSchemaAsync()`: the `Settings` envelope in `ContentDefinition` and `ReplaceContentDefinition` |
| `IContentPartSchemaDefinition` | `CrestApps.OrchardCore.Recipes.Core` | `GetPartSchemaAsync(ContentPartSchemaContext, ...)`: the part payload in the `Content` step |
| `IContainedContentPartSchemaDefinition` | `CrestApps.OrchardCore.Recipes.Core` | Optional, for container parts such as `BagPart` or `FlowPart`: `NestedItemsPropertyName` and `GetContainedContentTypesAsync(...)` |
| `PartSchemaDefinitionBase` | `...Schemas.Parts` | Implements settings and payload for a part; caches the settings schema |
| `FieldSchemaDefinitionBase` | `...Schemas.Fields` | Implements settings and payload for a field; caches the settings schema |

Register every contributor as `IContentSchemaDefinition`; the content item schema finds the part and field payload contracts on the same instances. `ContentPartSchemaContext` and `ContentFieldSchemaContext` give the exact `ContentTypePartDefinition` or `ContentPartFieldDefinition` being rendered. Contributions with the same `Name` are merged.

Implement `IContentSchemaDefinition` directly only when neither base class fits.

## Two settings surfaces

- `ContentParts[].Settings` describes the reusable part definition.
- `ContentTypes[].ContentTypePartDefinitionRecords[].Settings` describes the part attached to a type, next to Orchard Core's `ContentTypePartSettings` (placement metadata).
- Field settings live in `ContentPartFieldDefinitionRecords[].Settings`.

## Part schema

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;

namespace MyModule.Schemas;

public sealed class ContactCardPartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "ContactCardPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ContactCardPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ShowPhoneNumber", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("Layout", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum("Compact", "Full")
                            .Default("Compact")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Heading", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("PhoneNumber", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true);
}
```

Override `BuildPartSchemaAsync(context, cancellationToken)` instead of `BuildPartSchemaCore()` when the payload depends on the attachment or needs awaited data. When a part holds only content fields, do not add a payload contributor; the `Content` schema composes attached fields from their own definitions.

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentParts": [
        {
          "Name": "ContactCardPart",
          "Settings": {
            "ContactCardPartSettings": {
              "ShowPhoneNumber": true,
              "Layout": "Compact"
            }
          },
          "ContentPartFieldDefinitionRecords": []
        }
      ],
      "ContentTypes": [
        {
          "Name": "PersonPage",
          "DisplayName": "Person Page",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true,
              "Draftable": true,
              "Versionable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "ContactCardPart",
              "Name": "ContactCardPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "3"
                },
                "ContactCardPartSettings": {
                  "Layout": "Full"
                }
              }
            }
          ]
        }
      ]
    },
    {
      "name": "Content",
      "data": [
        {
          "ContentType": "PersonPage",
          "DisplayText": "Jane Doe",
          "Published": true,
          "Latest": true,
          "ContactCardPart": {
            "Heading": "Primary contact",
            "PhoneNumber": "+15555550100"
          }
        }
      ]
    }
  ]
}
```

## Field schema

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;
using Json.Schema;

namespace MyModule.Schemas;

public sealed class SocialLinkFieldSchema : FieldSchemaDefinitionBase
{
    public override string Name { get; } = "SocialLinkField";

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("SocialLinkFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("AllowedHost", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("OpenInNewTab", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Text", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("Url", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true);
}
```

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentParts": [
        {
          "Name": "ProfilePart",
          "ContentPartFieldDefinitionRecords": [
            {
              "Name": "SupportLink",
              "DisplayName": "Support link",
              "ContentFieldDefinition": {
                "Name": "SocialLinkField"
              },
              "Settings": {
                "SocialLinkFieldSettings": {
                  "AllowedHost": "example.com",
                  "OpenInNewTab": true
                }
              }
            }
          ]
        }
      ]
    },
    {
      "name": "Content",
      "data": [
        {
          "ContentType": "PersonPage",
          "ProfilePart": {
            "SupportLink": {
              "Text": "Contact support",
              "Url": "https://example.com/support"
            }
          }
        }
      ]
    }
  ]
}
```

## Registration

```csharp
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, ContactCardPartSchema>();
        services.AddScoped<IContentSchemaDefinition, SocialLinkFieldSchema>();
    }
}
```

This is the pattern CrestApps modules use (for example `PhoneFieldSchemaDefinition` and `AddressFieldSchemaDefinition` in Content Fields, `RolePickerPartSchemaDefinition` in Roles, `UserFullNamePartSchemaDefinition` in Users).
