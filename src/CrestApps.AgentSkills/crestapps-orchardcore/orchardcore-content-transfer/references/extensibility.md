# Content Transfer Extensibility

All contracts live in namespace `CrestApps.OrchardCore.ContentTransfer` unless noted. Register handlers from your feature's startup.

## Columns

Every handler returns `ImportColumn` definitions, which drive the template, the import UI and the export header.

| `ImportColumn` | Meaning |
|---|---|
| `Name` | Primary column name |
| `Description` | Shown in the import UI |
| `IsRequired` | Marks the column required |
| `AdditionalNames` | Alternate accepted names, to keep old templates working |
| `ValidValues` | Allowed values shown in the UI |
| `Type` | `ImportColumnType.All`, `ImportOnly` or `ExportOnly` |

Field columns follow `{PartName}_{FieldName}_{PropertyName}`.

## Content item handler

```csharp
using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using OrchardCore.ContentManagement;

namespace MyModule.ContentTransfer;

public sealed class ExternalSkuImportHandler : IContentImportHandler
{
    // IContentImportHandler columns are asynchronous; a handler with fixed columns returns them through Task.FromResult.
    public Task<IReadOnlyCollection<ImportColumn>> GetColumnsAsync(ImportContentContext context)
        => Task.FromResult<IReadOnlyCollection<ImportColumn>>(
        [
            new ImportColumn
            {
                Name = "ExternalSku",
                Description = "The SKU used by the product system.",
            },
        ]);

    public Task ImportAsync(ContentImportContext context)
    {
        foreach (DataColumn column in context.Columns)
        {
            if (string.Equals(column.ColumnName, "ExternalSku", StringComparison.OrdinalIgnoreCase))
            {
                context.ContentItem.Alter<ProductMetadataPart>(part =>
                    part.ExternalSku = context.Row[column]?.ToString()?.Trim());
            }
        }

        return Task.CompletedTask;
    }

    public Task ExportAsync(ContentExportContext context)
    {
        context.Row["ExternalSku"] = context.ContentItem.As<ProductMetadataPart>()?.ExternalSku;

        return Task.CompletedTask;
    }
}
```

```csharp
services.AddScoped<IContentImportHandler, ExternalSkuImportHandler>();
```

## Content part handler

`ImportContentPartContext.ContentTypePartDefinition` names the attachment; map contexts add `ContentItem`, `Columns`, `Row` (import) or `ContentPart`, `Row` (export).

```csharp
using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using OrchardCore.ContentManagement;

namespace MyModule.ContentTransfer;

public sealed class SkuPartImportHandler : IContentPartImportHandler
{
    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentPartContext context)
        => [
            new ImportColumn
            {
                Name = $"{context.ContentTypePartDefinition.Name}_Sku",
                Description = "The product SKU.",
                IsRequired = true,
            },
        ];

    public Task ImportAsync(ContentPartImportMapContext context)
    {
        var columnName = $"{context.ContentTypePartDefinition.Name}_Sku";

        foreach (DataColumn column in context.Columns)
        {
            if (string.Equals(column.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
            {
                context.ContentItem.Alter<SkuPart>(part => part.Sku = context.Row[column]?.ToString()?.Trim());
            }
        }

        return Task.CompletedTask;
    }

    public Task ExportAsync(ContentPartExportMapContext context)
    {
        context.Row[$"{context.ContentTypePartDefinition.Name}_Sku"] = (context.ContentPart as SkuPart)?.Sku;

        return Task.CompletedTask;
    }
}
```

```csharp
services.AddContentPartImportHandler<SkuPart, SkuPartImportHandler>();
```

## Content field handler

Derive from `StandardFieldImportHandler` (namespace `CrestApps.OrchardCore.ContentTransfer.Handlers`). Override `BindingPropertyName`, `SetValueAsync` and `GetValueAsync`, and optionally `Description`, `IsRequired` and `GetValidValues`.

```csharp
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Handlers;
using OrchardCore.ContentManagement;

namespace MyModule.ContentTransfer;

public sealed class RatingFieldImportHandler : StandardFieldImportHandler
{
    protected override string BindingPropertyName => nameof(RatingField.Value);

    protected override Task SetValueAsync(ContentFieldImportMapContext context, string value)
    {
        context.ContentPart.Alter<RatingField>(context.ContentPartFieldDefinition.Name, field =>
            field.Value = int.TryParse(value, out var parsed) ? parsed : null);

        return Task.CompletedTask;
    }

    protected override Task<object> GetValueAsync(ContentFieldExportMapContext context)
        => Task.FromResult<object>(context.ContentPart.Get<RatingField>(context.ContentPartFieldDefinition.Name)?.Value);

    protected override string Description(ImportContentFieldContext context)
        => "A rating from 1 to 5.";

    protected override string[] GetValidValues(ImportContentFieldContext context)
        => ["1", "2", "3", "4", "5"];
}
```

