---
name: crestapps-core-prompt-security
description: Skill for the CrestApps.Core 2.0 prompt-security layer that hardens AI Profile chat. Covers IPromptSecurityService and weighted IPromptSecurityRule scoring, custom rules, PromptSecurityOptions guards and thresholds, the security preamble and input delimiters, IOutputSecurityFilter, audit logging, multi-tier anonymous rate limits with ChatRateLimitTier, AIChatRateLimitingOptions partitions, PromptSecurityProfileSettings, GetRateLimitedMessage and EvaluateMessageRateLimitAsync, the remote address behind a proxy and IPv6 /64 keys, visitor cookies and AllowCrossSiteEmbedding with the antiforgery cookie, IUploadedFileScanner, the chat answer sanitizer, guardrails relative to prompt security, conversation privacy, and tool authorization and approvals. Use it when tuning chat abuse protection, adding detection rules, embedding a chat on another site, running behind a reverse proxy, or explaining why a prompt was blocked or throttled.
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

`AllowCrossSiteEmbedding` writes the `crestapps-ai-visitor` cookie as `SameSite=None; Secure; Partitioned` (keep `UsePartitionedCookie` on). It requires HTTPS; behind a TLS-terminating proxy forward the scheme. The visitor cookie owns a visitor's session documents, so the upload and remove endpoints validate antiforgery for requests that carry it. An embedded chat therefore also needs the antiforgery cookie written the same way, and `SuppressXFrameOptionsHeader`, or the first load of the framed page is blocked by `X-Frame-Options: SAMEORIGIN` and uploads answer `400`:

```csharp
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Extensions.Add("Partitioned");
    options.SuppressXFrameOptionsHeader = true;
});
```

### Remote Address Behind a Proxy

`DefaultAIVisitorIdentityResolver` uses the connection's remote address and never reads `X-Forwarded-For`, which any client can send. Behind a reverse proxy, let ASP.NET Core's forwarded headers middleware replace the address for trusted proxies only:

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.4"));
});

