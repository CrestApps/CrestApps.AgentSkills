# Writing a Custom Telephony Provider

Use this reference to add another PBX or carrier backend to CrestApps Telephony. Contact Center voice adapters (`IContactCenterVoiceProvider` and its executable interfaces, live media, queue ownership) are covered by `orchardcore-contact-center`; this page covers the Telephony side and the shared event pipeline.

## Seams

| Seam | Interface | Responsibility |
|---|---|---|
| Identity | `ITelephonyProvider` | `Name` and advertised `TelephonyCapabilities`, no operations |
| Call control | `ITelephonyCallControlProvider` | `DialAsync`, `HangupAsync` |
| Inbound | `ITelephonyInboundCallProvider` | `AnswerAsync`, `RejectAsync` |
| Hold and mute | `ITelephonyHoldProvider`, `ITelephonyMuteProvider` | Hold, resume, mute, unmute |
| Transfer | `ITelephonyTransferProvider`, `ITelephonyAttendedTransferProvider`, `ITelephonyConsultTransferProvider` | Blind, attended, and consult transfers |
| Conference and DTMF | `ITelephonyConferenceProvider`, `ITelephonyDtmfProvider` | `MergeAsync`, `SendDigitsAsync` |
| Voicemail and directory | `ITelephonyVoicemailProvider`, `ITelephonyDirectoryProvider` | Send to voicemail, list transfer targets |
| Extensions | `ITelephonyExtensionDialProvider` | Call an extension, add an extension to a conference |
| Browser bootstrap | `ITelephonySoftPhoneCredentialsProvider` | Short-lived browser credentials |
| Audio path | `ITelephonyAudioProvider` | `Browser` and/or `ExternalDevice`, configured mode, browser adapter name |
| State lookup | `ITelephonyCallStateProvider` | `GetCallStateAsync(callId)` returning `TelephonyCallLookupResult` |
| Per-user auth | `ITelephonyAuthenticationProvider` | OAuth authorization, code exchange, refresh, revoke |
| Event ingress | `INormalizedVoiceEventIngestor` | Single entry point for normalized `ProviderVoiceEvent` deliveries |

## 1. Implement the Provider

Reference `CrestApps.OrchardCore.Telephony.Abstractions` from the provider project and install the package that results in the web/startup project. The resolver creates the provider from its registered type on each resolution, so constructor injection works without registering the provider in DI.

```csharp
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace MyCompany.OrchardCore.MyPbx;

public sealed class MyPbxTelephonyProvider :
    ITelephonyProvider,
    ITelephonyCallControlProvider,
    ITelephonyHoldProvider,
    ITelephonyCallStateProvider
{
    public const string TechnicalName = "MyPbx";

    private readonly IStringLocalizer S;

    public MyPbxTelephonyProvider(IStringLocalizer<MyPbxTelephonyProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public LocalizedString Name => S["My PBX"];

    public TelephonyCapabilities Capabilities =>
        TelephonyCapabilities.Dial |
        TelephonyCapabilities.Hangup |
        TelephonyCapabilities.Hold |
        TelephonyCapabilities.Resume;

    public Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
    {
        // Translate request.To, request.From, request.IsExtension and request.Metadata into the vendor call.
        // Return TelephonyResult.Unknown(...) when the request may have reached the vendor but no answer came back.
        return Task.FromResult(TelephonyResult.Success(new TelephonyCall
        {
            CallId = "vendor-call-id",
            To = request.To,
            ProviderName = TechnicalName,
            Direction = CallDirection.Outbound,
        }));
    }

    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyCallLookupResult> GetCallStateAsync(string callId, CancellationToken cancellationToken = default)
        => Task.FromResult(new TelephonyCallLookupResult { Succeeded = true, Found = false });
}
```

Rules:

- Advertise a flag only when the matching contract is implemented, and implement a contract only when its flag is advertised. Either mismatch fails closed in `DefaultTelephonyService`.
- `Transfer` and `AttendedTransfer` are separate; advertise `Transfer` alone when the backend cannot consult first.
- Return provider-neutral `TelephonyCall` values. Use `TelephonyCall.Metadata` for context that must travel with the call, and keep provider-private keys in an `internal` class in your module rather than in shared contracts.
- A `TelephonyCallLookupResult` with `Succeeded = true` and `Found = false` is authoritative evidence that the call is gone; Telephony then deletes the orphaned active record. Return `Succeeded = false` on a transient lookup failure.
- Honor the request flags in `TelephonyConstants.RequestMetadata`, such as `ConferenceLeave`, `ConferenceEnd`, and `ConferenceParticipant`. A provider that ignores them treats the request as an ordinary hang-up.

