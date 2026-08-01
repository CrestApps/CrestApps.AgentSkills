---
name: orchardcore-rules
description: Skill for building and evaluating Orchard Core rules. Covers ICondition, IConditionEvaluator, ConditionOptions, rule registration, custom condition display drivers, condition operators, rule serialization, and Layers integration while keeping Rules distinct from Scripting. Use this skill when requests mention Orchard Core Rules, IConditionEvaluator, ConditionEvaluator, custom rule condition, rule builders, condition groups, Layers rules, or closely related Orchard Core implementation setup extension or troubleshooting work. Strong matches include work with OrchardCore.Rules, OrchardCore.Rules.Core, OrchardCore.Layers, Condition, ConditionGroup, Rule, IConditionFactory, IConditionResolver, IConditionOperatorResolver, AddRule, and AddRuleCondition. It also helps with built-in conditions, display drivers, rule evaluation, and the source-backed patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Rules

`OrchardCore.Rules` is the structured condition system used by features such as
Layers. A rule is a model of conditions evaluated against the current request
and context. It is different from `OrchardCore.Scripting`: scripting can power
a condition, but Rules provides condition models, editors, registration,
serialization, and composable all/any groups.

## Guidelines

- Enable the exact `OrchardCore.Rules` feature; it depends on `OrchardCore.Scripting`.
- Use `Condition` as the base type for a custom condition.
- Implement `IConditionEvaluator` with `ValueTask<bool> EvaluateAsync(Condition condition)`.
- Register standard conditions through `AddRuleCondition`; do not use obsolete `AddCondition`.
- Use `AddRule` when a condition has a display driver and the default condition factory is sufficient.
- Register polymorphic JSON information through the provided registration extensions.
- Keep evaluators scoped because a rule can require request-scoped services or a scripting engine.
- Use a display driver to expose a condition in the rule builder UI.
- Do not treat a JavaScript condition as a substitute for structured authorization logic.
- All C# classes in examples are sealed except View Models.

## Enable Rules

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Rules"
      ],
      "disable": []
    }
  ]
}
```

The module registers all, any, boolean, home page, URL, culture, role,
JavaScript, authenticated, anonymous, and content-type conditions. It also
registers string equality, prefix, suffix, and contains operators.

## Core Types

| Type | Purpose |
|---|---|
| `Rule` | Serializable collection of conditions evaluated as a rule. |
| `Condition` | Base model with `Name` and `ConditionId`. |
| `ConditionGroup` | Base for nested groups such as all and any. |
| `IConditionEvaluator` | Evaluates a condition instance. |
| `IConditionFactory` | Creates a model for a builder condition. |
| `IConditionResolver` | Resolves evaluators for a condition. |
| `IConditionOperatorResolver` | Resolves configured string operators. |

## Create a Custom Condition

Keep only serializable values in the condition model:

```csharp
using OrchardCore.Rules;

namespace MyModule.Rules;

public sealed class RequestHeaderCondition : Condition
{
    public string HeaderName { get; set; }
    public string ExpectedValue { get; set; }
}
```

The evaluator validates its expected condition type before using it:

```csharp
using Microsoft.AspNetCore.Http;
using OrchardCore.Rules;

namespace MyModule.Rules;

public sealed class RequestHeaderConditionEvaluator : IConditionEvaluator
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestHeaderConditionEvaluator(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ValueTask<bool> EvaluateAsync(Condition condition)
    {
        if (condition is not RequestHeaderCondition headerCondition)
        {
            return ValueTask.FromResult(false);
        }

        var headers = _httpContextAccessor.HttpContext?.Request.Headers;
        var actual = headers?[headerCondition.HeaderName].ToString();

        return ValueTask.FromResult(string.Equals(
            actual,
            headerCondition.ExpectedValue,
            StringComparison.OrdinalIgnoreCase));
    }
}
```

## Register a Builder Condition

`AddRuleCondition` adds the condition to `ConditionOptions`, registers the
scoped evaluator and default singleton factory, and registers derived JSON type
metadata. Supply a third generic type argument only when a custom
`IConditionFactory` is needed:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Rules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRuleCondition<
            RequestHeaderCondition,
            RequestHeaderConditionEvaluator>();
    }
}
```

When the condition needs editor UI, register its `DisplayDriver<Condition>` in
the same module. Keep view models unsealed because they bind form values.

## Use Rules with Layers

Layers stores a rule to decide whether a layer is active for a request. Prefer
the built-in URL, culture, role, and authentication conditions for common
targeting. Add a custom rule condition only when the selection is reusable and
needs the builder UI; simple one-off dynamic values can instead be handled by
the supported scripting condition.
