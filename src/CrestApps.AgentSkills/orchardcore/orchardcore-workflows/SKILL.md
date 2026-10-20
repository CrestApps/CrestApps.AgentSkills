---
name: orchardcore-workflows
description: Skill for authoring Orchard Core workflows and custom workflow activities. Covers workflow definitions, WorkflowType recipes, startup tasks, event and task activities, activity display drivers and editor scripts, Liquid and JavaScript expressions, expression encoding, script limits and cancellation that fault a workflow, correlation, blocking or resuming instances, workflow permissions, trimming, and running data pipelines from workflows. Use this skill when requests mention Orchard Core Workflows, WorkflowType Recipe Step, Built-in Event Activities, Built-in Task Activities, Custom Workflow Activities, TriggerEventAsync, Workflow Correlation, User Loggedout Event, CreateTenantTask, SmsTask, HttpRequestTask, or Run Data Pipeline Task. Strong matches include work with OrchardCore.Workflows, IWorkflowManager, WorkflowExecutionContext, IWorkflowScriptEvaluator, IWorkflowExpressionEvaluator, TaskActivity, EventActivity, ActivityDisplayDriver, AddActivity, WorkflowsPermissions, and WorkflowTrimmingSettings.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Workflows

Workflows are reusable visual definitions made of activities and outcome transitions. Create a workflow in **Workflows → Workflows**, add an event, configure tasks/events, connect outcomes, and enable the definition. Export a tested workflow through the workflow deployment step and reuse its serialized payload in a `WorkflowType` recipe step rather than hand-creating activity IDs or transitions.

- The **first event added** to a workflow is marked as its startup task automatically. Saving a workflow that has activities but no startup task shows a warning, and the workflow list shows a **No startup task** badge.
- Since Orchard Core 3.0, `WorkflowIndex.WorkflowStatus` is stored as an integer matching the `WorkflowStatus` enum. Compare raw SQL and custom index queries with the enum's integer value.

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

## Built-in activities

These are activity class names, not UI labels. Availability depends on the owning feature.

| Feature area | Event activities | Task activities |
|---|---|---|
| Core workflows | `WorkflowFaultEvent` | `NotifyTask`, `SetPropertyTask`, `SetOutputTask`, `CorrelateTask`, `ForkTask`, `JoinTask`, `ForLoopTask`, `ForEachTask`, `WhileLoopTask`, `IfElseTask`, `ScriptTask`, `LiquidTask`, `LogTask`, `CommitTransactionTask` |
| HTTP workflows | `HttpRequestEvent`, `HttpRequestFilterEvent`, `SignalEvent` | `HttpRedirectTask`, `HttpRequestTask`, `HttpResponseTask` |
| Timers | `TimerEvent` | — |
| User tasks (Workflows + Contents + Roles) | `UserTaskEvent` | — |
| Contents | `ContentCreatedEvent`, `ContentDeletedEvent`, `ContentPublishedEvent`, `ContentUnpublishedEvent`, `ContentUpdatedEvent`, `ContentDraftSavedEvent`, `ContentVersionedEvent` | `CreateContentTask`, `RetrieveContentTask`, `UpdateContentTask`, `DeleteContentTask`, `PublishContentTask`, `UnpublishContentTask` |
| Users | `UserCreatedEvent`, `UserDeletedEvent`, `UserEnabledEvent`, `UserDisabledEvent`, `UserUpdatedEvent`, `UserLoggedInEvent`, `UserLoggedOutEvent`, `UserConfirmedEvent` | `AssignUserRoleTask`, `ValidateUserTask`, `RegisterUserTask` |
| Data Pipelines | `DataPipelineRunCompletedEvent` | `RunDataPipelineTask` |
| Optional integrations | — | `EmailTask`, `SmsTask`, `NotifyUserTask`, `NotifyContentOwnerTask`, `UpdateTwitterStatusTask`, `ValidateReCaptchaTask`, `MetaConversionsApiEventTask` |
| Forms, roles, tenants | — | `ValidateAntiforgeryTokenTask`, `AddModelValidationErrorTask`, `ValidateFormTask`, `ValidateFormFieldTask`, `BindModelStateTask`, `HttpRedirectToFormLocationTask`, `UnassignUserRoleTask`, `GetUsersByRoleTask`, `DisableTenantTask`, `EnableTenantTask`, `CreateTenantTask`, `SetupTenantTask` |

