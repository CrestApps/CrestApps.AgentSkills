---
name: orchardcore-data-pipeline-steps
description: Skill for extending Orchard Core Data Pipelines in code. Covers a custom Data Pipelines step with DataPipelineStepType and IDataPipelineStepType, categories and ports, DescribeAsync, ExecuteAsync and the step context, back-pressure, DataPipelineRowReader for sort-like steps, step icons with the DataPipelineStepIcon shape, step editors with DataPipelineStepDisplayDriver, DataPipelineEditorContext and the designer editor contract (auto-apply, data-dpd-reload, the data-pipelines-editors script, ocat views), step secrets with DataPipelineSecrets, AddDataPipelineStep, keeping values between runs with GetState and SetState, OnSucceeded, IDataPipelineHandler, IDataPipelineRunHandler, shared links with ISharedLinkHandler and ISharedLinkManager, and building an incremental export. Use it for a custom data pipeline step, transform, source or destination, a step editor or icon, reacting to pipeline runs, sharing a module resource through links, or exporting data incrementally to an external system.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Data Pipeline Steps (extending Data Pipelines)

Modules extend the Data Pipelines module (Orchard Core 4.0) with services registered in their startup. This skill covers step types and everything around them. Using the module: `orchardcore-data-pipelines`. Data sources and file formats: `orchardcore-data-sources`. Formula functions and filter conditions: `orchardcore-data-formulas`.

## Extension points

| To add | Implement | Register |
|--------|-----------|----------|
| A transform (mask, enrich), source or destination | `DataPipelineStepType<TStep, TSettings>`, `IDataPipelineStepType` | `services.AddDataPipelineStep<TStep, TDriver>()` |
| A transform that sees every row first (sort-like) | A step type with `DataPipelineRowReader` and the data operations | same |
| The editor of a step | `DataPipelineStepDisplayDriver<TSettings, TViewModel>` + views | with its step type |
| The icon of a step type | `DataPipelineStepIcon-{StepName}.cshtml` | |
| Values kept between runs | `context.GetState` / `context.SetState` | |
| Work confirmed when a run succeeds | `context.Run.OnSucceeded` | |
| Reactions to pipeline publish/delete | `IDataPipelineHandler` | `services.AddScoped<IDataPipelineHandler, T>()` |
| Reactions to the end of runs | `IDataPipelineRunHandler` | `services.AddScoped<IDataPipelineRunHandler, T>()` |
| Links to a module resource | `ISharedLinkHandler` | `services.AddSharedLinkHandler<T>()` |

## Packages and dependencies

Install `OrchardCore.DataPipelines` in the web/startup project. The module project references what it uses (version `4.*`):

| Package | Provides |
|---------|----------|
| `OrchardCore.DataPipelines.Abstractions` | `DataPipelineStepType<,>`, `IDataPipelineStepType`, contexts, ports, `DataPipelineStep`, shared link abstractions and `AddSharedLinkHandler` |
| `OrchardCore.DataPipelines.Core` | `AddDataPipelineStepType<T>()`, `DataPipelineRowReader`, `DataPipelineFormulas`, `DataPipelineOptions`, `IDataTimeZoneResolver` |
| `OrchardCore.DataPipelines` | `DataPipelineStepDisplayDriver<,>`, `AddDataPipelineStep<,>()`, `DataPipelineEditorContext`, `DataPipelineSecrets`, `IDataPipelineHandler`, `IDataPipelineRunHandler`, `DataPipelineStateManager` |
| `OrchardCore.DataSources.Abstractions` / `.Core` | `DataField`, `DataBatch`, `DataFieldType`, data operations |

```csharp
[assembly: Module(
    Name = "Data Masking",
    Version = "1.0.0",
    Dependencies = ["OrchardCore.DataPipelines"]
)]
```

## Adding a step type

A step type is a **stateless, scoped** service; each step's settings are stored on its `DataPipelineStep` (an `Entity`, under `Properties.{TSettings}`). Derive from `DataPipelineStepType<TStep, TSettings>` with the step type's own class as `TStep` (or `DataPipelineStepType<TStep>` without settings) and implement `IDataPipelineStepType`, whose `Name`, `DisplayName`, `Description` and `Category` are **static abstract** members, so the designer lists step types and the engine knows categories without creating them. Read settings with `GetSettings(step)` (`SetSettings` writes them).