## 2. Register Provider Availability

Technical names in `TelephonyProviderOptions` are trimmed and compared case-insensitively. Registering a different type under a name another module already claimed throws at startup; use `ReplaceProvider` only for an intentional override.

```csharp
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace MyCompany.OrchardCore.MyPbx;

public sealed class MyPbxProviderOptionsConfiguration : IConfigureOptions<TelephonyProviderOptions>
{
    private readonly ISiteService _siteService;

    public MyPbxProviderOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(TelephonyProviderOptions options)
    {
        var settings = _siteService.GetSettings<MyPbxSettings>();

        options.TryAddProvider(MyPbxTelephonyProvider.TechnicalName, new TelephonyProviderTypeOptions(typeof(MyPbxTelephonyProvider))
        {
            IsEnabled = settings.IsEnabled && !string.IsNullOrWhiteSpace(settings.BaseUrl),
        });
    }
}
```

For a provider that is always available, `services.AddTelephonyProvider<TProvider>("Name")` registers it as enabled.

`TelephonyProviderOptions` is read through `IOptionsMonitor` and refreshed by an update signal that Telephony registers with `AddSignalOptionsChangeTokenSource<TelephonyProviderOptions>()`. After your settings change, request the refresh so the provider map is rebuilt without a shell release:

```csharp
using CrestApps.OrchardCore.Telephony;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Options;
using OrchardCore.Settings;

namespace MyCompany.OrchardCore.MyPbx;

public sealed class MyPbxSettingsDisplayDriver : SiteDisplayDriver<MyPbxSettings>
{
    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;

    public MyPbxSettingsDisplayDriver(IOptionsUpdateNotifier optionsUpdateNotifier)
    {
        _optionsUpdateNotifier = optionsUpdateNotifier;
    }

    protected override string SettingsGroupId
        => TelephonyConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, MyPbxSettings settings, BuildEditorContext context)
        => Initialize<MyPbxSettings>("MyPbxSettings_Edit", model =>
        {
            model.IsEnabled = settings.IsEnabled;
            model.BaseUrl = settings.BaseUrl;
        }).Location("Content:10#My PBX")
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, MyPbxSettings settings, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(settings, Prefix);

        _optionsUpdateNotifier.RequestUpdate<TelephonyProviderOptions>();

        return Edit(site, settings, context);
    }
}
```

Add a permission check for `TelephonyPermissions.ManageTelephonySettings` in real drivers. When the provider is enabled and `TelephonySettings.DefaultProviderName` is empty, set it to your technical name, as the built-in providers do.

```csharp
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Extensions;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace MyCompany.OrchardCore.MyPbx;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddTelephonyProviderOptionsConfiguration<MyPbxProviderOptionsConfiguration>()
            .AddSiteDisplayDriver<MyPbxSettingsDisplayDriver>();

        services.AddSiteSettingsPermission(TelephonyConstants.SettingsGroupId, TelephonyPermissions.ManageTelephonySettings);
    }
}
```

In the manifest, depend on `TelephonyConstants.Feature.Area` and use category `Telephony`.

## 3. Declare the Audio Path

- Advertise `TelephonyAudioCapabilities.Browser` only when a browser SDK or signaling adapter can send microphone audio and play remote audio, and return a stable `BrowserMediaAdapterName`. Browser audio fails closed without one.
- Advertise `ExternalDevice` when audio stays on a hard phone or provider-owned client.
- Advertise both only when the provider exposes a setting for it, and return that choice from `ConfiguredAudioMode`.

The browser script registers the adapter factory. The factory receives `credentials`, `localStream`, `remoteAudioElement`, `setRemoteStream`, and `showError`:

```js
window.telephonySoftPhone.mediaAdapters.myPbx = function (context) {
    return {
        handleCallState: function (call) {
            // Synchronize the vendor SDK session with authoritative call state.
        },
        dispose: function () {
            // Disconnect the SDK session and release resources.
        }
    };
};
```