```csharp
services.AddContentFieldImportHandler<RatingField, RatingFieldImportHandler>();
```

## Row filters

`InitializeAsync` runs once per import and opts the filter in. `PrepareBatchAsync` receives every row of a batch (`ContentImportRowFilterBatchContext.Rows`) so the filter can look data up in one query. `ShouldSkipRowAsync` decides per row; set `SkipReason` (and optionally `SkipCategory`) so the error file explains the skip. Keep import-scoped state on the instance.

```csharp
using CrestApps.OrchardCore.ContentTransfer;

namespace MyModule.ContentTransfer;

public sealed class ArchivedSkuRowFilter : IContentImportRowFilter
{
    private HashSet<string> _archived = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> InitializeAsync(ContentImportRowFilterInitContext context)
    {
        if (!string.Equals(context.ContentTypeDefinition.Name, "Product", StringComparison.Ordinal))
        {
            return Task.FromResult(false);
        }

        _archived = ["OLD-001", "OLD-002"];

        return Task.FromResult(true);
    }

    public Task PrepareBatchAsync(ContentImportRowFilterBatchContext context)
        => Task.CompletedTask;

    public Task<bool> ShouldSkipRowAsync(ContentImportRowFilterContext context)
    {
        var sku = context.Columns.Contains("SkuPart_Sku") ? context.Row["SkuPart_Sku"]?.ToString() : null;

        if (!string.IsNullOrWhiteSpace(sku) && _archived.Contains(sku))
        {
            context.SkipReason = "The SKU is archived.";

            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
```

```csharp
services.AddScoped<IContentImportRowFilter, ArchivedSkuRowFilter>();
```

## Export options on the export form

The export form is display-managed through `ExportRequest` (`ContentType`, `ContentTypeDefinition`, `Entry`, `RequiresQueue`). A driver renders its section for every export (the type is chosen client-side), validates in `UpdateAsync`, stores options on `ExportRequest.Entry` as a part, and sets `RequiresQueue` when the options must be read from the saved entry.

```csharp
using CrestApps.OrchardCore.ContentTransfer;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace MyModule.Drivers;

public sealed class CatalogExportOptionsDisplayDriver : DisplayDriver<ExportRequest>
{
    public override IDisplayResult Edit(ExportRequest model, BuildEditorContext context)
        => Initialize<CatalogExportOptionsViewModel>("CatalogExportOptions_Edit", _ => { })
            .Location("Content:20");

    public override async Task<IDisplayResult> UpdateAsync(ExportRequest model, UpdateEditorContext context)
    {
        var viewModel = new CatalogExportOptionsViewModel();
        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        if (viewModel.IncludeSnapshot && model.ContentType == "Product")
        {
            model.Entry.Put(new CatalogExportOptionsPart { IncludeSnapshot = true });
            model.RequiresQueue = true;
        }

        return Edit(model, context);
    }
}
```

```csharp
services.AddScoped<IDisplayDriver<ExportRequest>, CatalogExportOptionsDisplayDriver>();
```

The base module supplies the `ExportRequest.Edit.cshtml` container; you only add `CatalogExportOptions.Edit.cshtml`.

Read the option back in an `IContentImportHandler`: `ImportContentContext.Entry` is available in `GetColumnsAsync` and `ExportAsync`. Return `ImportColumnType.ExportOnly` columns only when the option is on, and set `ContentExportContext.Exclude = true` to drop an item from the output.

To avoid one query per row, implement `IContentExportBatchHandler.PrepareExportBatchAsync(ContentExportBatchContext context)` (`ContentItems` of the current page) on the same class and forward both interfaces to one scoped instance:

```csharp
services.AddScoped<CatalogExportHandler>();
services.AddScoped<IContentImportHandler>(sp => sp.GetRequiredService<CatalogExportHandler>());
services.AddScoped<IContentExportBatchHandler>(sp => sp.GetRequiredService<CatalogExportHandler>());
```

## Read another system's layout

An `IContentTransferFileReaderTransformer` is asked, after the format provider opens a file, to read it for the imported type. The first transformer that recognizes the file returns an `IContentTransferFileReader` of one row per content item with the type's import columns; return `null` otherwise. `Describe(contentType)` returns text the import page shows, or `null`. The Products module's `StoreExportReaderTransformer` reads Shopify and WooCommerce product exports this way.