| `Category` | Default inputs | Default outputs |
|------------|----------------|-----------------|
| `Source` | none | `Output` (rows) |
| `Transform` | `Input` (rows, required) | `Output` (rows) |
| `File` | `Input` (rows, required) | `Output` (files) |
| `Destination` | `Input` (files, required) | none |

- Override `GetInputs(step)` / `GetOutputs(step)` for other ports: `new DataPipelinePort(name, displayName, DataPipelinePortKind.Records | Files) { IsRequired = true, AllowsMany = true }`. Examples: **Join** has `Left` and `Right`; **Filter rows** has `Matched` and `Unmatched`; **Save content items** is a `Destination` with a rows `Input` and a `Rejected` output.
- **Destinations are never executed by a preview**; the category also tells the designer the step has effects outside the run.
- `DescribeAsync(DataPipelineDescribeContext)` validates settings against the input fields and sets the fields of each rows output. The designer calls it to list fields and issues; the engine calls it before a run. The default passes the input fields to every rows output.
  - `GetInputFields(port)` (first connection), `GetInputFieldSets(port)` (each connection), `SetOutputFields(fields, port)`, `AddError(message)` (blocks publishing and running), `AddWarning(message)`, `User`, `Services`, `CancellationToken`.
  - `fields.Find(name)` and `fields.IndexOf(name)` (`DataFieldListExtensions`) look fields up by technical name.
- `ExecuteAsync(DataPipelineStepContext)` reads inputs and writes outputs until inputs are exhausted. It runs **at the same time** as the other steps, in its own scope. Throw to fail the step (a failing step stops the run).

```csharp
public sealed class MaskTextStep : DataPipelineStepType<MaskTextStep, MaskTextStepSettings>, IDataPipelineStepType
{
    public const string StepName = "MaskText";

    public static string Name => StepName;
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<MaskTextStep>("Mask text");
    public static LocalizationSource Description { get; } = LocalizationSource.Create<MaskTextStep>("Hides all but the last characters of text fields.");
    public static DataPipelineStepCategory Category => DataPipelineStepCategory.Transform;

    public override async Task ExecuteAsync(DataPipelineStepContext context)
    {
        var output = context.GetOutput();

        await foreach (var batch in context.GetInput().ReadBatchesAsync(context.CancellationToken))
        {
            // Every step connected to the output stopped reading, such as Keep first rows.
            if (output.IsClosed)
            {
                return;
            }

            // Rows may be shared with other steps on the same output: clone before changing them.
            // Mask is the step's own helper (see the references file).
            var rows = batch.Rows.Select(row => Mask((object[])row.Clone())).ToList();

            // Waits while downstream steps are busy (back-pressure).
            await output.WriteAsync(new DataBatch(batch.Fields, rows), context.CancellationToken);
        }
    }
}
```

The complete step, with `DescribeAsync`, driver, views, icon and registration, is in [references/data-pipeline-steps-examples.md](references/data-pipeline-steps-examples.md).

### The step context

