# Custom Workflow Activity Examples

Complete Orchard Core 4.0 samples for activities, drivers, editors and the Workflows extension points. Every type named here exists in `OrchardCore.Workflows.Abstractions` or the `OrchardCore.Workflows` module.

## Packages

The activity, its outputs and provided values only need the abstractions. The expression editor helpers (`WorkflowExpressionInput`, `WorkflowExpressionInputValidator`, `CreateWorkflowExpressionEditorAsync`) live in the module package.

```xml
<ItemGroup>
  <PackageReference Include="OrchardCore.Workflows.Abstractions" Version="4.*" />
  <PackageReference Include="OrchardCore.Workflows" Version="4.*" />
</ItemGroup>
```

Add the module to the manifest dependencies so the Workflows feature is enabled first:

```csharp
[assembly: Feature(
    Id = "MyModule.Workflows",
    Name = "My Module Workflow Activities",
    Dependencies = ["OrchardCore.Workflows"],
    Category = "Workflows"
)]
```

## Task activity with expressions, outputs and provided values

`Condition` lets the designer pick any syntax (JavaScript when the stored expression has none). `CallbackUrl` is Liquid only, rendered into a URL, so values are URL-encoded.

```csharp
using System.Text.Encodings.Web;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace MyModule.Workflows.Activities;

public sealed class ApproveOrderTask : TaskActivity<ApproveOrderTask>, IActivityDescription, IActivityOutputs, IActivityProvidedValues
{
    private static readonly ActivityOutputDescriptor[] s_outputs =
    [
        new ActivityOutputDescriptor
        {
            Name = "Approved",
            TypeName = "boolean",
            DisplayName = LocalizationSource.Create<ApproveOrderTask>("Approved"),
            Description = LocalizationSource.Create<ApproveOrderTask>("Whether the order was approved."),
        },
    ];

    private static readonly ActivityProvidedValue[] s_providedValues =
    [
        ActivityProvidedValue.LastResult("string", LocalizationSource.Create<ApproveOrderTask>("The callback URL that was rendered.")),
    ];

    private readonly IWorkflowExpressionManager _expressionManager;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly UrlEncoder _urlEncoder;
    private readonly IStringLocalizer S;

    public ApproveOrderTask(
        IWorkflowExpressionManager expressionManager,
        IStringLocalizerFactory localizerFactory,
        UrlEncoder urlEncoder,
        IStringLocalizer<ApproveOrderTask> localizer)
    {
        _expressionManager = expressionManager;
        _localizerFactory = localizerFactory;
        _urlEncoder = urlEncoder;
        S = localizer;
    }

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ApproveOrderTask>("Approve Order Task");

    public static LocalizationSource CategoryName { get; } = LocalizationSource.Create<ApproveOrderTask>("Orders");

    public override LocalizedString DisplayText => _localizerFactory.Localize(DisplayName);

    public override LocalizedString Category => _localizerFactory.Localize(CategoryName);

    public WorkflowExpression<bool> Condition
    {
        get => GetProperty(() => new WorkflowExpression<bool>());
        set => SetProperty(value);
    }

    public WorkflowExpression<string> CallbackUrl
    {
        get => GetProperty(() => new WorkflowExpression<string>(null, WorkflowExpressionSyntaxes.Liquid));
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome(S["Approved"], S["Rejected"]);

    public IEnumerable<ActivityOutputDescriptor> GetOutputs()
        => s_outputs;

    public IEnumerable<ActivityProvidedValue> GetProvidedValues()
        => s_providedValues;

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        // Each expression runs with its own syntax; JavaScript is the fallback for expressions without one.
        var approved = await _expressionManager.EvaluateAsync(Condition, workflowContext, WorkflowExpressionSyntaxes.JavaScript);

        workflowContext.LastResult = await _expressionManager.EvaluateAsync(
            CallbackUrl,
            workflowContext,
            WorkflowExpressionSyntaxes.Liquid,
            new WorkflowExpressionEvaluationContext { Encoder = _urlEncoder });

        // The designer binds this output to a variable in the Outputs tab (Properties.OutputBindings).
        workflowContext.SetActivityOutput(activityContext, "Approved", approved);

        return Outcome(approved ? "Approved" : "Rejected");
    }
}
```

- Do not catch `OperationCanceledException`, `TimeoutException` or Jint limit exceptions; they must fault the workflow.
- Throwing from `ExecuteAsync` faults the instance, which is what lets a task's retry policy (**When it fails**) retry it.
- Values whose names depend on settings belong in `GetProvidedValues()`; fixed ones go in static fields so they are created once.

## View model, driver and views