Notes on specific activities:

- `UserRegisteredEvent` does not exist. Use `UserCreatedEvent` or `RegisterUserTask`.
- **User Loggedout Event** (`UserLoggedOutEvent`) fires when a user signs out through the account logout endpoint, complementing **User Loggedin Event**. Use it to clean up related state or send a notification.
- `CreateTenantTask` runs the same `ITenantValidator` validation as the tenants admin and API. An invalid name, a duplicate request URL prefix and host, or an unknown feature profile takes the `Failed` outcome, and the validation errors are assigned to `LastResult`. Always connect its `Failed` outcome.
- `SmsTask` has an optional **From Phone Number** (`FromNumber`, Liquid, include the country code such as `+1`). Leave it empty to use the provider's default sender.
- `HttpRequestTask` URL-encodes Liquid values rendered in **Url**, **Headers**, and **Content Type**, so user input cannot change the host, path, query, fragment, or inject headers. Use `| raw` only when a value is trusted and must not be encoded. The **Body** is not encoded.
- `UserTaskEvent` action names are HTML-encoded on the content editor buttons.
- `RunDataPipelineTask` queues a run of a published data pipeline (outcomes `Succeeded`/`Failed` when **Wait for the run to end** is on, otherwise `Queued`/`Failed`, plus `Skipped` with **Skip when a run is already queued or running**). The run is the last result and the `DataPipelineRun` output. Pair a `TimerEvent` with it to schedule pipelines. See `orchardcore-data-pipelines`.

## Expressions and workflow context

`WorkflowExecutionContext` exposes input, output, properties, correlation ID, the workflow instance, the last result, and a `CancellationToken`. `CorrelateTask`, `ForEachTask`, `ForLoopTask`, `IfElseTask`, `SetOutputTask`, `SetPropertyTask`, and `WhileLoopTask` have a syntax selector using `WorkflowScriptSyntax.JavaScript` or `WorkflowScriptSyntax.Liquid`.

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
{{ "Order received" | t }}
```

Use the JavaScript APIs `workflow()`, `workflowId()`, `input(name)`, `output(name, value)`, `property(name)`, `setProperty(name, value)`, `lastResult()`, `correlationId()`, `setCorrelationId(id)`, `signalUrl(signal)`, `setOutcome(outcome)`, and `createWorkflowToken(workflowTypeId, activityId, expiresInDays)` only in script-enabled fields. HTTP activities also get `httpContext()`, `queryString()`, `requestForm()`, `readBody()`, `responseWrite()`, `absoluteUrl()`, and `deserializeRequestData()`. Use Liquid only in fields whose syntax is Liquid; Liquid expressions run in a Liquid view scope, so the `t` localization filter works.

### Request values are strings

`queryString(name)`, `requestForm(name)`, and `deserializeRequestData()` (and the deprecated `queryStringAsJson()` / `requestFormAsJson()`) return a string for a single value, a list of strings for several, and `null` for none. `===`, `typeof`, `toUpperCase()`, and `JSON.stringify()` behave as for normal strings. A value read directly from ASP.NET Core, such as `httpContext().Request.Query['name']`, is still an object; prefer the helpers when you need a string.

```javascript
var data = deserializeRequestData();
if (data.status === "approved") {
    setOutcome("Approved");
}
```

### Script limits, cancellation, and faults

Every workflow JavaScript expression is bounded by `WorkflowExecutionContext.CancellationToken`, so `WorkflowExecutionContext.Cancel()` stops a running script. A script stopped before producing a value **faults the workflow at the activity that ran it**, logs the error, and triggers **Workflow Fault** (`WorkflowFaultEvent`). This applies to:

- `OperationCanceledException` (the evaluation's token) and `TimeoutException`, including ones thrown by a .NET method the script calls.
- Jint's `StatementsCountOverflowException`, `MemoryLimitExceededException`, `RecursionDepthOverflowException`, and `ExecutionCanceledException` from the limits configured on `Jint.Options` (`MaxStatements()`, `TimeoutInterval()`, `LimitMemory()`, `LimitRecursion()`, `CancellationToken()`).

A script that throws an ordinary error, does not parse, or returns a value of the wrong type still evaluates to the default value (`null`, `false`, `0`) and logs the error. A rejected promise, including `Constraints.PromiseTimeout`, is still a script error. A script evaluated outside an activity's execution (for example the error filter of a **Workflow Fault** event) lets the exception reach the caller that triggered the workflow. Configure limits as described in `orchardcore-scripting`.

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

`IWorkflowManager.TriggerEventAsync` also has `isExclusive` to avoid starting a duplicate instance already halted on the matching start event, and `isAlwaysCorrelated` to resume based on event type regardless of correlation ID. Prefer normal correlation unless the event semantics require one of those exceptions. Pass a custom DTO in the input and register it with `TemplateOptions.MemberAccessStrategy.Register<T>()` if Liquid must read its members.

## Write a custom task activity

Derive from `TaskActivity<TActivity>`, provide localized display metadata, declare all possible outcomes with `Outcome(...)`, and return an outcome from `ExecuteAsync`. Evaluate Liquid with `IWorkflowExpressionEvaluator` (pass an encoder that matches where the value goes) and JavaScript with `IWorkflowScriptEvaluator`. Do not catch the execution-limit exceptions those evaluators let through; they must fault the workflow.

```csharp
using System.Text.Encodings.Web;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace MyModule.Workflows.Activities;

