---
name: orchardcore-ai-file-sources
description: Skill for CrestApps AI File Sources in Orchard Core 4.0 (CrestApps.OrchardCore 3.0). Covers ingesting a folder of files on a schedule into a File AI data source, the per-tenant file-sources folder of the local connector, the FTP, FTPS and SFTP connectors, figure description with the vision slot and the utility slot, the File Source Ingestion background task and Read now, the CrestApps AI FileSources configuration section with FileSourceOptions, the AIFileSources admin list, the ManageFileSources permission, and custom ingestion connectors. Use it for CrestApps.OrchardCore.AI.FileSources, CrestApps.OrchardCore.AI.FileSources.Ftp, CrestApps.OrchardCore.AI.FileSources.Sftp, FileSource, FileSourceMetadata, FileSystemFileSourceMetadata, RemoteFileSourceMetadata, FtpConnectionMetadata, SftpConnectionMetadata, FigureProcessingMode, ITenantFileSourceRoot, IIngestionConnector, AddCoreIngestionConnector, IIngestionRunService, and FileSourceRunBackgroundTask.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI File Sources

## Ingest folders of files into the AI knowledge base

You are an Orchard Core expert. Generate configuration and code for CrestApps file sources: a **file source** reads a folder on a schedule and ingests every file into a **File** AI data source, storing each file's text, figures, charts, and tables as separately retrievable knowledge. AI profiles attached to that data source answer from the folder and can cite the figure they used.

### Guidelines

- Use file sources for files that already live somewhere (a server folder, an FTP drop, an SFTP export). Files users upload in a chat belong to AI Documents (see `orchardcore-ai-documents`).
- `CrestApps.OrchardCore.AI.FileSources` depends on `CrestApps.OrchardCore.AI.DataSources` and `CrestApps.OrchardCore.AI.Documents`, and ships the local file-system connector.
- The FTP and SFTP features only add ingestion connectors. They do not start an MCP server; the MCP FTP and SFTP resource features are separate (see `orchardcore-ai-mcp-resources`).
- A file source targets a **File** data source, which needs a knowledge base index with an embedding deployment (see `orchardcore-ai-data-sources`).
- The local connector can only read the tenant's own `App_Data/Sites/{TenantName}/file-sources` folder. That root is computed from the tenant identity and cannot be widened by configuration or by a request.
- Figures are described only when a deployment declaring `imageInput` fills the `vision` slot, or is named on the source.
- Keep FTP and SFTP credentials out of source control; the editor never sends a stored secret back.
- Install packages in the web/startup project.

### Features

| Feature | Feature ID | Depends on |
|---|---|---|
| AI File Sources | `CrestApps.OrchardCore.AI.FileSources` | `CrestApps.OrchardCore.AI.DataSources`, `CrestApps.OrchardCore.AI.Documents` |
| AI File Sources - FTP | `CrestApps.OrchardCore.AI.FileSources.Ftp` | AI File Sources |
| AI File Sources - SFTP | `CrestApps.OrchardCore.AI.FileSources.Sftp` | AI File Sources |

### Enable with a remote connector

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Elasticsearch",
        "CrestApps.OrchardCore.AI.DataSources.Elasticsearch",
        "CrestApps.OrchardCore.AI.FileSources",
        "CrestApps.OrchardCore.AI.FileSources.Sftp"
      ],
      "disable": []
    }
  ]
}
```

Add `CrestApps.OrchardCore.AI.Documents.Pdf` and `CrestApps.OrchardCore.AI.Documents.OpenXml` when the folder holds PDF or Office files (see `orchardcore-ai-documents-extractors`).

### Setup

1. Create a knowledge base index with an embedding deployment (**Search > Indexing**).
2. Under **Artificial Intelligence > Data Sources**, add a data source of type **File** and choose the destination index. A File data source has no connection settings and no field mapping.
3. Under **Artificial Intelligence > File Sources**, choose **Add File Source** and pick a connector (**File system**, **FTP / FTPS**, or **SFTP**).
4. Fill the shared fields and the connector section, then save.
5. Use **Read now** in the row's **Actions** menu to run immediately, or wait for the hourly task.
6. Attach the File data source to an AI profile's **Knowledge** tab.

### Shared fields

| Field | Stored as | Notes |
|---|---|---|
| **Name** | `FileSource.DisplayText` | Required |
| **Target File data source** | `FileSource.AIDataSourceId` | Required |
| **Enabled** | `FileSource.Enabled` | Disabled sources are skipped by the schedule |
| **Re-read interval (minutes)** | `FileSource.ReindexIntervalMinutes` | Empty uses `DefaultRunIntervalMinutes` |
| **Figures** | `FileSourceMetadata.FigureMode` | `Auto` (default), `All`, or `Off` |
| **Figures described per document** | `FileSourceMetadata.MaxFigureDescriptionsPerDocument` | Default 25 |
| **Vision deployment** | `FileSourceMetadata.VisionDeploymentName` | Empty uses the site's `vision` slot |
| **Utility deployment** | `FileSourceMetadata.UtilityDeploymentName` | Empty uses the site's `utility` slot |
| **Items per run** | `FileSourceMetadata.MaxItemsPerRun` | Empty uses the host default |
| **Language** | `FileSourceMetadata.Language` | BCP-47 tag of the corpus, when uniform |

With figures `Off` nothing is described; with no vision deployment a figure is stored with its caption but never described.

### Connectors

| Connector | `FileSource.Source` | Settings |
|---|---|---|
| File system | `FileSystem` | `FileSystemFileSourceMetadata`: **Folder** (`RootPath`, relative to the tenant's `file-sources` folder, empty is the folder itself), **Include sub-folders** (`Recursive`, default on), **Files per listing** (`MaxItems`) |
| FTP / FTPS | `Ftp` | `RemoteFileSourceMetadata` (`RootPath` default `/`, `Recursive`, `MaxItems`) plus `FtpConnectionMetadata`: host, port (21 when empty), username, password, encryption mode, data connection type, accept any certificate, connect and read timeouts in milliseconds, retry count |
| SFTP | `Sftp` | `RemoteFileSourceMetadata` plus `SftpConnectionMetadata`: host, port, username, password or private key with optional passphrase, optional proxy, connection timeout, keep-alive interval |

The local folder is resolved on canonical paths and each segment is checked for links, so `..` or a symbolic link that leaves the tenant folder is rejected (*That folder is outside the folder this tenant may read.*). The editor creates the tenant folder on demand. Remote connectors are not confined to a folder, because the credentials already scope them.

Leaving a password, private key, or passphrase blank on save keeps the stored value; type a new one to replace it.

### Scheduling

- **File Source Ingestion** (`FileSourceRunBackgroundTask`, `0 * * * *`) runs per tenant every hour and runs the enabled sources whose interval has elapsed. An interval under 60 minutes still runs at most hourly.
- **Read now** runs one source immediately through `IIngestionRunService`, ignoring the interval. The list shows each source's last run status and counts.
- Deleting a file source also removes the knowledge it produced.

### Configuration

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "FileSources": {
          "MaxItemsPerRun": 200,
          "MaxConcurrentFetches": 2,
          "DefaultRunIntervalMinutes": 1440
        }
      }
    }
  }
}
```