```csharp
using OrchardCore.Workflows.ViewModels;

namespace MyModule.Workflows.ViewModels;

public class ApproveOrderTaskViewModel
{
    public WorkflowExpressionInput Condition { get; set; } = new();

    public WorkflowExpressionInput CallbackUrl { get; set; } = new();
}
```

```csharp
using Microsoft.Extensions.Localization;
using MyModule.Workflows.Activities;
using MyModule.Workflows.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;
using OrchardCore.Workflows.ViewModels;

namespace MyModule.Workflows.Drivers;

public sealed class ApproveOrderTaskDisplayDriver : ActivityDisplayDriver<ApproveOrderTask, ApproveOrderTaskViewModel>
{
    private readonly WorkflowExpressionInputValidator _expressionValidator;

    internal readonly IStringLocalizer S;

    public ApproveOrderTaskDisplayDriver(
        WorkflowExpressionInputValidator expressionValidator,
        IStringLocalizer<ApproveOrderTaskDisplayDriver> stringLocalizer)
    {
        _expressionValidator = expressionValidator;
        S = stringLocalizer;
    }

    protected override void EditActivity(ApproveOrderTask activity, ApproveOrderTaskViewModel model)
    {
        model.Condition = WorkflowExpressionInput.From(activity.Condition);
        model.CallbackUrl = WorkflowExpressionInput.From(activity.CallbackUrl);
    }

    public override async Task<IDisplayResult> UpdateAsync(ApproveOrderTask activity, UpdateEditorContext context)
    {
        var model = new ApproveOrderTaskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Adds model errors for a missing required expression, a disallowed syntax, and what the syntax's provider rejects.
        // The designer shows them in the editor and in the Issues tab, and they block publishing.
        activity.Condition = _expressionValidator.Validate<bool>(model.Condition, context.Updater.ModelState, Prefix, nameof(model.Condition), new()
        {
            Label = S["Condition"],
            Required = true,
        });

        activity.CallbackUrl = _expressionValidator.Validate<string>(model.CallbackUrl, context.Updater.ModelState, Prefix, nameof(model.CallbackUrl), new()
        {
            Label = S["Callback URL"],
            Syntaxes = [WorkflowExpressionSyntaxes.Liquid],
            DefaultSyntax = WorkflowExpressionSyntaxes.Liquid,
        });

        return Edit(activity, context);
    }
}
```

`Views/Items/ApproveOrderTask.Fields.Edit.cshtml`:

```cshtml
@using OrchardCore.Workflows.Helpers
@using OrchardCore.Workflows.Models
@model MyModule.Workflows.ViewModels.ApproveOrderTaskViewModel

@{
    var conditionEditor = await Factory.CreateWorkflowExpressionEditorAsync(Html, m => m.Condition, editor =>
    {
        editor.Label = T["Condition"].Value;
        editor.Hint = T["An expression that evaluates to true or false."].Value;
        editor.Required = true;
        editor.Examples[WorkflowExpressionSyntaxes.Literal] = "true";
        editor.Examples[WorkflowExpressionSyntaxes.JavaScript] = "input(\"Total\") < 1000";
        editor.Examples[WorkflowExpressionSyntaxes.Liquid] = "{{ Workflow.Input.Total < 1000 }}";
    });

    var callbackEditor = await Factory.CreateWorkflowExpressionEditorAsync(Html, m => m.CallbackUrl, editor =>
    {
        editor.Label = T["Callback URL"].Value;
        editor.Syntaxes = [WorkflowExpressionSyntaxes.Liquid];
        editor.DefaultSyntax = WorkflowExpressionSyntaxes.Liquid;
    });
}
@await DisplayAsync(conditionEditor)
@await DisplayAsync(callbackEditor)
```

Set `editor.Multiline = true` for values that span several lines (a code editor whose language follows the syntax). Leave `Syntaxes` empty to allow every registered syntax.

`Views/Items/ApproveOrderTask.Fields.Design.cshtml` (body of the card on the canvas; the designer draws the card, icon and ports):

```cshtml
@using MyModule.Workflows.Activities
@model ActivityViewModel<ApproveOrderTask>

<header>
    <h4>@Model.Activity.GetTitleOrDefault(() => T["Approve order"])</h4>
</header>
<span>@T["Approve when <em>{0}</em>", Model.Activity.Condition.Expression]</span>
```

`Views/Items/ApproveOrderTask.Fields.Thumbnail.cshtml` (activities pane card):

```cshtml
<h4 class="card-title">@T["Approve order"]</h4>
<p>@T["Approve or reject an order with a condition."]</p>
```

The module's `_ViewImports.cshtml` needs `@using OrchardCore.Workflows.Helpers` (for `GetTitleOrDefault`) and `@using OrchardCore.Workflows.ViewModels` (for `ActivityViewModel<T>`), plus `@addTagHelper *, OrchardCore.ResourceManagement` if an editor registers scripts.