public sealed class MyApprovalTask : TaskActivity<MyApprovalTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IWorkflowScriptEvaluator _scriptEvaluator;
    private readonly UrlEncoder _urlEncoder;
    private readonly IStringLocalizer S;

    public MyApprovalTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IWorkflowScriptEvaluator scriptEvaluator,
        UrlEncoder urlEncoder,
        IStringLocalizer<MyApprovalTask> localizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _scriptEvaluator = scriptEvaluator;
        _urlEncoder = urlEncoder;
        S = localizer;
    }

    public override LocalizedString DisplayText => S["Approval task"];

    public override LocalizedString Category => S["My Module"];

    // Liquid template, rendered into a URL, so user values are URL-encoded.
    public WorkflowExpression<string> CallbackUrl
    {
        get => GetProperty(() => new WorkflowExpression<string>());
        set => SetProperty(value);
    }

    // JavaScript condition.
    public WorkflowExpression<bool> ApproveCondition
    {
        get => GetProperty(() => new WorkflowExpression<bool>());
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
        => Outcome(S["Approved"], S["Rejected"]);

    public override async Task<ActivityExecutionResult> ExecuteAsync(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
    {
        workflowContext.LastResult = await _expressionEvaluator.EvaluateAsync(CallbackUrl, workflowContext, _urlEncoder);

        var approved = await _scriptEvaluator.EvaluateAsync(ApproveCondition, workflowContext);

        return approved ? Outcome("Approved") : Outcome("Rejected");
    }
}
```

## Write a custom event activity

Derive from `EventActivity`. Its base `Execute` returns `Halt`, so implement `CanExecuteAsync` to accept only the input intended for the event and override `Resume` or `ResumeAsync` to return the continuation outcome.

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace MyModule.Workflows.Activities;

public sealed class MyApprovalEvent : EventActivity
{
    private readonly IStringLocalizer S;

    public MyApprovalEvent(IStringLocalizer<MyApprovalEvent> localizer)
    {
        S = localizer;
    }

    public override string Name => nameof(MyApprovalEvent);

    public override LocalizedString DisplayText => S["Approval received"];

    public override LocalizedString Category => S["My Module"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
        => Outcome(S["Done"]);

    public override Task<bool> CanExecuteAsync(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
        => Task.FromResult(workflowContext.Input.ContainsKey("RequestId"));

    public override ActivityExecutionResult Resume(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
        => Outcome("Done");
}
```

## Add display drivers and register activities

`ActivityDisplayDriver<TActivity>` creates the thumbnail and design shapes (`{Activity}_Fields_Thumbnail`, `{Activity}_Fields_Design`). Add `MyApprovalTask.Fields.Thumbnail.cshtml` and `MyApprovalTask.Fields.Design.cshtml` under `Views/Items`. Use `ActivityDisplayDriver<TActivity, TEditViewModel>` plus an unsealed view model and `*.Fields.Edit.cshtml` when the activity has configurable properties; override `EditActivity`/`UpdateActivity` (or their async variants) to map between them.

```csharp
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;

namespace MyModule.Workflows.Drivers;

public sealed class MyApprovalTaskDisplayDriver
    : ActivityDisplayDriver<MyApprovalTask, MyApprovalTaskViewModel>
{
    protected override void EditActivity(MyApprovalTask activity, MyApprovalTaskViewModel model)
    {
        model.CallbackUrl = activity.CallbackUrl.Expression;
        model.ApproveCondition = activity.ApproveCondition.Expression;
    }

    protected override void UpdateActivity(MyApprovalTaskViewModel model, MyApprovalTask activity)
    {
        activity.CallbackUrl = new WorkflowExpression<string>(model.CallbackUrl);
        activity.ApproveCondition = new WorkflowExpression<bool>(model.ApproveCondition);
    }
}

public sealed class MyApprovalEventDisplayDriver
    : ActivityDisplayDriver<MyApprovalEvent>;

public class MyApprovalTaskViewModel
{
    public string CallbackUrl { get; set; }

    public string ApproveCondition { get; set; }
}
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

Activity editor views must not depend on jQuery; the Workflows editor assets use native DOM APIs and ES modules. Look up elements with `Html.IdFor(...)` ids (the editor prefixes field ids), never hard-coded ids. For a JavaScript/Liquid syntax selector, reuse the built-in `workflow-syntax-toggle` script; see [workflow-examples.md](references/workflow-examples.md).

## Permissions

Use `OrchardCore.Workflows.WorkflowsPermissions` (in `OrchardCore.Workflows.Abstractions`):

| Permission | Default roles |
|---|---|
| `WorkflowsPermissions.ManageWorkflows` | Administrator |
| `WorkflowsPermissions.ExecuteWorkflows` | Administrator, Editor |
| `WorkflowsPermissions.ManageWorkflowSettings` | Administrator |

```csharp
if (!await _authorizationService.AuthorizeAsync(User, WorkflowsPermissions.ManageWorkflows))
{
    return Forbid();
}
```

The Editor role stereotype grants only `ExecuteWorkflows`; earlier builds granted it every workflow permission. Review existing Editor roles if they should not manage workflows.

## Trimming

The Trimming feature deletes old workflow instances (enabled by default, configured in **Settings → Workflow Trimming**). The background task runs daily and removes at most 5000 instances per run.

```json
{
  "steps": [
    {
      "name": "settings",
      "WorkflowTrimmingSettings": {
        "RetentionDays": 90,
        "Disabled": false,
        "Statuses": [
          "Finished",
          "Faulted",
          "Aborted"
        ]
      }
    }
  ]
}
```

Change the batch size in `appsettings.json` under the `OrchardCore:Workflows` section:

```json
{
  "OrchardCore": {
    "Workflows": {
      "Trimming": {
        "BatchSize": 1000
      }
    }
  }
}
```

## Upgrading from 3.x

- Permissions: `OrchardCore.Workflows.Permissions.ManageWorkflows` / `ExecuteWorkflows` / `ManageWorkflowSettings` → `OrchardCore.Workflows.WorkflowsPermissions.*`. The `Permissions` provider class still exists, only its static members were removed.
- Configuration: `OrchardCore:OrchardCore_Workflows` → `OrchardCore:Workflows` (env var `OrchardCore__Workflows__Trimming__BatchSize`). The old name is deprecated but still read.
- Outcomes: `Outcomes(...)` is obsolete → `Outcome(S["Done"], S["Failed"])` in `GetPossibleOutcomes` and `Outcome("Done")` in `ExecuteAsync`/`Resume`.
- Workflow scripts stopped by an execution limit or cancellation now fault the workflow instead of returning `null`/`false`/`0`. An `If/Else` whose condition timed out no longer silently takes `False`. Connect a **Workflow Fault** workflow if you need recovery.
- Request helpers return strings or lists of strings instead of objects. Scripts that worked around the old objects (for example comparing with `==` or calling `.toString()`) still work; `===` comparisons now work too.
- `CreateTenantTask` validates input and can take `Failed` where it used to create a tenant; its constructor takes an extra `ITenantValidator`.
- `HttpRequestTask` URL-encodes Liquid values in Url, Headers, and Content Type. Add `| raw` where a value intentionally contains URL syntax.
- The Editor role no longer gets `ManageWorkflows` and `ManageWorkflowSettings` by default.
- Workflow editor scripts no longer use jQuery. Custom activity editors that relied on the admin's jQuery must switch to native DOM APIs or require the jQuery resource explicitly.
