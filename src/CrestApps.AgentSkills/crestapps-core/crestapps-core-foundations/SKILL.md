---
name: crestapps-core-foundations
description: Skill for the CrestApps.Core foundation packages (CrestApps.Core.Abstractions, CrestApps.Core, CrestApps.Core.Infrastructure, CrestApps.Core.Support). Covers the CrestAppsCoreBuilder and feature builder layering, AddCoreServices, catalogs (IReadCatalog, ICatalog, INamedCatalog, ISourceCatalog, INamedSourceCatalog), catalog managers and the AddCatalogManagers pipeline, ICatalogEntryHandler and CatalogEntryHandlerBase lifecycle handlers with validation through ValidatingContext, QueryContext paging, multi-source binding sources (INamedCatalogSource, MultiSourceNamedCatalog), IStoreCommitter commit boundaries and filters, IUserAccessor for hub-safe caller resolution, IODataValidator, and shared utilities such as UniqueId, JSOptions, RedactedSecret and DataProtectionHelper. Use it when building custom catalogs, stores, handlers, or infrastructure on CrestApps.Core.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Foundations

You are a CrestApps.Core expert. Build on the framework contracts rather than on a specific store or provider.

## Package Layering

| Layer | Package | Contents |
|---|---|---|
| Abstractions | `CrestApps.Core.Abstractions` | `ExtensibleEntity`, `CatalogItem`, `SourceCatalogEntry`, `INameAwareModel`, `ISourceAwareModel`, `IDisplayTextAwareModel`, catalog and manager interfaces, handler contexts, `QueryContext`, `PageResult<T>`, `IStoreCommitter`, `IUserAccessor`, `UniqueId`, the feature builder classes |
| Core | `CrestApps.Core` | `AddCrestAppsCore(...)`, `AddCoreServices()`, `AddCatalogManagers()`, catalog managers, `CatalogEntryHandlerBase<T>`, multi-source catalogs, store-committer filters, `ODataFilterValidator`, `UserAccessor`, `JSOptions`, `DataProtectionHelper` |
| Infrastructure | `CrestApps.Core.Infrastructure.Abstractions`, `CrestApps.Core.Infrastructure` | indexing contracts (`ISearchIndexManager`, `IIndexProfileHandler`), `DataSourceConstants`, `DocumentIndexConstants`, `RedactedSecret`, `DictionaryExtensions` |
| Utilities | `CrestApps.Core.Support` | string, enum, date and JSON helpers |

`AddCrestAppsCore(...)` creates the root `CrestAppsCoreBuilder` and registers `ExtensibleEntityJsonOptions` plus the hosted service that applies them. `AddIndexingServices(...)` on the root creates `CrestAppsIndexingBuilder`. `AddAISuite(...)` creates `CrestAppsAISuiteBuilder`, which hosts the feature builders (`CrestAppsChatInteractionsBuilder`, `CrestAppsDocumentProcessingBuilder`, `CrestAppsDocumentIngestionBuilder`, `CrestAppsAIMemoryBuilder`, `CrestAppsAIPersonasBuilder`, `CrestAppsAIToolInstancesBuilder`, `CrestAppsA2AClientBuilder`, `CrestAppsMcpClientBuilder`, `CrestAppsMcpServerBuilder`). Every builder method maps to an `IServiceCollection` extension such as `AddCoreAIServices()`, so a host can still call the lower-level methods directly.

`AddCoreServices()` registers scoped `IODataValidator` (`ODataFilterValidator`), singleton `IHttpContextAccessor` and singleton `IUserAccessor` (`UserAccessor`). `AddAISuite(...)` calls it for you.

## Catalogs and Managers

