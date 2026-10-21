---
name: crestapps-core-file-sources
description: Skill for CrestApps.Core 2.0 file sources, the continuous intake path that keeps a File AI data source in step with a folder, an FTP server or an SFTP server. Covers CrestApps.Core.AI.FileSources, AddCoreFileSources with the CrestApps AI FileSources configuration section, AddCoreFileSystemConnector with AllowedRoots, AddCoreFtpIngestionConnector and AddCoreSftpIngestionConnector, FileSource records and their YesSql or Entity Framework Core stores, FileSourceMetadata per-source figure and model settings, IIngestionRunService and IFileSourceScheduler, FileSourceRunSummary, the IsComplete rule that prevents partial listings from deleting knowledge, discovery cursors and change tokens, typed knowledge objects and the knowledge figure endpoint, custom IIngestionConnector implementations, and upgrading from CrestApps.Core.AI.Indexers.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core File Sources

A **File** AI data source (`AIDataSourceSourceTypes.File`) is an inert bucket with no folder, credential or field mapping. A **file source** (`FileSource` record) picks a connector, configures it, and points at the File data source it fills. Several file sources can feed one knowledge base, each on its own schedule. Web crawlers (`crestapps-core-web-crawlers`) are the same shape for websites.

```text
FileSource (FileSystem) -+
FileSource (Ftp)        -+->  File AI data source  ->  knowledge-base index (typed objects)
FileSource (Sftp)       -+
```

Each file becomes separately retrievable objects (`document`, `article`, `text`, `figure`, `chart`, `table`) with a page number and a link to its article and document. Data source concepts are in `crestapps-core-data-sources`; readers and figure processing are in `crestapps-core-document-processing`.

## Registration

```csharp
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Documents.Endpoints;
using CrestApps.Core.AI.FileSources;
using CrestApps.Core.AI.Ftp;
using CrestApps.Core.AI.Sftp;
using CrestApps.Core.Data.YesSql;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddDocumentProcessing(documentProcessing => documentProcessing   // optional: chat uploads, Office readers
            .AddYesSqlStores()
            .AddPdf()
            .AddReferenceDownloads()))
    .AddIndexingServices(indexing => indexing
        .AddYesSqlStores()
        .AddPostgreSQL(
            builder.Configuration.GetSection("CrestApps:PostgreSQL"),
            postgreSQL => postgreSQL.AddAIDataSources())));

// After AddDocumentProcessing, so the HTML reader registered here wins .html.
builder.Services
    .AddCoreFileSources(builder.Configuration)
    .AddCoreFileSystemConnector()
    .AddCoreFtpIngestionConnector()    // CrestApps.Core.AI.Ftp (FluentFTP)
    .AddCoreSftpIngestionConnector();  // CrestApps.Core.AI.Sftp (SSH.NET)

builder.Services.AddCoreFileSourceStoresYesSql(); // or AddCoreFileSourceStoresEntityCore()

var app = builder.Build();

app.AddDownloadKnowledgeFigureEndpoint();
```

- `AddCoreFileSources()` registers the ingestion path (`AddCoreAIDocumentIngestion` with the plain-text reader, figure processing and backfill), the shared run service (`AddCoreAIIngestionRuntime()`), `IFileSourceScheduler`, `FileSourceBackgroundService`, the catalog handlers, the HTML reader for `.html`, `.htm` and `text/html`, and the **Files** entry in `AIDataSourceSourceOptions`.
- `AddCoreFileSources(IConfiguration)` also binds `FileSourceOptions` and `FileSystemConnectorOptions`.
- Only registered connectors are offered. A host that never references the FTP or SFTP package offers a folder and nothing else.
- Store calls register `IFileSourceStore`, `IIngestionItemStateStore` and `IKnowledgeObjectStore`. YesSql hosts create the tables with `CreateFileSourceIndexSchemaAsync`, `CreateIngestionItemStateIndexSchemaAsync` and `CreateKnowledgeObjectIndexSchemaAsync` (namespace `CrestApps.Core.Data.YesSql.Indexes.*`).
- `AddCoreAIDocumentReferenceDownloads()` (or `AddReferenceDownloads()`) adds the `File` citation link resolver. `AddDownloadKnowledgeFigureEndpoint()` serves `ai/knowledge/{dataSourceId}/figures/{canonicalId}`, authorized with `AIKnowledgeOperations.ViewFigures` against the `AIDataSource`; register an `AuthorizationHandler<OperationAuthorizationRequirement, AIDataSource>`. Both live in `CrestApps.Core.AI.Documents`.
- PDFs need a PDF reader: `AddPdf()` on the document-processing builder, or `services.AddCoreAIPdfIngestion()` from `CrestApps.Core.AI.Ingestion.Pdf`. Office files need `AddOpenXml()`.

