---
name: orchardcore-workflows
description: Skill for authoring Orchard Core 4.0 workflows and custom workflow activities. Covers the workflow designer, drafts, publishing and versions, typed variables and activity outputs, per-expression syntaxes (Literal, Liquid, JavaScript), branching, workflows as activities, real-time updates, the execution journal and retries, available data, script errors and execution limits, the WorkflowType recipe step, correlation and TriggerEventAsync, custom task and event activities, display drivers and the WorkflowExpressionEditor shape, permissions, trimming, and upgrading from 3.x or earlier 4.0 previews. Use this skill when requests mention Orchard Core Workflows, WorkflowType, workflow activities, the workflow designer, or workflow instances. Strong matches include IWorkflowManager, WorkflowExecutionContext, IWorkflowExpressionManager, WorkflowExpression, TaskActivity, EventActivity, ActivityDisplayDriver, AddActivity, IWorkflowTypeVersionStore, and WorkflowsPermissions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Workflows

A workflow type (definition) is a set of activities connected by outcome transitions. Events start or resume a workflow; tasks do work and return outcomes. Orchard Core 4.0 replaces the jsPlumb editor with a Vue **designer**, saves edits to a **draft**, and creates a numbered **version** on each publish; every instance runs on the version it started on.

- Author workflows in **Workflows**, then export them (workflow **Actions → Export**, or `AllWorkflowTypeDeploymentStep`) and reuse that payload in a `WorkflowType` recipe step instead of hand-writing activity ids and transitions.
- The first event added to a workflow without a start activity becomes its start activity. Only events can be start activities. The workflows list shows a **No startup task** badge when none is set.
- `WorkflowIndex.WorkflowStatus` is stored as the integer value of the `WorkflowStatus` enum (since 3.0).

## Enable the features

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

