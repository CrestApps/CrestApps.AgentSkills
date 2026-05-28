# Workflow Examples

## Example 1: Send Email on Content Published

A workflow that sends an email notification when a BlogPost is published.

### Workflow Structure

```
ContentPublishedEvent (BlogPost) → IfElseTask (check author) → SendEmailTask → NotifyTask
```

### Custom Activity Driver

```csharp
using OrchardCore.Workflows.Display;

public sealed class MyCustomTaskDisplayDriver : ActivityDisplayDriver<MyCustomTask>
{
    public override IDisplayResult Edit(MyCustomTask activity)
    {
        return Initialize<MyCustomTaskViewModel>("MyCustomTask_Edit", model =>
        {
            model.Message = activity.Message;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(MyCustomTask activity, IUpdateModel updater)
    {
        var model = new MyCustomTaskViewModel();
        await updater.TryUpdateModelAsync(model, Prefix);
        activity.Message = model.Message;
        return Edit(activity);
    }
}
```

## Example 2: Scheduled Cleanup Workflow

A workflow triggered by a timer that cleans up expired content.

### Timer Event Configuration

- **Name**: Content Cleanup
- **Trigger**: TimerEvent with cron `0 2 * * *` (daily at 2 AM)
- **Activities**:
  1. `TimerEvent` - Triggers daily.
  2. `ScriptTask` - Queries for expired content items.
  3. `ForLoopTask` - Iterates over expired items.
  4. `ContentDeleteTask` - Deletes each expired item.
  5. `LogTask` - Logs the cleanup summary.

## Example 3: Dual-Syntax Workflow Task Edit View

Workflow tasks that support both JavaScript and Liquid use a syntax selector with toggling fields. Here is the pattern for a task edit view (e.g., `IfElseTask.Fields.Edit.cshtml`):

```cshtml
@using OrchardCore.Workflows.Models
@using OrchardCore.Workflows.ViewModels
@model IfElseTaskViewModel

<div class="ocat-wrapper" asp-validation-class-for="Syntax">
    <label asp-for="Syntax" class="ocat-label">@T["Syntax"]</label>
    <div class="ocat-end">
        <select asp-for="Syntax" class="form-select">
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

<script at="Foot">
    document.addEventListener('DOMContentLoaded', () => {
        const syntaxElement = document.getElementById('@Html.IdFor(m => m.Syntax)');
        const javaScriptGroup = document.getElementById('@($"{Html.IdFor(m => m.ConditionExpression)}_group")');
        const liquidGroup = document.getElementById('@($"{Html.IdFor(m => m.LiquidConditionExpression)}_group")');

        const toggleEditors = () => {
            const useLiquid = syntaxElement.value === '@nameof(WorkflowScriptSyntax.Liquid)';
            javaScriptGroup.classList.toggle('d-none', useLiquid);
            liquidGroup.classList.toggle('d-none', !useLiquid);
        };

        syntaxElement.addEventListener('change', toggleEditors);
        toggleEditors();
    });
</script>
```

This pattern applies to all dual-syntax tasks: `IfElseTask`, `ForLoopTask`, `ForEachTask`, `WhileLoopTask`, `SetPropertyTask`, and `SetOutputTask`.
