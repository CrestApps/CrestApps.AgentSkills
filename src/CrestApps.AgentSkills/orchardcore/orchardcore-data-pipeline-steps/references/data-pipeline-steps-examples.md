# Data Pipeline Steps Examples

All examples target Orchard Core 4.0. The module depends on the `OrchardCore.DataPipelines` feature; the web/startup project references `OrchardCore.DataPipelines`.

```xml
<!-- Module project -->
<PackageReference Include="OrchardCore.DataPipelines" Version="4.*" />
<PackageReference Include="OrchardCore.DataPipelines.Abstractions" Version="4.*" />
<PackageReference Include="OrchardCore.DataPipelines.Core" Version="4.*" />
<PackageReference Include="OrchardCore.DataSources.Abstractions" Version="4.*" />
<PackageReference Include="OrchardCore.DataSources.Core" Version="4.*" />
```

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Data Masking",
    Version = "1.0.0",
    Dependencies = ["OrchardCore.DataPipelines"]
)]
```

## Transform step: Mask text

### Settings and step type

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

        // The step doesn't change the fields of the rows.
        context.SetOutputFields(fields);

        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync(DataPipelineStepContext context)
    {
        var settings = GetSettings(context.Step);
        var output = context.GetOutput();

        await foreach (var batch in context.GetInput().ReadBatchesAsync(context.CancellationToken))
        {
            // Every step connected to the output is done reading, such as Keep first rows.
            if (output.IsClosed)
            {
                return;
            }

            var indexes = settings.Fields.Select(batch.IndexOf).Where(index => index >= 0).ToArray();
            var rows = new List<object[]>(batch.Count);

            foreach (var source in batch.Rows)
            {
                // Rows may be shared with the other steps connected to the same output: copy before changing them.
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

            // Waits while the steps downstream are busy.
            await output.WriteAsync(new DataBatch(batch.Fields, rows), context.CancellationToken);
        }
    }
}
```

### Display driver and view model

```csharp
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using OrchardCore.DataPipelines.Drivers;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.Services;
using OrchardCore.DataSources;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Mvc.ModelBinding;
using DataMasking.Steps;

namespace DataMasking.Drivers;

public sealed class MaskTextStepDisplayDriver : DataPipelineStepDisplayDriver<MaskTextStepSettings, MaskTextStepViewModel>
{
    private readonly DataPipelineEditorContext _editorContext;

    internal readonly IStringLocalizer S;

    public MaskTextStepDisplayDriver(DataPipelineEditorContext editorContext, IStringLocalizer<MaskTextStepDisplayDriver> localizer)
    {
        _editorContext = editorContext;
        S = localizer;
    }

    protected override string StepName => MaskTextStep.StepName;

    // Fills both the editor (Settings tab) and the canvas summary.
    protected override ValueTask EditAsync(DataPipelineStep step, MaskTextStepSettings settings, MaskTextStepViewModel model)
    {
        model.Fields = [.. settings.Fields];
        model.VisibleCharacters = settings.VisibleCharacters;
        model.AvailableFields = _editorContext.GetInputFields();

        return ValueTask.CompletedTask;
    }

    // Runs a moment after each change in the editor.
    protected override ValueTask UpdateAsync(DataPipelineStep step, MaskTextStepSettings settings, MaskTextStepViewModel model, UpdateEditorContext context)
    {
        if (model.VisibleCharacters < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.VisibleCharacters), S["The number of visible characters can't be negative."]);
        }

        settings.Fields = (model.Fields ?? []).Where(name => !string.IsNullOrEmpty(name)).Distinct().ToList();
        settings.VisibleCharacters = Math.Max(0, model.VisibleCharacters);

        return ValueTask.CompletedTask;
    }
}

public class MaskTextStepViewModel
{
    public string[] Fields { get; set; } = [];

    public int VisibleCharacters { get; set; }

    // Not posted: the fields of the step's input, offered by the picker.
    [BindNever]
    public IReadOnlyList<DataField> AvailableFields { get; set; } = [];
}
```

### Views

