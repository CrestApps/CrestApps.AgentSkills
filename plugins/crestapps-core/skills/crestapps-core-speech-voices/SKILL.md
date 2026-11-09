---
name: crestapps-core-speech-voices
description: Skill for the ElevenLabs and Cartesia speech providers and provider-neutral voices in CrestApps.Core 2.0. Covers the CrestApps.Core.AI.ElevenLabs and CrestApps.Core.AI.Cartesia packages, AddElevenLabs and AddCartesia on the AI suite builder or AddCoreAIElevenLabs and AddCoreAICartesia, contained-connection speech deployments, streaming text-to-speech as MP3 or raw Pcm rate formats for cascaded realtime, speed limits, TextToSpeechOptionsProperties Emotion, Scribe and Ink speech recognition, Cartesia realtime transcription on the turns endpoint as a cascade's speech-to-text leg, voice search and cloning with IVoiceLibrary, IVoiceLibraryProvider and IVoiceLibraryResolver, and neutral speech markup with SpeechMarkup and SpeechMarkupTextToSpeechClient for break and spell tags. Use it when adding ElevenLabs or Cartesia voices, building a voice picker or cloning flow, writing prompts with pauses or spelled-out codes, or troubleshooting missing voices, wrong PCM rates or refused Cartesia connections.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Speech Voices (ElevenLabs and Cartesia)

ElevenLabs and Cartesia are speech-only providers: they have no chat, embedding or image models. Either can be the text-to-speech leg of a cascaded realtime deployment, the voice of a text chat, or the `ITextToSpeechClient` / `ISpeechToTextClient` any host code asks `IAIClientFactory` for. Cartesia can also be a cascade's speech-to-text leg. The voice library and speech markup they introduced are provider-neutral.

## Guidelines

- Register only the providers you use, in the web/startup project.
- Each provider uses a contained connection: the API key (and optional endpoint) live on the deployment, so no `ConnectionName` is needed.
- These providers have no default voice. Always choose one (profile or persona voice, or `TextToSpeechOptions.VoiceId`); a request without one fails with a message that says so.
- For a cascade's speaking leg ask for `Pcm<rate>` at a rate the provider produces. For its hearing leg use Cartesia on `ink-2` or `ink-preview`; ElevenLabs cannot hear for a cascade.
- Get the speaker's consent before cloning a voice, and delete only voices you created through `CloneAsync`.
- The request shapes follow the providers' public API docs and are tested against recorded-shape fakes, but have **not yet been run against live accounts**. Before production, make one real call of each kind with your key (MP3 and `Pcm24000` speech, a short transcription, a voice search, and clone plus delete if used).
- Cascades are covered by `crestapps-core-realtime-voice`; profile speech settings (rate, emotion, pronunciation, fallback voice, keywords) by `crestapps-core-ai-profiles`.

## Registration

```csharp
using CrestApps.Core.AI.Cartesia;
using CrestApps.Core.AI.ElevenLabs;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddElevenLabs()
        .AddCartesia()
    )
);
```

Without the suite builder:

```csharp
using CrestApps.Core.AI.Cartesia;
using CrestApps.Core.AI.ElevenLabs;

builder.Services
    .AddCoreAIServices()
    .AddCoreAIElevenLabs()
    .AddCoreAICartesia();
```

The extension methods live in the `CrestApps.Core.AI.ElevenLabs` and `CrestApps.Core.AI.Cartesia` namespaces. Each registers a named `HttpClient` (`CrestApps.ElevenLabs`, `CrestApps.Cartesia`), an `IAIClientProvider` that is also the `IVoiceLibraryProvider`, and a deployment provider with `UseContainedConnection = true`.

| | ElevenLabs | Cartesia |
|---|---|---|
| Client name | `ElevenLabsConstants.ClientName` (`ElevenLabs`) | `CartesiaConstants.ClientName` (`Cartesia`) |
| Text-to-speech model | `eleven_flash_v2_5` (default), `eleven_multilingual_v2`, ... | `sonic-2` (default), `sonic-3`, ... |
| Speech-to-text model | `scribe_v1` (default), `scribe_v2` | `ink-whisper` (default, batch); `ink-2` or `ink-preview` for a cascade |
| Default endpoint | `https://api.elevenlabs.io` (set `Endpoint` for a residency region) | `https://api.cartesia.ai` |
| Authentication | `xi-api-key` header | `X-API-Key` header plus a pinned `Cartesia-Version` |
| Cloning | From one or more recordings | From one clip |

