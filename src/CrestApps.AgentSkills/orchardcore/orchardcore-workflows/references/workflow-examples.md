# Workflow Examples

Build and test workflows in the designer, publish them, then export them for recipes. The JSON below shows the stored shape so you can read or adjust exported payloads.

## Example 1: Notify on Content Published

```
ContentPublishedEvent (BlogPost) → IfElseTask (Condition) → EmailTask → NotifyTask
```

- `ContentPublishedEvent` is the first event added, so it becomes the start activity. Pick the types in its searchable **Content types** list (`ContentTypeFilter`).
- The If/Else condition can be Literal, Liquid or JavaScript, for example `{{ Workflow.Input.ContentEvent.ContentType == "BlogPost" }}` (Liquid) or `input("ContentItem").Owner === "admin"` (JavaScript).
- The **Available data** tab of `EmailTask` lists `ContentItem` and `ContentEvent` with their fields, ready to insert as `{{ Workflow.Input.ContentItem.DisplayText }}`.

## Example 2: Retry a Flaky HTTP Call

```
TimerEvent → HttpRequestTask → [200]    LogTask (success)
                             → [Failed] LogTask (gave up)
```

The task is retried 3 times, 30 seconds apart and doubling, then follows `Failed` instead of faulting the instance.

```json
{
  "steps": [
    {
      "name": "WorkflowType",
      "data": [
        {
          "WorkflowTypeId": "syncinventory",
          "Name": "Sync inventory",
          "IsEnabled": true,
          "RecordActivityData": true,
          "Activities": [
            {
              "ActivityId": "everyhour",
              "Name": "TimerEvent",
              "X": 40,
              "Y": 40,
              "IsStart": true,
              "Properties": {
                "CronExpression": "0 * * * *"
              }
            },
            {
              "ActivityId": "callapi",
              "Name": "HttpRequestTask",
              "X": 380,
              "Y": 40,
              "IsStart": false,
              "Properties": {
                "Url": { "Expression": "https://inventory.example.com/sync" },
                "HttpMethod": "POST",
                "HttpResponseCodes": "200",
                "ActivityRetryPolicy": {
                  "MaxRetries": 3,
                  "DelaySeconds": 30,
                  "Backoff": "Exponential",
                  "OnFailure": "FollowFailedOutcome"
                }
              }
            },
            {
              "ActivityId": "logok",
              "Name": "LogTask",
              "X": 720,
              "Y": 40,
              "IsStart": false,
              "Properties": {
                "LogLevel": "Information",
                "Text": { "Expression": "Inventory synced: {{ Workflow.LastResult.StatusCode }}" }
              }
            },
            {
              "ActivityId": "logfailed",
              "Name": "LogTask",
              "X": 720,
              "Y": 220,
              "IsStart": false,
              "Properties": {
                "LogLevel": "Error",
                "Text": { "Expression": "Inventory sync failed: {{ Workflow.LastResult }}" }
              }
            }
          ],
          "Transitions": [
            { "SourceActivityId": "everyhour", "SourceOutcomeName": "Done", "DestinationActivityId": "callapi" },
            { "SourceActivityId": "callapi", "SourceOutcomeName": "200", "DestinationActivityId": "logok" },
            { "SourceActivityId": "callapi", "SourceOutcomeName": "Failed", "DestinationActivityId": "logfailed" }
          ]
        }
      ]
    }
  ]
}
```

- Retries cover failures that throw (DNS, connection or timeout errors). A response with a status code not listed in `HttpResponseCodes` is not a failure; it follows the `UnhandledHttpStatus` outcome, so connect it too.
- With `DelaySeconds` above 0, the instance waits faulted with a pending retry, and the **Workflow Retries** background task resumes it. With 0, retries run immediately in the same run.
- With `"OnFailure": "FaultWorkflow"` (the default), spent retries fault the instance and start workflows that begin with **Catch Workflow Fault Event**; fix the cause and use **Retry from here**.
- Events are never retried.

## Example 3: Workflow as an Activity

Child workflow, usable as an activity, with an input and an output variable:

```json
{
  "steps": [
    {
      "name": "WorkflowType",
      "data": [
        {
          "WorkflowTypeId": "formatgreeting",
          "Name": "Format a greeting",
          "IsEnabled": true,
          "IsActivity": true,
          "Variables": [
            { "Name": "name", "TypeName": "string", "DefaultValue": "world", "IsInput": true },
            { "Name": "greeting", "TypeName": "string", "IsOutput": true }
          ],
          "Activities": [
            {
              "ActivityId": "start",
              "Name": "StartedByWorkflowEvent",
              "X": 40,
              "Y": 40,
              "IsStart": true,
              "Properties": {}
            },
            {
              "ActivityId": "format",
              "Name": "SetVariableTask",
              "X": 380,
              "Y": 40,
              "IsStart": false,
              "Properties": {
                "VariableName": "greeting",
                "Value": { "Expression": "Hello, {{ Workflow.Variables.name }}!", "Syntax": "Liquid" }
              }
            }
          ],
          "Transitions": [
            { "SourceActivityId": "start", "SourceOutcomeName": "Done", "DestinationActivityId": "format" }
          ]
        }
      ]
    }
  ]
}
```

The parent calls it with `ExecuteWorkflowTask` and binds the child output to its own `reply` variable:

```json
{
  "ActivityId": "greet",
  "Name": "ExecuteWorkflowTask",
  "X": 380,
  "Y": 40,
  "IsStart": false,
  "Properties": {
    "WorkflowTypeId": "formatgreeting",
    "WaitForCompletion": true,
    "Inputs": {
      "name": { "Expression": "queryString('name') || 'world'", "Syntax": "JavaScript" }
    },
    "Outputs": [
      { "Name": "greeting", "TypeName": "string" }
    ],
    "OutputBindings": {
      "greeting": "reply"
    }
  }
}
```

Connect both `Done` and `Failed`. The parent's `reply` variable must be declared. The full working version is the **Workflow Variables Samples** recipe.

## Example 4: Follow Every Transition

A `ContentPublishedEvent` whose `Done` outcome is connected to two tasks. In the exported workflow type, the setting and the two transitions look like this (fragment):

```json
"BranchingMode": "All",
"Transitions": [
  { "SourceActivityId": "published", "SourceOutcomeName": "Done", "DestinationActivityId": "notifyeditors" },
  { "SourceActivityId": "published", "SourceOutcomeName": "Done", "DestinationActivityId": "purgecache" }
]
```

With `"BranchingMode": "All"`, both transitions of `Done` run in the order they were added, like Fork branches; add a `JoinTask` if later work must wait for both. With the default `FirstOnly`, only `notifyeditors` runs and the designer warns about the second transition.

## State Machine

```
SetVariableTask (state = "Draft")
  → ScriptTask (AvailableOutcomes: Draft, Review, Published; script: setOutcome(variable('state'));)
      [Draft]  → SignalEvent ("submit")  → SetVariableTask (state = "Review") → back to ScriptTask
      [Review] → ForkTask → SignalEvent ("approve") / SignalEvent ("reject")
                 → JoinTask (Mode WaitAny) → SetVariableTask (state = "Published" or "Draft") → back to ScriptTask
      [Published] (unconnected, the workflow finishes)
```

- Declare `state` as a Text variable.
- Correlate the instance with the content item id (`CorrelateTask` or a content event) and set **Instances at a time** to **One per correlated item** so each item has one lifecycle.
- The journal shows the states the instance went through; each state's work can be its own workflow run with **Execute Workflow**.

## Example 5: Scheduled Cleanup

- **Trigger**: `TimerEvent` with `CronExpression` `0 2 * * *`.
- **Activities**: `ScriptTask` (queries expired items, returns the list, `setOutcome("Done")`, **Result** output bound to an `array` variable `expired`) → `ForEachTask` (enumerable `variable('expired')`, JavaScript) → `DeleteContentTask` → `LogTask`.
- A `ForEachTask` over `null` follows `Done`.
- If `Jint.Options` sets `MaxStatements()` or `TimeoutInterval()` and the script exceeds it, the workflow faults at `ScriptTask` instead of continuing with an empty list. Turn on **Fault the workflow on script errors** to also stop on ordinary script errors, and start a separate workflow with **Catch Workflow Fault Event** to be notified.

## Example 6: Override a Built-in Activity Editor

Built-in expression editors now render the `WorkflowExpressionEditor` shape from `WorkflowExpressionInput` view-model properties. An override of `Views/Items/IfElseTask.Fields.Edit.cshtml` looks like this (the old `workflow-syntax-toggle` script and `ConditionExpression`/`LiquidConditionExpression`/`Syntax` fields are gone):

```cshtml
@using OrchardCore.Workflows.Models
@model IfElseTaskViewModel

@{
    var conditionEditor = await Factory.CreateWorkflowExpressionEditorAsync(Html, m => m.Condition, editor =>
    {
        editor.Label = T["Condition"].Value;
        editor.Hint = T["An expression that evaluates to true or false."].Value;
        editor.Required = true;
        editor.Examples[WorkflowExpressionSyntaxes.Literal] = "true";
        editor.Examples[WorkflowExpressionSyntaxes.JavaScript] = "input(\"Count\") > 0";
        editor.Examples[WorkflowExpressionSyntaxes.Liquid] = "{{ Workflow.Properties.Count > 0 }}";
    });
}
@await DisplayAsync(conditionEditor)
```

The same applies to `CorrelateTask`, `ForEachTask`, `ForLoopTask`, `SetOutputTask`, `SetPropertyTask`, `SetVariableTask` and `WhileLoopTask`.

## Example 7: Run a Data Pipeline Every Night

```
TimerEvent (0 1 * * *) → RunDataPipelineTask → [Succeeded] EmailTask (report link)
                                            → [Failed]    NotifyUserTask (administrators)
                                            → [Skipped]   LogTask
```

- Enable `OrchardCore.Workflows`, `OrchardCore.Workflows.Timers`, and `OrchardCore.DataPipelines`.
- On `RunDataPipelineTask`, pick the pipeline, keep **Wait for the run to end** on, and enable **Skip when a run is already queued or running**.
- **Parameters** is a Liquid template with one `name=value` per line, for example `Since={{ Workflow.Input.Since }}`.
- The run (with `Status`, `Deliveries`, `Steps`) is the last result and the `DataPipelineRun` output, so `EmailTask` can reference `{{ Workflow.Output.DataPipelineRun.Status }}`.
- To react to runs started elsewhere, start a workflow with `DataPipelineRunCompletedEvent`. See `orchardcore-data-pipelines`.

## Example 8: Clean Up on Logout

```
UserLoggedOutEvent → ScriptTask (clear per-user state) → LogTask
```

`UserLoggedOutEvent` (Users feature) fires after the user signs out through the account logout endpoint and provides `UserName` and `Roles` as input. For code-only logic, implement `ILogoutFormEvent` (or derive from `LogoutFormEventBase`) instead of a workflow.
