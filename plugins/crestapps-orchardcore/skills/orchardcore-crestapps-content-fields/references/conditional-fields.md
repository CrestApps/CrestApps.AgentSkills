# Conditional Fields

Feature `CrestApps.OrchardCore.ContentFields.ConditionalFields` (`ContentFieldsConstants.Features.ConditionalFields`), category **Content**, depends on `OrchardCore.Contents` and `OrchardCore.ContentTypes`. It adds **Show this field when** to the settings of every content field of every content type (edited under **Design > Content Definition**).

## The rule

`ConditionalFieldSettings.ShowWhen` (namespace `CrestApps.OrchardCore.ContentFields.Settings`) is a shared `ConditionRule` (namespace `CrestApps.OrchardCore.Conditions`, assembly `CrestApps.OrchardCore.Abstractions`); `null` means always shown.

| Type | Members |
|---|---|
| `ConditionRule` | `Match` (`All` or `Any`, `ConditionMatch`), `Conditions` |
| `Condition` | `Source`, `Operator` (`ConditionOperator`), `Values` (strings, invariant culture) |
| `ConditionOperator` | `Is`, `IsNot`, `IsOneOf`, `IsNoneOf`, `IsEmpty`, `IsNotEmpty`, `Contains`, `GreaterThan`, `LessThan`, `IsOrUnder` |

- `Source` is another field of the item as `{TypePartName}.{FieldName}` (for example `Ticket.Outage`), or the name (no dot) of a value a module provides through `IContentConditionSource`, such as Tickets' `TicketCategory`.
- `IsOneOf`/`IsNoneOf` use every value, `IsEmpty`/`IsNotEmpty` none, the others the first. `IsOrUnder` matches a taxonomy term or any term below it.
- A field under a hidden field is hidden too. The settings editor refuses cycles. A rule whose source field was removed is shown as such and ignored (the field shows).
- `ConditionEvaluator.IsMet(rule, lookup)` and `ConditionalVisibility.GetHidden(...)` / `FindCycle(...)` evaluate rules in code.

## Recipe

