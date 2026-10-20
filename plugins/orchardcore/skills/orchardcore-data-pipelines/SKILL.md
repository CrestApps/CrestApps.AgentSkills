---
name: orchardcore-data-pipelines
description: Skill for the Orchard Core Data Pipelines module (OrchardCore.DataPipelines), the visual ETL designer that reads data sources, transforms rows and delivers files or content items. Covers the designer, drafts and published versions, previews, source, transform, file and destination steps, Save content items, the FTP and SFTP modules, web API, email and download link destinations, placeholders, background runs, MaxConcurrentRuns, StepMemoryLimitMegabytes, DataPipelineOptions, incremental runs, secrets, permissions, the Run Data Pipeline Task and Data Pipeline Run Completed Event workflow activities, the DataPipelines recipe and deployment steps, and custom steps with AddDataPipelineStep, DataPipelineStepType, DataPipelineStepDisplayDriver, GetState, SetState, OnSucceeded, IDataPipelineHandler and IDataPipelineRunHandler. Use this skill when requests mention Orchard Core data pipelines, scheduled data exports, importing CSV or Excel as content items, ETL, or related implementation or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Data Pipelines

The Data Pipelines module ships with Orchard Core 4.0. Administrators design pipelines under **Tools > Data Pipelines** that read data, transform it and deliver it, without code. Pipelines read the data sources and use the formula language of the Data Pipelines Core feature (see `orchardcore-data-sources`).

## Features and modules

| Feature / module | Description |
|------------------|-------------|
| `OrchardCore.DataPipelines.Core` | Data sources, file formats, formulas, data operations, shared links. Enabled by dependency only. |
| `OrchardCore.DataPipelines` | Designer, runs and built-in steps. Depends on `OrchardCore.DataPipelines.Core` and `OrchardCore.Users`. |
| `OrchardCore.DataPipelines.Ftp` (Data Pipelines - FTP) | **Upload to an FTP server** step (FluentFTP). |
| `OrchardCore.DataPipelines.Sftp` (Data Pipelines - SFTP) | **Upload to an SFTP server** step (SSH.NET). |

Optional integrations light up when their features are enabled: Media (**Read a media file**, **Save to the media library**), Email (**Send by email**, **Share a download link**), Contents (**Save content items**, content items data source), Workflows (activities), Users and Queries (data sources).

