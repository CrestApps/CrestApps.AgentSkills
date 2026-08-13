---
name: orchardcore-workflows
description: Skill for authoring Orchard Core workflows and custom workflow activities. Covers workflow definitions, WorkflowType recipes, event and task activities, activity display drivers, Liquid and JavaScript expressions, correlation, and blocking or resuming instances. Use this skill when requests mention Orchard Core Workflows, Create a Workflow, WorkflowType Recipe Step, Built-in Event Activities, Built-in Task Activities, Custom Workflow Activities, TriggerEventAsync, or Workflow Correlation. Strong matches include work with OrchardCore.Workflows, IWorkflowManager, WorkflowExecutionContext, TaskActivity, EventActivity, ActivityDisplayDriver, AddActivity, TriggerEventAsync, and WorkflowTypeStep. It also helps with workflow examples, activity registration, custom event triggering, and the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Workflows

Workflows are reusable visual definitions made of activities and outcome transitions. Create a workflow in **Workflows → Workflows**, add an event as a start activity, configure tasks/events, connect outcomes, and enable the definition. Export a tested workflow through the workflow deployment step and reuse its serialized payload in a `WorkflowType` recipe step rather than hand-creating activity IDs or transitions.

In Orchard Core 3.0, `WorkflowIndex.WorkflowStatus` is stored as an integer
that matches the `WorkflowStatus` enum. Update raw SQL and custom index
queries to compare the column with the enum's integer value. Existing
values are migrated automatically.

## Enable the required features

Enable the base feature and every feature that contributes an activity before importing a workflow that uses it.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Workflows",
        "OrchardCore.Workflows.Http",
        "OrchardCore.Workflows.Timers",
        "OrchardCore.Contents"
      ],
      "disable": []
    }
  ]
}
```

## Define a workflow with a recipe

The `WorkflowType` step imports the serialized workflow definitions in `data`. Create the definition in the admin editor, then export it with `AllWorkflowTypeDeploymentStep`; retain that output as the recipe payload. This preserves activity properties, stable activity IDs, start flags, and transitions.

```json
{
  "steps": [
    {
      "name": "WorkflowType",
      "data": [
        {
          "WorkflowTypeId": "[js: uuid()]",
          "Name": "Example workflow",
          "IsEnabled": true,
          "Activities": [],
          "Transitions": []
        }
      ]
    }
  ]
}
```

An event can start a workflow and can also block an existing instance. A task performs work and returns one or more outcomes. Only events can be configured as start activities.

## Built-in activities in release/3.0

These are activity class names, not UI labels. Availability depends on the owning feature.

| Feature area | Event activities | Task activities |
|---|---|---|
| Core workflows | `WorkflowFaultEvent` | `NotifyTask`, `SetPropertyTask`, `SetOutputTask`, `CorrelateTask`, `ForkTask`, `JoinTask`, `ForLoopTask`, `ForEachTask`, `WhileLoopTask`, `IfElseTask`, `ScriptTask`, `LiquidTask`, `LogTask`, `CommitTransactionTask` |
| HTTP workflows | `HttpRequestEvent`, `HttpRequestFilterEvent`, `SignalEvent` | `HttpRedirectTask`, `HttpRequestTask`, `HttpResponseTask` |
| Timers and user tasks | `TimerEvent`, `UserTaskEvent` | — |
| Contents | `ContentCreatedEvent`, `ContentDeletedEvent`, `ContentPublishedEvent`, `ContentUnpublishedEvent`, `ContentUpdatedEvent`, `ContentDraftSavedEvent`, `ContentVersionedEvent` | `CreateContentTask`, `RetrieveContentTask`, `UpdateContentTask`, `DeleteContentTask`, `PublishContentTask`, `UnpublishContentTask` |
| Users | `UserCreatedEvent`, `UserDeletedEvent`, `UserEnabledEvent`, `UserDisabledEvent`, `UserUpdatedEvent`, `UserLoggedInEvent`, `UserConfirmedEvent` | `AssignUserRoleTask`, `ValidateUserTask`, `RegisterUserTask` |
| Optional integrations | — | `EmailTask`, `SmsTask`, `NotifyUserTask`, `NotifyContentOwnerTask`, `UpdateTwitterStatusTask`, `ValidateReCaptchaTask` |
| Forms, roles, tenants | — | `ValidateAntiforgeryTokenTask`, `AddModelValidationErrorTask`, `ValidateFormTask`, `ValidateFormFieldTask`, `BindModelStateTask`, `HttpRedirectToFormLocationTask`, `UnassignUserRoleTask`, `GetUsersByRoleTask`, `DisableTenantTask`, `EnableTenantTask`, `CreateTenantTask`, `SetupTenantTask` |

`ContentDraftSavedEvent` is a built-in content event, and `UserRegisteredEvent` is not the release/3.0 activity name. Use `UserCreatedEvent` or `RegisterUserTask` as appropriate.

## Expressions and workflow context

`WorkflowExecutionContext` exposes input, output, properties, correlation ID, the workflow instance, and the current last result. `CorrelateTask`, `ForEachTask`, `ForLoopTask`, `IfElseTask`, `SetOutputTask`, `SetPropertyTask`, and `WhileLoopTask` have a syntax selector using `WorkflowScriptSyntax.JavaScript` or `WorkflowScriptSyntax.Liquid`.

```javascript
var item = input("ContentItem");
var count = property("Count");
setProperty("Count", count + 1);
output("Result", item);
setCorrelationId(item.ContentItemId);
```

```liquid
{{ Workflow.Input.ContentItem.DisplayText }}
{{ Workflow.Properties.Count }}
{{ Workflow.CorrelationId }}
{{ 'Approved' | signal_url }}
```

Use the JavaScript APIs `workflow()`, `workflowId()`, `input(name)`, `output(name, value)`, `property(name)`, `setProperty(name, value)`, `lastResult()`, `correlationId()`, `setCorrelationId(id)`, and `signalUrl(signal)` only in script-enabled activity fields. Use Liquid only in fields whose syntax is Liquid.

## Correlation, blocking, and resuming

`CorrelateTask` evaluates its configured JavaScript or Liquid expression and sets `WorkflowExecutionContext.CorrelationId`. Use a stable domain identifier such as a content item ID when a later event must resume the same instance.

`EventActivity.Execute` halts the workflow. The workflow manager persists a halted instance when it reaches a blocking event, then `TriggerEventAsync` finds matching definitions/instances, checks the event's `CanExecuteAsync`, and resumes the activity with the supplied input. A workflow with no blocking activity completes in one execution.

Trigger a custom event with an event name matching the activity `Name` and use the same correlation ID when resuming a correlated instance:

```csharp
await workflowManager.TriggerEventAsync(
    nameof(MyApprovalEvent),
    new Dictionary<string, object>
    {
        ["RequestId"] = requestId,
    },
    correlationId: requestId);
