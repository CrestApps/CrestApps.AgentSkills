# Composed Recipe Step Schemas

Several Orchard Core recipe steps hold polymorphic entries: a discriminator (`$type`, `Name`, `Type`, `Source` or a key) selects which members an entry accepts. CrestApps Recipes composes these steps from one schema definition per entry type. Each definition contributes a fragment; the step schema adds a conditional branch (`if` the discriminator equals the const, `then` these members) per definition.

Rules shared by every family:

- The base class adds the shared members. Describe only the type's own members in `GetPropertyDefinitions(context)` (or `GetValueSchema(context)` for placement filters).
- Entries stay open: additional properties are allowed and unknown discriminator values validate, so recipes exported from tenants with more modules keep validating.
- Display text is not written into the JSON Schema. It is returned on the family's descriptor (`AdminNodeDescriptor`, `DeploymentStepDescriptor`, `QuerySourceDescriptor`, `RewriteRuleSourceDescriptor`, `PlacementNodeFilterDescriptor`, `RuleConditionDescriptor`, `RuleConditionOperatorDescriptor`, `SitemapSourceDescriptor`) by the family's schema service, so other features can build listings.
- Optional overrides on every base: `DisplayText`, `Description`, `RequiredProperties`, plus `Build...SchemaCore(context)` for object-level constraints and `Build...SchemaAsync(context, cancellationToken)` for asynchronous work.
- Register with the family's `Add...Schema<T>()` extension (namespace `Microsoft.Extensions.DependencyInjection`) behind the feature that owns the type. The last registration of a name wins.
- Describe every member the type persists. An omitted member does not fail validation; it silently loses its type and description.

## Rule conditions and operators (`Layers`)

Registered when `OrchardCore.Layers` is enabled (it depends on `OrchardCore.Rules`). Services: `IRuleSchemaService`.

Shared condition members: `$type` (required, `Namespace.Type, Assembly`), `Name` (required, the condition factory name), `ConditionId` (generated when omitted). Operand conditions add `Value` and `Operation`; groups add `DisplayText` and a recursive `Conditions` array, inlined to a bounded depth.

Built-in conditions: `UrlCondition`, `ContentTypeCondition`, `RoleCondition`, `CultureCondition`, `BooleanCondition`, `HomepageCondition`, `JavascriptCondition`, `IsAnonymousCondition`, `IsAuthenticatedCondition`, `AllConditionGroup`, `AnyConditionGroup`.

Built-in operators (each with `CaseSensitive`): `StringEqualsOperator`, `StringNotEqualsOperator`, `StringStartsWithOperator`, `StringNotStartsWithOperator`, `StringEndsWithOperator`, `StringNotEndsWithOperator`, `StringContainsOperator`, `StringNotContainsOperator`.

```json
{
  "steps": [
    {
      "name": "Layers",
      "Layers": [
        {
          "Name": "Knowledge Base",
          "LayerRule": {
            "Conditions": [
              {
                "$type": "OrchardCore.Rules.Models.AnyConditionGroup, OrchardCore.Rules",
                "Name": "AnyConditionGroup",
                "Conditions": [
                  {
                    "$type": "OrchardCore.Rules.Models.UrlCondition, OrchardCore.Rules",
                    "Name": "UrlCondition",
                    "Value": "/knowledge-base/",
                    "Operation": {
                      "$type": "OrchardCore.Rules.Models.StringStartsWithOperator, OrchardCore.Rules",
                      "CaseSensitive": false
                    }
                  }
                ]
              }
            ]
          }
        }
      ]
    }
  ]
}
```

Base classes: `RuleConditionSchemaDefinitionBase` (namespace `...Schemas.Rules`), `OperandConditionSchemaDefinitionBase` and `ConditionGroupSchemaDefinitionBase` (`...Schemas.Rules.Conditions`), `RuleConditionOperatorSchemaDefinitionBase` (`...Schemas.Rules`), `StringOperatorSchemaDefinitionBase` (`...Schemas.Rules.Operators`). Property helpers: `RuleConditionSchemaBuilders.String`, `NullableString`, `Boolean`.

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;
using Json.Schema;

namespace MyModule.Schemas;

// A leaf condition with its own members.
public sealed class WeatherConditionSchema : RuleConditionSchemaDefinitionBase
{
    public override string Name { get; } = "WeatherCondition";

    public override string TypeDiscriminator { get; } = "MyModule.Rules.WeatherCondition, MyModule";

    protected override string DisplayText => "Weather";

    protected override string Description => "Matches when the current temperature crosses a threshold.";

    protected override IEnumerable<string> RequiredProperties => ["City"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RuleConditionSchemaContext context)
    {
        yield return ("City", RuleConditionSchemaBuilders.String("The city whose temperature is checked."));
    }
}

// A value plus operator condition. The base adds Value and Operation (context.OperatorSchema).
public sealed class StereotypeConditionSchema : OperandConditionSchemaDefinitionBase
{
    public override string Name { get; } = "StereotypeCondition";

