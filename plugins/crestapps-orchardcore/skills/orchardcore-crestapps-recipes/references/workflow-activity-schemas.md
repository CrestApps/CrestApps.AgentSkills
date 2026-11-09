# Workflow Activity Schemas (`WorkflowType`)

The `WorkflowType` step imports workflow definitions. Each activity keeps its configuration in a free-form `Properties` bag whose shape depends on the activity. CrestApps Recipes composes one `Properties` schema per registered activity, so editors and AI agents get the property list, types, syntax hints, required members and outcomes as soon as the activity `Name` is typed.

Registered when `OrchardCore.Workflows` is enabled (`IWorkflowActivitySchemaService`, `WorkflowTypeRecipeStep`). Built-in definitions are added per owning feature: core control-flow tasks, `OrchardCore.Workflows.Http`, `OrchardCore.Workflows.Timers`, content events and tasks (`OrchardCore.Contents`), `EmailTask` (`OrchardCore.Email`), `SmsTask` (`OrchardCore.Sms`), forms, notifications, reCaptcha, roles, users, tenants, Twitter, Data Pipelines (`RunDataPipelineTask`, `DataPipelineRunCompletedEvent`), Facebook Pixel (`MetaConversionsApiEventTask`), the Orchard Core 4.0 additions (`ExecuteWorkflowTask`, `StartedByWorkflowEvent`, `SetVariableTask`, `UserLoggedOutEvent`), the CrestApps AI events and tasks (`CrestApps.OrchardCore.AI`) and the Contact Center events and tasks (`CrestApps.OrchardCore.ContactCenter`). Other CrestApps modules register their own activity schemas from their startups: Calendars, Scheduling, Bookings, Skills, Workforce, Work Orders, Dispatch, Orders, Shipping, Stripe, Inventory, Subscriptions, Report Builder, Wizard and Omnichannel. A guard test fails the CrestApps build when an activity has no schema.

## What the schema describes

- Category, description and outcomes, written into the `Properties` JSON Schema `description`. Outcome names are what `Transitions[].SourceOutcomeName` uses. Activities with dynamic outcomes (`ForkTask`, `ScriptTask`, `UserTaskEvent`) say so.
- Every persisted property with its type, description, Liquid or JavaScript syntax hint and requirement.
- Structural rules: a start activity must be an event, every activity needs an `ActivityId` (never generated), each transition names a source activity, source outcome and destination activity. Non-start activities can be tasks or events.
- Display text and the dynamic-outcome flag are on `WorkflowActivityDescriptor` from `IWorkflowActivitySchemaService.GetActivityDescriptorsAsync`, not in the schema.
- `Properties` stays open because `ActivityRecord` is an `Entity` that section drivers can extend. `WorkflowType` itself may also carry `Properties`.
- Activities registered with `AddActivity<TActivity, TDriver>()` but without a definition appear in the `Name` list with unconstrained `Properties`.

## Expressions, enums and `ActivityMetadata`

`WorkflowExpression<T>` properties serialize as an object with an optional, nullable `Expression` string and an optional `Syntax` (`Literal`, `Liquid`, `JavaScript` or another registered syntax); a bare string is rejected. `{ "Sender": {} }` and `{ "Sender": { "Expression": null } }` are both valid. Only the activities that let each expression pick its syntax read `Syntax` (`IfElseTask`, `WhileLoopTask`, `ForEachTask`, `ForLoopTask`, `SetPropertyTask`, `SetOutputTask`, `SetVariableTask`, `CorrelateTask`, `ExecuteWorkflowTask`); on those, the older `Liquid*` properties and activity-level `Syntax` are legacy. Some CrestApps tasks take plain Liquid strings rather than expression objects; follow each activity's schema. Enum properties accept the member name or its ordinal (`"BodyFormat": "Html"` or `"BodyFormat": 2`).

Every activity may carry `ActivityMetadata` with an optional, nullable `Title`, and `OutputBindings`, an object that maps output names (such as `Result`) to declared workflow variables. Every task may also carry `ActivityRetryPolicy` (`MaxRetries` 0–10, `DelaySeconds`, `Backoff`, `OnFailure`). The base class adds these; never declare them yourself.

The workflow type itself carries `Variables` (`Name`, `TypeName`, `DefaultValue`, `Description`, `IsInput`, `IsOutput`), `IsActivity` (the workflow can be started from another workflow), `IsSingletonPerCorrelation`, `BranchingMode` (`FirstOnly` or `All`), `FaultOnScriptErrors` and `RecordActivityData`. Importing an existing `WorkflowTypeId` publishes it as the next version and discards its draft; `VersionId` is ignored on import. See the `orchardcore-workflows` skill for what each one does.

