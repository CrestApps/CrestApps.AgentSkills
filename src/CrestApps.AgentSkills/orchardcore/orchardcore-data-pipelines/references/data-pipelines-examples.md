# Data Pipelines Examples

## Custom transform step: Mask text

A module that depends on the `OrchardCore.DataPipelines` feature adds a **Mask text** transform. The module project references `OrchardCore.DataPipelines.Abstractions`, `OrchardCore.DataPipelines.Core` and `OrchardCore.DataSources.Abstractions` (version `4.*`); the web/startup project references `OrchardCore.DataPipelines`.

### Manifest

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Data Masking",
    Version = "1.0.0",
    Dependencies = ["OrchardCore.DataPipelines"]
)]
```

### Step type

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.DataPipelines.Steps;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace DataMasking.Steps;

public sealed class MaskTextStepSettings
{
    public List<string> Fields { get; set; } = [];

    public int VisibleCharacters { get; set; } = 4;
}

public sealed class MaskTextStep : DataPipelineStepType<MaskTextStep, MaskTextStepSettings>, IDataPipelineStepType
{
    public const string StepName = "MaskText";

    private readonly IStringLocalizer S;

    public MaskTextStep(IStringLocalizer<MaskTextStep> localizer)
    {
        S = localizer;
    }

    public static string Name => StepName;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<MaskTextStep>("Mask text");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<MaskTextStep>("Hides all but the last characters of text fields.");

    // Transform: one required rows input, one rows output.
    public static DataPipelineStepCategory Category => DataPipelineStepCategory.Transform;

    public override Task DescribeAsync(DataPipelineDescribeContext context)
    {
        var fields = context.GetInputFields();
        var settings = GetSettings(context.Step);

        if (settings.Fields.Count == 0)
        {
            context.AddWarning(S["Choose the fields to mask."]);
        }

        foreach (var name in settings.Fields)
        {
            var field = fields.Find(name);

            if (field is null)
            {
                context.AddError(S["The input has no field named '{0}'.", name]);
            }
            else if (field.Type != DataFieldType.Text)
            {
                context.AddError(S["The field '{0}' isn't text.", name]);
            }
        }

        context.SetOutputFields(fields);

        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync(DataPipelineStepContext context)
    {
        var settings = GetSettings(context.Step);
        var output = context.GetOutput();

        await foreach (var batch in context.GetInput().ReadBatchesAsync(context.CancellationToken))
        {
            // Every downstream step stopped reading (e.g. Keep first rows).
            if (output.IsClosed)
            {
                return;
            }

            var indexes = settings.Fields.Select(batch.IndexOf).Where(index => index >= 0).ToArray();
            var rows = new List<object[]>(batch.Count);

            foreach (var source in batch.Rows)
            {
                // Rows may be shared with other steps on the same output: copy before changing.
                var row = (object[])source.Clone();

                foreach (var index in indexes)
                {
                    if (row[index] is string { Length: > 0 } text && text.Length > settings.VisibleCharacters)
                    {
                        row[index] = new string('*', text.Length - settings.VisibleCharacters) + text[^settings.VisibleCharacters..];
                    }
                }

                rows.Add(row);
            }

            // Waits while downstream steps are busy (back-pressure).
            await output.WriteAsync(new DataBatch(batch.Fields, rows), context.CancellationToken);
        }
    }
}
```

### Display driver and view model

```csharp
using OrchardCore.DataPipelines.Drivers;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DisplayManagement.Handlers;

namespace DataMasking.Drivers;

public sealed class MaskTextStepDisplayDriver : DataPipelineStepDisplayDriver<MaskTextStepSettings, MaskTextStepViewModel>
{
    protected override string StepName => MaskTextStep.StepName;

    protected override ValueTask EditAsync(DataPipelineStep step, MaskTextStepSettings settings, MaskTextStepViewModel model)
    {
        model.Fields = string.Join(", ", settings.Fields);
        model.VisibleCharacters = settings.VisibleCharacters;

        return ValueTask.CompletedTask;
    }

    protected override ValueTask UpdateAsync(
        DataPipelineStep step,
        MaskTextStepSettings settings,
        MaskTextStepViewModel model,
        UpdateEditorContext context)
    {
        settings.Fields = (model.Fields ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        settings.VisibleCharacters = Math.Max(0, model.VisibleCharacters);

        return ValueTask.CompletedTask;
    }
}

public class MaskTextStepViewModel
{
    public string Fields { get; set; }

    public int VisibleCharacters { get; set; }
}
```