    public override string TypeDiscriminator { get; } = "MyModule.Rules.StereotypeCondition, MyModule";

    protected override string ValueDescription => "The content type compared against the current request.";

    protected override IEnumerable<string> GetValueExamples(RecipeSchemaExamples examples) => examples.ContentTypeNames;
}

public sealed class RegexMatchOperatorSchema : RuleConditionOperatorSchemaDefinitionBase
{
    public override string Name { get; } = "RegexMatchOperator";

    public override string TypeDiscriminator { get; } = "MyModule.Rules.RegexMatchOperator, MyModule";

    protected override string DisplayText => "Matches pattern";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("IgnoreCase", RuleConditionSchemaBuilders.Boolean("Whether the pattern ignores case."));
    }
}
```

```csharp
services
    .AddRuleConditionSchema<WeatherConditionSchema>()
    .AddRuleConditionSchema<StereotypeConditionSchema>()
    .AddRuleConditionOperatorSchema<RegexMatchOperatorSchema>();
```

Rules: `TypeDiscriminator` must equal the serialized `$type`; `Name` must match the condition factory; operator display text comes from the invariant `S["..."]` key; value conditions must use `context.OperatorSchema` so new operators are picked up.

## Sitemap sources (`Sitemaps`)

Registered when `OrchardCore.Sitemaps` is enabled. Service: `ISitemapSchemaService`.

Sitemap members: `$type` (required; `OrchardCore.Sitemaps.Models.Sitemap, OrchardCore.Sitemaps.Abstractions` or `OrchardCore.Sitemaps.Models.SitemapIndex, OrchardCore.Sitemaps`), `SitemapId` (generated when omitted), `Name`, `Enabled`, `Path`, `SitemapSources`. Source members: `$type` (required), `Id`.

Built-in sources: `ContentTypesSitemapSource`, `CustomPathSitemapSource`, `SitemapIndexSource`. Helpers: `SitemapSchemaBuilders.ChangeFrequency`, `Priority`, `EnumValue`.

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps;
using Json.Schema;

namespace MyModule.Schemas;

public sealed class WeatherSitemapSourceSchema : SitemapSourceSchemaDefinitionBase
{
    public override string Name { get; } = "WeatherSitemapSource";

    public override string TypeDiscriminator { get; } = "MyModule.Models.WeatherSitemapSource, MyModule";

    protected override string DisplayText => "Weather";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(SitemapSourceSchemaContext context)
    {
        yield return ("City", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The city the page covers."));
        yield return ("ChangeFrequency", SitemapSchemaBuilders.ChangeFrequency("How often the page changes."));
    }
}
```

Register with `services.AddSitemapSourceSchema<WeatherSitemapSourceSchema>();`.

## Deployment steps (`deployment`)

Registered when `OrchardCore.Deployment` is enabled. Service: `IDeploymentSchemaService`. The service enumerates the tenant's deployment step factories, so a step is described only when its feature is enabled; a step without a definition still appears as a `Type` suggestion with an open `Step`.

Each plan entry has `Type` (the step CLR type name, which is `IDeploymentStepFactory.Name`) and `Step` (the payload). Marker steps such as `AllRolesDeploymentStep` use an empty object.

Built-in definitions include `AllContentDeploymentStep`, `ContentDeploymentStep`, `ContentItemDeploymentStep`, `ContentDefinitionDeploymentStep`, `ReplaceContentDefinitionDeploymentStep`, `DeleteContentDefinitionDeploymentStep`, `CustomFileDeploymentStep`, `RecipeFileDeploymentStep`, `JsonRecipeDeploymentStep`, `DeploymentPlanDeploymentStep`, `MediaDeploymentStep`, `CustomSettingsDeploymentStep`, `CustomUserSettingsDeploymentStep`, `QueryBasedContentDeploymentStep`, `AllTemplatesDeploymentStep`, `AllAdminTemplatesDeploymentStep`, `AllFeaturesDeploymentStep`, `SiteSettingsDeploymentStep`, `TranslationsDeploymentStep`, the Lucene, Elasticsearch and Azure AI Search index, rebuild and reset steps, `IndexProfileDeploymentStep`, `ResetIndexDeploymentStep` and `RebuildIndexDeploymentStep`.

```json
{
  "steps": [
    {
      "name": "deployment",
      "Plans": [
        {
          "Name": "Export catalog",
          "Steps": [
            {
              "Type": "ContentDeploymentStep",
              "Step": {
                "ContentTypes": [ "Product" ],
                "ExportAsSetupRecipe": false
              }
            },
            {
              "Type": "AllRolesDeploymentStep",
              "Step": {}
            }
          ]
        }
      ]
    }
  ]
}
```

Helpers: `DeploymentSchemaBuilders.Boolean`, `String`, `StringArray(description)`, `StringArray(description, itemExamples)`, `IncludeAll(itemsDescription)`. `AllowAdditionalProperties` can be overridden to close the payload. See SKILL.md for a full definition.

## Admin menu nodes (`AdminMenu`)