`Views/MaskText.Fields.Edit.cshtml` (add `@using DataMasking.Drivers` and `@using OrchardCore.DataSources` to the module's `_ViewImports.cshtml`):

```razor
@model MaskTextStepViewModel

@* Turns select[data-dp-select] into a searchable picker; requires bootstrap-select. *@
<script asp-name="data-pipelines-editors" at="Foot"></script>

<div class="ocat-wrapper" asp-validation-class-for="Fields">
    <label asp-for="Fields" class="ocat-label ocat-label-required">@T["Fields"]</label>
    <div class="ocat-end">
        <select asp-for="Fields" multiple class="form-select" data-dp-select data-live-search="true" data-show-selected-tags="true">
            @foreach (var field in Model.AvailableFields.Where(field => field.Type == DataFieldType.Text))
            {
                <option value="@field.Name" selected="@Model.Fields.Contains(field.Name)">@field.Name</option>
            }
            @foreach (var missing in Model.Fields.Where(name => Model.AvailableFields.All(field => field.Name != name)))
            {
                <option value="@missing" selected>@T["{0} (missing)", missing]</option>
            }
        </select>
        <span asp-validation-for="Fields"></span>
        <span class="hint">@T["The text fields to mask."]</span>
    </div>
</div>

<div class="ocat-limited-wrapper" asp-validation-class-for="VisibleCharacters">
    <label asp-for="VisibleCharacters" class="ocat-label">@T["Visible characters"]</label>
    <div class="ocat-limited">
        <input asp-for="VisibleCharacters" type="number" min="0" class="form-control" />
        <span asp-validation-for="VisibleCharacters"></span>
    </div>
</div>
```

`Views/MaskText.Fields.Design.cshtml`, the summary on the canvas:

```razor
@model MaskTextStepViewModel

<span>@(Model.Fields.Length == 0 ? T["No field to mask"] : T["Masks {0}", string.Join(", ", Model.Fields)])</span>
```

`Views/DataPipelineStepIcon-MaskText.cshtml`:

```html
<i class="fa-solid fa-mask" aria-hidden="true"></i>
```

### Registration

```csharp
using DataMasking.Drivers;
using DataMasking.Steps;
using Microsoft.Extensions.DependencyInjection;
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

## Sort-like step: Keep the largest rows

Keeps the `Count` rows with the largest value of a number field. It must see every row first, so it sorts with the bounded-memory `DataSorter`.

```csharp
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Localization;
using OrchardCore.DataPipelines.Steps;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Operations;
using OrchardCore.Localization;

namespace DataMasking.Steps;

public sealed class TopRowsStepSettings
{
    public string Field { get; set; }

    public int Count { get; set; } = 10;
}

public sealed class TopRowsStep : DataPipelineStepType<TopRowsStep, TopRowsStepSettings>, IDataPipelineStepType
{
    public const string StepName = "TopRows";

    private readonly IStringLocalizer S;

    public TopRowsStep(IStringLocalizer<TopRowsStep> localizer)
    {
        S = localizer;
    }

    public static string Name => StepName;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TopRowsStep>("Keep the largest rows");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<TopRowsStep>("Keeps the rows with the largest values of a number field.");

    public static DataPipelineStepCategory Category => DataPipelineStepCategory.Transform;

    public override Task DescribeAsync(DataPipelineDescribeContext context)
    {
        var fields = context.GetInputFields();
        var settings = GetSettings(context.Step);
        var field = fields.Find(settings.Field);

        if (field is null)
        {
            context.AddError(S["Choose the field to rank the rows by."]);
        }
        else if (!DataValues.IsNumeric(field.Type))
        {
            context.AddError(S["The field '{0}' isn't a number.", settings.Field]);
        }

        if (settings.Count < 1)
        {
            context.AddError(S["Keep at least one row."]);
        }

        context.SetOutputFields(fields);

        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync(DataPipelineStepContext context)
    {
        var settings = GetSettings(context.Step);

        await using var reader = await DataPipelineRowReader.OpenAsync(context.GetInput(), context.CancellationToken);

        var comparer = DataSorter.CreateComparer([(reader.Fields.IndexOf(settings.Field), Descending: true)]);

        // Holds up to StepMemoryLimitMegabytes, and spills the rest to the run's temporary folder.
        var sorted = DataSorter.SortAsync(
            reader.ReadRowsAsync(),
            comparer,
            DataPipelineRowReader.CreateSpillOptions(context),
            context.CancellationToken);

        var count = await DataPipelineRowReader.WriteRowsAsync(
            context.GetOutput(),
            reader.Fields,
            TakeAsync(sorted, settings.Count, context.CancellationToken),
            context.CancellationToken);

        context.LogInformation(S["Kept {0} rows.", count]);
    }

    private static async IAsyncEnumerable<object[]> TakeAsync(IAsyncEnumerable<object[]> rows, int count, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            yield break;
        }

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            yield return row;

            if (--count == 0)
            {
                yield break;
            }
        }
    }
}
```

Register it without an editor (configure it through recipes), or add a driver as for **Mask text**:

```csharp
services.AddDataPipelineStepType<TopRowsStep>();
```

## Destination step: Send rows to an analytics service

A destination that reads **rows**, upserts them by a key field, keeps the remote data set id between runs, and stores its API key as a secret. `IAnalyticsClient` is the module's own client.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.Services;
using OrchardCore.DataPipelines.Steps;
using OrchardCore.Localization;

namespace Analytics.Steps;

public sealed class SendToAnalyticsStepSettings
{
    public string Endpoint { get; set; }

    public string KeyField { get; set; }

    // Starts with "Protected": encrypted, never exported by deployment plans.
    public string ProtectedApiKey { get; set; }
}

public sealed class SendToAnalyticsStep : DataPipelineStepType<SendToAnalyticsStep, SendToAnalyticsStepSettings>, IDataPipelineStepType
{
    public const string StepName = "SendToAnalytics";

    private const string DataSetIdKey = "dataSetId";
    private const string FieldsKey = "fields";

    private readonly IStringLocalizer S;

    public SendToAnalyticsStep(IStringLocalizer<SendToAnalyticsStep> localizer)
    {
        S = localizer;
    }

    public static string Name => StepName;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SendToAnalyticsStep>("Send to analytics");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SendToAnalyticsStep>("Updates the rows of a data set of the analytics service, by key.");

    // A destination is never executed by a preview.
    public static DataPipelineStepCategory Category => DataPipelineStepCategory.Destination;

    // Destinations take files by default: this one takes rows.
    public override IReadOnlyList<DataPipelinePort> GetInputs(DataPipelineStep step)
        => [new(DataPipelinePort.Input, S["Rows"]) { IsRequired = true }];

    public override Task DescribeAsync(DataPipelineDescribeContext context)
    {
        var settings = GetSettings(context.Step);

        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            context.AddError(S["Enter the https endpoint of the analytics service."]);
        }

        if (context.GetInputFields().Find(settings.KeyField) is null)
        {
            context.AddError(S["Choose the field that identifies the rows."]);
        }

        if (string.IsNullOrEmpty(settings.ProtectedApiKey))
        {
            context.AddError(S["Enter the API key."]);
        }

        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync(DataPipelineStepContext context)
    {
        var settings = GetSettings(context.Step);
        var input = context.GetInput();

        var apiKey = context.Services.GetRequiredService<DataPipelineSecrets>().Unprotect(settings.ProtectedApiKey)
            ?? throw new InvalidOperationException(S["The API key can't be read. Enter it again."]);

        var client = context.Services.GetRequiredService<IAnalyticsClient>();
        var fieldNames = string.Join(",", input.Fields.Select(field => field.Name));

        // A new data set when there is none yet, or when the fields changed since it was created.
        var dataSetId = context.GetState<string>(DataSetIdKey);

        if (dataSetId is null || context.GetState<string>(FieldsKey) != fieldNames)
        {
            dataSetId = await client.CreateDataSetAsync(settings.Endpoint, apiKey, input.Fields, context.CancellationToken);
            context.SetState(DataSetIdKey, dataSetId);
            context.SetState(FieldsKey, fieldNames);
        }

        var sent = 0L;

        await foreach (var batch in input.ReadBatchesAsync(context.CancellationToken))
        {
            // Upsert by key, so a repeated run doesn't duplicate rows.
            await client.UpsertAsync(settings.Endpoint, apiKey, dataSetId, settings.KeyField, batch, context.CancellationToken);
            sent += batch.Count;
        }

        // No secret in the message.
        context.AddDelivery(S["Sent {0} rows to the analytics data set '{1}'.", sent, dataSetId]);
    }
}
```