| Member | Use |
|--------|-----|
| `GetInput(port = "Input")` | `ReadBatchesAsync` (rows), `ReadFilesAsync` (files), `Fields`, `IsConnected`. With several connections: `FieldSets`, `ConnectionLabels` (the source step titles) and `ReadConnectionBatchesAsync(connection)` to read one connection, such as one worksheet each. |
| `GetOutput(port = "Output")` | `WriteAsync(batch)`, `WriteFileAsync(file)`, `IsConnected`, `IsClosed`, `Fields` (as described). |
| `Run` | `User` (who the run reads and writes for), `Parameters`, `IsPreview`, `PreviewRowLimit`, `RunId`, `PipelineId`, `PipelineName`, `VersionNumber`, `StartedUtc`, `CreateFile(fileName, contentType)` (an empty `DataPipelineFile` in the run's temporary folder; write to `file.Path`), `State`, `OnSucceeded`. |
| `Services`, `CancellationToken` | The step's scope; the token is cancelled when the run is cancelled or fails. |
| `LogInformation`, `LogWarning`, `AddDelivery(description, url)` | Run history messages (warnings are counted) and deliveries. **Never include secrets.** |
| `GetState<T>(key)`, `SetState<T>(key, value)` | Values kept between successful runs. |

The engine counts the rows and files each step reads and writes. `DataPipelineFile` exposes `FileName`, `ContentType`, `Path`, `Length`, `RowCount` and `OpenRead()`.

Rules:

- A step reads and writes data for `Run.User` (the publisher, or the designer in a preview). Check that user's permissions before touching site data, as the built-in steps do.
- `now` for a run is `await DataPipelineFormulas.GetRunTimeAsync(context.Run)` (run start in the site time zone). Render file names and texts with placeholders such as `{PipelineName}` and `{Parameter:name}` through `DataPipelineTemplate.Render(template, context.Run, now)` or `RenderFileName(...)` (plus `WithExtension(fileName, extension)`).
- Formulas of a custom step compile with `DataPipelineFormulas.TryCompile(formula, fields, catalog, S, out error)` (rejects aggregates) and evaluate with `DataPipelineFormulas.Evaluate(expression, row, now, out error)` (see `orchardcore-data-formulas`).

### Steps that see every row first

Use the data operations (see `orchardcore-data-sources`) with `DataPipelineRowReader`:

```csharp
using OrchardCore.DataPipelines.Steps;
using OrchardCore.DataSources.Operations;

public override async Task ExecuteAsync(DataPipelineStepContext context)
{
    // Reads the rows of the input as one sequence, aligned on the fields of the first batch.
    await using var reader = await DataPipelineRowReader.OpenAsync(context.GetInput(), context.CancellationToken);

    var comparer = DataSorter.CreateComparer([(reader.Fields.IndexOf("Amount"), Descending: true)]);

    // StepMemoryLimitMegabytes, and a folder in the run's temporary folder deleted with it.
    var sorted = DataSorter.SortAsync(reader.ReadRowsAsync(), comparer, DataPipelineRowReader.CreateSpillOptions(context), context.CancellationToken);

    // Writes in batches of DataSourceQuery.DefaultBatchSize and returns the row count.
    await DataPipelineRowReader.WriteRowsAsync(context.GetOutput(), reader.Fields, sorted, context.CancellationToken);
}
```

`ReadRowsAsync()` can be read once.

## Giving a step type its icon

The designer renders the `DataPipelineStepIcon` shape (properties `StepType`, `Category`) with the `DataPipelineStepIcon__{StepName}` alternate. Without a template, a step type gets its category's icon (Source `fa-database`, Transform `fa-shuffle`, File `fa-file-lines`, Destination `fa-paper-plane`). Add `Views/DataPipelineStepIcon-MaskText.cshtml`:

```html
<i class="fa-solid fa-mask" aria-hidden="true"></i>
```

A theme or module overrides the icon of any step type the same way.

## Adding the editor of a step type

Derive from `DataPipelineStepDisplayDriver<TSettings, TViewModel>` (namespace `OrchardCore.DataPipelines.Drivers`). It renders `{StepName}_Fields_Edit` (the **Settings** tab, from `{StepName}.Fields.Edit.cshtml`) and `{StepName}_Fields_Design` (the canvas summary, from `{StepName}.Fields.Design.cshtml`), both filled by `EditAsync`, and saves with `UpdateAsync`:

```csharp
public sealed class MaskTextStepDisplayDriver : DataPipelineStepDisplayDriver<MaskTextStepSettings, MaskTextStepViewModel>
{
    private readonly DataPipelineEditorContext _editorContext;

    public MaskTextStepDisplayDriver(DataPipelineEditorContext editorContext)
    {
        _editorContext = editorContext;
    }

    protected override string StepName => MaskTextStep.StepName;

    protected override ValueTask EditAsync(DataPipelineStep step, MaskTextStepSettings settings, MaskTextStepViewModel model)
    {
        model.Fields = [.. settings.Fields];
        model.VisibleCharacters = settings.VisibleCharacters;

        // The fields of the step's input, to offer in a picker.
        model.AvailableFields = _editorContext.GetInputFields();

        return ValueTask.CompletedTask;
    }

    protected override ValueTask UpdateAsync(DataPipelineStep step, MaskTextStepSettings settings, MaskTextStepViewModel model, UpdateEditorContext context)
    {
        if (model.VisibleCharacters < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.VisibleCharacters), "The number of visible characters can't be negative.");
        }

        settings.Fields = (model.Fields ?? []).Where(name => !string.IsNullOrEmpty(name)).ToList();
        settings.VisibleCharacters = Math.Max(0, model.VisibleCharacters);

        return ValueTask.CompletedTask;
    }
}

public class MaskTextStepViewModel
{
    public string[] Fields { get; set; } = [];

    public int VisibleCharacters { get; set; }

    [BindNever]
    public IReadOnlyList<DataField> AvailableFields { get; set; } = [];
}
```

`AddModelError(Prefix, key, message)` is the `OrchardCore.Mvc.ModelBinding` extension; mark view model properties that aren't posted with `[BindNever]`. `DataPipelineEditorContext` (scoped, namespace `OrchardCore.DataPipelines.Services`) is set by the designer before it renders or applies an editor: `Pipeline`, `Step`, `Analysis`, `GetInputFields(port)`, and `ReloadEditor` (set it to `true` in `UpdateAsync` when the applied settings change the form itself, such as a mode that shows other inputs).

### The designer editor contract

- The designer loads the editor as a server-rendered fragment: its markup plus the scripts and stylesheets the views required through the resource manager. The fields bind under the driver's `Prefix` (`DataPipelineStep`); use `asp-for`, or `Html.Name("Keys[0].Field")` for hand-built names.
- Settings **apply as they change**: a `change` event (blur, select, checkbox) posts the form after a short delay and runs `UpdateAsync`; `input` only marks it dirty. The designer saves the draft, refreshes issues and fields, and redraws the editor when it has errors or `ReloadEditor` is set. An invalid form asks whether to discard the changes when the user leaves the step.
- Mark a field that changes the form with the `data-dpd-reload` attribute: the editor is loaded again once its change is applied.
- Editor scripts must use **event delegation** (editors are injected and removed at any time) and release what they create on the `oc:editor-unmounting` event. Don't rely on jQuery: the 4.0 admin scripts no longer use it.
- Views follow the `ocat-*` admin conventions (`ocat-wrapper`, `ocat-label`, `ocat-end`, `ocat-limited-wrapper`, `hint`; see `orchardcore-admin-edit-views`), with `asp-validation-class-for` and `asp-validation-for`.
- The module's `data-pipelines-editors` script (`<script asp-name="data-pipelines-editors" at="Foot"></script>`, depends on `bootstrap-select`) adds behaviors by attribute:

| Attributes | Behavior |
|------------|----------|
| `data-dp-list data-dp-prefix="Keys"` with `<template data-dp-template>`, `data-dp-rows`, `data-dp-row`, `data-dp-add`, `data-dp-remove`, `data-dp-up`, `data-dp-down` | Editable row lists (`Keys[__index__].Field` in the template), renumbered after each change, raising `change`. |
| `select[data-dp-select]` with `data-live-search`, `data-show-selected-tags` | Searchable bootstrap-select, created on inject and destroyed on unmount. |
| `data-dp-formula-scope`, `data-dp-formula`, `data-dp-insert="Field"`, `data-dp-insert-function="NAME"`, `data-dp-functions`, `data-dp-function-search` | Insert `[Field]` or `NAME()` at the caret; search the function list. |
| `data-dp-filter-row` and its `data-dp-filter-*` parts | Typed filter rows that show the comparisons of the chosen field's `data-dp-type`. |
| `data-dp-dataset-picker`, `data-dp-dataset-input`, `data-dp-dataset-open` | Opens the designer's data set dialog (`dp:choose-data-set` event). |

The `DataPipelineFieldOptions` partial (model `(IReadOnlyList<DataField> Fields, string Selected, bool AllowEmpty)`) renders field `<option>`s with `data-dp-type`, keeping a selected field the input no longer has marked as missing.

### Secrets in step settings

Store secrets in settings properties whose names start with `Protected` (`DataPipelineSecrets.ProtectedPrefix`): deployment removes them. In the driver, `_secrets.Update(settings.ProtectedApiKey, model.ApiKey, model.RemoveApiKey)` keeps the current value when the posted one is empty; never send the secret back to the view (pass a `HasApiKey` flag). In the step, `context.Services.GetRequiredService<DataPipelineSecrets>().Unprotect(settings.ProtectedApiKey)` returns `null` when the value can't be decrypted (report an error). The module's `DataPipelineSecretInput` partial (model `DataPipelineSecretInputViewModel`: `Name`, `Label`, `Hint`, `KeepHint`, `HasValue`, `RemoveName`, `RemoveLabel`, `Multiline`) renders the empty password box and the remove checkbox.

### Registration

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Step type (scoped) + DisplayDriver<DataPipelineStep>.
        services.AddDataPipelineStep<MaskTextStep, MaskTextStepDisplayDriver>();
    }
}
```

`services.AddDataPipelineStepType<TStep>()` (from `OrchardCore.DataPipelines.Core`) registers a step type without an editor. Keep `Name` and the settings type name stable: saved pipelines and recipes store them.

## Keeping values between runs

`context.GetState<T>(key)` / `context.SetState<T>(key, value)` keep values per step, such as a watermark or the id a remote system gave to what the step created. A run starts with the values the last **successful** run saved; values set by a run are saved only when it succeeds, never by a preview. Values are serialized as JSON; `null` removes one.

```csharp
var dataSetId = context.GetState<string>("remoteDataSetId");