Report invalid values with `context.Updater.ModelState.AddModelError(Prefix, nameof(model.Property), message)` in `UpdateAsync`.

### Views

The base driver renders `{StepName}_Fields_Edit` (Settings tab) and `{StepName}_Fields_Design` (canvas summary).

`Views/MaskText.Fields.Edit.cshtml`:

```razor
@model MaskTextStepViewModel

<div class="mb-3" asp-validation-class-for="Fields">
    <label asp-for="Fields" class="form-label">@T["Fields"]</label>
    <input asp-for="Fields" class="form-control" />
    <span asp-validation-for="Fields"></span>
    <span class="hint">@T["The text fields to mask, separated by commas."]</span>
</div>

<div class="mb-3" asp-validation-class-for="VisibleCharacters">
    <label asp-for="VisibleCharacters" class="form-label">@T["Visible characters"]</label>
    <input asp-for="VisibleCharacters" type="number" min="0" class="form-control" />
    <span asp-validation-for="VisibleCharacters"></span>
</div>
```

`Views/MaskText.Fields.Design.cshtml`:

```razor
@model MaskTextStepViewModel

<span>@T["Masks {0}", Model.Fields]</span>
```

`Views/DataPipelineStepIcon-MaskText.cshtml` (otherwise the category icon is used):

```html
<i class="fa-solid fa-mask" aria-hidden="true"></i>
```

### Registration

```csharp
using OrchardCore.Modules;

namespace DataMasking;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataPipelineStep<MaskTextStep, MaskTextStepDisplayDriver>();
    }
}
```

## Sort-like step with data operations

```csharp
using OrchardCore.DataPipelines.Steps;
using OrchardCore.DataSources.Operations;

public override async Task ExecuteAsync(DataPipelineStepContext context)
{
    await using var reader = await DataPipelineRowReader.OpenAsync(context.GetInput(), context.CancellationToken);

    var comparer = DataSorter.CreateComparer([(reader.Fields.IndexOf("Amount"), Descending: true)]);

    // Spill options use StepMemoryLimitMegabytes and the run's temporary folder.
    var sorted = DataSorter.SortAsync(
        reader.ReadRowsAsync(),
        comparer,
        DataPipelineRowReader.CreateSpillOptions(context),
        context.CancellationToken);

    await DataPipelineRowReader.WriteRowsAsync(context.GetOutput(), reader.Fields, sorted, context.CancellationToken);
}
```

## Run completion handler

```csharp
using Microsoft.Extensions.Logging;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.Services;

public sealed class FailedRunHandler : IDataPipelineRunHandler
{
    private readonly ILogger _logger;

    public FailedRunHandler(ILogger<FailedRunHandler> logger)
    {
        _logger = logger;
    }

    public Task CompletedAsync(DataPipelineRun run)
    {
        if (run.Status == DataPipelineRunStatus.Failed)
        {
            _logger.LogWarning("The data pipeline '{PipelineName}' failed: {Error}", run.PipelineName, run.Error);
        }

        return Task.CompletedTask;
    }
}
```

```csharp
services.AddScoped<IDataPipelineRunHandler, FailedRunHandler>();
```

## Import a CSV from the media library as content items (recipe)

The settings type names below are the ones an export shows. Prefer exporting a pipeline designed in the admin and editing the resulting recipe.

```json
{
  "steps": [
    {
      "name": "DataPipelines",
      "Pipelines": [
        {
          "PipelineId": "productsimport000000000001",
          "Name": "Products import",
          "IsEnabled": true,
          "Definition": {
            "Steps": [
              {
                "StepId": "read",
                "Type": "ReadMediaFile",
                "X": 180,
                "Y": 320,
                "Properties": {
                  "ReadMediaFileStepSettings": { "Path": "imports/products.csv" }
                }
              },
              {
                "StepId": "save",
                "Type": "SaveContentItems",
                "X": 480,
                "Y": 320,
                "Properties": {}
              }
            ],
            "Connections": [
              { "SourceStepId": "read", "SourcePort": "Output", "TargetStepId": "save", "TargetPort": "Input" }
            ]
          }
        }
      ]
    }
  ]
}
```

The imported pipeline is a draft: open it in the designer, finish the **Save content items** mapping, and publish it.
