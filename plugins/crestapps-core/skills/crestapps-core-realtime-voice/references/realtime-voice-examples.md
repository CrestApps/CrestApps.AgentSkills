# Realtime Voice Examples

## A Custom ICE Server Provider

Both the list the browser receives from `GetRealtimeIceServers()` and the server-relay peer resolve through `IRealtimeIceServerProvider`, so they always agree on the relay and its credentials. This provider fetches short-lived TURN credentials from a credential service the host owns and caches them for half their lifetime.

```csharp
using System.Net.Http.Json;
using CrestApps.Core.AI.Realtime;
using Microsoft.Extensions.Options;

namespace MyApp.Realtime;

public sealed class TurnServiceOptions
{
    public Uri CredentialEndpoint { get; set; }

    public string ApiKey { get; set; }

    public string[] FallbackStunUrls { get; set; } = ["stun:stun.l.google.com:19302"];
}

public sealed class TurnServiceIceServerProvider : IRealtimeIceServerProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<TurnServiceOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TurnServiceIceServerProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private IReadOnlyList<WebRtcIceServer> _cached;
    private DateTimeOffset _refreshAfter;

    public TurnServiceIceServerProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<TurnServiceOptions> options,
        TimeProvider timeProvider,
        ILogger<TurnServiceIceServerProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async ValueTask<IReadOnlyList<WebRtcIceServer>> GetIceServersAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null && _timeProvider.GetUtcNow() < _refreshAfter)
        {
            return _cached;
        }

        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            if (_cached is not null && _timeProvider.GetUtcNow() < _refreshAfter)
            {
                return _cached;
            }

            var options = _options.CurrentValue;
            var client = _httpClientFactory.CreateClient(nameof(TurnServiceIceServerProvider));

            using var request = new HttpRequestMessage(HttpMethod.Post, options.CredentialEndpoint);
            request.Headers.Add("X-Api-Key", options.ApiKey);

            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var issued = await response.Content.ReadFromJsonAsync<IssuedTurnCredentials>(cancellationToken);

            _cached =
            [
                new WebRtcIceServer { Urls = options.FallbackStunUrls },
                new WebRtcIceServer
                {
                    Urls = issued.Urls,
                    Username = issued.Username,
                    Credential = issued.Credential,
                },
            ];

            _refreshAfter = _timeProvider.GetUtcNow().AddSeconds(issued.TtlSeconds / 2.0);

            return _cached;
        }
        catch (Exception ex) when (ex is not OperationCanceledException && _cached is not null)
        {
            // Keep serving the credentials already issued; they are still valid for about half their lifetime.
            _logger.LogWarning(ex, "Refreshing TURN credentials failed; serving the previous credentials.");

            _refreshAfter = _timeProvider.GetUtcNow().AddSeconds(30);

            return _cached;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private sealed class IssuedTurnCredentials
    {
        public string[] Urls { get; set; } = [];

        public string Username { get; set; }

        public string Credential { get; set; }

        public int TtlSeconds { get; set; } = 3600;
    }
}
```

Register it as a singleton so it replaces the default `OptionsRealtimeIceServerProvider` (registered with `TryAddSingleton`):

```csharp
builder.Services.AddHttpClient(nameof(TurnServiceIceServerProvider));
builder.Services.Configure<TurnServiceOptions>(builder.Configuration.GetSection("MyApp:TurnService"));
builder.Services.AddSingleton<IRealtimeIceServerProvider, TurnServiceIceServerProvider>();
builder.Services.AddWebRtcRealtimeTransport();
```

Do not combine it with `AddCloudflareRealtimeTurn()`, which replaces the provider registration with its own.

## A Session Without the Chat Hubs

The hubs already run realtime for chat pages. A different transport (for example a telephony bridge) can drive `IRealtimeOrchestrator` directly. The scope must stay open for the whole session, because tools read their context from it; starting a session with tools and no scope throws.

