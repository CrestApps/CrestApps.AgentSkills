---
name: crestapps-core-tool-instances
description: Skill for user-configured AI tool instances in CrestApps.Core 2.0. Covers AddToolInstances with YesSql or EntityCore stores, authoring an IAIToolInstanceSource and registering it with AddSource, AIToolInstance names, descriptions and the tool_instance_ function prefix, attaching instances with AIToolInstanceMetadata, declared parameters with AIToolInstanceParameterCapabilities, AIToolParameterBindingOption, Model, Fixed and Context fills, AIToolParameterBinder, AIToolParameterSchemaBuilder and IAIToolParameterContextResolver, the built-in HTTP API request source, documentation search sources (sitemap, search index, Algolia and live website search) with DocumentationSearchOptions, the data source search source with multi-phrase Reciprocal Rank Fusion, and per-instance authorization by subclassing ToolInstanceRegistryProvider. Use it when letting users configure callable tools, exposing several knowledge bases or documentation sites to the model, or gating instances per user.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Tool Instances

A developer writes a **source** (`IAIToolInstanceSource`) once; users create many **instances** (`AIToolInstance`) of it, each with its own name, description and settings. Every instance reaches the model as a distinct function, so the model only decides when to call it.

## Guidelines

- `AddToolInstances(...)` registers the catalog manager, the completion handler and the default `IAIToolParameterContextResolver`; with `useDefaultRegistry: true` (default) it also registers `ToolInstanceRegistryProvider`.
- Register a store on the tool-instances builder (`AddYesSqlStores()` or `AddEntityCoreStores()`); the feature does not pick one. YesSql hosts create the table with `schemaBuilder.CreateAIToolInstanceIndexSchemaAsync(yesSqlStoreOptions)`.
- An instance's `Name` is its stable lookup key and is fixed after creation. Its function name is `instance.GetFunctionName()`: `tool_instance_` plus a provider-safe version of the name, capped at 64 characters, with a deterministic hash when normalization is lossy, so it never collides with an `AddCoreAITool` name.
- Write a specific `Description`; it is the model's main signal for choosing between instances.
- Protect secrets in settings with ASP.NET Core Data Protection and never expose them as model arguments. Use `Fixed` or `Context` parameter fills for values the model must not choose.

## Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddYesSqlStores()
        .AddToolInstances(toolInstances => toolInstances
            .AddYesSqlStores()
            .AddHttpApiRequestSource()
            .AddDocumentationSearchSources()
            .AddDataSourceSearchSource()
            .AddSource<OrderStatusToolInstanceSource>("order-status", entry =>
            {
                entry.DisplayName = new LocalizedString("order-status", "Order status");
                entry.Description = new LocalizedString("order-status", "Looks up orders in a configured order system.");
                entry.Category = new LocalizedString("Integrations", "Integrations");
            }))));
```

`AddSource<TSource>(name, configure)` registers a keyed scoped `IAIToolInstanceSource` under `name` (the value stored in each instance's `Source`) and records `AIToolInstanceSourceEntry` metadata (`DisplayName`, `Description`, `Category`, `Parameters`). An empty display name defaults to the source name.

## Implement a Source

```csharp
using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.AI;

public sealed class OrderStatusToolInstanceSource : IAIToolInstanceSource
{
    public AITool CreateTool(AIToolInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var settings = instance.TryGet<OrderStatusToolSettings>(out var stored)
            ? stored
            : new OrderStatusToolSettings();

        var functionName = instance.GetFunctionName();
        var description = string.IsNullOrWhiteSpace(instance.Description) ? functionName : instance.Description;

        return new OrderStatusToolFunction(functionName, description, settings, instance);
    }
}

public sealed class OrderStatusToolSettings
{
    public string BaseUrl { get; set; }

