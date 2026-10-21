---
name: crestapps-core-prompt-security
description: Skill for the CrestApps.Core 2.0 prompt-security layer that hardens AI Profile chat. Covers input validation with IPromptSecurityService and weighted IPromptSecurityRule scoring, custom rules, PromptSecurityOptions guards and thresholds, the security preamble and input delimiters including EnableChatInteractionInputDelimiters, output filtering with IOutputSecurityFilter, audit logging, multi-tier anonymous message and session-start rate limits with ChatRateLimitTier, AIChatRateLimitingOptions partitions keyed by user and network address, PromptSecurityProfileSettings per-profile throttles, the rate-limited slow-down message and GetRateLimitedMessage, visitor identity cookies and AllowCrossSiteEmbedding, IUploadedFileScanner, and how tool authorization relates (the profile boundary and IAIToolAccessEvaluator for Chat Interactions). Use it when tuning chat abuse protection, adding detection rules, embedding a chat on another site, or explaining why a prompt was blocked or throttled.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Prompt Security

A local, deterministic, regex-based layer for **AI Profile** chat: input validation, a security preamble and input delimiters, output filtering, rate limiting and audit logging. It is defense in depth, never a replacement for tool authorization.

## Guidelines

- `AddAISuite(...)` (through `AddCoreAIOrchestration()`) registers the layer: 26 built-in rules, the scoring engine, `IPromptSecurityService`, `IOutputSecurityFilter`, `IAIChatSecurityAuditService`, `IChatRateLimiter`, `IChatSessionStartRateLimiter` and `SecurityPromptOrchestrationHandler`. There is no `AddPromptSecurity(...)` method.
- `PromptSecurityOptions` is not bound from configuration by the framework. Bind it from your own section or site settings.
- Profiles can only change anti-spam throttles (`PromptSecurityProfileSettings`). Injection detection, output filtering, preamble, delimiters, length and blocking threshold are global.
- Chat Interactions get input delimiters (`EnableChatInteractionInputDelimiters`, on by default) but not the preamble or injection blocking; the operator controls their prompt, model and tools.

## Configure Site Policy

```csharp
using CrestApps.Core.AI.Security;

builder.Services.Configure<PromptSecurityOptions>(builder.Configuration.GetSection("CrestApps:AI:PromptSecurity"));

builder.Services.PostConfigure<PromptSecurityOptions>(options =>
{
    options.MaxPromptLength = 6000;
    options.BlockingThreshold = PromptRiskLevel.High;
    options.CustomBlockedPatterns.Add(@"\binternal-canary-123\b");
    options.AnonymousMessageRateLimitTiers =
    [
        new ChatRateLimitTier { Limit = 5, Window = TimeSpan.FromSeconds(30) },
        new ChatRateLimitTier { Limit = 30, Window = TimeSpan.FromMinutes(5) },
        new ChatRateLimitTier { Limit = 150, Window = TimeSpan.FromHours(1) },
    ];
});
```

| Option | Default |
|---|---|
| `EnableInjectionDetection`, `EnableOutputFiltering`, `EnableSecurityPreamble`, `EnableInputDelimiters`, `EnableChatInteractionInputDelimiters`, `EnableAuditLogging` | `true` |
| `MaxPromptLength` | `8000` |
| `BlockingThreshold` | `High` |
| `LowRiskScoreThreshold` / `Medium...` / `High...` / `Critical...` | `10` / `20` / `35` / `50` |
| `AnonymousMessageRateLimitTiers` | 5 per 30s, 30 per 5m, 150 per hour, 500 per day |
| `AnonymousSessionStartRateLimitTiers` | 5 per 30s, 10 per 5m, 150 per hour, 500 per day |
| `MaxMessagesPerWindow` / `RateLimitWindow` | `20` per 1 minute (authenticated callers; anonymous fallback when tiers are empty) |
| `MaxAnonymousSessionsPerWindow` / `AnonymousSessionRateLimitWindow` | `20` per 10 minutes (fallback when session-start tiers are empty) |

The single-window values are fallbacks: while a tier list is populated, changing only `MaxAnonymousSessionsPerWindow` has no effect. Clear the tiers or lower a tier instead. Tiers apply to anonymous traffic only; authenticated callers use `MaxMessagesPerWindow` and never hit session-start throttling.

In JSON a tier is `{ "Limit": 5, "Window": "00:00:30" }`.

## Rate-Limit Partitions

`AIChatRateLimitingOptions` decides which keys a throttle counts against. Each value is a `ChatRateLimitPartition` flags combination:

| Option | Default |
|---|---|
| `AuthenticatedMessagePartitions` | `AuthenticatedUser` and `NetworkAddress` |
| `AnonymousMessagePartitions` | `Visitor`, `NetworkAddress`, `Session` and `Connection` |
| `AnonymousSessionStartPartitions` | `Visitor` and `NetworkAddress` |

The network-address bucket is shared with anonymous throttling, so logging out does not reset a caller's per-IP allowance. Remove `NetworkAddress` from `AuthenticatedMessagePartitions` if authenticated users behind one NAT must not share a bucket.

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddChatInteractions(chat => chat
            .ConfigureVisitorIdentity(options =>
            {
                options.RemoteAddressMode = AIVisitorRemoteAddressMode.Hashed;
                options.AllowCrossSiteEmbedding = true; // only for chats framed on other sites
            })
            .ConfigureChatRateLimiting(options =>
            {
                options.AuthenticatedMessagePartitions = ChatRateLimitPartition.AuthenticatedUser;
            }))));
