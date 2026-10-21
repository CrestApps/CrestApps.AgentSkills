---
name: orchardcore-dnc-registry
description: Skill for configuring national and local do-not-call registry checks in CrestApps Orchard Core, including Azure Blob Storage for uploaded local lists. Covers the DncRegistry, UsaFtc, CanadaDncl and Local features and the DncRegistry.Azure module, global import enforcement, dialer screening that fails closed with DoNotCallScreeningException, canonical PhoneNumber input, background-imported local CSV lists, tenant-aware Azure containers, and custom INationalDoNotCallRegistry providers. Use this skill when requests mention Orchard Core do-not-call, DNC, telemarketing compliance, FTC or LNNTE-DNCL registries, local DNC CSV files, DNC screening failures, or DNC Azure Blob Storage. Strong matches include CrestApps.OrchardCore.DncRegistry, INationalDoNotCallRegistry, DoNotCallScreeningException, NumberSearchContext, LocalDncRegistry, ILocalDncListManager, DncRegistrySettings, UsaFtcDncRegistrySettings, CanadaDnclRegistrySettings, and DncRegistryBlobStorageOptions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core DNC Registry - Prompt Templates

## Configure Do-Not-Call Suppression

You are an Orchard Core expert. Generate accurate compliance-focused configuration and code for the CrestApps DNC Registry modules. The module screens canonical phone numbers against national registries and locally uploaded lists. The same registries screen Omnichannel contact imports and Contact Center outbound dialing. The optional Azure module moves uploaded local-list files to Azure Blob Storage.

### Guidelines

- Install `CrestApps.OrchardCore.DncRegistry` (and `CrestApps.OrchardCore.DncRegistry.Azure` when needed) in the web/startup project.
- Enable `CrestApps.OrchardCore.DncRegistry` first; it depends on `CrestApps.OrchardCore.PhoneNumbers` (see `orchardcore-phone-numbers`).
- Pass canonical `PhoneNumber` values (from `IPhoneNumberService.TryParse`) to registries. Never hand a registry a raw string.
- A registry that cannot complete a lookup throws `DoNotCallScreeningException`; never return an empty set on failure. An empty set means "screened and not listed".
- A registry with no credentials does not take part in screening and returns an empty set without throwing. Configure only registries whose answers you rely on.
- Keep `ProtectedApiKey` values and Azure connection strings out of source-controlled recipes and settings files.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

### Feature Overview

| Feature | Feature ID | Admin location |
|---|---|---|
| DNC Registry | `CrestApps.OrchardCore.DncRegistry` | **Settings → Content Import** (global enforcement) |
| USA FTC Do Not Call Registry | `CrestApps.OrchardCore.DncRegistry.UsaFtc` | **Settings → DNC Registries → USA FTC Registry** |
| Canada LNNTE-DNCL Registry | `CrestApps.OrchardCore.DncRegistry.CanadaDncl` | **Settings → DNC Registries → Canada LNNTE-DNCL Registry** |
| Local Do Not Call Registry | `CrestApps.OrchardCore.DncRegistry.Local` | **Interaction Center → Local DNC Registry** |
| DNC Registry - Azure Blob Storage | `CrestApps.OrchardCore.DncRegistry.Azure` (depends on Local) | Configuration only |

All screens require the **Manage DNC registry settings** permission (`DncRegistryPermissions.ManageDncRegistrySettings`). Each settings group registers that permission with `services.AddSiteSettingsPermission(...)`, so a role holding only this permission can open the pages. Provider menu items appear only when the provider feature is enabled.

### Enable Registries

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.DncRegistry",
        "CrestApps.OrchardCore.DncRegistry.Local",
        "CrestApps.OrchardCore.DncRegistry.UsaFtc"
      ],
      "disable": []
    }
  ]
}
```

## Registry Keys and Settings

| Registry | Key | Settings object | Properties |
|---|---|---|---|
| USA FTC | `usa-ftc-dnc` | `UsaFtcDncRegistrySettings` | `OrganizationId`, `BaseUrl` (default `https://telemarketing.donotcall.gov/api/`), `ProtectedApiKey` |
| Canada LNNTE-DNCL | `canada-lnnte-dncl` | `CanadaDnclRegistrySettings` | `AccountNumber`, `BaseUrl` (default `https://www.lnnte-dncl.gc.ca/api/`), `ProtectedApiKey` |
| Local | `local-dnc` | none | Lists are managed in the admin |

