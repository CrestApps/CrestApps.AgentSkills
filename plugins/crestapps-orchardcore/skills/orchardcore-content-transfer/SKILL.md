---
name: orchardcore-content-transfer
description: Skill for bulk content import and export in Orchard Core with CrestApps Content Transfer. Covers CSV and Excel formats, the OpenXml and Azure Blob Storage features, ContentImportOptions batching and chunked upload limits, BackgroundWork pacing, hosting limits, scale-out with Redis locks, per-type permissions, import locks and the stalled-row watchdog, queued exports, and extension points such as IContentImportHandler, StandardFieldImportHandler, IContentImportRowFilter, IContentTransferFileFormatProvider, IContentTransferFileReaderTransformer, IContentExportBatchHandler and ExportRequest options. Use this skill when requests mention Orchard Core bulk import, bulk export, CSV or Excel content transfer, spreadsheet mappings, an import stuck in Processing, or large file uploads. Strong matches include CrestApps.OrchardCore.ContentTransfer, CrestApps.OrchardCore.ContentTransfer.OpenXml, CrestApps.OrchardCore.ContentTransfer.Azure, ContentTransferPermissions and ContentImportOptions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Content Transfer

## Configure bulk import and export

You are an Orchard Core expert. CrestApps Content Transfer bulk imports and exports content items through pluggable file formats. The base feature supports CSV; OpenXml adds `.xlsx`; Azure Blob Storage keeps the transfer files in a shared container for scaled-out sites.

### Guidelines

- Install `CrestApps.OrchardCore.ContentTransfer` (and optionally `.OpenXml`, `.Azure`) in the web or startup project.
- The base feature depends on Orchard Core `OrchardCore.Resources` (for `bootstrap-select`) and `CrestApps.OrchardCore.Resources`.
- Legacy `.xls` is not supported; OpenXml handles `.xlsx`.
- Every content type takes part by default. `ContentTypeTransferSettings.AllowBulkImport` and `AllowBulkExport` (**Allow bulk import** / **Allow bulk export** on the type) default to `true`.
- Download the template for a type rather than hand-authoring columns; it reflects the registered handlers.
- Imports save drafts unless **Publish imported content** is checked. A row with an existing `ContentItemId` updates a new latest version; `ContentItemVersionId` is exported for reference and ignored on import.
- Validation runs through `IContentManager.ValidateAsync()`. Rejected rows download in the original format while that format is enabled.
- Exports above `ExportQueueThreshold`, or with options that set `ExportRequest.RequiresQueue`, are queued; smaller ones stream directly. Enable `OrchardCore.Notifications` for the built-in completion notification.
- On more than one instance, enable `OrchardCore.Redis.Lock`, store files in Azure Blob Storage, and keep session affinity for chunked uploads (or share the temp directory).

### Feature overview