```csharp
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Services;
using OrchardCore.ContentManagement.Metadata.Models;

namespace MyModule.ContentTransfer;

public sealed class LegacyCmsTransformer : IContentTransferFileReaderTransformer
{
    public Task<IContentTransferFileReader> TransformAsync(
        IContentTransferFileReader reader,
        ContentTypeDefinition contentType,
        CancellationToken cancellationToken = default)
    {
        var columns = reader.GetColumnNames();
        var titleIndex = columns.ToList().IndexOf("post_title");

        if (contentType.Name != "Article" || titleIndex < 0)
        {
            return Task.FromResult<IContentTransferFileReader>(null);
        }

        var rows = reader.ReadRows().Select(row => new[] { row[titleIndex] }).ToList();

        return Task.FromResult<IContentTransferFileReader>(
            new InMemoryContentTransferFileReader(["TitlePart_Title"], rows));
    }

    public string Describe(ContentTypeDefinition contentType)
        => contentType.Name == "Article" ? "Legacy CMS post exports are also accepted." : null;
}
```

```csharp
services.AddScoped<IContentTransferFileReaderTransformer, LegacyCmsTransformer>();
```

## Another file format

An `IContentTransferFileFormatProvider` declares `FileExtension`, `ContentType`, `CanHandle(fileName)`, `CreateReader(stream)` (an `IContentTransferFileReader` with `GetColumnNames`, `GetRowCount`, `ReadRows`) and `CreateWriter(stream, sheetName)` (an `IContentTransferFileWriter` with `WriteHeader`, `WriteRow`, `Flush`). Registered providers appear automatically in the import picker, template links and export selector. Put optional formats in their own feature, as OpenXml does.

```csharp
using CrestApps.OrchardCore.ContentTransfer;

namespace MyModule.ContentTransfer;

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

## Export completion notifications

`IContentTransferNotificationHandler.NotifyExportCompletedAsync(ContentTransferEntry entry, string contentTypeName)` runs when a queued export finishes. The built-in handler uses Orchard Core notifications when `OrchardCore.Notifications` is enabled; add your own for email, SMS or SignalR.

```csharp
services.AddScoped<IContentTransferNotificationHandler, EmailExportNotificationHandler>();
```

## Fail a row from a handler

A handler that cannot import a row calls `context.Fail(reason, category)` on the `ContentImportContext`. The row is recorded as failed with the reason (counted under the category in the error file) and its content item is not saved; `context.Errors` and `context.ErrorCategory` expose what was recorded. Throwing does not fail the row: a handler exception is logged and the row goes on.

```csharp
public Task ImportAsync(ContentImportContext context)
{
    if (!context.Columns.Contains("ExternalSku") || string.IsNullOrWhiteSpace(context.Row["ExternalSku"]?.ToString()))
    {
        context.Fail("The row has no external SKU.", "Missing SKU");
    }

    return Task.CompletedTask;
}
```

A row filter that skips a row can also set `SkipReasonCode`, a short snake_case code that is never localized (for example `duplicate` or `dnc_match`). The import counts skipped rows by this code on its telemetry; a missing or invalid code counts as `other`. Never put a value read from the row in it.

## Import-only and type-specific formats

`IContentTransferFileFormatProvider` has three optional members with defaults:

| Member | Default | Meaning |
|---|---|---|
| `bool CanExport` | `true` | `false` for a read-only format (another system's package). It is left out of exports, templates and the export format list; failed rows of its imports download as CSV |
| `bool CanImport(string contentType)` | `true` | The import page, the upload check and the background import offer the format only for the content types it accepts, so two formats may share an extension for different types. The background import picks the first format, by extension, that handles the file name and imports the type |
| `string DescribeImport(ContentTypeDefinition contentType)` | `null` | A line the import page shows under the file requirements |

The Learning QTI Import feature's `.zip` and `.xml` formats are import only and accept question types only.

## Rows of several content types

When one file creates items of several related types (for example a package of questions of different kinds), implement `IContentImportRowTypeProvider.GetRowContentTypes(ContentTypeDefinition contentType)` to name the types the rows of an import of that type may be. Each row then gives its type in the `ContentType` column (`ContentTransferColumns.ContentType`); a row with no type is the import's type.

- At upload, the import keeps only the row types the uploader may also import (`ImportContentFromFile` for the type and **Allow bulk import**) in `ImportContentOptionsPart.RowContentTypes`. A row naming any other type fails.
- A row whose `ContentItemId` names an item of another type than its own fails instead of being written by the wrong handlers.
- For a type no provider groups, `ContentType` is read as any other column.

```csharp
using CrestApps.OrchardCore.ContentTransfer;
using OrchardCore.ContentManagement.Metadata.Models;

