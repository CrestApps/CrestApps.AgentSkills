---
name: orchardcore-dnc-registry
description: Skill for configuring national and local do-not-call registry checks in CrestApps Orchard Core including Azure Blob Storage for uploaded local lists. Covers import suppression, provider settings, E.164 normalization, CSV processing, registry extensions, and tenant-aware Azure storage. Use this skill when requests mention Orchard Core do-not-call, DNC, telemarketing compliance, local DNC CSV files, or DNC Azure Blob Storage. Strong matches include work with CrestApps.OrchardCore.DncRegistry, INationalDoNotCallRegistry, LocalDncRegistry, ILocalDncListManager, NumberSearchContext, and DncRegistryBlobStorageOptions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core DNC Registry - Prompt Templates

## Configure Do-Not-Call Suppression

You are an Orchard Core expert. Generate accurate compliance-focused configuration and code for CrestApps DNC Registry modules. The feature checks selected national registries and maintains local suppression lists. The optional Azure module changes local-list file storage from the tenant file system to Azure Blob Storage.

### Guidelines

- Install `CrestApps.OrchardCore.DncRegistry` in the web/startup project.
- Enable `CrestApps.OrchardCore.DncRegistry` before provider features.
- Install and enable `CrestApps.OrchardCore.DncRegistry.Azure` only when Azure Blob Storage should back local DNC uploaded files.
- The core feature depends on `CrestApps.OrchardCore.PhoneNumbers`.
- Normalize list numbers to canonical `PhoneNumber` values using `IPhoneNumberService.TryParse`; do not compare raw display strings.
- Treat registry hits as suppression decisions and retain appropriate audit/export information for skipped import rows.
- Configure external registry credentials only in protected tenant settings.
- Use `INationalDoNotCallRegistry` to add providers and return only input numbers reported as registered.
- Use `NumberSearchContext.CountryCode` to narrow country-aware lookups.
- Do not claim that Azure stores DNC query indexes; it stores uploaded source files, while local indexes remain managed by the local DNC feature.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

### Feature Overview

| Feature | Feature ID | Purpose |
|---|---|---|
| DNC Registry | `CrestApps.OrchardCore.DncRegistry` | Common settings, permissions, and national-registry framework |
| USA FTC | `CrestApps.OrchardCore.DncRegistry.UsaFtc` | FTC national Do Not Call integration |
| Canada LNNTE-DNCL | `CrestApps.OrchardCore.DncRegistry.CanadaDncl` | Canadian national list integration |
| Local DNC Registry | `CrestApps.OrchardCore.DncRegistry.Local` | CSV upload, background import, YesSql records, and local suppression |
| Azure Blob backend | module dependency on `CrestApps.OrchardCore.DncRegistry.Local` | Replaces local source-file storage with Azure Blob Storage |

### Enable Local DNC Lists

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.DncRegistry",
        "CrestApps.OrchardCore.DncRegistry.Local"
      ],
      "disable": []
    }
  ]
}
```

Enable national features separately when their tenant credentials are available. The local feature is the only built-in feature that imports uploaded list files.

## Registry Model

Each registry implements `INationalDoNotCallRegistry`:

| Member | Purpose |
|---|---|
| `Key` | Stable registry identifier |
| `DisplayName` | User-facing registry name |
| `Description` | User-facing explanation |
| `GetRegisteredNumbersAsync(IEnumerable<PhoneNumber>)` | Returns the canonical input subset listed by the registry |
| `GetRegisteredNumbersAsync(IEnumerable<PhoneNumber>, NumberSearchContext)` | Supports country-aware filtering |

The default context overload delegates to the basic method, so only providers that need country filtering must override it.

```csharp
var context = new NumberSearchContext
{
    CountryCode = "US",
};

