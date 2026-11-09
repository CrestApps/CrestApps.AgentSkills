# AI Profile Feature Examples

Examples for the opt-in profile features added in CrestApps.Core 2.0: speaking styles, guardrails, conversation privacy, profile versions, speech and language settings, and typed data-extraction fields. Every setting is stored with `profile.AlterSettings<T>(...)` or `profile.WithSettings(...)` and read with `profile.TryGetSettings<T>(out var settings)`, so a profile saved before the feature existed behaves as it did.

## Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddSpeakingStyles()                    // CrestApps.Core.AI
        .AddGuardrails()                        // CrestApps.Core.AI
        .AddConversationPrivacy(options =>      // CrestApps.Core.AI.Chat
        {
            options.SettleTime = TimeSpan.FromMinutes(15); // default
            options.BatchSize = 50;                        // default
        })
        .AddProfileVersions(versions => versions
            .AddYesSqlStores())                 // or .AddEntityCoreStores()
    ));
```

The `IServiceCollection` equivalents are `AddCoreAISpeakingStyles()`, `AddCoreAIGuardrails()`, `AddCoreAIConversationPrivacy()` and `AddCoreAIProfileVersions()` plus `AddCoreAIProfileVersionStoresYesSql()` or `AddCoreAIProfileVersionStoresEntityCore()`. Speech, language and typed extraction need no registration; `AddCoreAIServices()` registers `AIProfileLanguageCompletionContextBuilderHandler` and `IConversationLanguageService`.

## Speaking Styles

```csharp
using CrestApps.Core.AI.Models;

profile.AlterSettings<AIProfileSpeakingStyleSettings>(settings =>
    settings.Styles = [AISpeakingStyles.AIDisclosure, AISpeakingStyles.EchoVerification]);
```

- A style is a System Prompt template in the `Speaking style` category (`AISpeakingStyles.Category`). `AISpeakingStyleCompletionContextBuilderHandler` renders each one in order and appends it to the system message after the persona and the profile's own instructions.
- Built-in ids (`AISpeakingStyles.BuiltIn`): `speaking-style-ai-disclosure`, `-scope-limits`, `-high-empathy`, `-echo-verification`, `-nato-spelling`, `-speech-normalization`, `-fuzzy-name-matching`, `-filler-words`.
- A missing or failing template is logged and skipped.

## Guardrails

```csharp
using CrestApps.Core.AI.Models;

profile.AlterSettings<AIProfileGuardrailSettings>(settings =>
{
    settings.CheckCustomer = true;
    settings.CheckReplies = true;
    settings.Topics = [AIGuardrailTopics.Violence, AIGuardrailTopics.RegulatedAdvice];
    settings.OtherTopics = "Prices of our competitors.";
    settings.PlaceholderReply = "I can only help with your order.";
});
```

- A topic is a System Prompt template in the `Guardrail topic` category (`AIGuardrailTopics.Category`). Built-in ids: `guardrail-topic-harassment`, `-hate`, `-self-harm`, `-sexual`, `-violence`, `-illegal-activity`, `-regulated-advice`, `-jailbreak`.
- `AIGuardrailService.CheckAsync(profile, text, AIGuardrailDirection.Customer or Reply, sessionId, cancellationToken)` asks the profile's utility deployment and returns an `AIGuardrailVerdict` (`Direction`, `Category`, `Topic`, `Reason`, `Text`, `Reply`) or `null`. A topic the profile did not refuse is allowed. When the check cannot run, the text goes on and a warning is logged. Requests are metered with `AIUsagePurposes.Guardrail`.
- With `AddGuardrails()`, `AIChatHubCore` checks typed messages after prompt security and replies after `IOutputSecurityFilter` with no host code. Other channels call `CheckAsync` themselves and show `AIGuardrailService.GetPlaceholderReply(profile)` (falls back to `AIGuardrailService.DefaultPlaceholderReply`).

```csharp
using CrestApps.Core.AI.Guardrails;

public sealed class GuardrailAlertHandler : IAIGuardrailHandler
{
    public Task TriggeredAsync(AIGuardrailVerdict verdict, CancellationToken cancellationToken)
    {
        // verdict.ProfileId, verdict.SessionId, verdict.Direction, verdict.Topic, verdict.Reason
        return Task.CompletedTask;
    }
}
```

```csharp
builder.Services.AddScoped<IAIGuardrailHandler, GuardrailAlertHandler>();
```

A failing handler is logged and does not stop the others.

## Conversation Privacy

```csharp
using CrestApps.Core.AI.Models;