```

`AllowCrossSiteEmbedding` writes the `crestapps-ai-visitor` cookie as `SameSite=None; Secure; Partitioned` (keep `UsePartitionedCookie` on). It requires HTTPS; behind a TLS-terminating proxy forward the scheme. Replace `IChatRateLimiter` for a distributed store in multi-instance deployments. `AIChatEndpointRateLimitingOptions` lets you swap the ASP.NET Core endpoint policy for session-start routes.

## Per-Profile Throttles

```csharp
profile.WithSettings(new PromptSecurityProfileSettings
{
    MaxMessagesPerWindow = 10,
    RateLimitWindow = TimeSpan.FromMinutes(2),
    AnonymousMessageRateLimitTiers = null,  // inherit the site tiers
    AnonymousSessionStartRateLimitTiers = [], // opt out, use the single window
    MaxAnonymousSessionsPerWindow = 3,
});
```

Every field is nullable and `null` inherits the site value. `0` disables that throttle for the profile.

## Add a Custom Rule

```csharp
using System.Text.RegularExpressions;
using CrestApps.Core.AI.Security;

public sealed partial class CanaryPromptSecurityRule : IPromptSecurityRule
{
    public string RuleId => "canary-token";

    public ValueTask<PromptSecurityRuleResult> EvaluateAsync(
        PromptSecurityEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.NormalizedInput) || !CanaryRegex().IsMatch(context.NormalizedInput))
        {
            return ValueTask.FromResult<PromptSecurityRuleResult>(null);
        }

        return ValueTask.FromResult(new PromptSecurityRuleResult
        {
            RuleId = RuleId,
            Categories = ["data-exfiltration"],
            Severity = PromptRiskLevel.Critical,
            Score = 50,
            Reason = "Detected the internal canary token.",
        });
    }

    [GeneratedRegex(@"\binternal-canary-123\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex CanaryRegex();
}
```

```csharp
builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPromptSecurityRule, CanaryPromptSecurityRule>());
```

Evaluate `NormalizedInput` (Unicode FormKC, invisible characters removed, whitespace collapsed) and fall back to `FoldedInput` (homoglyphs folded) only for lookalike attacks. Scores 8 to 12 contribute to an aggregate; 20 or more can block alone. Always set a regex timeout. Return `null` for no match.

## Input and Output Enforcement

- `IPromptSecurityService.ValidateInputAsync(PromptSecurityContext, CancellationToken)` returns a `PromptSecurityResult` with `Disposition` (`Safe`, `Flagged`, `Blocked`), `RiskLevel`, `Score`, `DetectionRule`, `MatchedRuleIds`, `MatchedRules`, `MatchedCategories` and `Telemetry`.
- `AIChatHubCore` validates Utility and Chat profile prompts before processing. A rate-limit block (`DetectionRule == PromptSecurityResult.RateLimitDetectionRule`, `"rate-limit"`) returns a slow-down message with the retry delay; override `GetRateLimitedMessage(PromptSecurityResult)` on the hub to change it. Other blocks return the generic safe message so detection details are not disclosed.
- `IOutputSecurityFilter.ValidateOutputAsync(OutputSecurityContext, CancellationToken)` checks system-prompt leakage, tool-schema disclosure, sensitive data and unsafe script content. The hub validates the **complete** response after streaming; a blocked response is not persisted and a safe replacement is sent.
- `EnableAuditLogging` records suspicious input and output through `IAIChatSecurityAuditService`.
- The preamble is wrapped in `[SECURITY DIRECTIVES — IMMUTABLE AND NON-NEGOTIABLE]` markers; user input is wrapped in `<|user_input_begin|>` / `<|user_input_end|>` after stripping injected delimiter tokens.

## Uploads

Every chat session and chat interaction upload passes `IUploadedFileScanner.ScanAsync(IFormFile, CancellationToken)` before storage. The default `NoOpUploadedFileScanner` returns `Clean`; register your scanner before document processing so `TryAdd` keeps yours. `FileScanResult.Infected(...)` and `FileScanResult.Error(...)` reject the upload (fail closed).

## Tool Authorization

- **AI sessions**: the AI Profile is the boundary. Tools, MCP connections and agents the profile author attached run for any caller allowed to use the profile, including anonymous ones. No per-user tool check runs at completion time.
- **Chat Interactions**: saved tool names are caller-controllable, so each send re-checks selectable tools with `IAIToolAccessEvaluator.IsAuthorizedAsync(ClaimsPrincipal user, string toolName)` and drops unauthorized ones with one warning. System and hidden tools are not checked; a `null` user (server-side work) skips the check. The default evaluator allows everything; replace it to enforce permissions.

## Choose the Right Extension Point

| Need | Use |
|---|---|
| Detect a prompt pattern | `IPromptSecurityRule` |
| Validate input directly | `IPromptSecurityService` |
| Output policy | `IOutputSecurityFilter` |
| Limits and thresholds | `PromptSecurityOptions` |
| Throttle keys | `AIChatRateLimitingOptions` |
| Distributed throttling | Replace `IChatRateLimiter` |
| Per-user tools in Chat Interactions | Replace `IAIToolAccessEvaluator` |

## Upgrading from 1.x

- Anonymous throttling is multi-tier. A host that tuned only `MaxAnonymousSessionsPerWindow` must edit or clear `AnonymousSessionStartRateLimitTiers` to see an effect.
- Authenticated message throttling now also counts the network address by default.
- Rate-limited users see a slow-down message with the retry delay instead of the generic blocked message.
- AI sessions no longer apply `IAIToolAccessEvaluator`; it now governs Chat Interaction sends only. It is not obsolete.
- Chat Interactions wrap user input in delimiters unless `EnableChatInteractionInputDelimiters` is `false`.