The rule is a field setting, so it travels with `ContentDefinition`. With `CrestApps.OrchardCore.Recipes`, `ConditionalFieldSettings` is part of the settings schema of the common field types.

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentParts": [
        {
          "Name": "Ticket",
          "ContentPartFieldDefinitionRecords": [
            {
              "Name": "Outage",
              "DisplayName": "Is this an outage?",
              "ContentFieldDefinition": { "Name": "BooleanField" }
            },
            {
              "Name": "AffectedUsers",
              "DisplayName": "Who else is affected?",
              "ContentFieldDefinition": { "Name": "TextField" },
              "Settings": {
                "ConditionalFieldSettings": {
                  "ShowWhen": {
                    "Match": "All",
                    "Conditions": [
                      { "Source": "Ticket.Outage", "Operator": "Is", "Values": ["true"] }
                    ]
                  }
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

## What the server does

The browser script only hides and disables controls; the server decides on every post, in every editor of the type (Orchard Core's content editor included):

1. `ConditionalFieldsDisplayDriver` (an item-level `ContentDisplayDriver`, run before field editors) keeps a snapshot of the item: a new item's values on create, the stored values on edit.
2. `ConditionalFieldsDisplayHandler` (an `IContentDisplayHandler` registered after Orchard Core's display coordinator) restores the snapshot value of each field that is hidden for the values just posted, and removes that field's validation errors. A posted value for a hidden field is never kept, and a hidden required field does not block the form.
3. `ConditionalFieldsPlacementProvider` leaves hidden fields out of displays.

On edit, a field hidden by a new answer keeps its stored value, so nothing is lost if the answer changes back.

In the editor, the `ConditionalFields_Edit` shape (in `Content:after`) carries the rules as JSON and requires the `crestapps-conditional-fields` script (`ContentFieldsConstants.ConditionalFieldsScript`), which toggles each field's `.field-wrapper` and disables the controls of hidden fields. Without script every field shows. Field editors rendered on the front-end theme get a plain fallback for Orchard Core's admin-only `Hint_Admin` shape.

## Built-in value readers

`TextField` (with predefined-list options), `BooleanField` (`true`/`false`), `NumericField`, `DateField`, `DateTimeField`, `TimeField`, `MultiTextField`, `ContentPickerField`, `UserPickerField`, `LocalizationSetContentPickerField`, `LinkField`, `HtmlField`, `MarkdownField`, `TaxonomyField` (term ids with ancestors, for `IsOrUnder`), `PhoneField` (`PhoneNumber`) and `AddressField` (registered on the path `Address.CountryCode`).

## Extension points

All in namespace `CrestApps.OrchardCore.ContentFields.Conditions` (ContentFields.Abstractions). Register them in a startup with `[RequireFeatures(ContentFieldsConstants.Features.ConditionalFields)]`.

| Contract | Use |
|---|---|
| `IContentConditionSource` | Values that are not fields (a ticket category): `DescribeAsync(type)` returns `ContentConditionSourceDescriptor`s (`Name`, `DisplayName`, `Kind`, `InputName` the browser reads, `IsHierarchical`, `Options`, `Ancestors`); `ReadAsync(name, contentItem)` returns a `ConditionValue` |
| `IContentFieldValueReader` | Your own field type: `FieldType`, `ReadAsync(JsonNode field, definition)`, `DescribeAsync(definition, descriptor)` to fill options |
| `IContentFieldVisibilityProvider` | Hide fields for your own reason (for example fields customers never fill in): `GetHiddenFieldsAsync(contentItem, type)`; hidden fields get the same restore and error removal |
| `IContentFieldStateService` | Read `ContentFieldState(Key, PartName, FieldName, DisplayName, IsHidden, IsEmpty)` for each field of an item, for example to skip hidden fields in a summary |

```csharp
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.ContentFields.Conditions;
using OrchardCore.ContentManagement.Metadata.Models;

namespace MyModule.Conditions;

public sealed class RatingFieldValueReader : IContentFieldValueReader
{
    public string FieldType => "RatingField";

    public Task<ConditionValue> ReadAsync(JsonNode field, ContentPartFieldDefinition definition)
        => Task.FromResult(field?["Stars"] is JsonValue stars
            ? ConditionValue.Of(stars.ToString())
            : ConditionValue.Empty);

    public Task DescribeAsync(ContentPartFieldDefinition definition, ContentConditionSourceDescriptor descriptor)
    {
        for (var i = 1; i <= 5; i++)
        {
            descriptor.Options.Add(new ContentConditionOption(i.ToString(), $"{i} stars"));
        }

        return Task.CompletedTask;
    }
}
```

```csharp
services.AddScoped<IContentFieldValueReader, RatingFieldValueReader>();
```

## Cascading taxonomy picker

The `TaxonomyCascadingPicker` shape picks a term one level at a time (Category, then Sub-category) and posts one value. It requires `crestapps-cascading-picker`; without script it is one indented list.

```cshtml
@await DisplayAsync(await New.TaxonomyCascadingPicker(
    InputName: Html.NameFor(m => m.CategoryId).ToString(),
    Choices: Model.Choices,
    SelectedId: Model.CategoryId,
    Required: true,
    LevelLabels: new[] { T["Category"].Value, T["Sub-category"].Value }))
```

- `Choices` are `CascadingPickerItem(Id, Text, ParentId, Selectable = true, Description = null)` (namespace `CrestApps.OrchardCore.ContentFields.Taxonomies`). Other properties: `InputId`, `EmptyText`.
- Build choices from a taxonomy without the Taxonomies module with `TaxonomyTermTree.From(taxonomyContentItem)`: `Nodes` in tree order (`TaxonomyTermNode(Id, Name, ParentId, Level, AncestorIds, Content)`), `Find(id)`, `ChildrenOf(parentId)` and `GetPathText(id, " > ")`.