## Configuration

```json
{
  "CrestApps": {
    "AI": {
      "FileSources": {
        "MaxItemsPerRun": 200,
        "MaxConcurrentFetches": 2,
        "RunCheckIntervalMinutes": 15,
        "DefaultRunIntervalMinutes": 1440,
        "FileSystem": {
          "AllowedRoots": [ "App_Data/file-sources" ]
        }
      }
    }
  }
}
```

- `FileSystemConnectorOptions.AllowedRoots` is empty by default, which means **no folder may be read**. Registering the connector grants nothing.
- Roots are absolute or relative to `FileSystemConnectorOptions.BasePath`, which the configuration callback sets to the content root.
- `..` is refused outright in a root and in an item id, containment is compared by path segment, and an item id is re-checked at fetch time.
- A file source with no `RootPath` reads the single allowed root; with several roots it must name one.
- Name a developer's own folder in user secrets, for example `dotnet user-secrets set "CrestApps:AI:FileSources:FileSystem:AllowedRoots:0" "D:\docs"`, or the environment variable `CrestApps__AI__FileSources__FileSystem__AllowedRoots__0`.

## Creating Records in Code

```csharp
using CrestApps.Core;
using CrestApps.Core.AI.FileSources.Connectors;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Models;

var dataSource = new AIDataSource
{
    DisplayText = "Datasheets",
    Source = AIDataSourceSourceTypes.File,
    AIKnowledgeBaseIndexProfileName = "datasheets-kb",
};
await dataSourceCatalog.CreateAsync(dataSource, cancellationToken);

var fileSource = new FileSource
{
    DisplayText = "Datasheet folder",
    Source = FileSystemIngestionConnector.ConnectorName, // "FileSystem", "Ftp" or "Sftp"
    AIDataSourceId = dataSource.ItemId,
    ReindexIntervalMinutes = 60,
};

fileSource.Put(new FileSystemFileSourceMetadata
{
    RootPath = "App_Data/file-sources/datasheets",
    Recursive = true,
});

fileSource.Put(new FileSourceMetadata
{
    FigureMode = FigureProcessingMode.Auto,
    VisionDeploymentName = "gpt-4o",     // null means use the application's vision deployment
    MaxFigureDescriptionsPerDocument = 25,
    MaxItemsPerRun = 500,
    Language = "en",
});

await fileSourceStore.CreateAsync(fileSource, cancellationToken);
```

- FTP and SFTP records store a `RemoteFileSourceMetadata` folder (`RootPath` default `/`, `Recursive`, `MaxItems`) plus `FtpConnectionMetadata` or `SftpConnectionMetadata` with secrets protected by `FtpResourceConstants.DataProtectionPurpose` or `SftpResourceConstants.DataProtectionPurpose` (see `crestapps-core-mcp-file-resources`).
- Saving validates the connector settings, refuses a data source that is not `File` or no longer exists, and refuses a vision deployment that does not accept image input.
- `FileSourceMetadata` also has `UtilityDeploymentName`. A deployment deleted later falls back to the application's own.

## Running