Global enforcement lives in `DncRegistrySettings` (`EnforceGlobally`, `EnforcedRegistryKeys`). Provision it with the generic `settings` step:

```json
{
  "steps": [
    {
      "name": "settings",
      "DncRegistrySettings": {
        "EnforceGlobally": true,
        "EnforcedRegistryKeys": [
          "usa-ftc-dnc",
          "local-dnc"
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

Saved API keys are protected and never redisplayed; entering a new value replaces the key. `BaseUrl` is required when saving from the admin screen.

## Where Screening Happens

- **Omnichannel imports** (Omnichannel Management with Content Transfer): importer-selected registries are merged with globally enforced keys; checks run in parallel across providers. Matching rows are skipped (or imported marked Do not call) and listed in the error export with the reason. When a registry cannot be reached the row is skipped with that reason; the import keeps going. See `orchardcore-omnichannel-management` and `orchardcore-content-transfer`.
- **Contact Center outbound dialing**: a `DoNotCallScreeningException` suppresses the attempt with `ComplianceScreeningUnavailable`, which is not terminal, so the activity is retried in a later cycle. Extension calls skip screening because they are not consumer outreach. See `orchardcore-contact-center` and `orchardcore-telephony`.

## Local Do Not Call Registry

The Local feature registers `LocalDncRegistry` (key `local-dnc`), `ILocalDncListManager`, `ILocalDncFileStore`, `LocalDncListIndex`, `LocalDncEntryIndex`, and the **Local DNC Import Processor** background task.

- An upload saves a `LocalDncList` as **Pending** and returns immediately; rows import in the background.
- The task runs every 10 minutes. It starts pending imports, resumes **Processing** or **Deleting** lists that saved no progress for 10 minutes, and retries **Failed** imports up to 5 times with growing delays. **Process now** in the admin grants 5 more retries. A list whose file is missing is not retried.
- Stored states are `Pending`, `Processing`, `Completed`, `Failed`, `Paused`, and `Deleting`. The admin shows a completed list with rejected rows as **Completed with errors**.
- Only **Completed** lists are used for screening. Lookups query `LocalDncEntryIndex` by E.164 number and optional country.
- Files live under `App_Data/Sites/{tenant}/DncRegistry` unless Azure storage is enabled.
- Import and deletion are paced by the shared `CrestApps:BackgroundWork:Pacing` settings (see `orchardcore-content-transfer`).

CSV files hold one phone number per row in a single column. The importer skips blank rows, a header row, duplicates in the file and invalid numbers, rejects multi-column rows, and parses numbers without `+` using the country chosen at upload. Rejected rows with reasons are downloadable as CSV.

```csv
555-123-4567
(555) 234-5678
5559876543
```

### Admin list

The **Local DNC Registry** page renders through Orchard Core's `AdminList` shape (list name `LocalDncLists`, row model `LocalDncList`, grid columns `Name`, `Status` and `Actions`). Customize rows with `LocalDncList_SummaryAdmin` placement for the List layout and `SummaryAdminGrid` placement for the Grid layout, or add columns with an `IAdminListColumnsProvider` for `LocalDncLists`. See `orchardcore-admin-lists`.

## Azure Blob Storage Backend

Configure `CrestApps:DncRegistry:AzureBlobStorage` under `OrchardCore` in the root `appsettings.json` (a tenant's own `App_Data/Sites/{tenant}/appsettings.json` starts at `CrestApps`):

```json
{
  "OrchardCore": {
    "CrestApps": {
      "DncRegistry": {
        "AzureBlobStorage": {
          "ConnectionString": "",
          "ContainerName": "dnc-registry-{{ ShellSettings.Name }}",
          "BasePath": "",
          "CreateContainer": true,
          "RemoveContainer": false,
          "RemoveFilesFromBasePath": false
        }
      }
    }
  }
}
```

| Setting | Meaning |
|---|---|
| `ConnectionString` | Required. Keep it in a secret source. |
| `ContainerName` | Required. Liquid with `ShellSettings`; resolved lowercase and must be a valid container name (3 to 63 lowercase letters, digits, single hyphens). |
| `BasePath` | Optional folder in the container; Liquid supported (for example `{{ ShellSettings.Name }}` in a shared container). |
| `CreateContainer` | Create the container on tenant activation. |
| `RemoveContainer` | Delete the container when the tenant is removed. Do not use on a shared container. |
| `RemoveFilesFromBasePath` | Delete only `BasePath` content on tenant removal. Use with a shared container. |

`DncRegistryBlobStorageOptions` derives from `MediaBlobStorageOptionsBase`. When `ConnectionString` or `ContainerName` is missing, the feature logs an error and the local file store keeps serving files. Azure stores only uploaded source files; the entry indexes stay in the database.

## Add a Registry Provider

Reference `CrestApps.OrchardCore.DncRegistry.Abstractions` and register the provider in its own feature that depends on `CrestApps.OrchardCore.DncRegistry`:

```csharp
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.PhoneNumbers;

