# Indexing Source Examples

## Example 1: Provider Wrapper Extension

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing.Core;

namespace OrchardCore.OpenSearch;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenSearchIndexingSource(
        this IServiceCollection services,
        string implementationType,
        Action<IndexingOptionsEntry> action = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(implementationType);

        services.AddIndexingSource<
            OpenSearchIndexManager,
            OpenSearchDocumentIndexManager,
            OpenSearchIndexNameProvider>(
            OpenSearchConstants.ProviderName,
            implementationType,
            action);

        return services;
    }
}
```

## Example 2: Content Source Registration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Data.Migration;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;

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

## Example 3: Custom Source Registration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace Contoso.OpenSearch.Products;

public static class ProductIndexingConstants
{
    public const string IndexSource = "Products";
}

[Feature("Contoso.OpenSearch.Products")]
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddIndexProfileHandler<ProductOpenSearchIndexProfileHandler>()
            .AddOpenSearchIndexingSource(ProductIndexingConstants.IndexSource, o =>
            {
                o.DisplayName = S["Products in OpenSearch"];
                o.Description = S["Create an OpenSearch index for product records."];
            });
    }
}
```

## Example 4: Options-Gated Registration

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing.Core;
using OrchardCore.OpenSearch.Core.Models;

namespace OrchardCore.OpenSearch;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguredOpenSearchIndexingSource(
        this IServiceCollection services,
        string implementationType,
        Action<IndexingOptionsEntry> action = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(implementationType);

        services.AddIndexingSource<
            OpenSearchIndexManager,
            OpenSearchDocumentIndexManager,
            OpenSearchIndexNameProvider,
            OpenSearchConnectionOptions>(
            OpenSearchConstants.ProviderName,
            implementationType,
            action);

        return services;
    }
}
```

## Example 5: Source-Specific Index Profile Handling

```csharp
using OrchardCore.Entities;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.OpenSearch.Core.Models;

namespace Contoso.OpenSearch.Products;

public sealed class ProductOpenSearchIndexProfileHandler : IndexProfileHandlerBase
{
    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => ConfigureAsync(context.Model);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => ConfigureAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => ConfigureAsync(context.Model);

    private static Task ConfigureAsync(IndexProfile indexProfile)
    {
        if (!string.Equals(indexProfile.ProviderName, OpenSearchConstants.ProviderName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(indexProfile.Type, ProductIndexingConstants.IndexSource, StringComparison.OrdinalIgnoreCase))
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