- `IReadCatalog<T>`: `FindByIdAsync`, `GetAllAsync`, `GetAsync(ids)`, `PageAsync(page, pageSize, queryContext)`.
- `ICatalog<T>`: adds `CreateAsync`, `UpdateAsync`, `DeleteAsync`.
- `INamedCatalog<T>`: adds `FindByNameAsync`. `ISourceCatalog<T>`: adds `GetAsync(source)`. `INamedSourceCatalog<T>`: adds `GetAsync(name, source)`.
- Every member takes an optional `CancellationToken`.
- Resolve a catalog for raw persistence. Resolve a manager (`ICatalogManager<T>`, `INamedCatalogManager<T>`, `ISourceCatalogManager<T>`, `INamedSourceCatalogManager<T>`) when handlers must run. `AddCatalogManagers()` registers the open-generic managers; both store packages call it.
- Managers run handlers in this order: `NewAsync` runs `Initializing` then `Initialized` and assigns `ItemId` with `UniqueId.GenerateId()`; `ValidateAsync` runs `Validating` then `Validated`; `CreateAsync`, `UpdateAsync` and `DeleteAsync` run their before and after events around the catalog call; reads run `Loaded`.
- `CreateAsync` and `UpdateAsync` do **not** validate. Call `ValidateAsync` first and stop when `Succeeded` is false.
- Handler exceptions are caught and logged by `HandlerExtensions.InvokeAsync`, so throwing in a handler never blocks a save. Report problems through `ValidatingContext<T>.Result.Fail(...)`.
- `QueryContext` has `Name`, `Source` and `Sorted`. `PageResult<T>` has `Count` and `Entries`.

### Custom Model, Handler, and Manager

```csharp
using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace MyApp.Catalogs;

public sealed class TicketCategory : CatalogItem, INameAwareModel
{
    public string Name { get; set; }

    public string Description { get; set; }
}

public sealed class TicketCategoryHandler : CatalogEntryHandlerBase<TicketCategory>
{
    private readonly INamedCatalog<TicketCategory> _catalog;

    public TicketCategoryHandler(INamedCatalog<TicketCategory> catalog)
    {
        _catalog = catalog;
    }

    public override async Task ValidatingAsync(
        ValidatingContext<TicketCategory> context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult("A name is required.", [nameof(TicketCategory.Name)]));

            return;
        }

        var existing = await _catalog.FindByNameAsync(context.Model.Name, cancellationToken);

        if (existing is not null && existing.ItemId != context.Model.ItemId)
        {
            context.Result.Fail(new ValidationResult("The name is already used.", [nameof(TicketCategory.Name)]));
        }
    }
}

public sealed class TicketCategoryService
{
    private readonly INamedCatalogManager<TicketCategory> _manager;

    public TicketCategoryService(INamedCatalogManager<TicketCategory> manager)
    {
        _manager = manager;
    }

    public async Task<ValidationResultDetails> CreateAsync(
        string name,
        string description,
        CancellationToken cancellationToken = default)
    {
        var category = await _manager.NewAsync(name, cancellationToken: cancellationToken);
        category.Description = description;

        var result = await _manager.ValidateAsync(category, cancellationToken);

        if (result.Succeeded)
        {
            await _manager.CreateAsync(category, cancellationToken);
        }

        return result;
    }
}
```

Register the catalog with the store package the host uses, then the handler and service:

```csharp
using CrestApps.Core;
using CrestApps.Core.Data.EntityCore.Services;

// Entity Framework Core (CrestApps.Core.Data.EntityCore)
builder.Services.AddNamedDocumentCatalog<TicketCategory, NamedDocumentCatalog<TicketCategory>>();

// or YesSql (CrestApps.Core.Data.YesSql); also register the index provider and create its table
// builder.Services.AddYesSqlNamedDocumentCatalog<TicketCategory, TicketCategoryIndex>("App");

builder.Services.AddScoped<ICatalogEntryHandler<TicketCategory>, TicketCategoryHandler>();
builder.Services.AddScoped<TicketCategoryService>();
```

`AddDocumentCatalog`, `AddNamedDocumentCatalog`, `AddSourceDocumentCatalog` and `AddNamedSourceDocumentCatalog` are defined in `CrestApps.Core.Abstractions` and take the model and the concrete catalog type. See `crestapps-core-entitycore-stores` and `crestapps-core-yessql-stores` for the backend-specific catalog types and indexes.

## Multi-Source Binding Sources