| Key | Default | Meaning |
|---|---|---|
| `MaxItemsPerRun` | 200 | Items one run may take on when the source sets no limit |
| `MaxConcurrentFetches` | 2 | Files fetched at a time |
| `DefaultRunIntervalMinutes` | 1440 | Interval of a source that names none |
| `RunCheckIntervalMinutes` | 15 | Poll interval of the framework's hosted scheduler; no effect under Orchard Core |

The section binds to `FileSourceOptions` through `IShellConfiguration`. `AllowedLocalRoots` is discarded on purpose and replaced by the tenant folder, and the file-system connector's own allowed roots and base path are post-configured to that folder. A tenant's own `App_Data/Sites/{tenant}/appsettings.json` writes the same keys starting at `CrestApps`, without the `OrchardCore` wrapper.

### Admin list and permissions

**Artificial Intelligence > File Sources** is an `AdminList` named `AIFileSources` with the grid columns `Name`, `Connector`, `LastRun`, `Modified`, and `Actions`; rows are the `FileSource` shape in `SummaryAdmin` and `SummaryAdminGrid` (see `orchardcore-admin-lists`). Every screen, including **Read now**, requires `ManageFileSources` (**Manage file sources**, Administrator by default). Citations of an ingested figure or chart link to a figure download endpoint guarded by `OrchardKnowledgeFigureAuthorizationHandler`.

File sources have no recipe or deployment step; create them in the admin or in code through `ISourceCatalogManager<FileSource>`.

### Add a custom connector

A connector answers two questions: what is there (`DiscoverAsync`), and give me that one (`FetchAsync`). Readers, figure processing, and storage are shared.

```csharp
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreIngestionConnector<SharePointIngestionConnector>("SharePoint", descriptor =>
        {
            descriptor.DisplayName = S["SharePoint"];
            descriptor.Description = S["Reads files from a SharePoint document library."];
        });

        services.AddDisplayDriver<FileSource, SharePointFileSourceDisplayDriver>();
    }
}
```

| `IIngestionConnector` member | Contract |
|---|---|
| `Name` | The registered name, stored as `FileSource.Source` |
| `ValidateAsync(source, result, cancellationToken)` | Add failures to the `ValidationResultDetails` |
| `DiscoverAsync(source, continuationToken, cancellationToken)` | List items. A connector that could not list everything must say so; a partial listing taken as complete deletes what it missed. |
| `FetchAsync(source, itemId, cancellationToken)` | Return the content, or `null` when the item is gone or empty |

The display driver returns `null` unless `fileSource.Source` is your connector name, stores its settings with `fileSource.Put(...)`, and protects secrets with data protection. The same connector shape also serves web crawler records (see `orchardcore-ai-web-crawlers`).

### Troubleshooting

| Symptom | Check |
|---|---|
| No **File** data source to target | Create one under **Data Sources** first; the file source editor says so when none exists. |
| *That folder is outside the folder this tenant may read* | Use a path inside `App_Data/Sites/{TenantName}/file-sources`, without `..` or links that leave it. |
| Source never runs | It is disabled, its interval has not elapsed, or background tasks are off for the tenant. |
| Figures not described | No `imageInput` deployment fills the `vision` slot, or **Figures** is `Off`. |
| PDF or Office files skipped | Enable the PDF or OpenXml extractor feature. |
| Answers do not cite files | The File data source is not attached to the profile, or its knowledge base has no embedding deployment. |
