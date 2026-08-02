---
name: orchardcore-content-transfer
description: Skill for bulk content import and export in Orchard Core using CrestApps Content Transfer. Covers CSV and Excel formats, transfer templates, mappings, background jobs, chunked uploads, filters, custom file-format providers, and content-type transfer settings. Use this skill when requests mention Orchard Core bulk import, bulk export, CSV content transfer, Excel content transfer, spreadsheet mappings, import rows, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.ContentTransfer, CrestApps.OrchardCore.ContentTransfer.OpenXml, IContentImportManager, IContentTransferFileFormatProvider, IContentImportHandler, IContentImportRowFilter, and ContentImportOptions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Content Transfer

## Configure bulk import and export

You are an Orchard Core expert. Generate accurate import and export guidance for CrestApps Content Transfer. The base module is CSV-first. The optional OpenXml module adds `.xlsx` support through the same provider pipeline.

### Guidelines

- Install `CrestApps.OrchardCore.ContentTransfer` in the web or startup project.
- Install `CrestApps.OrchardCore.ContentTransfer.OpenXml` in that same project only when Excel workbook support is required.
- Enable `CrestApps.OrchardCore.ContentTransfer` for CSV. Enable `CrestApps.OrchardCore.ContentTransfer.OpenXml` for `.xlsx`; it depends on the base feature.
- Do not claim that legacy `.xls` files are supported. The OpenXml provider handles `.xlsx`.
- The base module registers `CsvContentTransferFileFormatProvider` with `text/csv`.
- The OpenXml module registers `ExcelContentTransferFileFormatProvider` with `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.
- Content types opt in by default. Set their transfer settings to opt out of bulk import or export.
- Start imports from **Content → Import** and exports from **Content → Export**.
- Download the template for the selected type rather than hand-authoring columns. It reflects active import handlers and their metadata.
- Imports are queued and processed in batches. Entries can be pending, processing, paused, completed, completed with errors, or failed.
- Failed rows can be downloaded in the original enabled format. Preserve the header columns before correcting and retrying them.
- Imports create drafts by default. Select publish in the UI only when immediate publication is intended.
- Existing `ContentItemId` values update an item in a new latest version. `ContentItemVersionId` is reference-only and is ignored by imports.
- Smaller exports download immediately; exports beyond `ExportQueueThreshold` run in the background.
- Enable `OrchardCore.Notifications` when the built-in completion notification is desired.
- Keep upload limits aligned with the host or reverse-proxy request-body limits. Keep chunking enabled for large files.

### Feature overview

| Feature ID | Format and capability |
|---|---|
| `CrestApps.OrchardCore.ContentTransfer` | CSV import/export, admin UI, transfer mappings, and background processing |
| `CrestApps.OrchardCore.ContentTransfer.OpenXml` | Optional Excel `.xlsx` reader and writer |

### Enable CSV and Excel support

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

## Use the admin flow

### Import content

1. Go to **Content → Import**.
2. Select the content type and download its template.
3. Populate the header columns exactly as the template provides them.
4. Upload a `.csv` file, or a `.xlsx` file when the OpenXml feature is enabled.
5. Choose whether imported items remain drafts or are published.
6. Submit the import and monitor the transfer entry list.
7. Pause or resume an import if necessary. For error entries, download the rejected rows, correct them, and submit again.

The import pipeline calls `IContentManager.ValidateAsync()`. A successfully parsed row can still fail model validation, so use the error export rather than assuming every uploaded row is accepted.

### Export content

1. Go to **Content → Export**.
2. Select a content type and one enabled format.
3. Select published, latest, or all versions as required.
4. Optionally filter by created date, modified date, or owner.
5. Download a small export directly, or wait for the background export when it is queued.

The OpenXml writer uses the selected content type as its workbook sheet name, truncating a name beyond Excel's 31-character sheet-name limit.

## Configure batching and uploads

Configure the per-tenant shell section under `OrchardCore:CrestApps:ContentTransfer`.

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
      }
    }
  }
}
```

| Option | Default | Meaning |
|---|---:|---|
| `ImportBatchSize` | 100 | Rows processed per import batch |
| `ExportBatchSize` | 200 | Content items written per export batch |
| `ExportQueueThreshold` | 500 | Item count that starts queued export behavior |
| `MaxUploadFileSize` | 1 GB | Total import size limit in bytes and `0` disables it |
| `MaxUploadChunkSize` | 25 MB | Per-request chunk size and `0` disables chunking |
| `TemporaryFileLifetime` | 1 hour | Retention for unfinished assembled-upload files |