```json
{
  "steps": [
    {
      "name": "WorkflowType",
      "data": [
        {
          "WorkflowTypeId": "notify-on-publish",
          "Name": "Notify on publish",
          "IsEnabled": true,
          "IsSingleton": false,
          "DeleteFinishedWorkflows": false,
          "Activities": [
            {
              "ActivityId": "1",
              "Name": "ContentPublishedEvent",
              "IsStart": true,
              "X": 100,
              "Y": 100,
              "Properties": {
                "ActivityMetadata": { "Title": "When an article is published" },
                "ContentTypeFilter": [ "Article" ]
              }
            },
            {
              "ActivityId": "2",
              "Name": "EmailTask",
              "IsStart": false,
              "X": 400,
              "Y": 100,
              "Properties": {
                "Recipients": { "Expression": "editors@example.com" },
                "Subject": { "Expression": "{{ Workflow.Input.ContentItem.DisplayText }} was published" },
                "BodyFormat": "Html",
                "HtmlBody": { "Expression": "<p>The article is now live.</p>" }
              }
            }
          ],
          "Transitions": [
            {
              "SourceActivityId": "1",
              "SourceOutcomeName": "Done",
              "DestinationActivityId": "2"
            }
          ]
        }
      ]
    }
  ]
}
```

## Describe a custom event or task

Derive from `WorkflowActivitySchemaDefinitionBase` (namespace `CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows`). `GetPropertyDefinitions` receives a `WorkflowActivitySchemaContext` (`ActivityName`, `IsEvent`, `IsTask`, `Category`, `DisplayText`, `Examples`).

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace MyCompany.Workflows.Schemas;

public sealed class SendWebhookTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name { get; } = "SendWebhookTask";

    protected override string Category => "HTTP";

    protected override string DisplayText => "Send Webhook Task";

    protected override string Description => "Posts the workflow payload to an external endpoint.";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["Url"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Url", WorkflowActivitySchemaBuilders.LiquidExpression("The absolute URL the payload is posted to."));
        yield return ("Payload", WorkflowActivitySchemaBuilders.LiquidExpression("The JSON body sent to the endpoint."));
        yield return ("ContentType", WorkflowActivitySchemaBuilders.String("The content type that triggered the call.", context.Examples.ContentTypeNames));
        yield return ("TimeoutSeconds", WorkflowActivitySchemaBuilders.Integer("Seconds to wait for a response. Defaults to 30."));
        yield return ("Retry", WorkflowActivitySchemaBuilders.Boolean("Whether a failed request is retried once."));
    }
}
```

```csharp
services.AddWorkflowActivitySchema<SendWebhookTaskSchema>();
```

Register it behind `[RequireFeatures("OrchardCore.Workflows", "CrestApps.OrchardCore.Recipes")]` plus your activity's feature.

### Property builders (`WorkflowActivitySchemaBuilders`)

| Builder | Use for |
|---|---|
| `Expression(description)` | A `WorkflowExpression<T>` whose syntax another property selects |
| `LiquidExpression(description)` | A `WorkflowExpression<T>` evaluated as Liquid |
| `ScriptExpression(description)` | A `WorkflowExpression<T>` evaluated as JavaScript |
| `String(description)` / `String(description, examples)` | A string, optionally with suggestions |
| `StringEnum(description, values)` | A string from a closed set |
| `EnumValue(description, names)` | A .NET enum, by name or ordinal |
| `Boolean`, `Integer`, `Number` | Scalars |
| `StringArray(description)` / `StringArray(description, itemExamples)` | String arrays, optionally with suggestions |
| `StringEnumArray(description, values)` | An array from a closed set |
| `Any(description)` | Any JSON value |
| `ActivityMetadata()` | Reserved for the base class |

### Rules

- Describe everything the activity writes and reads, including inherited members and runtime state (prefix runtime state descriptions with `Runtime state.`).
- Omit `[Obsolete]` and never-read members (for example `EmailTask.Body`, `EmailTask.IsHtmlBody`, `UpdateContentTask.ContentItemIdExpression`); the open bag still accepts them.
- Use outcome names from the invariant `S["..."]` key, not translated labels.
- Set `HasDynamicOutcomes` when outcomes come from the activity's configuration.
- Mark a property required only when the activity cannot run without it. Express conditional requirements in `BuildActivitySchemaCore`.
- Override `AllowAdditionalProperties` to `false` only when no section driver can target the activity.
- Use examples, not `enum`, for dynamic values such as content types, roles or providers.

### Conditional requirements and asynchronous work

Override `BuildActivitySchemaCore(context)` for object-level constraints. Call the base first; it already contains `ActivityMetadata`. The built-in `ForEachTaskSchema` requires `LiquidEnumerable` when `Syntax` is `Liquid` and `Enumerable` otherwise:

```csharp
protected override WorkflowActivitySchema BuildActivitySchemaCore(WorkflowActivitySchemaContext context)
{
    var schema = base.BuildActivitySchemaCore(context);

    schema.Properties = schema.Properties
        .AllOf(new JsonSchemaBuilder()
            .If(new JsonSchemaBuilder()
                .Properties(("Syntax", new JsonSchemaBuilder().Const("Liquid")))
                .Required("Syntax"))
            .Then(new JsonSchemaBuilder().Required("LiquidEnumerable"))
            .Else(new JsonSchemaBuilder().Required("Enumerable")));

    return schema;
}
```

Override `BuildActivitySchemaAsync(context, cancellationToken)` when the schema needs awaited data; its default wraps `BuildActivitySchemaCore`. Definitions are resolved from DI, so they can take services through the constructor. Neither seam caches; build fresh each call.