## Event activity

An event halts by default (`EventActivity.Execute` returns `Halt()`). `CanExecuteAsync` decides whether a trigger is for this activity; `Resume` returns the outcome to continue with.

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;

namespace MyModule.Workflows.Activities;

public sealed class OrderApprovedEvent : EventActivity, IActivityDescription, IActivityProvidedValues
{
    private static readonly ActivityProvidedValue[] s_providedValues =
    [
        new ActivityProvidedValue
        {
            Source = WorkflowValueSource.Input,
            Name = "OrderId",
            TypeName = "string",
            Description = LocalizationSource.Create<OrderApprovedEvent>("The id of the approved order."),
        },
    ];

    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IStringLocalizer S;

    public OrderApprovedEvent(IStringLocalizerFactory localizerFactory, IStringLocalizer<OrderApprovedEvent> localizer)
    {
        _localizerFactory = localizerFactory;
        S = localizer;
    }

    public override string Name => nameof(OrderApprovedEvent);

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderApprovedEvent>("Order Approved Event");

    public static LocalizationSource CategoryName { get; } = LocalizationSource.Create<OrderApprovedEvent>("Orders");

    public override LocalizedString DisplayText => _localizerFactory.Localize(DisplayName);

    public override LocalizedString Category => _localizerFactory.Localize(CategoryName);

    public override bool HasEditor => false;

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome(S["Done"]);

    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => workflowContext.Input.ContainsKey("OrderId");

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    public IEnumerable<ActivityProvidedValue> GetProvidedValues()
        => s_providedValues;
}

public sealed class OrderApprovedEventDisplayDriver : ActivityDisplayDriver<OrderApprovedEvent>;
```

Trigger it where the order is approved. The correlation id resumes only the instance waiting on that order:

```csharp
await _workflowManager.TriggerEventAsync(
    nameof(OrderApprovedEvent),
    new Dictionary<string, object> { ["OrderId"] = order.Id },
    correlationId: order.Id);
```

## Registration

```csharp
using Microsoft.Extensions.DependencyInjection;
using MyModule.Workflows.Activities;
using MyModule.Workflows.Drivers;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;
using OrchardCore.Workflows.Options;

namespace MyModule;

[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<ApproveOrderTask, ApproveOrderTaskDisplayDriver>(activity => activity.Icon = "fa-solid fa-check");
        services.AddActivity<OrderApprovedEvent, OrderApprovedEventDisplayDriver>(activity => activity.Icon = "fa-solid fa-thumbs-up");

        // Default icon for every activity in the "Orders" category, keyed by the untranslated category.
        services.Configure<WorkflowOptions>(options => options.CategoryIcons[ApproveOrderTask.CategoryName.Value] = "fa-solid fa-cart-shopping");
    }
}
```

A module can also document values of another module's activity when it registers it:

```csharp
services.AddActivity<ApproveOrderTask, ApproveOrderTaskDisplayDriver>(activity => activity
    .Provides(WorkflowValueSource.Properties, "Customer", "object", LocalizationSource.Create<WorkflowsStartup>("The customer of the order."),
        [new ActivityProvidedValueMember { Name = "Email", TypeName = "string" }]));
```

## Custom expression syntax

Name, display name and editor language (`plaintext`, `liquid`, `javascript` or another Monaco language) are static, so editors list syntaxes without creating providers. A provider registered with an existing name replaces it.

```csharp
using System.Globalization;
using OrchardCore.Localization;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace MyModule.Workflows.Expressions;

public sealed class UpperCaseExpressionProvider : IWorkflowExpressionProvider
{
    public static string Name => "UpperCase";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<UpperCaseExpressionProvider>("Upper case");

    public static string EditorLanguage => "plaintext";

    public Task<T> EvaluateAsync<T>(WorkflowExpression<T> expression, WorkflowExecutionContext workflowContext, WorkflowExpressionEvaluationContext context)
    {
        var value = expression.Expression?.ToUpperInvariant();

        // Record the evaluation in the journal when the workflow records activity data.
        workflowContext?.ReportEvaluation(Name, expression.Expression, value);

        return Task.FromResult((T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture));
    }

    public IReadOnlyList<string> Validate(string expression, Type valueType)
        => [];
}
```

```csharp
services.AddWorkflowExpressionProvider<UpperCaseExpressionProvider>();
```

## Custom variable type

Variable types are not services: `WorkflowVariableTypeOptions` keeps one instance per type, created with the parameterless constructor and shared, so they must be stateless. `Editor` is `text`, `number`, `boolean`, `datetime`, `json` or `none`. Values that do not round-trip through JSON need an `IWorkflowValueSerializer`.

```csharp
using System.Text.Json.Nodes;
using OrchardCore.Localization;
using OrchardCore.Workflows.Services;