The default chunk size is below the common IIS request-filtering threshold. If increasing it, also raise IIS `maxAllowedContentLength` or the relevant reverse-proxy limit. Setting a large maximum file size without raising the chunk size remains safer because every request stays bounded.

## Understand mappings

`IContentImportManager` gathers template columns, then the selected `IContentTransferFileFormatProvider` reads or writes rows. Handlers own the mapping:

| Extension point | Use it for |
|---|---|
| `IContentImportHandler` | Content-item-level metadata |
| `IContentPartImportHandler` | Properties belonging to a content part |
| `IContentFieldImportHandler` | Properties of a custom content field |
| `IContentImportRowFilter` | Rejecting a row before normal mappings run |
| `IContentTransferFileFormatProvider` | An additional file extension and reader/writer |

Field-handler column names conventionally follow `{PartName}_{FieldName}_{PropertyName}`. `StandardFieldImportHandler` is the correct base for most custom fields.

### Add a custom field mapping

```csharp
using CrestApps.OrchardCore.ContentTransfer;

namespace MyModule;

public sealed class RatingFieldImportHandler : StandardFieldImportHandler
{
    protected override string BindingPropertyName => nameof(RatingField.Value);

    protected override Task SetValueAsync(ContentFieldImportMapContext context, string value)
    {
        context.ContentPart.Alter<RatingField>(context.ContentPartFieldDefinition.Name, field =>
        {
            field.Value = int.TryParse(value, out var rating) ? rating : null;
        });

        return Task.CompletedTask;
    }

    protected override Task<object> GetValueAsync(ContentFieldExportMapContext context)
    {
        var field = context.ContentPart.Get<RatingField>(context.ContentPartFieldDefinition.Name);
        return Task.FromResult<object>(field?.Value);
    }
}
```

Register the handler in the feature startup:

```csharp
services.AddContentFieldImportHandler<RatingField, RatingFieldImportHandler>();
```

### Filter unwanted rows

`InitializeAsync()` chooses whether the filter participates for an import. `PrepareBatchAsync()` is called before batch evaluation, and `ShouldSkipRowAsync()` receives each row.

```csharp
using CrestApps.OrchardCore.ContentTransfer;

namespace MyModule;

public sealed class ArchivedSkuFilter : IContentImportRowFilter
{
    public Task<bool> InitializeAsync(ContentImportRowFilterInitContext context)
        => Task.FromResult(string.Equals(context.ContentTypeDefinition.Name, "Product", StringComparison.Ordinal));

    public Task PrepareBatchAsync(ContentImportRowFilterBatchContext context)
        => Task.CompletedTask;

    public Task<bool> ShouldSkipRowAsync(ContentImportRowFilterContext context)
        => Task.FromResult(
            context.Row.Table.Columns.Contains("ProductPart_Sku") &&
            string.Equals(context.Row["ProductPart_Sku"]?.ToString(), "ARCHIVED", StringComparison.OrdinalIgnoreCase));
}
```

```csharp
services.AddScoped<IContentImportRowFilter, ArchivedSkuFilter>();
```

## Add another file format

An `IContentTransferFileFormatProvider` declares the extension, MIME type, file-name check, and reader and writer. Register it as a singleton. Put optional format support in a separate feature like the OpenXml module.

```csharp
using CrestApps.OrchardCore.ContentTransfer;

namespace MyModule;

public sealed class JsonLinesFormatProvider : IContentTransferFileFormatProvider
{
    public string FileExtension => ".jsonl";

    public string ContentType => "application/x-ndjson";

    public bool CanHandle(string fileName)
        => Path.GetExtension(fileName).Equals(FileExtension, StringComparison.OrdinalIgnoreCase);

    public IContentTransferFileReader CreateReader(Stream stream)
        => new JsonLinesReader(stream);

    public IContentTransferFileWriter CreateWriter(Stream stream, string sheetName)
        => new JsonLinesWriter(stream);
}
```

```csharp
services.AddSingleton<IContentTransferFileFormatProvider, JsonLinesFormatProvider>();
```

## Troubleshooting

- If `.xlsx` is absent from the import picker or export selector, enable the exact OpenXml feature and verify its package is in the web project.
- A missing content type commonly means its transfer setting opted out or authorization excluded the user.
- Use CSV quoting for commas, quotes, and newlines. The built-in CSV provider supports RFC 4180-style quoted values.
- Do not disable chunking for large Internet-facing uploads unless every hosting layer accepts the full body size.
- File parsing success does not bypass content validation. Inspect the exported rejected rows for validation errors.