profile.AlterSettings<AIProfileDataPrivacySettings>(settings =>
{
    settings.StorageMode = AIDataStorageMode.RedactPersonalData; // Everything (default), RedactPersonalData, MetadataOnly
    settings.StorageModeSinceUtc = DateTime.UtcNow;              // conversations active before this are left alone
    settings.RetentionDays = 90;                                 // null keeps them forever
    settings.NamesToKeep = ["Ava", "Contoso"];                   // assistant and business names are not personal data
});
```

`AddConversationPrivacy()` schedules nothing. Run `AIConversationPrivacyService.ProcessAsync` per profile from a hosted service and keep the returned cursor:

```csharp
using CrestApps.Core.AI.Chat.Privacy;

var privacy = scope.ServiceProvider.GetRequiredService<AIConversationPrivacyService>();

foreach (var profile in await profileManager.GetAsync(AIProfileType.Chat))
{
    cursors.TryGetValue(profile.ItemId, out var cursor);

    var result = await privacy.ProcessAsync(profile, cursor, cancellationToken);

    if (result.Cursor is null)
    {
        cursors.Remove(profile.ItemId);
    }
    else
    {
        cursors[profile.ItemId] = result.Cursor.Value;
    }
}

// The service stages changes; commit the unit of work (IStoreCommitter) afterwards.
```

- `ProcessAsync` applies the mode once to each finished conversation older than `SettleTime` (marking it with `AIConversationPrivacyMetadata` and clearing its stored remote address), then deletes up to `BatchSize` conversations past `RetentionDays`. `AIConversationPrivacyResult` reports `Cursor`, `Applied` and `Deleted`.
- `ApplyAsync(session, mode)`, `ApplyAsync(session, mode, namesToKeep, cancellationToken)` and `DeleteAsync(sessionId)` serve one-off requests such as "forget this conversation". `DeleteAsync` goes through `IAIChatSessionStore.DeleteAsync`.
- `AIPersonalDataRedactor` finds emails (written or spoken, such as "mike at example dot com"), phone numbers, Luhn-checked card numbers and US SSNs by pattern. Names, addresses, birth dates and identity numbers come from one utility-model call per conversation (metered as `AIUsagePurposes.PersonalDataRedaction`); without a utility deployment only the patterns run.
- A transcript is redacted one speaker's run of turns at a time, skipping the other side's short turns (up to 400 characters), so a detail dictated over several turns is replaced in every turn it spans. A found email is also removed from other turns that say a fragment of it.
- `AIPersonalDataRedactor.Redact(text, terms)` and `RedactConversation(speakers, turns, terms)` work on any text.

Data a host keeps elsewhere about a conversation takes part through a handler:

```csharp
using CrestApps.Core.AI.Chat.Privacy;
using CrestApps.Core.AI.Models;

public sealed class EvaluationPrivacyHandler : AIConversationPrivacyHandlerBase
{
    private readonly IEvaluationStore _store;

    public EvaluationPrivacyHandler(IEvaluationStore store)
    {
        _store = store;
    }

    public override async Task CollectingAsync(AIConversationPrivacyContext context, CancellationToken cancellationToken)
    {
        var evaluation = await _store.FindBySessionAsync(context.Session.SessionId, cancellationToken);
        context.Items["evaluation"] = evaluation;
        context.Texts.Add(evaluation?.Summary);
    }

    public override async Task ApplyingAsync(AIConversationPrivacyContext context, CancellationToken cancellationToken)
    {
        if (context.Items["evaluation"] is not Evaluation evaluation)
        {
            return;
        }

        evaluation.Summary = context.Mode == AIDataStorageMode.MetadataOnly
            ? null
            : context.Redact(evaluation.Summary);

        await _store.SaveAsync(evaluation, cancellationToken);
    }

    public override Task DeletingAsync(AIChatSession session, CancellationToken cancellationToken)
        => _store.DeleteBySessionAsync(session.SessionId, cancellationToken);
}
```

`IEvaluationStore` and `Evaluation` stand for the host's own types. Register the handler as `IAIConversationPrivacyHandler`. `CrestApps.Core.AI.Documents` registers one that deletes a deleted conversation's documents.

## Profile Versions

Each create or update through the profile manager is numbered (`AIProfileVersionMetadata.Version`) and kept as an `AIProfileVersion` (`ProfileId`, `Version`, `CreatedUtc`, `Author`, `Note`, `Snapshot`). New chat sessions copy the answering profile's `AIProfileVersionMetadata`.

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;

var (versions, count) = await versionStore.PageAsync(profile.ItemId, page: 1, pageSize: 20);
var latest = await versionStore.GetLatestVersionAsync(profile.ItemId);
var version = await versionStore.FindByIdAsync(versionId);

foreach (var row in AIProfileVersionComparer.Compare(older.Snapshot, newer.Snapshot, templateTitles).Where(r => r.Changed))
{
    Console.WriteLine($"{row.Section} / {row.Label}: {row.Then} -> {row.Now}");
}

var metadata = profile.GetOrCreate<AIProfileVersionMetadata>();
metadata.PendingNote = $"Restored from version {version.Version}";
profile.Put(metadata);

AIProfileVersionRestorer.Restore(profile, version.Snapshot);
await profileManager.UpdateAsync(profile);
```

