---
name: crestapps-core-resilience
description: Skill for adding retry and resilience middleware to Microsoft.Extensions.AI clients in CrestApps.Core using UseDefaultResilience and UseResilience.
---

# CrestApps.Core AI Resilience - Prompt Templates

## Add Resilience to AI Clients

You are a CrestApps.Core expert. Generate code and guidance for adding builder-based resilience middleware to Microsoft.Extensions.AI clients using the `CrestApps.Core.AI.Resilience` package.

### Guidelines

- `CrestApps.Core.AI.Resilience` is a standalone package. Reference it explicitly in the consuming project.

  ```xml
  <PackageReference Include="CrestApps.Core.AI.Resilience" Version="*" />
  ```

  It depends on `Microsoft.Extensions.AI` and `Microsoft.Extensions.Resilience`.
- The extensions live in namespace `CrestApps.Core.AI.Resilience` and apply to the corresponding Microsoft.Extensions.AI builders: `ChatClientBuilder`, `EmbeddingGeneratorBuilder<TInput, TEmbedding>`, `ImageGeneratorBuilder`, `SpeechToTextClientBuilder`, and `TextToSpeechClientBuilder`.
- Framework-owned completion and utility chat paths in `CrestApps.Core` already use the default retry policy internally. Use this package for host-created clients or when an application wants to opt in explicitly.
- Two ways to apply resilience:
  1. Resolve a chat client through `IAIClientFactory.CreateChatClientAsync(deployment, configurePipeline)`. The factory passes a `ChatClientBuilder` to the callback and owns the final build.
  2. Convert a raw client to its builder with `.AsBuilder()`, apply `UseDefaultResilience()` or `UseResilience(...)`, then finish with `Build(serviceProvider)`.
- When you build manually, always pass the active `IServiceProvider` to `Build(serviceProvider)`. Never call `Build()` or `Build(null)` — downstream middleware may need DI to resolve tools and runtime components.

### Default Policy

`UseDefaultResilience()` is intentionally narrow. It retries provider rate-limit failures such as HTTP `429 Too Many Requests`. Tune it with `AIChatClientRetryOptions`.

| Setting | Default |
|---|---|
| `MaxRateLimitRetries` | `5` |
| `RateLimitRetryDelay` | `1 second` |
| `BackoffType` | `DelayBackoffType.Exponential` |
| `UseJitter` | `true` |
| `MaxRetryDelay` | `32 seconds` |

The default produces an approximate schedule of ~1-2s, ~2-4s, ~4-8s, ~8-16s, ~16-32s across five retries. Actual delays vary because jitter is on.

### Chat Example

Through the factory (recommended when the factory creates the client):

```csharp
var resilientClient = await aiClientFactory.CreateChatClientAsync(
    deployment,
    builder => builder.UseDefaultResilience());
```

From an existing raw `IChatClient`:

```csharp
var resilientClient = chatClient
    .AsBuilder()
    .UseDefaultResilience()
    .Build(serviceProvider);
```

### Customizing the Default Settings

Keep the built-in rate-limit handling but tune the retry shape:

```csharp
var resilientClient = await aiClientFactory.CreateChatClientAsync(
    deployment,
    builder => builder.UseDefaultResilience(options =>
    {
        options.MaxRateLimitRetries = 3;
        options.RateLimitRetryDelay = TimeSpan.FromSeconds(2);
        options.BackoffType = DelayBackoffType.Exponential;
        options.UseJitter = true;
        options.MaxRetryDelay = TimeSpan.FromSeconds(20);
    }));
```

For a fixed (non-exponential) schedule:

```csharp
var resilientClient = chatClient
    .AsBuilder()
    .UseDefaultResilience(options =>
    {
        options.MaxRateLimitRetries = 4;
        options.RateLimitRetryDelay = TimeSpan.FromSeconds(5);
        options.BackoffType = DelayBackoffType.Constant;
        options.UseJitter = false;
        options.MaxRetryDelay = TimeSpan.FromSeconds(5);
    })
    .Build(serviceProvider);
```

### Fully Custom Pipelines

Use `UseResilience(...)` for full control over the Polly pipeline:

```csharp
var resilientClient = chatClient
    .AsBuilder()
    .UseResilience(pipeline => pipeline.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 2,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Exception is HttpRequestException ex &&
            ex.StatusCode == HttpStatusCode.TooManyRequests),
    }))
    .Build(serviceProvider);
```

You can also pass a prebuilt `ResiliencePipeline` to share one pipeline across multiple clients.

### Other Client Types

The same `UseDefaultResilience()` / `UseResilience(...)` extensions are available on the other builders. Factory and raw-builder forms both work.

```csharp
// Embeddings
var embeddings = embeddingGenerator.AsBuilder().UseDefaultResilience().Build(serviceProvider);

// Image generation
var images = imageGenerator.AsBuilder().UseDefaultResilience().Build(serviceProvider);

// Speech to text
var stt = speechToTextClient.AsBuilder().UseDefaultResilience().Build(serviceProvider);

// Text to speech
var tts = textToSpeechClient.AsBuilder().UseDefaultResilience().Build(serviceProvider);
```

### Streaming Notes

- `ITextToSpeechClient` streaming retries are supported when the failure happens before the first streamed update is yielded.
- `ISpeechToTextClient` non-streaming retries work for both seekable and non-seekable streams.
- `ISpeechToTextClient` streaming retries require a seekable input stream so the audio can be replayed safely across retry attempts.

### When to Use Which

- `UseDefaultResilience()` — a safe default for provider throttling, framework-style retries on your own clients, when you do not yet need a custom Polly pipeline.
- `UseResilience(...)` — custom retry predicates, additional strategies, or one shared prebuilt pipeline across multiple clients.