| Service | Does |
|---|---|
| `IIngestionRunService.RunAsync(source)` | one run of one record; returns and stores a `FileSourceRunSummary` |
| `IFileSourceScheduler.RunDueAsync()` | runs every due source; returns `FileSourceSchedulerResult(Considered, Ran, Failed)` |
| `IFileSourceScheduler.GetDueAsync(now)` / `IsDue(source, now)` | lets a host spread work over its own workers |
| `FileSourceBackgroundService` | timer that calls `RunDueAsync` every `RunCheckIntervalMinutes` |

The scheduler is scoped and creates no scope. A host with its own scheduler (Orchard Core background tasks, a cron job) resolves it inside its own scope and should remove `FileSourceBackgroundService`, or sources run twice. Commit the store afterwards (`IStoreCommitter.CommitAsync`), as the background jobs do; otherwise objects, item state and summaries are discarded with the scope. Due-ness is read from the stored summary, so restarts do not rerun everything.

`FileSourceRunSummary` stores `Status` (`Running`, `Succeeded`, `PartiallyCompleted`, `Failed`), `StartedUtc`, `CompletedUtc`, `DiscoveryCompleted`, `DiscoveryCursor`, `ItemsDiscovered`, `ItemsIndexed`, `ItemsSkipped`, `ItemsDeleted`, `ItemsFailed`, `FiguresDiscovered`, `FiguresPendingDescription` and `Error`. Read it with `source.TryGet<FileSourceRunSummary>(out var summary)`.

## What Gets Removed

- **A partial listing never deletes anything.** Removals happen only when the connector returns `IngestionDiscoveryResult.IsComplete = true`.
- A run reads at most `MaxItemsPerRun` items. When there are more, the connector returns a `DiscoveryCursor` and the next run resumes; a resumed run is never complete.
- Each item carries an opaque change token (compared verbatim). An unchanged token is skipped; an item with no token is re-read every run.
- A document's identity comes from its bytes. Moving a file keeps its document, and a changed file replaces the document it produced unless another item of the same record still produces it.
- **Reset state** clears per-item state (`IngestionItemState`) so the next run re-reads everything; it does not delete stored knowledge.

Figures found during a run are stored as `PendingDescription` and transcribed later by the figure backfill job, using the record's vision deployment.

## Custom Connectors

Implement `IIngestionConnector` (namespace `CrestApps.Core.AI.Indexing`) and register it with `AddCoreIngestionConnector<TConnector>(name, descriptor => ...)`. The record's `Source` must equal `name`. See [references/connector-examples.md](references/connector-examples.md) for a complete connector.

## Upgrading from 1.x and 2.0 previews

- 1.x had no file sources. Preview builds used `CrestApps.Core.AI.Indexers`; it is now `CrestApps.Core.AI.FileSources` with `AddCoreFileSources()`, `FileSourceOptions`, `FileSourceRunStatus` and the `FileSource*` stored shapes.
- Move configuration to `CrestApps:AI:FileSources` and bind it with `AddCoreFileSources(builder.Configuration)`. The 2.0 changelog says `CrestApps:Indexers` is still read as a fallback, but the current source falls back to a section literally named `CrestApps:File sources`, so do not rely on the old name.
- Use `FileSystemConnectorOptions.AllowedRoots` under the `FileSystem` sub-section. The older `FileSourceOptions.AllowedLocalRoots` (`CrestApps:AI:FileSources:AllowedLocalRoots`) is still merged into the allowed roots, so move entries rather than keeping both.
- The `Ingested` data source type is now `File`. File sources are their own `FileSource` records, no longer web crawler records. Hosts with stored preview records move them across.
- Docs and changelog mention `IFileSourceRunService`; the shipped interface is `IIngestionRunService` in `CrestApps.Core.AI.Ingestion`.
- FTP and SFTP connectors moved to `CrestApps.Core.AI.Ftp` and `CrestApps.Core.AI.Sftp`. Connector names stored on records are still `Ftp` and `Sftp`.
