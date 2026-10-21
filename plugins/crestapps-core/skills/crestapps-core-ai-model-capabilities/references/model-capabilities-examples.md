# Model Capabilities Examples

## Add a Custom Parameter End to End

This example adds a `verbosity` parameter that only reasoning models expose, binds it onto the request, declares it on a deployment, and stores a selected value on a profile.

### 1. Register the definition and the binder

```csharp
using CrestApps.Core.AI;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Localization;

builder.Services.AddAIDeploymentParameter(
    VerbosityParameter.Name,
    new LocalizedString(VerbosityParameter.Name, "Verbosity"),
    parameter =>
    {
        parameter.Description = new LocalizedString(VerbosityParameter.Name, "How much detail the model writes.");
        parameter.Kind = AIDeploymentParameterKind.Choice;
        parameter.RequiredFeature = AIDeploymentFeatureNames.Reasoning;
        parameter.DefaultValue = "medium";
        parameter.Order = 20;
        parameter.AllowedValues =
        [
            new AIDeploymentParameterOption { Value = "low", DisplayName = new LocalizedString("low", "Low") },
            new AIDeploymentParameterOption { Value = "medium", DisplayName = new LocalizedString("medium", "Medium") },
            new AIDeploymentParameterOption { Value = "high", DisplayName = new LocalizedString("high", "High") },
        ];
    });

builder.Services.AddScoped<IAIDeploymentParameterBinder, VerbosityModelParameterBinder>();
```

```csharp
public static class VerbosityParameter
{
    public const string Name = "verbosity";
}
```

### 2. Implement the binder

`IAIDeploymentParameterBinder.BindAsync` returns `Task`. The context gives the resolved `Descriptor`, the validated `Value`, the `ChatOptions` being built, the `CompletionContext` and the `Deployment`.

```csharp
using CrestApps.Core.AI.Capabilities;
using Microsoft.Extensions.AI;

public sealed class VerbosityModelParameterBinder : IAIDeploymentParameterBinder
{
    public string ParameterName => VerbosityParameter.Name;

    public Task BindAsync(AIDeploymentParameterBindingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.Value))
        {
            return Task.CompletedTask;
        }

        context.ChatOptions.AdditionalProperties ??= [];
        context.ChatOptions.AdditionalProperties["verbosity"] = context.Value;

        return Task.CompletedTask;
    }
}
```

Without a binder the applier writes the value to `ChatOptions.AdditionalProperties` under the parameter name anyway. Write a binder when the provider expects a different key or a typed `ChatOptions` member. The Azure OpenAI completion client cannot forward binder-less custom parameters and logs a warning instead.

### 3. Declare it on a deployment

```json
{
  "CrestApps": {
    "AI": {
      "Deployments": [
        {
          "ClientName": "OpenAI",
          "Name": "gpt-5-chat",
          "ModelName": "gpt-5",
          "ConnectionName": "openai",
          "Features": [ "textGeneration", "toolCalling", "streaming", "reasoning" ],
          "Parameters": {
            "reasoningEffort": { "DefaultValue": "Medium" },
            "verbosity": { "AllowedValues": [ "low", "medium" ], "DefaultValue": "low" }
          }
        }
      ]
    }
  }
}
```

Because `verbosity` requires `reasoning`, removing `reasoning` from `Features` silently removes the parameter from the deployment's effective capabilities.

### 4. Store a selection on a profile

```csharp
using CrestApps.Core.AI.Models;

profile.Put(new AIDeploymentParametersMetadata
{
    Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [AIDeploymentParameterNames.ReasoningEffort] = "High",
        [VerbosityParameter.Name] = "medium",
    },
    UtilityValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [AIDeploymentParameterNames.ReasoningEffort] = "Low",
    },
});

await profileManager.UpdateAsync(profile);
```

The same metadata works on `AIProfileTemplate` and `ChatInteraction`. A value the chat deployment does not allow (for example `high` above) falls back to the deployment default and is logged.

## Add a Custom Feature

```csharp
builder.Services.AddAIDeploymentFeature(
    "citations",
    new LocalizedString("citations", "Native citations"),
    feature =>
    {
        feature.Description = new LocalizedString("citations", "The model returns source citations natively.");
        feature.Order = 200;
        feature.EnabledByDefault = false;
    });
```

Check it at the call site with `(await capabilityService.GetCapabilitiesAsync(name)).SupportsFeature("citations")`. The built-in enforcement only knows the built-in features; enforce a custom feature in your own handler or client middleware.

## Build a Picker from a Slot

```csharp
var deployments = await deploymentManager.GetAllBySlotAsync(AIDeploymentSlotNames.Embedding);

var options = deployments
    .Select(deployment => new { deployment.Name, deployment.ModelName, deployment.ClientName })
    .ToList();
```

## Validate Before Save

Editors reject a realtime-only deployment in a text slot and a non-realtime deployment in a realtime slot:

```csharp
var chatDeployment = await deploymentManager.FindByNameAsync(model.ChatDeploymentName);

if (chatDeployment is not null &&
    !capabilityService.SupportsFeatureOrUnconstrained(chatDeployment, AIDeploymentFeatureNames.TextGeneration))
{
    modelState.AddModelError(nameof(model.ChatDeploymentName), "The selected deployment cannot hold a text conversation.");
}
```