    public string ProtectedApiKey { get; set; }
}
```

`OrderStatusToolFunction` is the application's `AIFunction`; a full parameter-aware version is in [tool-instance-parameters-examples.md](references/tool-instance-parameters-examples.md). Save settings with `instance.Put(new OrderStatusToolSettings { ... })`.

## Attach Instances

```csharp
profile.Alter<AIToolInstanceMetadata>(metadata =>
{
    metadata.ToolInstanceNames = ["orders-eu", "weather-api"];
});
```

The same works on a `ChatInteraction`. `AIToolInstanceCompletionContextBuilderHandler` copies the names onto `AICompletionContext.ToolInstanceNames`, and the registry provider materializes each one, so every orchestrator honors them.

## Parameters

Parameters let whoever configures an instance declare typed, described arguments instead of an untyped bag. Each parameter (`AIToolInstanceParameter`) has a `Name`, `Description`, `Type` (`String`, `Integer`, `Number`, `Boolean`), `Required`, `DefaultValue`, `AllowedValues`, `IsSecret`, a **fill** and a **binding**:

| `AIToolParameterFill` | In the schema | Value from |
|---|---|---|
| `Model` | Yes | The model, or the default when omitted |
| `Fixed` | No | A value pinned on the instance |
| `Context` | No | An `IAIToolParameterContextResolver` key such as `user.id`, `user.name`, `user.email`, `resource.id`, `now.utc` |

Parameter support is opt-in per source. A source advertises what it can honor with `AIToolInstanceSourceEntry.Parameters`; a source without capabilities hides the editor and rejects saved parameters, so a parameter can never be shown to the model and then dropped.

```csharp
.AddSource<OrderStatusToolInstanceSource>("order-status", entry =>
{
    entry.DisplayName = new LocalizedString("order-status", "Order status");
    entry.Parameters = new AIToolInstanceParameterCapabilities
    {
        ReservedNames = ["query"],
        Bindings =
        [
            new AIToolParameterBindingOption("Argument")
            {
                DisplayName = new LocalizedString("Argument", "Call argument"),
            },
            new AIToolParameterBindingOption("Header")
            {
                DisplayName = new LocalizedString("Header", "Request header"),
                AllowedFills = [AIToolParameterFill.Fixed, AIToolParameterFill.Context],
            },
        ],
    };
});
```

Then consume them in the function: `AIToolParameterBinder.GetParameters(instance)`, `AIToolParameterSchemaBuilder.Merge(baseSchema, parameters)` for the schema (only model-filled parameters appear), and `AIToolParameterBinder.Resolve(parameters, arguments, services, unprotect)` at invocation, which applies precedence, coercion, allowed values and defaults and returns errors to report to the model. Declaring without consuming silently drops values; test both halves.

Register an `IAIToolParameterContextResolver` (`SupportedKeys` plus `TryResolve(contextKey, services, out value)`) to add context keys; resolvers are tried in registration order.

## Built-in Sources

| Registration | Source name | What an instance does |
|---|---|---|
| `AddHttpApiRequestSource()` | `http-api-request` | Calls one HTTP API with stored auth (`None`, `ApiKey`, `Bearer`, `Basic`, `OAuth2` with cached tokens), headers, `PathTemplate` and parameter bindings `Query:<key>`, `Path`, `Body:<dotted.path>`, `Header:<name>` (model fills refused for headers) |
| `AddSitemapDocumentationSource()` | `sitemap-documentation` | Crawls a site's sitemap (index, gzip, RSS/Atom, text, `robots.txt` discovery) and ranks pages locally |
| `AddSearchIndexDocumentationSource()` | `search-index-documentation` | Downloads a prebuilt JSON search index and ranks it locally |
| `AddAlgoliaDocumentationSource()` | `algolia-documentation` | Queries Algolia DocSearch |
| `AddWebsiteSearchSource()` | `website-search` | Queries a site's own search API live (WordPress REST by default; paths overridable) |
| `AddDataSourceSearchSource()` | `data-source-search` | Vector search over one AI data source |

`AddDocumentationSearchSources()` adds all four documentation sources. `DocumentationSearchOptions` sets `MaxResultsPerSite` (5), `MaxPagesPerSite` (200), `MaxConcurrentRequests` (4), `CacheDuration` (1 hour) and `FirstSearchWaitBudget` (8 seconds): the first search on an unindexed site builds the corpus in the background and answers "still indexing" after the budget instead of blocking. Empty crawls are cached for only 5 minutes, and empty results tell the model rewording will not help.

`DataSourceSearchToolSettings` holds `DataSourceId`, `RetrievalMode` (`DataSourceRetrievalMode.Chunk` or `Hierarchical`), `TopNDocuments`, `Strictness` and an OData `Filter`. Queries use the embedding deployment the index was built with. One call may carry up to three phrases; they are embedded in one batch, searched in parallel and merged with Reciprocal Rank Fusion under shared `[doc:N]` citations. `Hierarchical` returns whole source documents and degrades to chunks when they cannot be read.

## Gate Instances Per User

`ToolInstanceRegistryProvider` is public. Subclass it and override `ShouldIncludeInstanceAsync`:

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

public sealed class PermissionAwareToolInstanceRegistryProvider : ToolInstanceRegistryProvider
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserAccessor _userAccessor;

    public PermissionAwareToolInstanceRegistryProvider(
        IServiceProvider serviceProvider,
        ILogger<ToolInstanceRegistryProvider> logger,
        IAuthorizationService authorizationService,
        IUserAccessor userAccessor)
        : base(serviceProvider, logger)
    {
        _authorizationService = authorizationService;
        _userAccessor = userAccessor;
    }

    protected override async ValueTask<bool> ShouldIncludeInstanceAsync(
        AIToolInstance instance,
        AICompletionContext context,
        CancellationToken cancellationToken)
    {
        var user = _userAccessor.User;

        if (user is null)
        {
            return true;
        }

        var result = await _authorizationService.AuthorizeAsync(user, instance, "UseToolInstance");

        return result.Succeeded;
    }
}
```

```csharp
ai.AddToolInstances(toolInstances => toolInstances
    .AddYesSqlStores()
    .AddHttpApiRequestSource(),
    useDefaultRegistry: false);

builder.Services.AddScoped<IToolRegistryProvider, PermissionAwareToolInstanceRegistryProvider>();
```

Keep `useDefaultRegistry: true` only when a custom provider is additive. The provider skips unknown sources with a warning and catches exceptions from `CreateTool`.

## Upgrading from 1.x

- `ToolInstanceRegistryProvider`'s constructor takes `(IServiceProvider, ILogger<ToolInstanceRegistryProvider>)` and resolves `INamedCatalog<AIToolInstance>` lazily; subclasses written against samples that passed the catalog must change their base call.
- Instances that declare no parameters keep a byte-identical schema. Parameters are new and opt-in per source.
- New sources in 2.0: `AddDataSourceSearchSource()` and the live `AddWebsiteSearchSource()`. The sitemap source now follows sitemap indexes, gzip, feeds and `robots.txt`, and no longer blocks on a slow first crawl.