## Deployments

```json
{
  "CrestApps": {
    "AI": {
      "Deployments": [
        {
          "ClientName": "ElevenLabs",
          "Name": "elevenlabs-tts",
          "ModelName": "eleven_flash_v2_5",
          "ApiKey": "your-elevenlabs-key",
          "Features": [ "textToSpeech" ]
        },
        {
          "ClientName": "Cartesia",
          "Name": "cartesia-tts",
          "ModelName": "sonic-2",
          "ApiKey": "your-cartesia-key",
          "Features": [ "textToSpeech" ]
        },
        {
          "ClientName": "Cartesia",
          "Name": "cartesia-ink",
          "ModelName": "ink-2",
          "ApiKey": "your-cartesia-key",
          "Features": [ "speechToText" ]
        }
      ]
    }
  }
}
```

Top-level fields of a configured deployment (here `ApiKey`, optionally `Endpoint`) become its properties, which the contained connection reads. `ModelName` picks the model and falls back to `Name`.

## Text-to-Speech

- Audio streams as it is generated, so a realtime reply starts speaking before it is complete.
- `TextToSpeechOptions.AudioFormat` set to `Pcm<rate>` (for example `Pcm24000`) returns raw 16-bit PCM; anything else returns MP3. ElevenLabs produces PCM at 8, 16, 22.05, 24, 32, 44.1 and 48 kHz; Cartesia at 8, 16, 22.05, 24, 44.1 and 48 kHz. Any other rate fails rather than playing at the wrong speed.
- A 16-bit sample split across two network reads is held for the next chunk, so streamed PCM never shifts by a byte.
- `TextToSpeechOptions.Speed` is clamped to what the API accepts: ElevenLabs 0.7 to 1.2, Cartesia 0.6 to 1.5.
- `TextToSpeechOptions.Language` is sent as an ISO 639-1 code (`es-MX` becomes `es`). ElevenLabs multilingual v2 models detect the language and are sent none; Cartesia defaults to `en`.
- `TextToSpeechOptionsProperties.Emotion` (`"emotion"` in `AdditionalProperties`) selects Cartesia's emotion control (`calm`, `excited`, ...). ElevenLabs ignores it. A profile sets it with `AIProfileSpeechSettings.SpeakingEmotion`.

```csharp
using CrestApps.Core.AI.Speech;
using Microsoft.Extensions.AI;

var tts = await clientFactory.CreateTextToSpeechClientAsync(deployment);

var options = new TextToSpeechOptions
{
    VoiceId = voiceId,
    AudioFormat = "Pcm24000",
    Speed = 1.1f,
    AdditionalProperties = new AdditionalPropertiesDictionary
    {
        [TextToSpeechOptionsProperties.Emotion] = "calm",
    },
};

await foreach (var update in tts.GetStreamingAudioAsync("Your code is <spell>AB12</spell>.", options, cancellationToken))
{
    // Play or forward each chunk.
}
```

## Speech Recognition

- Both batch APIs transcribe a whole recording, so a streaming call yields the finished transcript as one update. This serves dictation, not a cascade.
- Audio with a container (WAV, MP3, Ogg, FLAC, WebM) is uploaded as is. Anything else is treated as raw 16-bit mono PCM at `SpeechToTextOptions.SpeechSampleRate` (16 kHz when unset) and wrapped in a WAV header (`SpeechAudioFormat.ToUploadable`).
- The language is sent as an ISO 639-1 code.
- `SpeechToTextOptionsProperties.BoostedKeywords` are sent as key terms to ElevenLabs Scribe v2 models. Scribe v1 and Cartesia ignore them.

## Cartesia as a Cascade's Speech-to-Text Leg

Cartesia's realtime client opens transcription sessions on the turns endpoint (`wss://api.cartesia.ai/stt/turns/websocket`), which decides on the server where each user turn ends:

- Each `turn.update` carries the turn so far; its new words become a partial transcript, which the cascade treats as the user talking over the assistant.
- `turn.end` carries the definitive text, the completed transcript that starts the reply, metered with the audio sent since the previous turn ended.
- Speech is reported with the turn's first words, so sounds without words (including punctuation alone) never start a turn or interrupt the assistant.
- A silence duration from the session's turn detection caps how long Cartesia waits after speech (640 to 11,200 ms); without one it waits at most 1,200 ms. A turn Cartesia leaves open with words in it is ended by the session a second past that cap.
- Models: name `ink-2` or `ink-preview`. A deployment on `ink-whisper` or with no model hears with `ink-2`. Cartesia detects the language; its realtime models transcribe English, French, Hindi, Japanese and Spanish.
- A refused connection says why (API key, model or audio format, account credit, or too many connections). Each event is logged at Debug; what the user said only at Trace.
- Cartesia's realtime client only supports transcription sessions; asking it for a speech-to-speech session throws `NotSupportedException`. ElevenLabs' realtime client always throws, because its recognition is batch only.