| Feature ID | Purpose |
|---|---|
| `CrestApps.OrchardCore.ContentTransfer` | CSV import and export, **Content > Import** and **Content > Export**, background processing |
| `CrestApps.OrchardCore.ContentTransfer.OpenXml` | Excel `.xlsx` reader and writer |
| `CrestApps.OrchardCore.ContentTransfer.Azure` | Stores import and export files in Azure Blob Storage |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContentTransfer",
        "CrestApps.OrchardCore.ContentTransfer.OpenXml"
      ],
      "disable": []
    }
  ]
}
```

### Permissions

`ContentTransferPermissions` (namespace `CrestApps.OrchardCore.ContentTransfer`) declares descriptions with `LocalizationSource`. The four non-typed permissions go to **Administrator** by default.

| Permission | Grants |
|---|---|
| `ListContentTransferEntries` | **Content > Import** menu and the **Bulk Import** list |
| `DeleteContentTransferEntries` | Deleting entries (implies list) |
| `ImportContentFromFile` | Importing any type, templates, error files, pause and resume |
| `ImportContentFromFile_{ContentType}` | The same for one type (implied by `ImportContentFromFile`) |
| `ExportContentFromFile` | **Content > Export** menu and exporting any type |
| `ExportContentFromFile_{ContentType}` | The same for one type (implied by `ExportContentFromFile`) |

Build a typed permission with `ContentTransferPermissions.CreateDynamicPermission(ContentTransferPermissions.ImportContentFromFileOfType, "Product")`.

## Import processing

1. An upload is stored through `IContentTransferFileStore` as a **Pending** `ContentTransferEntry` and processing starts immediately. The **Imported Files Processor** background task (every 10 minutes) picks up pending entries and resumes stalled ones.
2. One run works on an import at a time under a lock named after the entry. The lock lasts 30 minutes, so a run reads rows for at most 15 minutes, saves progress after the batch in flight, and the import continues under a fresh lock.
3. A watchdog names any row slower than a minute in a warning, abandons a row still running after 10 minutes or a run past 28 minutes (releasing the lock), and skips a row that stops two runs, listing it in the error file. An abandoned run cannot save progress later.
4. Statuses: **Pending**, **Processing**, **Paused**, **Deleting**, **Completed**, **Completed with errors**, **Failed**. A **Processing** entry with no saved progress for 10 minutes is stalled; **Resume import** continues paused, failed, pending and stalled imports from the last saved batch, and reports when a run is still working.
5. Deleting an entry removes it and its file in the background; it never deletes imported content.

The admin lists use Orchard Core's `AdminList` standard (List or Grid layout, filters in the toolbar); see the framework `orchardcore-admin-lists` skill for customizing rows.

## Configuration

All values are read from the tenant shell configuration (`OrchardCore:CrestApps:ContentTransfer` in the root `appsettings.json`, or `CrestApps:ContentTransfer` in `App_Data/Sites/{Tenant}/appsettings.json`). Restart the tenant after changing them.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContentTransfer": {
        "ImportBatchSize": 100,
        "ExportBatchSize": 200,
        "ExportQueueThreshold": 500,
        "MaxUploadFileSize": 1073741824,
        "MaxUploadChunkSize": 26214400,
        "TemporaryFileLifetime": "01:00:00"
      },
      "BackgroundWork": {
        "Pacing": {
          "DatabaseShare": 0.25,
          "MaxPause": "00:00:30"
        }
      }
    }
  }
}
```

| `ContentImportOptions` | Default | Meaning |
|---|---|---|
| `ImportBatchSize` | 100 | Rows per import batch |
| `ExportBatchSize` | 200 | Items per export batch |
| `ExportQueueThreshold` | 500 | Item count above which an export is queued |
| `MaxUploadFileSize` | 1 GB | Total upload size in bytes (64-bit, can exceed 1 GB); `0` disables the check |
| `MaxUploadChunkSize` | 25 MB | Chunk size per request; `0` disables chunking |
| `TemporaryFileLifetime` | 1 hour | Retention of unfinished chunked uploads |

`BackgroundWorkPacingOptions` (`CrestApps:BackgroundWork:Pacing`) paces imports and Local DNC list work: after each batch the job pauses in proportion to how long the batch took. `DatabaseShare` (0.05 to 1, `1` disables pacing) and `MaxPause` cap it. Batches from all tenants take turns on one in-memory gate per instance; a batch waiting more than five minutes runs anyway.

Environment variable form: `OrchardCore__CrestApps__ContentTransfer__MaxUploadFileSize=5368709120`.

### Hosting and proxy limits

The module raises the Kestrel request-body limit for the import endpoint to one chunk plus overhead. Two layers enforce limits before the app runs, and the default 25 MB chunk stays below both:

- IIS request filtering (`maxAllowedContentLength`, about 28.6 MB by default). Raise it in `web.config` only if you raise `MaxUploadChunkSize` to `30000000` or more.
- Reverse proxies (Nginx `client_max_body_size`, Apache `LimitRequestBody`) must allow one chunk plus overhead.

With chunking disabled every layer must accept the whole file in one request.

### Scaled-out sites and Azure Blob Storage