namespace MyCompany.OrchardCore.Compliance;

public sealed class MyRegistry : INationalDoNotCallRegistry
{
    private readonly IHttpClientFactory _httpClientFactory;

    public MyRegistry(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string Key => "my-registry";

    public string DisplayName => "My Registry";

    public string Description => "Checks numbers against My Registry.";

    public Task<HashSet<PhoneNumber>> GetRegisteredNumbersAsync(
        IEnumerable<PhoneNumber> phoneNumbers,
        CancellationToken cancellationToken = default)
        => GetRegisteredNumbersAsync(phoneNumbers, null, cancellationToken);

    public async Task<HashSet<PhoneNumber>> GetRegisteredNumbersAsync(
        IEnumerable<PhoneNumber> phoneNumbers,
        NumberSearchContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(MyRegistry));

            // Submit the canonical numbers (filter by context?.CountryCode when set) and
            // return only the inputs the registry reports as listed.
            return [];
        }
        catch (HttpRequestException ex)
        {
            // Fail closed: an unreachable registry has not screened anything.
            throw new DoNotCallScreeningException(Key, "My Registry could not be reached.", ex);
        }
    }
}
```

```csharp
services.AddHttpClient(nameof(MyRegistry));
services.AddScoped<INationalDoNotCallRegistry, MyRegistry>();
```

- Return only numbers from the input set; skip numbers the upstream API cannot express (for example non-NANP numbers on a North American registry) rather than sending an altered number.
- Add a settings model, a display driver, and an admin menu entry under **Settings → DNC Registries** when the provider needs credentials.

## Upgrading from 2.x

- The Local registry repair migration now resolves the prefixed table name, so it no longer throws on tenants with a table prefix and no longer rolls back other features' migrations in the same run (most visibly the Omnichannel contact migration).
- Custom registries must throw `DoNotCallScreeningException` on lookup failure instead of returning an empty set.
- The Local DNC admin page uses Orchard Core's `AdminList`; overrides of the old list markup must move to `LocalDncList_SummaryAdmin` and `SummaryAdminGrid` placement.
- The Azure section is shell configuration, so in the root `appsettings.json` it belongs under `OrchardCore` (`OrchardCore:CrestApps:DncRegistry:AzureBlobStorage`), not at the root.

## Troubleshooting

| Symptom | Check |
|---|---|
| Local registry has no hits | The list reached `Completed`, and inputs normalize to the same E.164 value |
| Upload returns but no entries exist | The background import is pending, paused, or failed; check the list state and errors |
| Local numbers rejected | Choose the correct country at upload |
| Dialer keeps skipping a number with ComplianceScreeningUnavailable | A registry is throwing `DoNotCallScreeningException`; fix its credentials or connectivity |
| Azure storage ignored | Both `ConnectionString` and `ContainerName` are set under `OrchardCore:CrestApps:DncRegistry:AzureBlobStorage` |
| Invalid container name at tenant start | The tenant name breaks Azure naming rules; use a shared container with `BasePath` |