```csharp
deployment.Alter<CascadedRealtimeMetadata>(cascade =>
{
    cascade.SpeechToTextDeploymentName = "cartesia-ink";
    cascade.ChatDeploymentName = "gpt-4.1";
    cascade.TextToSpeechDeploymentName = "elevenlabs-tts";
});
```

## Voice Library: Search and Cloning

`ISpeechVoiceResolver` returns a short list (the first 100 voices) for pickers. `IVoiceLibrary` searches beyond it and clones voices.

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Speech;

var library = await voiceLibraryResolver.GetLibraryAsync(deployment, cancellationToken);

if (library is null)
{
    return; // The deployment's provider has no voice library.
}

var page = await library.SearchAsync(new VoiceSearchRequest
{
    Query = "calm",
    Language = "es",
    Gender = SpeechVoiceGender.Female,
    PageSize = 25,
}, cancellationToken);

// page.Voices, and page.NextCursor as the next request's Cursor.

if (library.SupportsCloning)
{
    var voice = await library.CloneAsync(new VoiceCloneRequest
    {
        Name = "Support agent",
        Language = "en",
        Samples = [new VoiceCloneSample { FileName = "agent.mp3", ContentType = "audio/mpeg", Content = recording }],
    }, cancellationToken);

    // voice.Id can now be used as the deployment's voice. Remove it with library.DeleteAsync(voice.Id).
}
```

- `IVoiceLibraryResolver` (registered by `AddCoreAIServices`) finds the `IVoiceLibraryProvider` whose `CanHandle(clientName)` matches the deployment and opens it with the deployment's connection through `GetLibrary(connection)`. It returns `null` when no provider has a library.
- Filters an API does not take (ElevenLabs language and gender, Cartesia language) are applied to the returned page, so a page can hold fewer voices than `PageSize`; keep following `NextCursor`.
- A library without cloning (`SupportsCloning` is `false`) throws `NotSupportedException` from `CloneAsync` and `DeleteAsync`.
- To add a library for another provider, implement `IVoiceLibraryProvider` and `IVoiceLibrary` and register the provider with `TryAddEnumerable`.

## Speech Markup

Prompts and replies can carry two neutral tags so they do not depend on the provider that speaks them:

| Markup | Meaning |
|---|---|
| `<break time="500ms"/>` or `<break time="1.5s"/>` | A pause, at most 3 seconds |
| `<spell>AB12</spell>` | Read one character at a time |

`SpeechMarkup.Render(text, dialect)` (namespace `CrestApps.Core.AI.Services`) renders them for `SpeechMarkupDialect.ElevenLabs` (break tags, hyphenated letters), `Cartesia` (break and spell tags) or `Plain` (punctuation and spaced letters). `SpeechMarkup.Strip(text)` renders `Plain`, and `HasMarkup(text)` detects tags. The ElevenLabs and Cartesia clients render markup themselves; the OpenAI and Azure AI Speech text-to-speech clients are wrapped in `SpeechMarkupTextToSpeechClient`, so a tag is never read aloud. Wrap any other provider the same way:

```csharp
using CrestApps.Core.AI.Services;

ITextToSpeechClient client = new SpeechMarkupTextToSpeechClient(innerClient, SpeechMarkupDialect.Plain);
```

## Errors and Troubleshooting

- A failed call throws `HttpRequestException` with `StatusCode` set; the message names the provider, the status, a hint for 401 (check the API key) and 429 (rate or concurrency limit), and the start of the response body. The API key is never included.
- **"No voice" failure**: choose a voice; these providers have no default.
- **A cascade refuses the speaking leg or audio plays at the wrong speed**: the session asked for a PCM rate the provider does not produce (for example 32 kHz on Cartesia).
- **Cartesia HTTP 400 on a cascade**: the deployment's model is not a realtime model; use `ink-2` or `ink-preview`.
- **Boosted keywords have no effect**: only ElevenLabs `scribe_v2` models take key terms.
