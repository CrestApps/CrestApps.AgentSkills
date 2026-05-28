---
name: orchardcore-workflows
description: Skill for creating and managing Orchard Core workflows. Covers workflow type definitions, custom activities, events, workflow expressions, and workflow triggers. Use this skill when requests mention Orchard Core Workflows, Create a Workflow, Enabling Workflows, Built-in Event Activities, Built-in Task Activities, Creating a Custom Activity, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Workflows, OrchardCore.Workflows.Http, OrchardCore.Workflows.Timers, OrchardCore.Workflows.Activities, OrchardCore.Workflows.Models, TaskActivity, IStringLocalizer, IEnumerable, WorkflowExecutionContext, ActivityContext, EventActivity, IServiceCollection. It also helps with workflow examples, Built-in Task Activities, Creating a Custom Activity, Creating a Custom Event, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Workflows - Prompt Templates

## Create a Workflow

You are an Orchard Core expert. Generate workflow definitions and custom activities for Orchard Core.

### Guidelines

- Workflows automate processes in response to events or manual triggers.
- Enable the `OrchardCore.Workflows` feature to use workflows.
- Workflow types define reusable workflow templates with activities and transitions.
- Activities are the building blocks: tasks (actions) and events (triggers).
- Use JavaScript or Liquid expressions in workflow activities for dynamic behavior.
- Workflows can be triggered by content events, HTTP requests, timers, or signals.
- Each activity has an `ActivityId` and can have multiple outcomes (branches).

### Enabling Workflows

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Workflows",
        "OrchardCore.Workflows.Http",
        "OrchardCore.Workflows.Timers"
      ],
      "disable": []
    }
  ]
}
```

### Built-in Event Activities

- `ContentCreatedEvent` - Triggered when a content item is created.
- `ContentPublishedEvent` - Triggered when a content item is published.
- `ContentUpdatedEvent` - Triggered when a content item is updated.
- `ContentDeletedEvent` - Triggered when a content item is deleted.
- `HttpRequestEvent` - Triggered by an incoming HTTP request.
- `TimerEvent` - Triggered on a schedule (cron expression).
- `SignalEvent` - Triggered by a named signal.
- `UserRegisteredEvent` - Triggered when a new user registers.

### Built-in Task Activities

- `NotifyTask` - Displays a notification message.
- `LogTask` - Writes to the application log.
- `SetPropertyTask` - Sets a workflow property (supports JavaScript and Liquid syntax).
- `SetOutputTask` - Sets a workflow output value (supports JavaScript and Liquid syntax).
- `IfElseTask` - Conditional branching (supports JavaScript and Liquid syntax).
- `ForLoopTask` - Loop with counter (supports JavaScript and Liquid syntax).
- `ForEachTask` - Iterate over a collection (supports JavaScript and Liquid syntax).
- `WhileLoopTask` - Loop while condition is true (supports JavaScript and Liquid syntax).
- `ForkTask` - Parallel execution branches.
- `JoinTask` - Wait for parallel branches to complete.
- `HttpRequestTask` - Make an outbound HTTP request.
- `HttpRedirectTask` - Redirect the user to a URL.
- `ContentCreateTask` - Create a new content item.
- `ContentPublishTask` - Publish a content item.
- `SendEmailTask` - Send an email notification.
- `ScriptTask` - Execute a JavaScript expression and return the result.
- `LiquidTask` - Execute a Liquid expression and return the result.

### Workflow Script Syntax

Many workflow tasks (IfElseTask, ForLoopTask, ForEachTask, WhileLoopTask, SetPropertyTask, SetOutputTask) now support both **JavaScript** and **Liquid** syntax. Each task has a `Syntax` property that selects between `WorkflowScriptSyntax.JavaScript` and `WorkflowScriptSyntax.Liquid`.

- When `JavaScript` is selected, the task evaluates the JavaScript expression field.
- When `Liquid` is selected, the task evaluates the Liquid expression field instead.

```csharp
namespace OrchardCore.Workflows.Models;

public enum WorkflowScriptSyntax
{
    JavaScript,
    Liquid,
}
```

### Creating a Custom Activity

```csharp
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

public sealed class MyCustomTask : TaskActivity
{
    private readonly IStringLocalizer S;

    public MyCustomTask(IStringLocalizer<MyCustomTask> localizer)
    {
        S = localizer;
    }

    public override string Name => "MyCustomTask";

    public override LocalizedString DisplayText => S["My Custom Task"];

    public override LocalizedString Category => S["Custom"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
    {
        return Outcomes(S["Done"], S["Failed"]);
    }

    public override async Task<ActivityExecutionResult> ExecuteAsync(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
    {
        try
        {
            // Custom logic here
            return Outcomes("Done");
        }
        catch
        {
            return Outcomes("Failed");
        }
    }
}
```

### Creating a Custom Event

```csharp
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

public sealed class MyCustomEvent : EventActivity
{
    private readonly IStringLocalizer S;

    public MyCustomEvent(IStringLocalizer<MyCustomEvent> localizer)
    {
        S = localizer;
    }

    public override string Name => "MyCustomEvent";

    public override LocalizedString DisplayText => S["My Custom Event"];

    public override LocalizedString Category => S["Custom"];

    public override bool CanExecute(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
    {
        return true;
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
    {
        return Outcomes(S["Done"]);
    }
}
```

### Registering Custom Activities

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<MyCustomTask, MyCustomTaskDisplayDriver>();
        services.AddActivity<MyCustomEvent, MyCustomEventDisplayDriver>();
    }
}
```

### Workflow Expressions

Workflows support JavaScript and Liquid expressions. Tasks with dual syntax (IfElseTask, ForLoopTask, etc.) use the `Syntax` property to select which expression to evaluate.

**JavaScript expressions:**
```javascript
// Access workflow input
var contentItem = input("ContentItem");

// Access workflow properties
var count = property("LoopCount");

// Set output
setOutcome("Done");
```

**Liquid expressions:**
```liquid
{{ Workflow.Input.ContentItem.DisplayText }}
{{ Workflow.Properties.MyProperty }}
{% if Workflow.Properties.Count > 0 %}true{% endif %}
```

### LiquidTask Example

`LiquidTask` is the Liquid counterpart of `ScriptTask`. It evaluates a Liquid expression and stores the result in `WorkflowContext.LastResult`:

```csharp
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

public class LiquidTask : TaskActivity<LiquidTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    protected readonly IStringLocalizer S;

    public LiquidTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<LiquidTask> localizer)
    {
        _expressionEvaluator = expressionEvaluator;
        S = localizer;
    }

    public override LocalizedString DisplayText => S["Liquid Task"];
    public override LocalizedString Category => S["Control Flow"];

    public WorkflowExpression<object> Expression
    {
        get => GetProperty(() => new WorkflowExpression<object>());
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(
        WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcomes(S["Done"]);

    public override async Task<ActivityExecutionResult> ExecuteAsync(
        WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        workflowContext.LastResult = await _expressionEvaluator.EvaluateAsync(
            Expression, workflowContext, null);
        return Outcomes("Done");
    }
}
```

### Timer Cron Expressions

Timer events use cron expressions:

- `*/5 * * * *` - Every 5 minutes.
- `0 * * * *` - Every hour.
- `0 0 * * *` - Every day at midnight.
- `0 0 * * 1` - Every Monday at midnight.
- `0 0 1 * *` - First day of every month.