- `PendingNote` goes onto the kept version and is cleared from the stored profile.
- `Compare` lists every field and setting, grouped into `AIProfileVersionComparer.Sections`, and leaves out identity and per-save fields. `templateTitles` (name to title) is optional.
- `Restore` keeps the profile's identifier, name, type, source, owner, creation time and version number. Saving records the restore as a new version.
- YesSql hosts create the index table with `CreateAIProfileVersionIndexSchemaAsync(options)`. EntityCore stores versions in the shared catalog tables.

## Speech Settings

```csharp
using CrestApps.Core.AI.Models;

profile.AlterSettings<AIProfileSpeechSettings>(settings =>
{
    settings.SpeakingRate = 1.1;                  // clamped to 0.25-4, multiplies the deployment's pace
    settings.SpeakingEmotion = "calm";            // Cartesia only, passed as TextToSpeechOptionsProperties.Emotion
    settings.PronunciationEntries = [new PronunciationEntry { Text = "SQL", Alias = "sequel" }];
    settings.FallbackTextToSpeechDeploymentName = "azure-speech-tts";
    settings.BoostedKeywords = ["Contoso", "SKU"];
    settings.RecognitionLanguages = ["en-US", "es-MX"];
    settings.InterruptionSensitivity = 0.5;       // 0 never interrupts; null keeps the default
    settings.EnableBackchanneling = true;         // cascaded realtime only
    settings.BackchannelPhrases = ["mm-hm", "I see"];
});
```

The chat hub wraps its text-to-speech client with `ProfileSpeech.DecorateAsync` (fallback, pronunciation, rate). The cascaded realtime pipeline gets the same settings through `CascadedRealtimeProfileOptions`. A host calling TTS itself can use `ProfileSpeech`, `PronunciationDictionary`, `PronunciationTextToSpeechClient` and `FallbackTextToSpeechClient`. Keywords and languages reach providers in `SpeechToTextOptions.AdditionalProperties` under `SpeechToTextOptionsProperties.BoostedKeywords` and `RecognitionLanguages`.

## Language Settings

```csharp
using CrestApps.Core.AI.Models;

profile.AlterSettings<AIProfileLanguageSettings>(settings =>
{
    settings.AllowedLanguages = ["en", "es"]; // empty means any; "es" matches "es-MX"
    settings.StartingLanguage = "en";
});
```

- `AICompletionContextKeys.ConversationLanguage` in `AICompletionContext.AdditionalProperties` overrides the starting language. `AIProfileLanguageCompletionContextBuilderHandler` appends the reply-language instruction and writes the resolved language back to the key.
- `IConversationLanguageService.ResolveStartingLanguage(profile, conversationLanguage)` resolves it. `ReportDetectedLanguageAsync(profile, sessionId, currentLanguage, detectedLanguage, cancellationToken)` runs every `IConversationLanguageHandler.LanguageDetectedAsync(DetectedLanguageContext)` when an allowed, different language is reported. Core detects nothing; the host feeds the hook and its handler can set `DetectedLanguageContext.VoiceName`.

## Typed Data-Extraction Fields

```csharp
using CrestApps.Core.AI.Models;

profile.AlterSettings<AIProfileDataExtractionSettings>(settings =>
{
    settings.EnableDataExtraction = true;
    settings.DataExtractionEntries =
    [
        new DataExtractionEntry { Name = "budget", Description = "Monthly budget", FieldType = DataExtractionFieldType.Number },
        new DataExtractionEntry { Name = "plan", Description = "Chosen plan", FieldType = DataExtractionFieldType.Enum, AllowedValues = ["Basic", "Pro"] },
        new DataExtractionEntry { Name = "interests", Description = "Products of interest", FieldType = DataExtractionFieldType.List, AllowedValues = ["Phones", "Tablets", "Laptops"] },
    ];
});
```

`DataExtractionValueNormalizer.TryNormalize(entry, value, out var normalized)` runs before each value is stored: `Number` becomes an invariant decimal, `Boolean` becomes `true`/`false`, `Date` becomes `yyyy-MM-dd`, `Enum` takes the allowed spelling, and `List` stores one value per item without duplicates (restricted to `AllowedValues` when set). A rejected value is dropped and logged at debug level. `Text` (the default) stores values as extracted, so existing entries are unchanged.