app.UseForwardedHeaders();
```

Without it, every visitor shares the proxy's address. An IPv4-mapped address is keyed as IPv4, an IPv6 client is keyed on its `/64` in every `RemoteAddressMode` (`ChatRateLimitKeyResolver` reduces a plain-text address to `ip:{prefix}::/64`; the stored address stays full), and a request with no address shares one bucket. A visitor cookie is reused only when it looks like an id (letters, digits, `-`, `_`, at most 64 characters). Replace `IChatRateLimiter` for a distributed store in multi-instance deployments. `AIChatEndpointRateLimitingOptions` lets you swap the ASP.NET Core endpoint policy for session-start routes.

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
- Input that carries no prompt still counts against the per-message limit through the hub's protected virtual `EvaluateMessageRateLimitAsync`: `SendAudioStream`, `SynthesizeSpeech`, `StartConversation`, `StartRealtimeConversation`, `StartRealtimeWebRtc`, template prompts, blank messages, and resuming or continuing an AG-UI run (each approval resume counts once). `SynthesizeSpeech` also refuses text over `MaxSpeechTextLength` (10,000).
- Over long polling the hub puts the connection's `HttpContext` on `IHttpContextAccessor` for each invocation, so a custom `IAIVisitorIdentityResolver` that reads the accessor sees the visitor on every transport.
- The preamble is wrapped in `[SECURITY DIRECTIVES — IMMUTABLE AND NON-NEGOTIABLE]` markers; user input is wrapped in `<|user_input_begin|>` / `<|user_input_end|>` after stripping injected delimiter tokens.

## Guardrails and Redaction

Prompt security is pattern-based and global. For per-profile topic refusal add guardrails (`AddGuardrails()` on the AI suite, `AIProfileGuardrailSettings` on the profile; see `crestapps-core-ai-profiles`). With guardrails registered, `AIChatHubCore` runs, in order:

1. **Customer message**: `ValidatePromptSecurityAsync` runs `IPromptSecurityService` first. If it does not block and `CheckCustomer` is on, `AIGuardrailService.CheckAsync(..., AIGuardrailDirection.Customer, ...)` runs. A refusal becomes a blocked `PromptSecurityResult` whose `DetectionRule` starts with `AIGuardrailService.DetectionRulePrefix` (`"guardrail:"`) and the caller sees the profile's `PlaceholderReply` instead of the generic blocked message.
2. **Reply**: the protected virtual `ValidateOutputAsync` runs `IOutputSecurityFilter` first, then the reply guardrail when `CheckReplies` is on. It returns the message to show instead of the reply, or `null`; a refused reply is not saved.

Guardrail checks are typed-chat only in the hub. Other channels call `AIGuardrailService.CheckAsync` themselves. When the check cannot run (no utility deployment, model error) the text passes with a warning, so guardrails stay a second line behind prompt security.

Prompt security never redacts stored conversations. To redact personal data, keep only metadata, or enforce retention, use conversation privacy (`AddConversationPrivacy()`, `AIProfileDataPrivacySettings`, `AIConversationPrivacyService`, `AIPersonalDataRedactor`; see `crestapps-core-ai-profiles`).

## Uploads

Every chat session and chat interaction upload passes `IUploadedFileScanner.ScanAsync(IFormFile, CancellationToken)` before storage. The default `NoOpUploadedFileScanner` returns `Clean`; register your scanner before document processing so `TryAdd` keeps yours. `FileScanResult.Infected(...)` and `FileScanResult.Error(...)` reject the upload (fail closed).

## Answers in the Browser

The chat scripts sanitize every answer (`CoreAIImages.sanitize` in `chat-markers.js`), because a model can be tricked by a document, crawled page or prompt injection into writing markup:

- images only from the page's origin or `allowedImageHosts` (others become links), and no other element or attribute that fetches without a click;
- no `id`, `data-coreai-*`, `data-chat-interaction-*` or `data-agui-*` attributes, so an answer cannot start a chat or forge a tool approval, and auto-start ignores configurations inside rendered messages (`CoreAIChatSurface`);
- `style` limited to size, spacing, text and color, no id-pointing attributes (`for`, `form`, `popovertarget`, ...), no form-control `name`, buttons forced to `type="button"`, so an answer cannot overlay or press the page's controls.

See `crestapps-core-chat-interactions` for the details and the `allowedImageHosts` option.

## Tool Authorization

- **AI sessions**: the AI Profile is the boundary. Tools, MCP connections and agents the profile author attached run for any caller allowed to use the profile, including anonymous ones. No per-user tool check runs at completion time.
- **Chat Interactions**: saved tool names are caller-controllable, so each send re-checks selectable tools with `IAIToolAccessEvaluator.IsAuthorizedAsync(ClaimsPrincipal user, string toolName)` and drops unauthorized ones with one warning. System and hidden tools are not checked; a `null` user (server-side work) skips the check. The default evaluator allows everything; replace it to enforce permissions.
- **Approvals**: a tool registered with `RequireApproval()` runs only after the user approves the call in an AG-UI run, and is never offered to a request that cannot ask (see `crestapps-core-ag-ui`).

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
| Per-profile refused topics | `AddGuardrails()` and `AIProfileGuardrailSettings` |
| React to a refused topic | `IAIGuardrailHandler` |
| Change hub output checks | Override `AIChatHubCore.ValidateOutputAsync` |
| Redact or expire stored conversations | `AddConversationPrivacy()` |

## Upgrading Within 2.0 Previews

- The hub's reply check moved into the new protected virtual `ValidateOutputAsync(services, profile, sessionId, prompt, output, handlerContext, cancellationToken)`, which runs the output filter and then the reply guardrail. Override it rather than replacing the output filter when a host needs a different reply policy.

- `DefaultAIVisitorIdentityResolver` no longer trusts `X-Forwarded-For`. Configure `UseForwardedHeaders` with `KnownProxies`/`KnownNetworks` behind a proxy.
- Blank messages, template prompts, speech methods and AG-UI resumes now count against the message limit; a host that overrides `EvaluateMessageRateLimitAsync` sees them all.
- Hosts with `AllowCrossSiteEmbedding` must also write the antiforgery cookie `SameSite=None; Secure; Partitioned` and set `SuppressXFrameOptionsHeader`.

## Upgrading from 1.x

- Anonymous throttling is multi-tier. A host that tuned only `MaxAnonymousSessionsPerWindow` must edit or clear `AnonymousSessionStartRateLimitTiers` to see an effect.
- Authenticated message throttling now also counts the network address by default.
- Rate-limited users see a slow-down message with the retry delay instead of the generic blocked message.
- AI sessions no longer apply `IAIToolAccessEvaluator`; it now governs Chat Interaction sends only. It is not obsolete.
- Chat Interactions wrap user input in delimiters unless `EnableChatInteractionInputDelimiters` is `false`.