The state is saved only when the run succeeds, so a failed first run creates the data set again on the next run; make `CreateDataSetAsync` idempotent or clean up on the remote side.

### Editor with a secret

```csharp
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DataPipelines.Drivers;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.Services;
using OrchardCore.DataSources;
using OrchardCore.DisplayManagement.Handlers;
using Analytics.Steps;

namespace Analytics.Drivers;

public sealed class SendToAnalyticsStepDisplayDriver : DataPipelineStepDisplayDriver<SendToAnalyticsStepSettings, SendToAnalyticsStepViewModel>
{
    private readonly DataPipelineSecrets _secrets;
    private readonly DataPipelineEditorContext _editorContext;

    public SendToAnalyticsStepDisplayDriver(DataPipelineSecrets secrets, DataPipelineEditorContext editorContext)
    {
        _secrets = secrets;
        _editorContext = editorContext;
    }

    protected override string StepName => SendToAnalyticsStep.StepName;

    protected override ValueTask EditAsync(DataPipelineStep step, SendToAnalyticsStepSettings settings, SendToAnalyticsStepViewModel model)
    {
        model.Endpoint = settings.Endpoint;
        model.KeyField = settings.KeyField;

        // Never send the secret back to the browser.
        model.HasApiKey = !string.IsNullOrEmpty(settings.ProtectedApiKey);
        model.AvailableFields = _editorContext.GetInputFields();

        return ValueTask.CompletedTask;
    }

    protected override ValueTask UpdateAsync(DataPipelineStep step, SendToAnalyticsStepSettings settings, SendToAnalyticsStepViewModel model, UpdateEditorContext context)
    {
        settings.Endpoint = model.Endpoint?.Trim();
        settings.KeyField = model.KeyField;

        // An empty posted value keeps the current secret; RemoveApiKey clears it.
        settings.ProtectedApiKey = _secrets.Update(settings.ProtectedApiKey, model.ApiKey?.Trim(), model.RemoveApiKey);

        return ValueTask.CompletedTask;
    }
}

public class SendToAnalyticsStepViewModel
{
    public string Endpoint { get; set; }

    public string KeyField { get; set; }

    public string ApiKey { get; set; }

    public bool RemoveApiKey { get; set; }

    [BindNever]
    public bool HasApiKey { get; set; }

    [BindNever]
    public IReadOnlyList<DataField> AvailableFields { get; set; } = [];
}
```