```xml
<!-- Web/startup project -->
<PackageReference Include="OrchardCore.DataPipelines" Version="4.*" />
<PackageReference Include="OrchardCore.DataPipelines.Ftp" Version="4.*" />
<PackageReference Include="OrchardCore.DataPipelines.Sftp" Version="4.*" />
```

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.DataPipelines",
        "OrchardCore.DataPipelines.Sftp"
      ]
    }
  ]
}
```

## Concepts

- A pipeline is a graph of steps connected by ports. Ports carry **rows** (typed fields) or **files**; a connection joins ports of the same kind. An output can feed several steps; some inputs accept several connections.
- Categories: **Source** (rows out), **Transform** (rows in/out), **File** (rows in, files out), **Destination** (files in, or rows in for **Save content items**).
- All steps run concurrently on batches, so memory does not depend on the row count. Sort, join, group and distinct hold up to `StepMemoryLimitMegabytes` and spill to temporary run files.
- The designer validates each step (missing settings, unknown fields, unconnected required inputs, loops, disabled features) before publishing.

## Designer, drafts and versions

- The designer edits a **draft**, auto-saved; concurrent saves are rejected with an offer to reload or overwrite.
- **Publish** creates numbered versions; pipelines with errors cannot be published. **Discard draft** and **Versions** (view, restore into the draft) are available.
- Runs always execute the **published version** with the access of the **user who published it**, which is why `ManageDataPipelines` is security critical.
- **Previews** (Data tab) run the draft up to the selected step with the previewing user's access, read at most `PreviewRowLimit` rows per source, never execute destinations, and stop after two minutes.

## Built-in steps

| Category | Steps (type name) |
|----------|-------------------|
| Source | **Read a data source** (`DataSource`): data set, fields, filters, max rows, parameters (`name=value` per line), **Read only new rows**. **Read a media file** (`ReadMediaFile`): CSV, Excel, JSON, JSON Lines from the media library. |
| Transform | **Filter rows** (`Filter`, Matched/Unmatched outputs), **Add calculated fields** (`CalculatedFields`), **Convert time zone** (`ConvertTimeZone`), **Select fields** (`SelectFields`), **Sort rows** (`Sort`), **Group and summarize** (`Aggregate`), **Join** (`Join`, Left/Right inputs), **Combine rows** (`Union`), **Remove duplicates** (`Distinct`), **Keep first rows** (`Limit`). |
| File | **Create a file** (`CreateFile`: csv, xlsx, json, jsonl or any registered format; one worksheet per connection for Excel). **Zip files** (`ZipFiles`). |
| Destination | **Save to the media library** (`SaveToMedia`), **Share a download link** (`ShareDownloadLink`), **Upload to an FTP server** (`UploadToFtp`), **Upload to an SFTP server** (`UploadToSftp`), **Send to a web API** (`SendToWebApi`), **Send by email** (`SendByEmail`), **Save content items** (`SaveContentItems`, Rejected rows output with an `Error` field). |

Notes:

- Step formulas are per row (no aggregate functions); use **Group and summarize** for counts and sums.
- **Save content items** creates or updates items matched by content item id or display text, validates like the editor, can publish or save drafts, and requires edit (and publish) permission for the run user.
- **Save to the media library** only saves allowed extensions. `.csv`, `.json`, `.jsonl` and `.zip` are not allowed by default; add them to `OrchardCore:Media:AllowedFileExtensions` (the list replaces the defaults). Use **Share a download link** for data that must stay private.
- **Send to a web API** uses the `DataPipelines` named `HttpClient` (`services.AddHttpClient("DataPipelines")` to configure a proxy); credentials go in the authentication settings, not headers.
- SFTP: set the **host key fingerprint** (`SHA256:...`) to pin the server key; without it the step warns and logs the presented fingerprint.

### Placeholders

File names, FTP/SFTP remote folders, email subject/body, share link subject, and source parameter/filter values accept `{PipelineName}`, `{PipelineId}`, `{RunId}`, `{Date:format}` (run start, site time zone; `{Date}` is `yyyy-MM-dd`), `{Parameter:name}` and `{FileNames}` (email only). The default file name is `{PipelineName}-{Date:yyyyMMdd-HHmmss}`.

## Runs

- Started by **Run now** / **Run** (`RunDataPipelines` permission) or the **Run Data Pipeline Task** workflow activity. Disabled or never-published pipelines do not run.
- Runs are queued and execute in the background; one run per pipeline at a time, at most `MaxConcurrentRuns` per tenant. A run silent for 10 minutes is marked failed.
- Temporary files live under `App_Data/Sites/{TenantName}/DataPipelines/Runs/{RunId}` and are deleted when the run ends.
- **Run History** shows status, steps, rows/files in and out, deliveries, warnings and the last 200 log messages; queued or running runs can be cancelled. Runs are deleted after `RunRetentionDays`.

### Incremental runs

- **Read only new rows** on a growing number or date field (e.g. `ModifiedUtc`) keeps the largest value read; the next run reads only larger values. The first run reads everything.
- Kept values are saved only when a run succeeds; failed or cancelled runs re-read the same rows, so destinations should upsert by key.
- **Start over** (pipeline **Actions** menu) forgets kept values; `DataPipelineStateManager.ClearAsync(pipelineId)` does the same in code.
- When every change must be delivered, read a change queue with a custom data source that calls `query.Context.OnSucceeded(...)`.

## Configuration

`DataPipelineOptions` binds to `OrchardCore:DataPipelines` (all tenants or per tenant):

```json
{
  "OrchardCore": {
    "DataPipelines": {
      "StepMemoryLimitMegabytes": 32,
      "MaxConcurrentRuns": 4,
      "RunRetentionDays": 30,
      "PreviewRowLimit": 100
    }
  }
}
```

```csharp
services.Configure<DataPipelineOptions>(options =>
{
    options.StepMemoryLimitMegabytes = 128;
    options.MaxConcurrentRuns = 2;
});
```

`RunRetentionDays = 0` keeps runs forever.

## Security

| Permission | Allows |
|------------|--------|
| `ManageDataPipelines` | Create, design, preview, publish and delete. Security critical; implies the others. |
| `RunDataPipelines` | Run published pipelines and cancel runs. Implies `ViewDataPipelines`. |
| `ViewDataPipelines` | List pipelines, read-only designer, view runs. |
| `ManageSharedLinks` | Manage **Tools > Shared Links** (Data Pipelines Core). |

Permissions are on `OrchardCore.DataPipelines.DataPipelinePermissions`. Administrator has all of them by default.

Secrets (passwords, private keys, passphrases, tokens, API keys) are encrypted with the tenant's Data Protection keys, never shown again, never logged, and not exported. A secret copied from another site cannot be decrypted and must be re-entered.

## Workflows

With `OrchardCore.Workflows` enabled, two activities appear in the **Data** category:

- **Run Data Pipeline Task**: pipeline, **Parameters** (Liquid, one `name=value` per line, read with `{Parameter:name}`), **Skip when a run is already queued or running** (outcome `Skipped`), **Wait for the run to end** (default; outcomes `Succeeded`/`Failed`, otherwise `Queued`). Fails immediately for a missing, unpublished or disabled pipeline.
- **Data Pipeline Run Completed Event**: triggers for one or any pipeline on every, succeeded, or failed/cancelled run. The last result exposes `RunId`, `PipelineId`, `PipelineName`, `Status`, `Error`, `CompletedUtc`, `Deliveries` and `Steps`.

Schedule pipelines with a **Timer Event** followed by **Run Data Pipeline Task** with skip-when-active enabled (see `orchardcore-workflows`).

## Recipes and deployment

The **Data Pipelines** deployment step (category **Data**, `IncludeAll` by default, or selected `PipelineIds`) exports the published definition (or the draft if never published), without secrets (settings starting with `Protected`), runs or shared links.

The `DataPipelines` recipe step imports pipelines. Step settings live in `Properties` under the settings type name, as an export shows them:

```json
{
  "steps": [
    {
      "name": "DataPipelines",
      "Pipelines": [
        {
          "PipelineId": "4fa5t9rfmxagnt3ab966tzrk5r",
          "Name": "Blog posts export",
          "Description": "Exports the blog posts every night.",
          "IsEnabled": true,
          "Definition": {
            "Steps": [
              {
                "StepId": "source",
                "Type": "DataSource",
                "Title": "Blog posts",
                "X": 180,
                "Y": 320,
                "Properties": {
                  "DataSourceStepSettings": {
                    "Source": "Contents",
                    "DataSet": "BlogPost",
                    "Fields": [ "ContentItemId", "DisplayText", "ModifiedUtc" ],
                    "IncrementalField": "ModifiedUtc"
                  }
                }
              },
              {
                "StepId": "file",
                "Type": "CreateFile",
                "X": 480,
                "Y": 320,
                "Properties": {
                  "CreateFileStepSettings": {
                    "Format": "xlsx",
                    "FileName": "posts-{Date:yyyyMMdd}",
                    "UseDisplayNames": true
                  }
                }
              }
            ],
            "Connections": [
              { "SourceStepId": "source", "SourcePort": "Output", "TargetStepId": "file", "TargetPort": "Input" }
            ]
          }
        }
      ]
    }
  ]
}
```

- Pipelines are matched by `PipelineId` (max 26 characters); existing ones get the imported name, description and definition **as a draft** and keep their published version. Missing ids create a new pipeline.
- Imported definitions never run until a user publishes them (runs use the publisher's access). Re-enter secrets before publishing.
- `IsEnabled` defaults to `true`.

## Extending data pipelines

| To add | Implement | Register |
|--------|-----------|----------|
| A data source | `RecordDataSource` / `IDataSource` | `services.AddDataSource<T>()` (see `orchardcore-data-sources`) |
| A file format | `IDataFileFormat` / `IDataWorkbookFormat` | `services.AddDataFileFormat<T>()` |
| A formula function | `ExpressionFunction.CreateScalar` / `CreateAggregate` | `services.AddExpressionFunction(...)` |
| A step type with editor | `DataPipelineStepType<TStep, TSettings>` + `DataPipelineStepDisplayDriver<TSettings, TViewModel>` | `services.AddDataPipelineStep<TStep, TDriver>()` |
| A step type without editor | `DataPipelineStepType<TStep>` | `services.AddDataPipelineStepType<TStep>()` |
| A step icon | `DataPipelineStepIcon-{StepName}.cshtml` | |
| Reactions to publish/delete | `IDataPipelineHandler` | `services.AddScoped<IDataPipelineHandler, T>()` |
| Reactions to run completion | `IDataPipelineRunHandler` | `services.AddScoped<IDataPipelineRunHandler, T>()` |
| Shared links for a resource | `ISharedLinkHandler` | `services.AddSharedLinkHandler<T>()` |

### Step type essentials

- Step types are stateless scoped services (namespace `OrchardCore.DataPipelines.Steps`, package `OrchardCore.DataPipelines.Abstractions`). Settings are stored on the `DataPipelineStep` entity and read with `GetSettings(step)`.
- `Name`, `DisplayName`, `Description` (`LocalizationSource`) and `Category` (`DataPipelineStepCategory`) are **static**; the class also implements `IDataPipelineStepType`.
- `Category` sets default ports; override `GetInputs`/`GetOutputs` for others (set `AllowsMany` on an input port to accept several connections).
- `DescribeAsync(DataPipelineDescribeContext)` validates settings (`AddError`, `AddWarning`) and sets output fields (`SetOutputFields`).
- `ExecuteAsync(DataPipelineStepContext)` reads `GetInput(port).ReadBatchesAsync` / `ReadFilesAsync` and writes `GetOutput(port).WriteAsync` / `WriteFileAsync`. Check `output.IsClosed`, and clone rows before changing them (rows may be shared).
- Context members: `Run.User`, `Run.Parameters`, `Run.IsPreview`, `Run.CreateFile(fileName, contentType)`, `Services`, `CancellationToken`, `LogInformation`, `LogWarning`, `AddDelivery` (never log secrets).
- Steps that read or write site data must check `Run.User` permissions.
- For sort-like steps, use `DataPipelineRowReader.OpenAsync(input)`, `DataPipelineRowReader.CreateSpillOptions(context)` and `DataPipelineRowReader.WriteRowsAsync` with the data operations.

### State and confirmation

```csharp
public override async Task ExecuteAsync(DataPipelineStepContext context)
{
    // Values saved by the last successful run.
    var dataSetId = context.GetState<string>("remoteDataSetId");

    if (dataSetId is null)
    {
        dataSetId = await _client.CreateDataSetAsync(context.CancellationToken);

        // Saved only if this run succeeds; never by a preview.
        context.SetState("remoteDataSetId", dataSetId);
    }

    var sentIds = new List<long>();

    // ... deliver rows and collect ids.

    // Runs after every step succeeded, before the state is saved. A failure fails the run.
    context.Run.OnSucceeded(async (services, cancellationToken) =>
    {
        await services.GetRequiredService<ChangeQueue>().RemoveAsync(sentIds, cancellationToken);
    });
}
```

### Handlers

```csharp
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.Services;

public sealed class ChangeQueuePipelineHandler : IDataPipelineHandler
{
    private readonly ChangeQueue _queue;

    public ChangeQueuePipelineHandler(ChangeQueue queue)
    {
        _queue = queue;
    }

    // PublishedAsync(DataPipeline, DataPipelineVersion) also has a default implementation.
    public Task DeletedAsync(DataPipeline pipeline)
        => _queue.RemoveAllAsync(pipeline.PipelineId);
}
```

`IDataPipelineHandler` runs in the scope of the change, before commit. `IDataPipelineRunHandler.CompletedAsync(DataPipelineRun run)` runs in its own scope after the run is saved.

See [references/data-pipelines-examples.md](references/data-pipelines-examples.md) for a complete custom transform step with its display driver, views, icon and registration.
