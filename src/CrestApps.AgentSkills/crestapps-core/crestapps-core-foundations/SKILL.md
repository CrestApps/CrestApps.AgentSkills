---
name: crestapps-core-foundations
description: Skill for using CrestApps.Core catalogs lifecycle handlers OData validation builders infrastructure and package layering.
---

# CrestApps.Core Foundations - Prompt Templates

## Build on Core Abstractions

You are a CrestApps.Core expert. Prefer framework contracts over provider-specific implementations.

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai.AddOpenAI())
);
```

`AddCrestAppsCore(...)` creates the root `CrestAppsCoreBuilder`. `AddIndexingServices(...)` creates `CrestAppsIndexingBuilder`. Feature packages add their own builders beneath this root; keep application hosts at the top, core abstractions and infrastructure underneath, then AI features, providers, search backends, and storage implementations.

## Catalogs and Lifecycle

- Use `IReadCatalog<T>` for reads and `ICatalog<T>` for CRUD.
- Use `INamedCatalog<T>`, `ISourceCatalog<T>`, or `INamedSourceCatalog<T>` only when the model supports name and/or source lookup.
- Use `ICatalogManager<T>`, `INamedCatalogManager<T>`, `ISourceCatalogManager<T>`, or `INamedSourceCatalogManager<T>` to apply lifecycle handling around a catalog.
- Register `ICatalogEntryHandler<T>` implementations for initializing, initialized, loaded, validating, validated, creating, created, updating, updated, deleting, and deleted events.

`CatalogManagerBase<T>` performs the handler pipeline. `CatalogManager<T>`, `NamedCatalogManager<T>`, `SourceCatalogManager<T>`, and `NamedSourceCatalogManager<T>` supply the matching lookup model. `AddCatalogManagers()` registers these manager families.

Use `INamedCatalogSource<T>`, `INamedSourceCatalogSource<T>`, `IWritableNamedCatalogSource<T>`, and `IWritableNamedSourceCatalogSource<T>` for multi-source bindings. `WritableCatalogBindingSource<T>` and `WritableNamedCatalogBindingSource<T>` are the built-in writable adapters.

## OData Filters and Commit Boundaries

`AddCoreServices()` registers scoped `IODataValidator` as `ODataFilterValidator`. Use `IsValidFilter(filter)` before passing a user-provided filter to an `IODataFilterTranslator`. The validator is deliberately basic syntax validation; the selected backend performs full query validation.

```csharp
if (!oDataValidator.IsValidFilter(filter))
{
    throw new ArgumentException("The OData filter is invalid.", nameof(filter));
}
```

Storage providers that stage writes implement `IStoreCommitter`. Add `AddCrestAppsStoreCommitterFilter()` after `AddControllersWithViews()`, use `StoreCommitterEndpointFilter` with Minimal API groups, or enable the SignalR overload on `ISignalRServerBuilder`. The default `NoOpStoreCommitter` allows hosts without staged storage to use the shared pipeline.

## Infrastructure Utilities

Use `DataSourceConstants` and `DocumentIndexConstants` instead of duplicated provider strings. `RedactedSecret` represents values that must not appear in logs or UI output. `DictionaryExtensions` provides shared dictionary helpers. `DataProtectionHelper` is the core helper for protected-data operations. `ExtensibleEntityJsonOptionsInitializer` configures extensible-entity JSON options at host startup.