var suppressedNumbers = await registry.GetRegisteredNumbersAsync(phoneNumbers, context, cancellationToken);
```

Do not use an untrusted UI display name as a registry key. Persist and configure stable keys.

## Import Suppression

Under **Settings → Import Content Settings**, configure global enforcement and choose registry keys that must always run. When Omnichannel Management and Content Transfer are available, imports merge registries chosen by the importer with globally enforced registry keys.

Suppression checks may run across selected providers in parallel. An import should report skipped DNC rows in its error export, together with the registry or reason, instead of silently dropping data.

The settings objects can be provisioned through Orchard Core’s generic settings recipe step:

```json
{
  "steps": [
    {
      "name": "settings",
      "DncRegistrySettings": {
        "EnforceGlobally": true,
        "EnforcedRegistryKeys": [
          "usa-ftc-dnc",
          "canada-lnnte-dncl"
        ]
      },
      "UsaFtcDncRegistrySettings": {
        "OrganizationId": "example-org",
        "BaseUrl": "https://telemarketing.donotcall.gov/api/"
      }
    }
  ]
}
```

Set protected API keys through secure administration or an approved secret source. Do not place `ProtectedApiKey` values in source-controlled recipes.

## External Registry Providers

| Provider | Configuration location |
|---|---|
| USA FTC | **Settings → DNC Registries → USA FTC Registry** |
| Canada LNNTE-DNCL | **Settings → DNC Registries → Canada LNNTE-DNCL Registry** |

The USA setting supplies organization id, base URL, and a protected API key. The Canada setting supplies account number, base URL, and a protected API key. Consult the official provider onboarding process for current service access and contract requirements.

## Local DNC Registry

Enable `CrestApps.OrchardCore.DncRegistry.Local`, then use **Interaction Center → Local DNC Registry** to view and delete lists or upload a replacement. The feature registers:

- `LocalDncRegistry` as an `INationalDoNotCallRegistry` with the stable key `local-dnc`.
- `ILocalDncListManager` through `DefaultLocalDncListManager`.
- `ILocalDncFileStore` for uploaded source files.
- `LocalDncImportBackgroundTask` for deferred import.
- `LocalDncListIndex` and `LocalDncEntryIndex` for query performance.

Each `LocalDncList` tracks its country, stored source file, totals, progress, imported count, error messages, status, and timestamps. Each `LocalDncEntry` stores list id, country, and normalized phone number.

### CSV Requirements

Supply one phone number per row in one populated column:

```csv
555-123-4567
(555) 234-5678
5559876543
```

During upload, select the country used as the parsing region for local numbers. The importer skips blank rows, header rows, duplicates in the same file, invalid numbers, and multi-column data. It queues work rather than importing in the upload request.

The list state can be `Pending`, `Processing`, `Paused`, `Completed`, `Failed`, or `Deleting`. Display progress from `TotalRecords`, `TotalProcessed`, `ImportedCount`, and error data instead of assuming the upload response means the list is ready.

### Local Lookup

`LocalDncRegistry` first normalizes input through `IPhoneNumberService`, then queries `LocalDncEntryIndex` by E.164 phone number and optional country. It returns a hit only when the matching source list completed. Do not manually query storage files at call time.

## Azure Blob Storage Backend

Install `CrestApps.OrchardCore.DncRegistry.Azure` in the web/startup project and enable its module after the Local DNC feature. It reads the tenant configuration section:

```json
{
  "CrestApps": {
    "DncRegistry": {
      "AzureBlobStorage": {
        "ConnectionString": "Use a secure configuration provider",
        "ContainerName": "tenant-dnc",
        "BasePath": "dnc"
      }
    }
  }
}
```

`DncRegistryBlobStorageOptions` derives from `MediaBlobStorageOptionsBase`. The backend requires a non-empty `ConnectionString` and `ContainerName`. When either is absent, it logs an error and leaves the normal local file-store registration intact.

With valid options it replaces `ILocalDncFileStore` with a `BlobFileStore`-backed `LocalDncFileStore`. `DncRegistryBlobContainerTenantEvents` creates a private container during tenant activation and removes the container or configured base-path files during tenant removal according to `CreateContainer`, `RemoveContainer`, and `RemoveFilesFromBasePath`.

Keep the connection string in user secrets, environment variables, Key Vault, or another secure configuration provider. Never put it in a checked-in appsettings file.

## Add a Registry Provider

Reference the DNC Registry abstractions, implement the interface, and register it in a dedicated feature:

```csharp
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.PhoneNumbers;

namespace MyCompany.OrchardCore.Compliance;

public sealed class MyRegistry : INationalDoNotCallRegistry
{
    public string Key => "my-registry";

    public string DisplayName => "My Registry";

    public string Description => "Checks numbers against My Registry.";

    public Task<HashSet<PhoneNumber>> GetRegisteredNumbersAsync(
        IEnumerable<PhoneNumber> phoneNumbers,
        CancellationToken cancellationToken = default)
    {
        // Submit normalized numbers and return only matched inputs.
        throw new NotImplementedException();
    }
}
```

```csharp
services.AddHttpClient(nameof(MyRegistry));
services.AddScoped<INationalDoNotCallRegistry, MyRegistry>();
```

Add site settings, a display driver, and a navigation entry when the provider requires tenant configuration. Support the `NumberSearchContext` overload when the remote API can filter by country.

## Troubleshooting

| Symptom | Check |
|---|---|
| Local registry has no hits | Verify the list reached `Completed` and that input normalizes to the same canonical `PhoneNumber` value |
| Upload returns but no entries exist | The background importer is still pending or failed; inspect list state and errors |
| Local number is rejected | Select the correct ISO region during upload |
| National feature is missing | Enable its specific USA FTC or Canada LNNTE-DNCL feature |
| Azure storage is ignored | Confirm both connection string and container name in `CrestApps:DncRegistry:AzureBlobStorage` |
| Azure tenant cleanup is unsafe | Review `RemoveContainer` and `RemoveFilesFromBasePath` before removing a tenant |