namespace MyModule.ContentTransfer;

public sealed class QuestionRowTypeProvider : IContentImportRowTypeProvider
{
    public IEnumerable<string> GetRowContentTypes(ContentTypeDefinition contentType)
        => contentType.Name == "Question"
            ? ["MultipleChoiceQuestion", "TrueFalseQuestion", "EssayQuestion"]
            : [];
}
```

```csharp
services.AddSingleton<IContentImportRowTypeProvider, QuestionRowTypeProvider>();
```

## Import targets that are not content types

A module can import its own records (not content items) through Content Transfer and get the same upload, background run, progress, pause and resume, and failed-rows file. The Skills module imports skills and competency frameworks this way.

| Contract | Members |
|---|---|
| `IContentTransferImportTarget` | `Name` (stored as `ContentTransferEntry.ContentType`), `DisplayName`, `IsContentType`, `AuthorizeAsync(user, authorizationService)`, `GetColumnsAsync()` (import page columns and template) |
| `IContentTransferRecordImporter` | Adds `ImportAsync(ContentTransferImportBatch batch, cancellationToken)` |
| `IContentTransferImportTargetResolver` | `FindAsync(name)` (a `ContentTypeImportTarget` for a content type that allows bulk import, or a registered importer), `ListRecordImportersAsync(user)`, `CanImportAsync(user, name)` |
| `ContentTransferImportBatch` | `Entry` (keep state across batches and resumes in `Entry.Properties`), `Rows` in file order |
| `ContentTransferImportRow` | `RowIndex`, `Columns`, `Get(column, params otherNames)` (case-insensitive, trimmed, `null` when blank), `Has(column)`, `Fail(error, category)`, `Failed`, `Error`, `ErrorCategory` |

Rules:

- The target `Name` must contain a dot (for example `Projects.Project`); content type names cannot, so a target never shadows a content type. A name without a dot is ignored.
- `AuthorizeAsync` is the only check: the import page, upload, template, pause, resume and failed-rows file all use it. `ImportContentFromFile` does not grant it.
- Every row not failed counts as imported. If `ImportAsync` throws, every row of the batch it did not fail fails with the message, and the import goes on.
- Row filters and file reader transformers apply to content types only.
- The import page is `/Admin/import/contents/{Name}`; the **Import** menu of the Bulk Import list offers the targets the user may import. Link to the page from your own admin screen.

```csharp
using System.Security.Claims;
using CrestApps.OrchardCore.ContentTransfer;
using Microsoft.AspNetCore.Authorization;

namespace MyModule.ContentTransfer;

public sealed class ProjectImportTarget : IContentTransferRecordImporter
{
    private readonly IProjectStore _projects;

    public ProjectImportTarget(IProjectStore projects)
    {
        _projects = projects;
    }

    public string Name => "Projects.Project";

    public string DisplayName => "Projects";

    public bool IsContentType => false;

    public Task<bool> AuthorizeAsync(ClaimsPrincipal user, IAuthorizationService authorizationService)
        => authorizationService.AuthorizeAsync(user, ProjectPermissions.ManageProjects);

    public Task<IReadOnlyCollection<ImportColumn>> GetColumnsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<ImportColumn>>(
        [
            new ImportColumn { Name = "Name", IsRequired = true },
            new ImportColumn { Name = "Code", Description = "A unique project code." },
        ]);

    public async Task ImportAsync(ContentTransferImportBatch batch, CancellationToken cancellationToken = default)
    {
        foreach (var row in batch.Rows)
        {
            if (row.Get("Name") is not { } name)
            {
                row.Fail("The row has no name.", "Missing name");

                continue;
            }

            await _projects.SaveAsync(name, row.Get("Code"), cancellationToken);
        }
    }
}
```

Register it in a startup that requires the Content Transfer feature. Reference `CrestApps.OrchardCore.ContentTransfer.Core` for the abstractions and `ContentTransferConstants`:

```csharp
using CrestApps.OrchardCore.ContentTransfer;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures(ContentTransferConstants.Feature.ModuleId)]
public sealed class ContentTransferStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddContentTransferImportTarget<ProjectImportTarget>();
}
```