if (dataSetId is null)
{
    dataSetId = await _client.CreateDataSetAsync(context.CancellationToken);

    // Saved when the run succeeds.
    context.SetState("remoteDataSetId", dataSetId);
}
```

`context.Run.State` holds every step's values (keys `{stepId}/{key}`, see `DataPipelineStepContext.GetStateKey`). **Start over** clears them, and so does `DataPipelineStateManager.ClearAsync(pipelineId)`.

## Confirming work when a run succeeds

`context.Run.OnSucceeded((services, cancellationToken) => ...)` runs once every step succeeded, before the state is saved, with the services of a tenant scope committed when the action ends. A failed or cancelled run doesn't run it (the next run reads the same entries again); a preview never does. A failing action fails the run and its state isn't saved.

```csharp
context.Run.OnSucceeded(async (services, cancellationToken) =>
{
    await services.GetRequiredService<ChangeQueue>().RemoveAsync(ids, cancellationToken);
});
```

A data source asks for the same with `query.Context.OnSucceeded(...)` and identifies the reader with `query.Context.ConsumerId` (`{pipelineId}/{stepId}`, `null` in a preview): the **Read a data source** step passes the action on once it read every row. See `orchardcore-data-sources`.

## Reacting to changes of pipelines

`IDataPipelineHandler` (namespace `OrchardCore.DataPipelines.Services`) has `PublishedAsync(DataPipeline pipeline, DataPipelineVersion version)` and `DeletedAsync(DataPipeline pipeline)`, both with default implementations. Handlers run in the scope of the change, before it is committed.

```csharp
public sealed class ChangeQueuePipelineHandler : IDataPipelineHandler
{
    private readonly ChangeQueue _queue;