namespace MyModule.Workflows.Variables;

public sealed class SkuVariableType : IWorkflowVariableType
{
    public static string Name => "sku";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SkuVariableType>("SKU");

    public static string Editor => "text";

    public bool TryCoerce(object value, out object result)
    {
        if (value is JsonValue json && json.TryGetValue<string>(out var jsonText))
        {
            value = jsonText;
        }

        if (value is null)
        {
            result = null;

            return true;
        }

        if (value is string text && text.Length is > 0 and <= 32)
        {
            result = text.Trim().ToUpperInvariant();

            return true;
        }

        result = null;

        return false;
    }
}
```

```csharp
services.AddWorkflowVariableType<SkuVariableType>();
```

## Activity presets

Presets add an existing activity with preset properties to the activities pane (the workflows usable as an activity are presets of `ExecuteWorkflowTask`). The stored workflow references the real activity, so it keeps working if the preset disappears.

```csharp
using System.Text.Json.Nodes;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace MyModule.Workflows.Services;

public sealed class WeatherPresetProvider : IActivityPresetProvider
{
    public Task<IEnumerable<ActivityPreset>> GetPresetsAsync()
        => Task.FromResult<IEnumerable<ActivityPreset>>(
        [
            new ActivityPreset
            {
                Id = "weather:forecast",
                ActivityName = "HttpRequestTask",
                DisplayText = "Weather forecast",
                Category = "Weather",
                Icon = "fa-solid fa-cloud-sun",
                Properties = new JsonObject
                {
                    ["Url"] = new JsonObject { ["Expression"] = "https://weather.example.com/forecast" },
                    ["HttpMethod"] = "GET",
                },
            },
        ]);
}
```

```csharp
services.AddScoped<IActivityPresetProvider, WeatherPresetProvider>();
```

## Global values and functions

List a global Liquid value (added with `TemplateOptionsBuilder.WithGlobalValue()`) or a JavaScript function (added with `IGlobalMethodProvider`) in the **Global** and **Functions** groups of **Available data**:

```csharp
using OrchardCore.Localization;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace MyModule.Workflows.Services;

public sealed class StoreWorkflowGlobalValueProvider : IWorkflowGlobalValueProvider
{
    private static readonly WorkflowGlobalValue[] s_globalValues =
    [
        WorkflowGlobalValue.Liquid("Store", "object", LocalizationSource.Create<StoreWorkflowGlobalValueProvider>("The settings of the store."),
        [
            new ActivityProvidedValueMember { Name = "Currency", TypeName = "string", Description = LocalizationSource.Create<StoreWorkflowGlobalValueProvider>("The currency of the prices.") },
        ]),
        WorkflowGlobalValue.Function("formatPrice(amount)", "string", LocalizationSource.Create<StoreWorkflowGlobalValueProvider>("Formats an amount in the store's currency.")),
    ];

    public IEnumerable<WorkflowGlobalValue> GetGlobalValues()
        => s_globalValues;
}
```

```csharp
services.AddScoped<IWorkflowGlobalValueProvider, StoreWorkflowGlobalValueProvider>();
```

## Editor scripts in the designer

An editor that needs a script registers it as a resource and follows the designer contract: initialize editors added later, scope to a wrapper, keep fields current, and clean up on `oc:editor-unmounting`.

```cshtml
<div data-task-editor="approve-order" data-target-id="@Html.IdFor(m => m.CallbackUrl.Expression)">
    @* fields *@
</div>
<script asp-name="my-module-approve-order-editor" at="Foot"></script>
```

```javascript
// Registered as the "my-module-approve-order-editor" script resource.
const init = (element) => {
    if (element.dataset.initialized) {
        return;
    }

    element.dataset.initialized = "true";

    const field = document.getElementById(element.dataset.targetId);
    const onInput = () => field.dispatchEvent(new Event("change", { bubbles: true }));
    field?.addEventListener("blur", onInput);

    element.addEventListener("oc:editor-unmounting", () => field?.removeEventListener("blur", onInput), { once: true });
};

const initAll = () => document.querySelectorAll('[data-task-editor="approve-order"]').forEach(init);

initAll();
new MutationObserver(initAll).observe(document.body, { childList: true, subtree: true });
```

Inside the Orchard Core repository, `observeAndInit` (`@orchardcore/bloom/helpers/observeAndInit`) and `onEditorUnmounting`, `dispatchFieldChange` and `bindCodeMirrorToTextArea` (`@orchardcore/bloom/helpers/editorLifecycle`) implement the same contract.