`Views/SendToAnalytics.Fields.Edit.cshtml`:

```razor
@using OrchardCore.DataPipelines.Drivers
@model SendToAnalyticsStepViewModel

<div class="ocat-wrapper" asp-validation-class-for="Endpoint">
    <label asp-for="Endpoint" class="ocat-label ocat-label-required">@T["Endpoint"]</label>
    <div class="ocat-end">
        <input asp-for="Endpoint" type="url" class="form-control" placeholder="https://analytics.example.com" />
        <span asp-validation-for="Endpoint"></span>
    </div>
</div>

<div class="ocat-limited-wrapper">
    <label asp-for="KeyField" class="ocat-label ocat-label-required">@T["Key field"]</label>
    <div class="ocat-limited">
        <select asp-for="KeyField" class="form-select">
            <partial name="DataPipelineFieldOptions" model="@((Model.AvailableFields, Model.KeyField, true))" />
        </select>
    </div>
</div>

<partial name="DataPipelineSecretInput" model="@(new DataPipelineSecretInputViewModel
{
    Name = nameof(Model.ApiKey),
    Label = T["API key"].Value,
    HasValue = Model.HasApiKey,
    KeepHint = T["Leave empty to keep the current API key."].Value,
    RemoveName = nameof(Model.RemoveApiKey),
    RemoveLabel = T["Remove the API key"].Value,
})" />
```

```csharp
services.AddDataPipelineStep<SendToAnalyticsStep, SendToAnalyticsStepDisplayDriver>();
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

    // Called in its own scope after the run is saved: succeeded, failed or cancelled.
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

## Using the custom steps in a recipe

Settings are stored under the settings type name, as an export shows them. Enter secrets in the designer after the import, then publish.

```json
{
  "steps": [
    {
      "name": "DataPipelines",
      "Pipelines": [
        {
          "PipelineId": "maskedusers00000000000001",
          "Name": "Masked users",
          "Definition": {
            "Steps": [
              {
                "StepId": "users",
                "Type": "DataSource",
                "X": 180,
                "Y": 320,
                "Properties": {
                  "DataSourceStepSettings": { "Source": "Users", "DataSet": "Users", "Fields": [ "UserId", "UserName", "Email", "PhoneNumber" ] }
                }
              },
              {
                "StepId": "mask",
                "Type": "MaskText",
                "X": 480,
                "Y": 320,
                "Properties": {
                  "MaskTextStepSettings": { "Fields": [ "Email", "PhoneNumber" ], "VisibleCharacters": 4 }
                }
              },
              {
                "StepId": "analytics",
                "Type": "SendToAnalytics",
                "X": 780,
                "Y": 320,
                "Properties": {
                  "SendToAnalyticsStepSettings": { "Endpoint": "https://analytics.example.com", "KeyField": "UserId" }
                }
              }
            ],
            "Connections": [
              { "SourceStepId": "users", "SourcePort": "Output", "TargetStepId": "mask", "TargetPort": "Input" },
              { "SourceStepId": "mask", "SourcePort": "Output", "TargetStepId": "analytics", "TargetPort": "Input" }
            ]
          }
        }
      ]
    }
  ]
}
```
