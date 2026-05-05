# Search Provider Examples

## Example 1: Minimal OpenSearch Provider Structure

This is the minimum structure for a new provider module that follows the current Elasticsearch pattern.

### Manifest

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "OpenSearch",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion
)]

[assembly: Feature(
    Id = "OrchardCore.OpenSearch",
    Name = "OpenSearch",
    Description = "Creates OpenSearch indexes to support search scenarios.",
    Dependencies =
    [
        "OrchardCore.Queries.Core",
        "OrchardCore.Indexing",
        "OrchardCore.ContentTypes",
    ],
    Category = "Search"
)]
```

### Startup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;
using OrchardCore.OpenSearch.Core.Handlers;
using OrchardCore.OpenSearch.Core.Models;
using OrchardCore.OpenSearch.Core.Services;
using OrchardCore.OpenSearch.Drivers;
using OrchardCore.OpenSearch.Services;
using OrchardCore.Queries;
using OrchardCore.Search;

namespace OrchardCore.OpenSearch;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<OpenSearchConnectionOptions>, OpenSearchConnectionOptionsConfigurations>();
        services.AddTransient<IOpenSearchClientFactory, OpenSearchClientFactory>();

        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IOpenSearchClientFactory>();
            var options = sp.GetRequiredService<IOptions<OpenSearchConnectionOptions>>().Value;

            return factory.Create(options);
        });

        services.AddOpenSearchServices();
        services.AddDisplayDriver<Query, OpenSearchQueryDisplayDriver>();
        services.AddScoped<IQueryHandler, OpenSearchQueryHandler>();
        services.AddDisplayDriver<IndexProfile, OpenSearchIndexProfileDisplayDriver>();
        services.AddIndexProfileHandler<OpenSearchIndexProfileHandler>();
    }
}

[RequireFeatures("OrchardCore.Search")]
public sealed class SearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSearchService<OpenSearchService>(OpenSearchConstants.ProviderName);
    }
}
```

## Example 2: Content Indexing Integration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Data.Migration;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;
using OrchardCore.OpenSearch.Core.Handlers;

namespace OrchardCore.OpenSearch;

[RequireFeatures("OrchardCore.Contents")]
public sealed class ContentsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentsStartup(IStringLocalizer<ContentsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<IndexingMigrations>();

        services
            .AddIndexProfileHandler<OpenSearchContentIndexProfileHandler>()
            .AddOpenSearchIndexingSource(IndexingConstants.ContentsIndexSource, o =>
            {
                o.DisplayName = S["Content in OpenSearch"];
                o.Description = S["Create an OpenSearch index based on site contents."];
            });
    }
}
```

## Example 3: Provider-Specific Index Profile Defaults

```csharp
using OrchardCore.Entities;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.OpenSearch.Core.Models;

namespace OrchardCore.OpenSearch.Core.Handlers;

public sealed class OpenSearchIndexProfileHandler : IndexProfileHandlerBase
{
    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => ApplyDefaultsAsync(context.Model);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => ApplyDefaultsAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => ApplyDefaultsAsync(context.Model);

    private static Task ApplyDefaultsAsync(IndexProfile indexProfile)
    {
        if (!string.Equals(indexProfile.ProviderName, OpenSearchConstants.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (!indexProfile.TryGet<OpenSearchIndexMetadata>(out var metadata))
        {
            metadata = new OpenSearchIndexMetadata();
        }

        metadata.IndexMappings ??= new OpenSearchIndexMap();
        indexProfile.Put(metadata);

        return Task.CompletedTask;
    }
}
```

## Example 4: Optional Admin Actions Only When Needed

Add a controller only if the provider introduces actions that the generic indexing UI does not already cover.

Good reasons to add a controller:

- `IndexInfo(string id)` to inspect raw provider index metadata
- `RunQuery(string id)` to test provider DSL queries
- provider-specific diagnostics endpoints

Do not add a controller for ordinary profile editing or reset and rebuild workflows.