    public ChangeQueuePipelineHandler(ChangeQueue queue)
    {
        _queue = queue;
    }

    public Task DeletedAsync(DataPipeline pipeline)
        => _queue.RemoveAllAsync(pipeline.PipelineId);
}
```

```csharp
services.AddScoped<IDataPipelineHandler, ChangeQueuePipelineHandler>();
```

## Reacting to the end of runs

`IDataPipelineRunHandler.CompletedAsync(DataPipelineRun run)` runs in its own scope after the run is saved, whether it succeeded, failed or was cancelled (`run.Status`, `run.Error`, `run.Steps`, `run.Deliveries`, `run.PipelineName`). The workflow activities use it to resume waiting workflows. Example in the references file.

## Sharing a resource from a module

A module that depends on Data Pipelines Core shares its own resources (such as reports) through shared links and gets their security, expiry, use limit, emails, revocation and admin page (see `orchardcore-data-pipelines` for the security model).

```csharp
using OrchardCore.DataPipelines.SharedLinks;

public sealed class ReportLinkHandler : ISharedLinkHandler
{
    public static string Kind => "Reports.Report";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ReportLinkHandler>("Report");

    // Called once the user is signed in, is a recipient, and has the token.
    public Task<IActionResult> OpenAsync(SharedLink link, HttpContext httpContext)
        => Task.FromResult<IActionResult>(new ViewResult
        {
            ViewName = "SharedReport",
            ViewData = { ["ReportId"] = link.ResourceId },
        });
}
```

```csharp
// AddSharedLinkHandler lives in the OrchardCore.DataPipelines.SharedLinks namespace.
services.AddSharedLinkHandler<ReportLinkHandler>();
```

```csharp
var result = await _sharedLinkManager.CreateAsync(new SharedLinkRequest
{
    Kind = ReportLinkHandler.Kind,
    ResourceId = report.Id,
    Title = report.Name,
    SourceName = report.Name,
    Recipients = ["alice", "bob@example.com"],
    Lifetime = TimeSpan.FromDays(30),
    MaxUses = 10,
});
```

- `SharedLinkRequest`: `Kind`, `ResourceId`, `Title`, `SourceName`, `Properties`, `Recipients` (user names or emails of site users; requires Users), `Lifetime` (7 days default), `MaxUses` (0 = unlimited), `NotifyByEmail` (default `true`, requires Email), `Subject`, `Message`.
- `SharedLinkResult`: `Link`, `Url` (holds the token; deliver it yourself with `NotifyByEmail = false`), `Notified`.
- Links open at `/SharedLinks/{linkId}?token=...`, which calls the handler of their kind. Optional handler members: `GetSourceUrl(link, urlHelper)` (admin page shown in the shared links list) and `DeletedAsync(link)` (release the resource).
- `ISharedLinkManager`: `CreateAsync`, `GetAsync(linkId)`, `UseAsync(linkId, token, user)` (returns a `SharedLinkAccessResult`: `Allowed`, `NotFound`, `Expired`, `UsedUp`, `InvalidToken`, `NotARecipient`), `RevokeAsync`, `DeleteAsync`, paged `ListAsync(search, skip, take)`, and `ListAsync(kind, resourceId)` (most recent first, active or not):

```csharp
// When the report is deleted, delete its links too.
foreach (var link in await _sharedLinkManager.ListAsync(ReportLinkHandler.Kind, report.Id))
{
    await _sharedLinkManager.DeleteAsync(link);
}
```

## Building an incremental export

A module exports data incrementally to its own system (such as an analytics service) and leaves fields and filters to pipeline designers. It provides:

1. **The data**: a data source that describes every exportable field, including computed or discovered ones, so the designer lists them and users choose and filter them (see `orchardcore-data-sources`).
2. **The changes**: either **Read only new rows** on a field that grows with every change, or, when every change must be delivered, a data set that reads a change queue the module fills (for example from a content handler), one entry per record and per consumer (`query.Context.ConsumerId`). It returns the current values of each changed record plus a field telling whether it was changed or deleted, and removes the entries it read with `query.Context.OnSucceeded` so a failed run reads them again. An `IDataPipelineHandler` removes the entries of a deleted pipeline.
3. **The delivery**: a destination step that sends the rows and updates or deletes them **by key**, so a repeated run doesn't duplicate them. It keeps (`SetState`) what the system returns, such as the id of the data set it created and the fields it created it with, to know when to send everything again. Its credentials are `Protected` secrets.
4. **The schedule**: a workflow with a **Timer Event** and the **Run Data Pipeline Task** with **Skip when a run is already queued or running**.

Users then pick and rename fields with **Select fields**, filter with source filters or **Filter rows**, add calculated fields, and follow each run in the run history. One run of a pipeline executes at a time, with the publisher's access. A full destination step is in [references/data-pipeline-steps-examples.md](references/data-pipeline-steps-examples.md).
