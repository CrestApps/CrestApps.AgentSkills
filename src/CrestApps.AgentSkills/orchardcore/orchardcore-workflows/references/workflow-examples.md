# Workflow Examples

## Example 1: Send Email on Content Published

A workflow that sends an email notification when a BlogPost is published.

### Workflow Structure

```
ContentPublishedEvent (BlogPost) → IfElseTask (check author) → EmailTask → NotifyTask
```

`ContentPublishedEvent` is the first event added, so the editor marks it as the startup task automatically.

### Custom Activity Driver with an Editor

```csharp
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;

namespace MyModule.Workflows.Drivers;

public sealed class MyCustomTaskDisplayDriver
    : ActivityDisplayDriver<MyCustomTask, MyCustomTaskViewModel>
{
    protected override void EditActivity(MyCustomTask activity, MyCustomTaskViewModel model)
    {
        model.Message = activity.Message.Expression;
    }

    protected override void UpdateActivity(MyCustomTaskViewModel model, MyCustomTask activity)
    {
        activity.Message = new WorkflowExpression<string>(model.Message);
    }
}

public class MyCustomTaskViewModel
{
    public string Message { get; set; }
}
```

The base driver renders `MyCustomTask_Fields_Edit` (`Views/Items/MyCustomTask.Fields.Edit.cshtml`) and binds the view model with the driver's `Prefix`.

## Example 2: Scheduled Cleanup Workflow

A workflow triggered by a timer that cleans up expired content.

- **Name**: Content Cleanup
- **Trigger**: `TimerEvent` with cron `0 2 * * *` (daily at 2 AM)
- **Activities**:
  1. `TimerEvent` - Triggers daily.
  2. `ScriptTask` - Queries for expired content items and calls `setOutcome("Done")`.
  3. `ForEachTask` - Iterates over the expired items.
  4. `DeleteContentTask` - Deletes each expired item.
  5. `LogTask` - Logs the cleanup summary.

If a site sets `MaxStatements()` or `TimeoutInterval()` on `Jint.Options` and the query script exceeds it, the workflow faults at `ScriptTask` instead of continuing with an empty list. Add a separate workflow that starts with `WorkflowFaultEvent` to get notified.

## Example 3: Dual-Syntax Workflow Task Edit View

Workflow tasks that support both JavaScript and Liquid use a syntax selector. The built-in `workflow-syntax-toggle` ES module script toggles the groups listed in the `data-javascript-group-ids` and `data-liquid-group-ids` attributes (JSON arrays of element ids) of a `select[data-syntax-toggle]`, so no jQuery or custom script is needed. This is the pattern used by `IfElseTask.Fields.Edit.cshtml`:

```cshtml
@using System.Text.Json
@using OrchardCore.Workflows.Models
@using OrchardCore.Workflows.ViewModels
@model IfElseTaskViewModel

@{
    var javaScriptGroupIds = new[] { $"{Html.IdFor(m => m.ConditionExpression)}_group" };
    var liquidGroupIds = new[] { $"{Html.IdFor(m => m.LiquidConditionExpression)}_group" };
}

<div class="ocat-wrapper" asp-validation-class-for="Syntax">
    <label asp-for="Syntax" class="ocat-label">@T["Syntax"]</label>
    <div class="ocat-end">
        <select asp-for="Syntax" class="form-select" data-syntax-toggle data-javascript-group-ids="@JsonSerializer.Serialize(javaScriptGroupIds)" data-liquid-group-ids="@JsonSerializer.Serialize(liquidGroupIds)">
            <option value="@nameof(WorkflowScriptSyntax.JavaScript)">@T["JavaScript"]</option>
            <option value="@nameof(WorkflowScriptSyntax.Liquid)">@T["Liquid"]</option>
        </select>
        <span asp-validation-for="Syntax"></span>
    </div>
</div>

<div id="@(Html.IdFor(m => m.ConditionExpression))_group" class="ocat-wrapper @(Model.Syntax == WorkflowScriptSyntax.JavaScript ? "" : "d-none")" asp-validation-class-for="ConditionExpression">
    <label asp-for="ConditionExpression" class="ocat-label">@T["Condition Expression"]</label>
    <div class="ocat-end">
        <input type="text" asp-for="ConditionExpression" class="form-control code" />
        <span asp-validation-for="ConditionExpression"></span>
        <span class="hint">@T["A JavaScript expression. Example: {0}", "input(\"Count\") > 0"]</span>
    </div>
</div>

<div id="@(Html.IdFor(m => m.LiquidConditionExpression))_group" class="ocat-wrapper @(Model.Syntax == WorkflowScriptSyntax.Liquid ? "" : "d-none")" asp-validation-class-for="LiquidConditionExpression">
    <label asp-for="LiquidConditionExpression" class="ocat-label">@T["Condition Expression"]</label>
    <div class="ocat-end">
        <input type="text" asp-for="LiquidConditionExpression" class="form-control code" />
        <span asp-validation-for="LiquidConditionExpression"></span>
        <span class="hint">@T["A Liquid expression. Example: {0}", "{{ Workflow.Properties[\"Sample\"].size > 0 }}"]</span>
    </div>
</div>

<script asp-name="workflow-syntax-toggle" at="Foot" type="module"></script>
```

This pattern applies to all dual-syntax tasks: `IfElseTask`, `ForLoopTask`, `ForEachTask`, `WhileLoopTask`, `SetPropertyTask`, and `SetOutputTask`. Always build ids with `Html.IdFor(...)`, because the activity editor prefixes field ids.

## Example 4: Run a Data Pipeline Every Night

```
TimerEvent (0 1 * * *) → RunDataPipelineTask → [Succeeded] EmailTask (report link)
                                            → [Failed]    NotifyUserTask (administrators)
                                            → [Skipped]   LogTask
```

- Enable `OrchardCore.Workflows`, `OrchardCore.Workflows.Timers`, and `OrchardCore.DataPipelines`.
- On `RunDataPipelineTask`, pick the pipeline, keep **Wait for the run to end** on, and enable **Skip when a run is already queued or running**.
- **Parameters** is a Liquid template with one `name=value` per line, for example `Since={{ Workflow.Input.Since }}`.
- The run (with `Status`, `Deliveries`, `Steps`) is the last result and the `DataPipelineRun` output, so `EmailTask` can reference `{{ Workflow.Output.DataPipelineRun.Status }}` and its `Deliveries`.
- To react to runs started elsewhere, start a workflow with `DataPipelineRunCompletedEvent` (correlated by pipeline id). See `orchardcore-data-pipelines`.

## Example 5: Clean Up on Logout

```
UserLoggedOutEvent → ScriptTask (clear per-user state) → LogTask
```

`UserLoggedOutEvent` (**User Loggedout Event**, Users feature) fires after the user signs out through the account logout endpoint. For code-only logic, implement `ILogoutFormEvent` (or derive from `LogoutFormEventBase`) instead of a workflow.