## 4. Normalize Provider Events

Translate every webhook or stream event into an immutable `ProviderVoiceEvent` record and pass it to `INormalizedVoiceEventIngestor.IngestAsync`. Do not call a consumer such as `IProviderVoiceEventService` or `IProviderVoiceEventSink` directly. The ingestor canonicalizes the provider identity, takes the ingestion lease for the call once, and hands the delivery to every `INormalizedVoiceEventHandler` (call history always, Contact Center projection when Contact Center Voice is on).

```csharp
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;

namespace MyCompany.OrchardCore.MyPbx;

public sealed class MyPbxEventTranslator
{
    private readonly INormalizedVoiceEventIngestor _ingestor;

    public MyPbxEventTranslator(INormalizedVoiceEventIngestor ingestor)
    {
        _ingestor = ingestor;
    }

    public Task<bool> OnCallAnsweredAsync(string callId, string eventId, CancellationToken cancellationToken)
        => _ingestor.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = MyPbxTelephonyProvider.TechnicalName,
            ProviderCallId = callId,
            State = VoiceCallState.Connected,
            IdempotencyKey = eventId,
            OccurredUtc = DateTime.UtcNow,
        }, cancellationToken);
}
```

- Populate `IsMuted`, `RecordingState`, `RecordingReference`, `IsConference`, `ParticipantCount`, `AnswerClassification`, and `HangupCause` when the backend reports them.
- Derive a variant with a `with` expression rather than mutating an event.
- A custom `INormalizedVoiceEventHandler` returns `false` when it had nothing to project; it must never suppress the delivery for other handlers, and must not take its own distributed lock on the same call.

### Transport Models

| Model | Shape |
|---|---|
| Webhook | Provider-owned minimal API endpoint validates the signature, commits the raw delivery to `IProviderWebhookInbox`, and an `IProviderWebhookInboxHandler` normalizes it (Telnyx uses this) |
| Live stream | A tenant-scoped `ModularTenantEvents` component keeps the socket open, reconnects with backoff, and resolves services through `ShellScope.UsingChildScopeAsync(...)` per event (Asterisk ARI uses this) |
| Hybrid | Both, normalized into the same `ProviderVoiceEvent` |

The durable inbox and the inbound routing sinks are Contact Center abstractions; see `orchardcore-contact-center`.

## 5. Per-User Authentication (Optional)

Only providers that need each user to connect an account implement `ITelephonyAuthenticationProvider`:

- `RequiresUserAuthentication`, `AuthenticationScheme` (for example `TelephonyConstants.AuthenticationSchemes.OAuth2`), and `SupportsProofKeyForCodeExchange`.
- `GetAuthorizationUrlAsync`, `ExchangeCodeAsync`, `RefreshTokensAsync`, and `RevokeTokensAsync`, which returns `Task<TelephonyResult>` so callers can tell confirmed success from definitive or indeterminate failure.

Tokens are stored encrypted on the user through `ITelephonyUserTokenStore`. Register `{scheme}://{host}/Telephony/Connect/Callback` (with any tenant prefix) as the redirect URI. The routes `TelephonyOAuthConnect`, `TelephonyOAuthCallback`, and `TelephonyOAuthDisconnect` are provided by the core module; do not duplicate them.

## Firewall Checklist

| Path | Protocol |
|---|---|
| Browser to Orchard (SignalR) | `https`, `wss` (keep SSE and long-polling fallbacks open) |
| Provider webhook to Orchard | `https` |
| Orchard to provider REST | `https` |
| Orchard to provider event socket | `wss` in production |

## Registration Checklist

1. Provider type, settings, settings driver, and `IConfigureOptions<TelephonyProviderOptions>`.
2. Capability contracts matching the advertised flags.
3. `ITelephonyAudioProvider` and a browser adapter only for executable audio modes.
4. `ITelephonyCallStateProvider` when the backend can look up a call by id.
5. Event ingress (webhook plus inbox, or a tenant-scoped stream listener) normalized into `ProviderVoiceEvent`.
6. Contact Center adapters behind `[RequireFeatures("CrestApps.OrchardCore.ContactCenter.Voice")]` when needed (see `orchardcore-contact-center`).
7. Tests for state mapping, idempotency, capability and contract parity, and restart reconciliation.