Files default to `App_Data/Sites/{tenant}/Temp` on local disk, which other instances cannot read and containers lose on restart. Enable `CrestApps.OrchardCore.ContentTransfer.Azure` and configure `CrestApps:ContentTransfer:AzureBlobStorage`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContentTransfer": {
        "AzureBlobStorage": {
          "ConnectionString": "",
          "ContainerName": "content-transfer-{{ ShellSettings.Name }}",
          "BasePath": "",
          "CreateContainer": true,
          "RemoveContainer": true
        }
      }
    }
  }
}
```

- `ConnectionString` and `ContainerName` are required. When either is missing the feature logs an error and local storage keeps serving files.
- `ContainerName` and `BasePath` accept Liquid with `ShellSettings`. Use a container per tenant (with `RemoveContainer`), or a shared container with `BasePath` `{{ ShellSettings.Name }}` and `RemoveFilesFromBasePath` instead of `RemoveContainer`.
- Resolved container names are lowercased and must be valid Azure names (3 to 63 lowercase letters, digits and single hyphens); a tenant name with an underscore fails.
- Existing local files are not moved. Let running imports finish first.

Also enable `OrchardCore.Redis.Lock` so two instances never import the same file, and keep session affinity (or point `OrchardCore:TempDirectory:Path` at a shared mount) so all chunks of an upload reach one instance.

## Extend the pipeline

`IContentImportManager.GetColumnsAsync()` gathers columns from registered handlers, the selected `IContentTransferFileFormatProvider` reads or writes the file, and handlers map rows. Read [references/extensibility.md](references/extensibility.md) for complete examples.

| Extension point | Use it for | Registration |
|---|---|---|
| `IContentImportHandler` | Content-item-level columns | `services.AddScoped<IContentImportHandler, T>()` |
| `IContentPartImportHandler` | Columns owned by a part | `services.AddContentPartImportHandler<TPart, THandler>()` |
| `StandardFieldImportHandler` / `IContentFieldImportHandler` | Columns of a field (`{Part}_{Field}_{Property}`) | `services.AddContentFieldImportHandler<TField, THandler>()` |
| `IContentImportRowFilter` | Skip rows before mapping (`InitializeAsync`, `PrepareBatchAsync`, `ShouldSkipRowAsync`) | `services.AddScoped<IContentImportRowFilter, T>()` |
| `IContentTransferFileReaderTransformer` | Read another system's layout (for example Shopify product exports) | `services.AddScoped<IContentTransferFileReaderTransformer, T>()` |
| `IContentTransferFileFormatProvider` | Another file format | `services.AddSingleton<IContentTransferFileFormatProvider, T>()` |
| `IDisplayDriver<ExportRequest>` | Extra options on the export form | `services.AddScoped<IDisplayDriver<ExportRequest>, T>()` |
| `IContentExportBatchHandler` | Preload data for a page of exported items | Forward from the same scoped handler instance |
| `IContentTransferNotificationHandler` | Notify when a queued export completes | `services.AddScoped<IContentTransferNotificationHandler, T>()` |

`AddContentPartImportHandler` and `AddContentFieldImportHandler` live in namespace `CrestApps.OrchardCore.ContentTransfer`; `StandardFieldImportHandler` in `CrestApps.OrchardCore.ContentTransfer.Handlers`. Built-in handlers cover `TitlePart`, `HtmlBodyPart`, `MarkdownBodyPart`, common fields and taxonomy fields (`{Part}_{Field}_Terms` by name, or `{Part}_{Field}_TermContentItemIds`).

Omnichannel Management contributes contact-specific import options (duplicate phone filtering, lead country, DNC checks) and an export **CRM last activity** section; see the `orchardcore-omnichannel-management` skill.

## Upgrading from 2.x

- Admin lists render through Orchard Core's `AdminList` shape. `ContentTransferEntryDisplayDriver` places the row shapes (`ContentTransferEntry_Checkbox_SummaryAdmin`, `ContentTransferEntry_Fields_SummaryAdmin`, ...) for both the `SummaryAdmin` (List) and `SummaryAdminGrid` (Grid) display types. A custom driver that adds a shape to these rows must give it a `SummaryAdminGrid` location too, and overrides of the old list markup must be redone.
- The module depends on `OrchardCore.Resources` because `crestapps-bootstrap-select` was removed in favor of Orchard Core's `bootstrap-select`.
- Imports are supervised by a watchdog and lock slices, so an import stuck on one row no longer holds the lock until a restart. A DNC registry timeout now skips the row instead of ending the run.
- New in 3.0: the Azure Blob Storage feature, `BackgroundWork:Pacing`, `IContentExportBatchHandler`, `ExportRequest` export options, `ContentExportContext.Exclude`, and `IContentTransferFileReaderTransformer`.
- Permission descriptions use `LocalizationSource`.

## Troubleshooting

- `.xlsx` missing from the pickers: enable the OpenXml feature and install its package in the web project.
- A content type is missing: `AllowBulkImport` or `AllowBulkExport` is off, or the user lacks the typed permission.
- An import stays **Processing**: check the log for the watchdog warnings and the lock; with several instances, enable Redis locks. Use **Resume import** after 10 minutes without progress.
- *The import file no longer exists*: files were on a local disk another instance or a restarted container cannot read; use Azure Blob Storage.
- Every row was created twice: two instances imported the same file without a shared lock.
- Uploads fail with IIS `404.13`: lower `MaxUploadChunkSize` below `30000000` or raise `maxAllowedContentLength`.
- Parsing success does not bypass validation; read the rejected-rows file.