```

`IWorkflowManager.TriggerEventAsync` also has `isExclusive` to avoid starting a duplicate instance already halted on the matching start event, and `isAlwaysCorrelated` to resume based on event type regardless of correlation ID. Prefer normal correlation unless the event semantics require one of those exceptions.

## Write a custom task activity

Derive from `TaskActivity<TActivity>`, provide localized display metadata, declare all possible outcomes, and return an outcome from `ExecuteAsync`. Add a display driver and its Razor shapes under `Views/Items`.

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Models;

namespace MyModule.Workflows.Activities;

public sealed class MyApprovalTask : TaskActivity<MyApprovalTask>
{
    private readonly IStringLocalizer<MyApprovalTask> _localizer;

    public MyApprovalTask(IStringLocalizer<MyApprovalTask> localizer)
    {
        _localizer = localizer;
    }

    public override LocalizedString DisplayText => _localizer["Approval task"];

    public override LocalizedString Category => _localizer["My Module"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
        => Outcomes(_localizer["Done"], _localizer["Rejected"]);

    public override Task<ActivityExecutionResult> ExecuteAsync(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
        => Task.FromResult(Outcomes("Done"));
}
```

## Write a custom event activity

Derive from `EventActivity`. Its base `Execute` returns `Halt`, so implement `CanExecuteAsync` to accept only the input intended for the event and override `Resume` or `ResumeAsync` to return the continuation outcome.

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Models;

namespace MyModule.Workflows.Activities;

public sealed class MyApprovalEvent : EventActivity
{
    private readonly IStringLocalizer<MyApprovalEvent> _localizer;

    public MyApprovalEvent(IStringLocalizer<MyApprovalEvent> localizer)
    {
        _localizer = localizer;
    }

    public override string Name => nameof(MyApprovalEvent);

    public override LocalizedString DisplayText => _localizer["Approval received"];

    public override LocalizedString Category => _localizer["My Module"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
        => Outcomes(_localizer["Done"]);

    public override Task<bool> CanExecuteAsync(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
        => Task.FromResult(workflowContext.Input.ContainsKey("RequestId"));

    public override ActivityExecutionResult Resume(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
        => Outcomes("Done");
}
```

## Add display drivers and register activities

`ActivityDisplayDriver<TActivity>` creates thumbnail and design shapes. Add `MyApprovalTask.Fields.Thumbnail.cshtml` and `MyApprovalTask.Fields.Design.cshtml` under `Views/Items`; use `ActivityDisplayDriver<TActivity, TEditViewModel>` plus an unsealed view model and `*.Fields.Edit.cshtml` when the activity has configurable properties.

```csharp
using OrchardCore.Workflows.Display;

namespace MyModule.Workflows.Drivers;

public sealed class MyApprovalTaskDisplayDriver
    : ActivityDisplayDriver<MyApprovalTask>;

public sealed class MyApprovalEventDisplayDriver
    : ActivityDisplayDriver<MyApprovalEvent>;
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<MyApprovalTask, MyApprovalTaskDisplayDriver>();
        services.AddActivity<MyApprovalEvent, MyApprovalEventDisplayDriver>();
    }
}
```

The exact registration API is `AddActivity<TActivity, TDriver>()`. It registers the activity and display driver in `WorkflowOptions`; it does not automatically trigger an event. Trigger the registered event through `IWorkflowManager.TriggerEventAsync`.