| Feature | Provides |
|---|---|
| `OrchardCore.Workflows` | Designer, engine, core activities, trimming, journal, variables, expression syntaxes. Depends on `OrchardCore.Liquid` and `OrchardCore.Scripting`. |
| `OrchardCore.Workflows.Http` | HTTP events and tasks, signals, `signal_url`, HTTP script functions. |
| `OrchardCore.Workflows.Timers` | `TimerEvent`. |
| `OrchardCore.Workflows.Session` | `CommitTransactionTask`. |
| `OrchardCore.SignalR` (separate module) | Turns on [real-time updates](#real-time-updates) of the designer and instance pages. |

User tasks (`UserTaskEvent`) are available when Workflows, Contents and Roles are enabled. Enable every feature that contributes an activity before importing a workflow that uses it.

## The designer

- **Layout.** Activities pane (left, searchable, drag or click to add), canvas (pan, zoom, connect by dragging from an outcome port, click a dangling `+` port to add the next activity already connected), workflow panel (right, tabs **Variables**, **Workflow** settings, **Issues**), activity panel (bottom, opened by double-click, the settings button or Enter; tabs **Settings**, **Outputs**, **Available data**, and **Runs** on instance pages).
- **Issues.** Unreachable activities, missing start activity, broken connections, unused extra transitions, and activity settings that the activity's driver rejects (for example a required expression left empty). Errors block publishing; warnings are confirmed.
- **Workflows list.** **Edit** opens the designer; **Clone** (formerly Duplicate), **Export** and **Delete** are in each row's **Actions** menu. The old **Properties** page is gone; settings are edited in the designer's **Workflow** tab as part of the draft. **Create Workflow** only asks for a name.
- **Instances at a time** replaces the **Single instance** checkbox: **Any number**, **One at a time** (`IsSingleton`), or **One per correlated item** (`IsSingletonPerCorrelation`, an event does not start a new instance while an instance with the same correlation id waits).
- **Run…** runs the *published* version of a workflow that starts with **Started By Workflow** (asks for its input variables) or with an **HTTP Request** event (sends a request to its URL with the query or form values the workflow reads). Requires **Execute workflows** and an enabled workflow.
- **Instances.** The designer's **Instances** button and the workflows list's **Instances** button (every workflow) show counts per status (Halted, Running, Faulted, Finished, Aborted), filters by status, creation date and workflow, and bulk **Retry**, **Cancel** and **Delete**. An instance page shows the instance in a read-only designer with its blocking activities, executed path, variables, journal and state.
- **Keyboard.** Ctrl+Z/Ctrl+Y undo canvas changes, Tab moves between activities, Enter edits, Delete removes (undo toast), Shift+F10 opens the activity actions (including **Connect an outcome to…** and **Collapse the activities after it**).

Overrides of `WorkflowType/Edit`, `Workflow/Details`, the activity create/edit pages or `Activity_DesignReadOnly` no longer apply; remove or rewrite them.

## Drafts, publishing and versions

- Designer changes are saved to a draft as you make them. The published definition keeps running until **Publish**, which creates the next version (1, 2, 3...). **Discard draft** drops the draft. Concurrent editors get **Reload** or **Overwrite**.
- A version holds activities, transitions, variables and the execution settings (`IsSingleton`, `IsSingletonPerCorrelation`, `LockTimeout`, `LockExpiration`, `DeleteFinishedWorkflows`, `IsActivity`, `BranchingMode`, `FaultOnScriptErrors`, `RecordActivityData`). Renaming or enabling/disabling does not create a version.
- **Versions** in the toolbar lists who published each version and how many instances run on it, with **View**, **Compare** (with the draft or the published version) and **Restore** (into the draft).
- New instances start on the published version (`Workflow.WorkflowTypeVersionId`). Restarting an instance starts a new one on the published version. Instances created before upgrading keep running on the current definition.
- `IWorkflowTypeEventHandler` handlers run on publish, not on every designer change. Recipes, deployment plans and `IWorkflowTypeStore.SaveAsync` still change the workflow type directly; `SaveAsync` creates a version when activities, transitions, variables or execution settings changed.
- `IWorkflowTypeVersionStore` lists and loads versions (`ListAsync`, `GetAsync`, `GetLatestAsync`). `GetWorkflowTypeAsync(workflowType, versionId)` returns the definition an instance runs; never save the object it returns.

Keep only the most recent versions per workflow (versions that instances run on are never deleted; every version is kept by default):

```json
{
  "OrchardCore": {
    "Workflows": {
      "Versions": {
        "MaxCount": 20
      }
    }
  }
}
```

## The WorkflowType recipe step

`WorkflowTypeStep` deserializes each `data` entry into `WorkflowType`:

| Property | Notes |
|---|---|
| `WorkflowTypeId` | Stable id. An existing id is **updated**: the import becomes the next version, its instances are kept on their versions, and its draft is discarded. A new id is created. |
| `Name`, `IsEnabled` | Not versioned. |
| `IsSingleton`, `IsSingletonPerCorrelation`, `LockTimeout`, `LockExpiration`, `DeleteFinishedWorkflows` | Execution settings. |
| `IsActivity` | Usable by **Execute Workflow**. |
| `BranchingMode` | `"FirstOnly"` (default) or `"All"`. |
| `FaultOnScriptErrors`, `RecordActivityData` | Both default to `false` when omitted (the designer checks **Record activity data** for new workflows). |
| `Variables` | `Name`, `TypeName`, `DefaultValue` (JSON), `Description`, `IsInput`, `IsOutput`. |
| `Activities` | `ActivityId`, `Name` (activity type name), `X`, `Y`, `IsStart`, `Properties`. |
| `Transitions` | `SourceActivityId`, `SourceOutcomeName`, `DestinationActivityId`. |

Exports omit `Id` and `VersionId`. On import, an `HttpRequestEvent` whose `Url` is missing or was generated by another site is given a new URL; a valid one is kept, so re-importing does not create a version.

Common activity `Properties` keys: `ActivityMetadata` (`Title`), expressions as `{ "Expression": "...", "Syntax": "Liquid" }`, `OutputBindings` (`{ "outputName": "variableName" }`) and `ActivityRetryPolicy` on tasks.

```json
{
  "steps": [
    {
      "name": "WorkflowType",
      "data": [
        {
          "WorkflowTypeId": "orderhook",
          "Name": "Order webhook",
          "IsEnabled": true,
          "BranchingMode": "FirstOnly",
          "RecordActivityData": true,
          "Variables": [
            { "Name": "quantity", "TypeName": "number", "DefaultValue": 1 },
            { "Name": "message", "TypeName": "string" }
          ],
          "Activities": [
            {
              "ActivityId": "received",
              "Name": "HttpRequestEvent",
              "X": 40,
              "Y": 40,
              "IsStart": true,
              "Properties": {
                "ActivityMetadata": { "Title": "Order received" },
                "HttpMethod": "GET",
                "ValidateAntiforgeryToken": false
              }
            },
            {
              "ActivityId": "compose",
              "Name": "SetVariableTask",
              "X": 380,
              "Y": 40,
              "IsStart": false,
              "Properties": {
                "VariableName": "message",
                "Value": {
                  "Expression": "{{ Workflow.Variables.quantity }} item(s) ordered.",
                  "Syntax": "Liquid"
                }
              }
            },
            {
              "ActivityId": "reply",
              "Name": "HttpResponseTask",
              "X": 720,
              "Y": 40,
              "IsStart": false,
              "Properties": {
                "Content": { "Expression": "{{ Workflow.Variables.message }}" },
                "HttpStatusCode": 200,
                "ContentType": { "Expression": "text/plain" }
              }
            }
          ],
          "Transitions": [
            { "SourceActivityId": "received", "SourceOutcomeName": "Done", "DestinationActivityId": "compose" },
            { "SourceActivityId": "compose", "SourceOutcomeName": "Done", "DestinationActivityId": "reply" }
          ]
        }
      ]
    }
  ]
}
```

The **Workflow Variables Samples** recipe (`workflows-variables-samples`) in the Workflows module is a complete reference for variables, output bindings and Execute Workflow. More payloads are in [workflow-examples.md](references/workflow-examples.md).

## Built-in activities

Activity class names (stored in `ActivityRecord.Name`), not UI labels:

| Feature area | Events | Tasks |
|---|---|---|
| Workflows | `WorkflowFaultEvent` (**Catch Workflow Fault Event**), `StartedByWorkflowEvent` | `NotifyTask`, `SetPropertyTask`, `SetVariableTask`, `SetOutputTask`, `CorrelateTask`, `ExecuteWorkflowTask`, `ForkTask`, `JoinTask`, `ForLoopTask`, `ForEachTask`, `WhileLoopTask`, `IfElseTask`, `ScriptTask`, `LiquidTask`, `LogTask` |
| Workflows Session | — | `CommitTransactionTask` |
| HTTP | `HttpRequestEvent`, `HttpRequestFilterEvent`, `SignalEvent` | `HttpRedirectTask`, `HttpRequestTask`, `HttpResponseTask` |
| Timers | `TimerEvent` (`CronExpression`, `UseLocalTime`) | — |
| User tasks | `UserTaskEvent` | — |
| Contents | `ContentCreatedEvent`, `ContentDeletedEvent`, `ContentPublishedEvent`, `ContentUnpublishedEvent`, `ContentUpdatedEvent`, `ContentDraftSavedEvent`, `ContentVersionedEvent` | `CreateContentTask`, `RetrieveContentTask`, `UpdateContentTask`, `DeleteContentTask`, `PublishContentTask`, `UnpublishContentTask` |
| Users | `UserCreatedEvent`, `UserDeletedEvent`, `UserEnabledEvent`, `UserDisabledEvent`, `UserUpdatedEvent`, `UserLoggedInEvent`, `UserLoggedOutEvent`, `UserConfirmedEvent` | `AssignUserRoleTask`, `ValidateUserTask`, `RegisterUserTask` |
| Data Pipelines | `DataPipelineRunCompletedEvent` | `RunDataPipelineTask` |
| Other modules | — | `EmailTask`, `SmsTask`, `NotifyUserTask`, `NotifyContentOwnerTask`, `UpdateTwitterStatusTask`, `ValidateReCaptchaTask`, `MetaConversionsApiEventTask`, `ValidateAntiforgeryTokenTask`, `AddModelValidationErrorTask`, `ValidateFormTask`, `ValidateFormFieldTask`, `BindModelStateTask`, `HttpRedirectToFormLocationTask`, `UnassignUserRoleTask`, `GetUsersByRoleTask`, `DisableTenantTask`, `EnableTenantTask`, `CreateTenantTask`, `SetupTenantTask` |

- `UserRegisteredEvent` does not exist; use `UserCreatedEvent` or `RegisterUserTask`.
- `ForEachTask` over a `null` enumerable follows `Done` (it used to throw).
- `CreateTenantTask` validates with `ITenantValidator`; invalid input takes `Failed` with the errors as `LastResult`. Always connect `Failed`.
- `HttpRequestTask` URL-encodes Liquid values in **Url**, **Headers** and **Content Type** (use `| raw` only for trusted values); **Body** is not encoded.
- `SmsTask` has an optional **From Phone Number** (`FromNumber`, Liquid). `UserTaskEvent` action names are HTML-encoded on the editor buttons.
- `RunDataPipelineTask` queues a run of a published pipeline (outcomes `Succeeded`/`Failed` when waiting, else `Queued`/`Failed`, plus `Skipped`); see `orchardcore-data-pipelines`.

## Expressions and syntaxes

Each expression of **Correlate**, **For Each**, **For Loop**, **If/Else**, **Set Output**, **Set Property**, **Set Variable** and **While Loop** has its own syntax, chosen next to the expression. Other activities keep a fixed syntax (for example `LogTask.Text` and `HttpRequestTask.Url` are Liquid, `ScriptTask.Script` is JavaScript).

| Syntax | `WorkflowExpressionSyntaxes` | Value |
|---|---|---|
| Literal | `Literal` | The text itself converted to the expected type (`true`, invariant numbers, JSON objects, JSON arrays or comma-separated lists). |
| Liquid | `Liquid` | A Liquid template (`{{ Workflow.Properties.Count > 0 }}`). |
| JavaScript | `JavaScript` | A JavaScript expression (`input("Count") > 0`). |

An expression is stored with its syntax:

```json
"Condition": {
  "Expression": "{{ Workflow.Properties.Count > 0 }}",
  "Syntax": "Liquid"
}
```

Activities stored the 3.x way (one `Syntax` setting plus a JavaScript and a `Liquid...` property per expression, such as `ConditionExpression`/`LiquidCondition`) run as before and move to the new shape when saved in the designer. `WorkflowExpression<T>.Syntax` is `null` for them, and the activity picks the syntax (`WorkflowExpressionSyntaxes.Resolve(...)` does this for the built-ins).

### JavaScript and Liquid

```javascript
var item = input("ContentItem");
setVariable("total", variable("quantity") * variable("unitPrice"));
setProperty("Count", (property("Count") || 0) + 1);
output("Result", item);
setCorrelationId(item.ContentItemId);
```

```liquid
{{ Workflow.Input.ContentItem.DisplayText }}
{{ Workflow.Input.ContentEvent.ContentType }}
{{ Workflow.Variables.total }}
{{ Workflow.Properties.Count }}
{{ Workflow.LastResult }}
{{ Workflow.CorrelationId }}
{{ 'Approved' | signal_url }}
```

- Workflow functions: `workflow()`, `workflowId()`, `input(name)`, `output(name, value)`, `property(name)`, `setProperty(name, value)`, `variable(name)`, `setVariable(name, value)`, `lastResult()`, `correlationId()`, `setCorrelationId(id)`, `signalUrl(signal)`, `createWorkflowToken(...)`; `setOutcome(name)` only in `ScriptTask`.
- HTTP functions: `httpContext()`, `queryString()`, `requestForm()`, `readBody()`, `responseWrite()`, `absoluteUrl()`, `deserializeRequestData()` (and the deprecated `queryStringAsJson()`/`requestFormAsJson()`).
- Request helpers return a string for one value, a list of strings for several, `null` for none, so `===`, `typeof` and string methods work. `httpContext().Request.Query['name']` is still an object.
- Liquid runs in a view scope, so `t` and other view filters work. Register custom input DTOs with `TemplateOptions.MemberAccessStrategy.Register<T>()` for Liquid to read their members, and never put secrets in input.

## Variables

Declare typed variables in the designer's **Variables** tab (versioned with the workflow). Each instance has its own values.

| Type name | Values |
|---|---|
| `string` | Text; numbers, dates, booleans and JSON convert to text. |
| `number` | `double`, from numbers and invariant text. |
| `boolean` | `true`/`false`. |
| `datetime` | UTC date and time, ISO 8601 text without offset is UTC. |
| `object` | Dictionary from objects or JSON object text. |
| `array` | List from lists or JSON array text. |
| `any` | Any value, unconverted. |
| `contentItem` | Content item (Contents feature); stored as the id and reloaded on resume. |

- Read and write with `variable()`/`setVariable()`, `{{ Workflow.Variables.name }}`, the **Set Variable** task (`VariableName`, `Value`), or by binding an activity output in the **Outputs** tab (`Properties.OutputBindings`).
- Names are identifiers, unique ignoring case. Defaults apply when an instance starts (or resumes on a version that adds the variable).
- A value that does not convert faults the workflow (`WorkflowVariableException`), except in `setVariable()`, where it is a script error.
- A variable is a typed workflow property (`Properties["name"]`), so `property()`, `Workflow.Properties` and Set Property still see it; setting an undeclared name with Set Variable stores a plain property.
- Built-in outputs: Script `Result`; Liquid `Result`; Set Property `Value`; HTTP Request `Body`, `StatusCode`, `Response`; Create/Retrieve/Update Content `ContentItem`; Execute Workflow the child's output variables.

## Available data and last result

The activity panel's **Available data** tab lists what the selected activity's expressions can read, with a JavaScript and a Liquid expression for each (click to insert): **Variables**, **This activity** (values set before its own expressions run), **From an activity** (inputs, properties and outputs of activities on a path to it), **Workflow** (last result, correlation id), **Inputs**, **Global** Liquid values (`Site`, `User`, `Request`, `Culture`, `Environment`, `Content`) and **Functions**. Script editors offer the same completions.

`lastResult()` / `{{ Workflow.LastResult }}` is what the previous activity returned: the content item after Retrieve Content, the response (`Body`, `Headers`, `StatusCode`, `ReasonPhrase`, `IsSuccessStatusCode`) after HTTP Request, the send result after Email/SMS, the outputs (or fault message) after Execute Workflow, the current item or index in loops. Content events provide `ContentItem` and `ContentEvent` (`Name`, `ContentType`, `ContentItemId`, `ContentItemVersionId`, `IsStart`); **Catch Workflow Fault Event** provides `WorkflowFault`.

## Branching

- Use **Fork** and **Join** (`Mode` `WaitAll` or `WaitAny`) for explicit parallel branches.
- With the workflow setting **Outcomes with several transitions** = **Follow every transition** (`BranchingMode: "All"`), an outcome connected to several activities runs each, in the order added, like Fork branches. The default **Follow the first transition only** ignores the extra transitions and the designer warns about them.
- State machine pattern: a `state` text variable, a **Script** task whose available outcomes are the states and whose script is `setOutcome(variable('state'));`, events per state that set the next state and loop back. See [workflow-examples.md](references/workflow-examples.md#state-machine).

## Workflows as activities

1. In the child workflow's settings, check **Usable as an activity** (`IsActivity`).
2. Mark its variables **Input** (`IsInput`) and **Output** (`IsOutput`).
3. Start it with **Started By Workflow** (`StartedByWorkflowEvent`); otherwise its first start activity is used.

Child workflows appear in the activities pane's **Workflows** category as presets of **Execute Workflow** (`ExecuteWorkflowTask`: `WorkflowTypeId`, `Inputs` as a dictionary of expressions, `WaitForCompletion`, `Outputs`). With `WaitForCompletion`, the task follows `Done` when the child finishes (even if it waits on events), stores the child outputs, and follows `Failed` when the child faults; without it, `Done` follows as soon as the child starts. The child runs on its published version with `ParentWorkflowId`/`ParentActivityId`; nesting is limited to 16 levels. Re-edit the task after changing the child's outputs.

Code can start a child with `IWorkflowManager.StartChildWorkflowAsync(workflowType, parentContext, parentActivityId, input)`; the parent activity resumes with a `ChildWorkflowResult` input (`ChildWorkflowResult.InputKey`). Report a started child to the journal with `workflowContext.ReportChildWorkflow(workflowId)`. Modules add activities-pane entries with preset properties through `IActivityPresetProvider` (see [custom-activity-examples.md](references/custom-activity-examples.md#activity-presets)).

## Execution journal and retries

- Each executed activity is recorded as a `WorkflowExecutionRecord` (status, outcomes, timing, error, `ChildWorkflowId`) in the `WorkflowJournal` collection and deleted with its instance. The instance page highlights the executed path, marks the faulted activity, and lists records in the **Journal** tab.
- **Record activity data** (`RecordActivityData`) also keeps evaluated expressions and results, outputs, changed variables and the last result (values cut at 2,000 characters, 32,000 per record), shown in the **Runs** tab. Leave it off for workflows handling sensitive data.
- **Retry from here** reruns a faulted instance from an activity with its saved state (requires **Execute workflows**; `IWorkflowManager.RetryActivityAsync(workflow, activityId)`). Instances from before the upgrade have no journal; select the activity to retry.
- **Automatic retries** (task settings, **When it fails**) are stored as `Properties.ActivityRetryPolicy`: `MaxRetries` (0 to 10), `DelaySeconds` (0 retries at once), `Backoff` (`Fixed` or `Exponential`, capped at one day), `OnFailure` (`FaultWorkflow` or `FollowFailedOutcome`, which adds a `Failed` outcome whose last result is the error). Delayed retries leave the instance faulted with `Workflow.PendingRetry`; the **Workflow Retries** background task runs them every minute on one node. Events are never retried, and **Catch Workflow Fault Event** runs only after the retries are spent.

```json
{
  "OrchardCore": {
    "Workflows": {
      "Journal": {
        "Enabled": true,
        "MaxRecordsPerInstance": 1000
      }
    }
  }
}
```

## Script errors and execution limits

- A JavaScript expression that throws, does not parse, or returns an unconvertible value still evaluates to the default (`null`, `false`, `0`) and logs the error, but the error is now reported (`WorkflowExecutionContext.ReportScriptError`, `ScriptErrors`): the instance page outlines the activity in amber and the journal shows a **Script error** badge.
- With **Fault the workflow on script errors** (`FaultOnScriptErrors`), the instance faults at that activity with a `WorkflowScriptException` instead. Off by default.
- A script stopped before producing a value always faults the workflow at its activity and triggers **Catch Workflow Fault Event**: `OperationCanceledException`, `TimeoutException`, and Jint's `StatementsCountOverflowException`, `MemoryLimitExceededException`, `RecursionDepthOverflowException` and `ExecutionCanceledException` from `Jint.Options` limits (`MaxStatements()`, `TimeoutInterval()`, `LimitMemory()`, `LimitRecursion()`, `CancellationToken()`) or from `WorkflowExecutionContext.Cancel()`. Outside an activity (for example a fault event's error filter) the exception reaches the caller.
- A rejected promise, including `Constraints.PromiseTimeout`, is an ordinary script error. Configure limits as described in `orchardcore-scripting`. Custom activities must not catch these execution-limit exceptions.

## Real-time updates

With `OrchardCore.SignalR` enabled, the designer shows who else has the workflow open and offers **Reload** when someone changes, publishes or discards the draft, and instance pages follow running instances. Pages connect to the tenant's `/hubs/workflows` hub, which requires **Manage workflows**; it works with the Redis and Azure SignalR backplanes. `IWorkflowDesignerNotifier` (`WorkflowTypeChangedAsync`, `InstanceChangedAsync`) receives the changes; without SignalR a no-op implementation is used.

## Correlation, blocking and resuming

`CorrelateTask` sets `WorkflowExecutionContext.CorrelationId`; use a stable id (content item id, order id) so a later event resumes the right instance. An `EventActivity` halts by default; the manager persists the instance, and `TriggerEventAsync` resumes matching halted instances (and starts workflows whose start activity matches) after the event's `CanExecuteAsync`.

```csharp
await workflowManager.TriggerEventAsync(
    nameof(OrderApprovedEvent),
    new Dictionary<string, object>
    {
        ["OrderId"] = orderId,
    },
    correlationId: orderId);
```

`isExclusive` avoids starting a duplicate instance already halted on the start event; `isAlwaysCorrelated` resumes by event type regardless of correlation. Prefer normal correlation or **One per correlated item**. Resuming an instance whose blocking activity is missing from its version faults it.

## Custom activities

| Need | Base class |
|---|---|
| Do work and return an outcome | `TaskActivity<TActivity>` (`Name` is the type name) |
| Start or wait for something external | `EventActivity` (override `Name`; `Execute` halts by default) |

Lifecycle: `GetPossibleOutcomes`/`GetPossibleOutcomesAsync` declares ports; `CanExecuteAsync` gates; `ExecuteAsync` returns `Outcome("...")`, `Halt()` or `Noop()`; after a halt, `ResumeAsync`/`Resume` returns the outcome. Hooks: `OnInputReceivedAsync`, `OnWorkflowStartingAsync`/`StartedAsync`, `OnWorkflowRestartingAsync`/`RestartedAsync`, `OnWorkflowResumingAsync`/`ResumedAsync`, `OnActivityExecutingAsync`/`ExecutedAsync`.

Rules for 4.0 activities:

- Implement `IActivityDescription` with static `LocalizationSource DisplayName` and `CategoryName`, and return `_localizerFactory.Localize(...)` from `DisplayText`/`Category` (inject `IStringLocalizerFactory`). Reuse `ActivityCategoryLocalizationSources` (`Content`, `User`, `Http`, `Messaging`, `Validation`, `Primitives`, `ControlFlow`, `UI`, ...) for shared categories.
- Declare outcomes with `Outcome(S["Done"], S["Failed"])` and return `Outcome("Done")`. `Outcomes(...)` is obsolete.
- Store settings with `GetProperty(() => default)`/`SetProperty(value)`; keep values serializable.
- Evaluate expressions with `IWorkflowExpressionManager.EvaluateAsync(expression, workflowContext, defaultSyntax, context)` so each expression uses its own syntax; pass `new WorkflowExpressionEvaluationContext { Encoder = ... }` when Liquid output goes into HTML or a URL. `IWorkflowExpressionEvaluator` (Liquid) and `IWorkflowScriptEvaluator` (JavaScript) still exist for fixed-syntax fields.
- Give the designer outputs with `IActivityOutputs` + `workflowContext.SetActivityOutput(activityContext, name, value)`, and document values with `IActivityProvidedValues` (or `ActivityRegistration.Provides(...)`), including `ActivityProvidedValue.LastResult(...)`.
- In the driver, edit expressions with `WorkflowExpressionInput` view-model properties, the `WorkflowExpressionEditor` shape (`Factory.CreateWorkflowExpressionEditorAsync(Html, m => m.Condition, ...)`) and `WorkflowExpressionInputValidator.Validate<T>(...)`. These live in the `OrchardCore.Workflows` module package; the activity itself only needs `OrchardCore.Workflows.Abstractions`.

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace MyModule.Workflows.Activities;

public sealed class ApproveOrderTask : TaskActivity<ApproveOrderTask>, IActivityDescription
{
    private readonly IWorkflowExpressionManager _expressionManager;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IStringLocalizer S;

    public ApproveOrderTask(
        IWorkflowExpressionManager expressionManager,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<ApproveOrderTask> localizer)
    {
        _expressionManager = expressionManager;
        _localizerFactory = localizerFactory;
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

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome(S["Approved"], S["Rejected"]);

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var approved = await _expressionManager.EvaluateAsync(Condition, workflowContext, WorkflowExpressionSyntaxes.JavaScript);

        return Outcome(approved ? "Approved" : "Rejected");
    }
}
```

Register the activity with its driver; the optional callback sets the designer icon (otherwise the category icon from `WorkflowOptions.CategoryIcons`, keyed by the untranslated category `Value`, is used):

```csharp
services.AddActivity<ApproveOrderTask, ApproveOrderTaskDisplayDriver>(activity => activity.Icon = "fa-solid fa-check");
services.Configure<WorkflowOptions>(options => options.CategoryIcons[ApproveOrderTask.CategoryName.Value] = "fa-solid fa-cart-shopping");
```

`ActivityDisplayDriver<TActivity>` renders `{Activity}_Fields_Thumbnail` (activities pane card) and `{Activity}_Fields_Design` (canvas body); `ActivityDisplayDriver<TActivity, TViewModel>` adds `{Activity}_Fields_Edit`. Put the views in `Views/Items/`. The designer draws the card, icon and outcome ports itself. Register activities that depend on another feature in a `[RequireFeatures("...")]` startup. Trigger custom events through `IWorkflowManager.TriggerEventAsync`; `AddActivity` does not.

The full task (outputs, provided values, driver, views), a custom event, and the extension points (custom syntax, variable type, presets, global values) are in [custom-activity-examples.md](references/custom-activity-examples.md).

### Activity editors in the designer

The designer injects `{Activity}_Fields_Edit` into its activity panel without a page reload and posts it as a form when a field changes. Plain form fields need nothing more. Editors with scripts must:

- Register the script as a resource (`<script asp-name="..." at="Foot">`), and initialize editors added to the page later (the script is not loaded twice).
- Scope the script to a wrapper such as `data-task-editor="my-task"`, and read element ids from `data-` attributes or `Html.IdFor(...)`; never hard-code ids.
- Keep form fields current: the designer dispatches `submit` on the form before collecting `FormData`; dispatch a bubbling `change` on a field when the user commits an edit.
- Release what they create on the bubbling `oc:editor-unmounting` event.
- Not depend on jQuery.

## Permissions

`OrchardCore.Workflows.WorkflowsPermissions` (in `OrchardCore.Workflows.Abstractions`):

| Permission | Default roles | Gates |
|---|---|---|
| `ManageWorkflows` | Administrator | Workflows list, designer, instances, the `/hubs/workflows` hub. Security critical. |
| `ExecuteWorkflows` | Administrator, Editor | **Run…**, **Retry from here** and bulk retry. Security critical. |
| `ManageWorkflowSettings` | Administrator | Workflow settings (trimming); implied by `ManageWorkflows`. |

```csharp
if (!await _authorizationService.AuthorizeAsync(User, WorkflowsPermissions.ManageWorkflows))
{
    return Forbid();
}
```

## Trimming

Trimming deletes old instances (and their journal). Configure it in **Settings → Workflow Trimming** or with the `settings` step; it runs daily and removes at most 5000 instances per run.

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

Change the batch size with `OrchardCore:Workflows:Trimming:BatchSize` in `appsettings.json`.

## Upgrading from 3.x

- Permissions: `OrchardCore.Workflows.Permissions.*` static members → `OrchardCore.Workflows.WorkflowsPermissions.*`. The Editor role stereotype grants only `ExecuteWorkflows`; review existing Editor roles.
- Configuration: `OrchardCore:OrchardCore_Workflows` → `OrchardCore:Workflows` (old name still read for trimming).
- `Outcomes(...)` is obsolete → `Outcome(...)`.
- Scripts stopped by an execution limit or cancellation fault the workflow instead of returning `null`/`false`/`0`; request helpers return strings.
- `CreateTenantTask` validates and can take `Failed`; `HttpRequestTask` URL-encodes Liquid in Url, Headers and Content Type.
- Existing workflows become version 1 with no manual step; existing instances keep their status and run on the current definition (no journal or executed path). Activities whose stored settings their editor rejects are listed in **Issues** and block publishing until fixed, but keep running.
- Everything in the next section also applies.

## Upgrading from earlier 4.0 previews

- **Editor.** The jsPlumb editor, `WorkflowTypeViewModel`, `WorkflowTypeUpdateModel`, `ActivityEditViewModel`, `RenderActivity.cshtml`, the activity create/edit pages (their URLs redirect to the designer with `?activityId=`) and `Activity_DesignReadOnly` are removed. `WorkflowType/Edit` renders the designer with `WorkflowDesignerViewModel`; `WorkflowViewModel.ActivityDesignShapes`/`WorkflowTypeJson` → `DesignerConfigJson`.
- **Resources.** `workflow-editor`/`workflow-viewer` scripts and their stylesheets are removed; `jsplumb` and `jsplumbtoolkit-defaults` are obsolete. `workflow-syntax-toggle` is unused (still registered, to be removed); `task-editors/correlate-task` is removed.
- **Expression activities.** Correlate, For Each, For Loop, If/Else, Set Output, Set Property, Set Variable and While Loop take `IWorkflowExpressionManager` instead of `IWorkflowScriptEvaluator`/`IWorkflowExpressionEvaluator`; their view models use `WorkflowExpressionInput` (for example `IfElseTaskViewModel.Condition` replaces `ConditionExpression`, `LiquidConditionExpression` and `Syntax`). Update overridden editor views to render the `WorkflowExpressionEditor` shape.
- **Drivers.** The Set Property driver is `SetPropertyTaskDisplayDriver`; `SetVariableTaskDisplayDriver` now belongs to the new Set Variable task.
- **Localization.** Built-in activities and the base classes `ContentActivity`, `ContentEvent`, `ContentTask`, `UserActivity`, `UserEvent`, `TenantActivity`, `TenantTask` and `NotifyUserTaskActivity` take an `IStringLocalizerFactory` before their `IStringLocalizer<T>`; derived classes must pass it. Category PO entries now use `msgctxt "OrchardCore.Workflows.Activities.ActivityCategoryLocalizationSources"` (or the module's sources class).
- **Engine constructors.** `WorkflowManager` takes `IWorkflowTypeVersionStore`, `IWorkflowVariableTypeProvider`, `IWorkflowExecutionJournal`, `IWorkflowDesignerNotifier` and `IStringLocalizerFactory`; `WorkflowTypeStore` takes `IWorkflowTypeVersionStore` and `IWorkflowExecutionJournal`; `WorkflowTrimmingService` takes `IWorkflowExecutionJournal`. Resolve them from DI instead of constructing them.
- **Interfaces.** Custom `IWorkflowManager` implementations add `StartChildWorkflowAsync`, `RetryActivityAsync` and `RunDueRetryAsync`; custom `IWorkflowStore` implementations add `ListDueRetriesAsync` and `HasHaltedInstanceAsync(workflowTypeId, correlationId)`. `WorkflowExecutionRecordStatus` gains `Retrying` and `Failed`.
- **Behavior.** `IWorkflowTypeEventHandler` fires on publish; publishing no longer affects running instances; a `WorkflowType` recipe for an existing id updates it (keeping instances) instead of deleting and recreating it.
- **Editor scripts.** Activity editors with scripts must follow the [designer editor contract](#activity-editors-in-the-designer).