Connections and deployments merge configuration, database, and custom sources. `MultiSourceNamedCatalog<T>` and `MultiSourceNamedSourceCatalog<T>` order the registered `INamedCatalogSource<T>` or `INamedSourceCatalogSource<T>` instances by `Order`, keep the first entry for each name, and send writes to the first writable source (`IWritableNamedCatalogSource<T>`). Database sources use `Order = 0`, configuration sources `Order = 100`.

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;

namespace MyApp.Deployments;

public sealed class PlatformDeploymentSource : INamedSourceCatalogSource<AIDeployment>
{
    private readonly IPlatformDeploymentClient _client;

    public PlatformDeploymentSource(IPlatformDeploymentClient client)
    {
        _client = client;
    }

    public int Order => 50;

    public async ValueTask<IReadOnlyCollection<AIDeployment>> GetEntriesAsync(
        IReadOnlyCollection<AIDeployment> knownEntries,
        CancellationToken cancellationToken = default)
    {
        var knownNames = knownEntries
            .Select(entry => entry.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var deployments = await _client.GetDeploymentsAsync(cancellationToken);

        return deployments
            .Where(deployment => !knownNames.Contains(deployment.Name))
            .ToArray();
    }
}
```

```csharp
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Scoped<INamedSourceCatalogSource<AIDeployment>, PlatformDeploymentSource>());
```

`WritableCatalogBindingSource<T>` and `WritableNamedCatalogBindingSource<T>` wrap an existing catalog as an `Order = 0` writable source; the store packages use them in `AddEntityCoreNamedSourceBindingSource<T>()` and `AddYesSqlNamedSourceBindingSource<T, TIndex>()`.

## Commit Boundaries

The first-party stores stage writes and flush them through scoped `IStoreCommitter.CommitAsync()`.

- MVC: `AddControllersWithViews().AddCrestAppsStoreCommitterFilter()` commits after each action that did not throw.
- Minimal APIs: `.AddEndpointFilter<StoreCommitterEndpointFilter>()` (namespace `CrestApps.Core.Filters`) commits after the handler.
- SignalR: `AddSignalR().AddCrestAppsStoreCommitterFilter()`, or `.AddSignalR(addStoreCommitterFilter: true)` on the AI suite builder.
- Background work: create a scope, write, then call `CommitAsync()` on that scope's `IStoreCommitter`.
- The filters resolve `IStoreCommitter` from DI. `NoOpStoreCommitter` is internal and is never registered, so register a store package (or your own scoped committer) before adding a filter.

## Caller Identity

Resolve the caller through `IUserAccessor.User`, not `IHttpContextAccessor`. SignalR hub methods run outside the request pipeline, so `HttpContext` is often null there. The default `UserAccessor` returns the principal a hub assigned for the current invocation and falls back to the HTTP request principal. A null `User` means a trusted server-side invocation (for example a background task), not an anonymous caller.

## OData Filters

Validate user-supplied filters with `IODataValidator.IsValidFilter(filter)` before handing them to an `IODataFilterTranslator`. The validator checks syntax only; the search backend performs full validation. Replace it with `services.AddScoped<IODataValidator, MyValidator>()` for stricter rules such as a field allow-list.

## Utilities

- `UniqueId.GenerateId()` and `UniqueId.IsValid(id)` create and check the 26-character item identifiers.
- `JSOptions.Default`, `JSOptions.CaseInsensitive` and `JSOptions.Indented` are shared read-only `JsonSerializerOptions`.
- `JsonExtensions.FromObject(...)` and the `Clone()` helpers copy property bags and `JsonObject` values.
- `DataProtectionHelper.Unprotect(protector, value, logger, message)` unprotects a secret and logs instead of throwing.
- `RedactedSecret` keeps secrets out of logs and UI output.
- `DataSourceConstants.ColumnNames` names the data-source knowledge index fields and `DocumentIndexConstants.ColumnNames` the uploaded-document chunk fields; they are different index schemas.

## Upgrading from 1.x

- Every type now lives in a file named after it (226 types moved, for example each YesSql `*Index` and `*IndexProvider`). Namespaces and members did not change, so code that compiles against 1.x still compiles.
- Feature builders added in 2.0 include `CrestAppsDocumentIngestionBuilder` and `CrestAppsAIPersonasBuilder`.