Registered when `OrchardCore.AdminMenu` is enabled. Service: `IAdminMenuSchemaService`. `LinkAdminNode` and `PlaceholderAdminNode` are always described; `ContentTypesAdminNode` needs `OrchardCore.Contents`; `ListsAdminNode` needs `OrchardCore.Lists`.

Shared node members: `$type`, `UniqueId`, `Enabled`, `Position`, `Priority`, `LinkToFirstChild`, `LocalNav`, `Culture`, `Classes`, `MenuName`, and the recursive `Items` array.

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu;
using Json.Schema;

namespace MyModule.Schemas;

public sealed class WeatherAdminNodeSchema : AdminNodeSchemaDefinitionBase
{
    public override string Name => "WeatherAdminNode";

    public override string TypeDiscriminator => "MyModule.AdminNodes.WeatherAdminNode, MyModule";

    protected override string DisplayText => "Weather";

    protected override IEnumerable<string> RequiredProperties => ["City"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(AdminNodeSchemaContext context)
    {
        yield return ("City", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The city shown."));
    }
}
```

Register with `services.AddAdminNodeSchema<WeatherAdminNodeSchema>();`. `Name` is the node type name; `TypeDiscriminator` is its `Namespace.Type, Assembly`.

## Query sources (`Queries`)

Registered when `OrchardCore.Queries` is enabled. Service: `IQuerySchemaService`. `Sql` needs `OrchardCore.Queries.Sql`, `Lucene` needs `OrchardCore.Lucene`, `Elasticsearch` needs `OrchardCore.Elasticsearch`.

Shared members: `Name` (unique query name), `Source` (discriminator), `Schema`, `ReturnContentItems`. Source members are flattened onto the query object, not nested. Queries have no `$type`.

```json
{
  "steps": [
    {
      "name": "Queries",
      "Queries": [
        {
          "Name": "RecentArticles",
          "Source": "Sql",
          "Template": "SELECT * FROM ContentItemIndex WHERE ContentType = 'Article'",
          "ReturnContentItems": true
        }
      ]
    }
  ]
}
```

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Queries;
using Json.Schema;

namespace MyModule.Schemas;

public sealed class WeatherQuerySourceSchema : QuerySourceSchemaDefinitionBase
{
    public override string Name => "Weather";

    protected override IEnumerable<string> RequiredProperties => ["City"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(QuerySourceSchemaContext context)
    {
        yield return ("City", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The city the query returns."));
    }
}
```

Register with `services.AddQuerySourceSchema<WeatherQuerySourceSchema>();`. `Name` must equal the serialized `Source` value.

## URL rewrite rules (`UrlRewriting`)

Registered when `OrchardCore.UrlRewriting` is enabled, with the built-in `Redirect` and `Rewrite` sources. Service: `IRewriteRuleSchemaService`.

Shared members: `Id` (empty to create), `Name`, `Source` (discriminator), `Order`. Source members are flattened onto the rule. `Redirect` carries `Pattern`, `SubstitutionPattern`, `RedirectType`; `Rewrite` carries `Pattern`, `SubstitutionPattern`, `SkipFurtherRules`.

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.UrlRewriting;
using Json.Schema;

namespace MyModule.Schemas;

public sealed class GeoRedirectRuleSourceSchema : RewriteRuleSourceSchemaDefinitionBase
{
    public override string Name => "GeoRedirect";

    protected override IEnumerable<string> RequiredProperties => ["Country"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RewriteRuleSourceSchemaContext context)
    {
        yield return ("Country", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The ISO country code matched."));
    }
}
```

Register with `services.AddRewriteRuleSourceSchema<GeoRedirectRuleSourceSchema>();`.

## Placement node filters (`Placements`)

Registered when `OrchardCore.Placements` is enabled, with the built-in `path` filter; `contentType` and `contentPart` need `OrchardCore.Contents`. Service: `IPlacementSchemaService`. Each built-in filter accepts a string or an array of strings.

Shared node members: `place` (required, such as `Content:1` or `-`), `displayType`, `differentiator`, `shape`, `alternates`, `wrappers`.

```json
{
  "steps": [
    {
      "name": "Placements",
      "Placements": {
        "TextField": [
          {
            "place": "Content:2",
            "contentType": [ "Article" ],
            "path": "/blog"
          }
        ]
      }
    }
  ]
}
```

A filter returns a value schema instead of property definitions and is keyed by `Key`, which must equal `IPlacementNodeFilterProvider.Key`:

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Placements;
using Json.Schema;

namespace MyModule.Schemas;

public sealed class CulturePlacementNodeFilterSchema : PlacementNodeFilterSchemaDefinitionBase
{
    public override string Key => "culture";

    protected override string DisplayText => "Culture";

    protected override JsonSchemaBuilder GetValueSchema(PlacementNodeFilterSchemaContext context)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The culture name that activates the placement, such as en-US.");
}
```

Register with `services.AddPlacementNodeFilterSchema<CulturePlacementNodeFilterSchema>();`.
