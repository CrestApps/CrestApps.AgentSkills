---
name: crestapps-core-prompt-security
description: Skill for configuring CrestApps.Core prompt security rules, input validation, and output filtering.
---

# CrestApps.Core Prompt Security - Prompt Templates

## Harden AI Profile Chat

You are a CrestApps.Core expert. Generate code and guidance for the built-in prompt-security layer used by AI Profile chat.

### Guidelines

- `AddAISuite(...)` registers the orchestration layer, including prompt-security services. Do not invent a public `AddPromptSecurity(...)` registration method.
- `IPromptSecurityService.ValidateInputAsync(PromptSecurityContext, CancellationToken)` evaluates user input. It returns `PromptSecurityResult`, which can be safe, flagged, or blocked.
- Implement `IPromptSecurityRule` for organization-specific detection. A rule evaluates a normalized `PromptSecurityEvaluationContext` and returns `null` when it does not match.
- Configure global guards through `PromptSecurityOptions`. Profile settings can alter anti-spam throttles, but not the global injection, output, preamble, delimiter, length, or blocking controls.
- Keep tool authorization independent of prompt detection. Prompt security is defense in depth, not an authorization replacement.

### Configure Site Policy

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai.AddOpenAI()));

builder.Services.Configure<PromptSecurityOptions>(options =>
{
    options.MaxPromptLength = 6000;
    options.BlockingThreshold = PromptRiskLevel.High;
    options.MaxMessagesPerWindow = 12;
    options.RateLimitWindow = TimeSpan.FromMinutes(2);
    options.CustomBlockedPatterns.Add(@"\binternal-canary-123\b");
});
```

`PromptSecurityOptions` enables injection detection, output filtering, a security preamble, input delimiters, and audit logging by default. It also provides aggregate-score thresholds and anonymous-session rate limits.

### Add a Custom Rule

```csharp
using CrestApps.Core.AI.Security;

public sealed class CanaryPromptSecurityRule : IPromptSecurityRule
{
    public string RuleId => "canary-token";

    public ValueTask<PromptSecurityRuleResult> EvaluateAsync(
        PromptSecurityEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.NormalizedInput.Contains(
            "internal-canary-123",
            StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(new PromptSecurityRuleResult
            {
                RuleId = RuleId,
                Categories = ["data-exfiltration"],
                Severity = PromptRiskLevel.Critical,
                Score = 50,
                Reason = "Detected the internal canary token.",
            });
        }

        return ValueTask.FromResult<PromptSecurityRuleResult>(null);
    }
}
```

Register the rule alongside the built-ins:

```csharp
builder.Services.AddSingleton<IPromptSecurityRule, CanaryPromptSecurityRule>();
```

### Input and Output Enforcement

- `AIChatHubCore` calls `ValidateInputAsync` before processing Utility and Chat profile prompts. A blocked result sends the standard safe error and skips processing.
- `IOutputSecurityFilter.ValidateOutputAsync(OutputSecurityContext, CancellationToken)` is implemented by `DefaultOutputSecurityFilter`. It checks system-prompt leakage, tool-schema disclosure, sensitive data patterns, and unsafe script content.
- The current chat hub validates the **complete** response after it streams chunks. A blocked result is not persisted and a safe replacement message is sent. Do not describe this as pre-stream output prevention.
- `EnableAuditLogging` records suspicious input and output events through `IAIChatSecurityAuditService`.

### Choose the Right Extension Point

| Need | Use |
|---|---|
| Detect a prompt pattern | `IPromptSecurityRule` |
| Validate user input directly | `IPromptSecurityService` |
| Apply an output-specific policy | `IOutputSecurityFilter` |
| Change limits and thresholds | `Configure<PromptSecurityOptions>` |