```csharp
using System.Threading.Channels;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Realtime;

namespace MyApp.Realtime;

public sealed class VoiceBridge
{
    private readonly IAIProfileManager _profileManager;
    private readonly IRealtimeOrchestrator _orchestrator;
    private readonly ILogger<VoiceBridge> _logger;

    public VoiceBridge(
        IAIProfileManager profileManager,
        IRealtimeOrchestrator orchestrator,
        ILogger<VoiceBridge> logger)
    {
        _profileManager = profileManager;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <param name="callerAudio">PCM16, 24 kHz, mono frames from the caller.</param>
    /// <param name="playAsync">Receives PCM16, 24 kHz, mono assistant audio.</param>
    public async Task RunAsync(
        string profileName,
        ChannelReader<ReadOnlyMemory<byte>> callerAudio,
        Func<ReadOnlyMemory<byte>, Task> playAsync,
        CancellationToken cancellationToken)
    {
        var profile = await _profileManager.FindByNameAsync(profileName, cancellationToken)
            ?? throw new InvalidOperationException($"Profile '{profileName}' was not found.");

        using var invocationScope = AIInvocationScope.Begin();

        await using var conversation = await _orchestrator.StartAsync(
            new RealtimeOrchestrationRequest
            {
                Resource = profile,
                AllowInterruption = true,
                SpeechLanguage = "en-US",
            },
            cancellationToken);

        // Open the call with a greeting instead of waiting for the caller to speak first.
        await conversation.RequestUnpromptedResponseAsync("Greet the caller and ask how you can help.", cancellationToken);

        var pump = PumpCallerAudioAsync(conversation, callerAudio, cancellationToken);

        await foreach (var evt in conversation.GetEventsAsync(cancellationToken))
        {
            switch (evt.Type)
            {
                case RealtimeConversationEventType.AssistantAudioDelta:
                    await playAsync(evt.Audio);
                    break;

                case RealtimeConversationEventType.UserTranscript when !conversation.RespondsAutomatically:
                    // A grounded session answers only when asked, after retrieval for this utterance.
                    await conversation.GroundTurnAsync(evt.Text, cancellationToken);
                    await conversation.RequestResponseAsync(cancellationToken);
                    break;

                case RealtimeConversationEventType.Error:
                    _logger.LogWarning("Realtime error: {Message}", evt.ErrorMessage);
                    break;
            }
        }

        await pump;
    }

    private static async Task PumpCallerAudioAsync(
        IRealtimeConversation conversation,
        ChannelReader<ReadOnlyMemory<byte>> callerAudio,
        CancellationToken cancellationToken)
    {
        await foreach (var frame in callerAudio.ReadAllAsync(cancellationToken))
        {
            await conversation.SendAudioAsync(frame, cancellationToken);
        }
    }
}
```

The hub runner (`RealtimeChatSessionRunner`) adds what this sketch leaves out: turn persistence, barge-in truncation (`TruncateAssistantAudioAsync`), the idle and maximum-duration guard rails, the grounding acknowledgement and watchdog, and the lifecycle events sent to the browser. Prefer the hubs for chat surfaces.

To leave a voicemail, call `conversation.RequestStandaloneMessageAsync(instructions)` when no response is in flight. It runs out of band, with a single system item as context and tools off; include the call's language and anything the message must say in the instructions.

## Wiring the Browser Client

The bundled chat scripts already call `CoreAIRealtime.attach`. A custom chat interaction page wires it to its own hub connection:

```html
<script src="https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui/dist/realtime-audio.min.js"></script>
<script>
    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/chat-interaction')
        .withAutomaticReconnect()
        .build();

    const started = connection.start();
    const itemId = document.querySelector('#chat-app').dataset.itemId;

    const controller = window.CoreAIRealtime.attach({
        connection,
        ensureConnected: () => started,
        realtimeEnabled: true,
        // Render this from RealtimeTransportSettings.IsWebRtcEnabled(services) on the server.
        webRtcEnabled: document.body.dataset.realtimeWebrtcEnabled === 'true',
        voiceName: 'alloy',
        sendStart: (subject, voice, language, silenceMs, vadThreshold, allowInterruption) => {
            connection.send('StartRealtimeConversation', itemId, subject, voice, language, silenceMs, vadThreshold, allowInterruption);
        },
        sendStartWebRtc: (offerSdp, voice, language, silenceMs, vadThreshold, allowInterruption) => {
            connection.send('StartRealtimeWebRtc', itemId, offerSdp, voice, language, silenceMs, vadThreshold, allowInterruption);
        },
        // Do not hand over the message box or send button: typing stays available between sessions.
        selectors: {
            realtimeButton: '#realtime-btn',
            micButton: '#mic-btn',
        },
        onSessionStateChanged: (active) => {
            document.querySelector('#chat-input').hidden = active;
            document.querySelector('#send-btn').hidden = active;
        },
    });

    connection.on('ReceiveAudioChunk', (identifier, base64Audio, contentType) => {
        if (contentType === 'audio/pcm' && base64Audio) {
            controller.receivePcm(Uint8Array.from(atob(base64Audio), (c) => c.charCodeAt(0)));
        }
    });
</script>
```

`sendStart` receives a `signalR.Subject` that the module fills with base64 PCM16 frames; the hub reads it as `IAsyncEnumerable<string>`. The WebRTC answer and server ICE candidates arrive on `ReceiveRealtimeAnswer` and `ReceiveRealtimeIceCandidate`, which the module subscribes to itself, as it does `ReceiveRealtimeEvent`. `getTransportStats()` and `getGateLevel()` on the controller are the first things to read when audio is choppy or the user is not heard.
