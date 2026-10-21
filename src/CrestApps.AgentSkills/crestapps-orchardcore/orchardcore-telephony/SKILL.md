---
name: orchardcore-telephony
description: Skill for configuring and extending the provider-agnostic CrestApps Orchard Core Telephony module and its soft phone. Covers the Telephony, Soft Phone, Soft Phone Extension and Azure Blob Storage features, provider selection, capability-gated provider contracts, TelephonyHub events, dial destination safety, incoming-call cards, call history, extension dialing, encrypted recording storage, TelephonyProviderOptions read through IOptionsMonitor, configuration sections, and upgrading from 2.x. Use this skill when requests mention CrestApps Telephony, soft phone, click-to-dial, extensions, call history, recording storage, or writing a custom telephony provider. Strong matches include CrestApps.OrchardCore.Telephony, CrestApps.OrchardCore.Telephony.Azure, ITelephonyProvider, TelephonyCapabilities, ITelephonyService, TelephonyHub, TelephonySettings, IIncomingCallContextProvider, IDialDestinationPolicy, IRecordingMediaStore, and ProviderVoiceEvent.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Telephony - Prompt Templates

## Configure Provider-Agnostic Telephony

You are an Orchard Core expert. Generate accurate code, configuration, recipes, and administration guidance for the CrestApps Telephony module. It supplies a provider-neutral call-control layer, a SignalR hub, call history, an internal extension registry, encrypted recording storage, and an optional floating soft phone. It does not provide SMS; use Orchard Core SMS (`orchardcore-sms`) or the Omnichannel skills for text messaging.

In this module, **provider** means the telephony backend adapter (Telnyx, Asterisk, or a custom PBX or carrier integration), not the phone company in the billing sense.

### Guidelines

- Install `CrestApps.OrchardCore.Telephony` (and any provider package) in the web/startup project.
- Enable `CrestApps.OrchardCore.Telephony` for the runtime services, the hub, call history, extensions, and the **Settings → Communication → Telephony** screen. There is no separate administration feature in 3.0.
- The core feature depends on `OrchardCore.Users` and `OrchardCore.SignalR` (the Orchard Core framework SignalR module; see `orchardcore-signalr`).
- Enable `CrestApps.OrchardCore.Telephony.SoftPhone` only when the floating widget or the front-end **Soft Phone** widget is needed. Enable `CrestApps.OrchardCore.Telephony.SoftPhone.Extension` for the standalone `/softphone` page used by the browser extension and desktop app.
- Enable a provider feature (`orchardcore-telephony-telnyx`, `orchardcore-telephony-asterisk`, or your own) and select it as the default provider.
- Call `ITelephonyService` from application code; never call a provider directly, because the service enforces capability, contract, and dial-destination checks.
- Implement `ITelephonyProvider` plus only the capability contracts the backend really supports, and advertise exactly those `TelephonyCapabilities`.
- Read `TelephonyProviderOptions` through `IOptionsMonitor<TelephonyProviderOptions>` and call `IOptionsUpdateNotifier.RequestUpdate<TelephonyProviderOptions>()` after a provider's settings change.
- Keep provider credentials and user tokens server-side; never send an API key or OAuth refresh token to browser code.
- Map hubs with `SignalRHubRoutes.GetHubPath<T>()` and generate client URLs with `Html.SignalRHubUrl<T>()`; `HubRouteManager` no longer exists.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

### Feature Overview

| Feature | Feature ID | Purpose |
|---|---|---|
| Telephony | `CrestApps.OrchardCore.Telephony` | Provider resolver, services, `TelephonyHub`, OAuth routes, settings screen and menu, call history, extension registry, recording store |
| Telephony Soft Phone Core | `CrestApps.OrchardCore.Telephony.SoftPhone.Core` | Shared soft phone presenter, resources and `softphone/dial` endpoint. Enabled by dependency only |
| Telephony Soft Phone | `CrestApps.OrchardCore.Telephony.SoftPhone` | Floating soft phone on admin pages plus a placeable **Soft Phone** widget. Depends on `OrchardCore.Widgets` |
| Telephony Soft Phone Extension | `CrestApps.OrchardCore.Telephony.SoftPhone.Extension` | Standalone `/softphone` page and `GET /softphone/extension-config` for the CrestApps Soft Phone browser extension and desktop app |
| Telephony - Azure Blob Storage | `CrestApps.OrchardCore.Telephony.Azure` | Stores the encrypted recording and voice-media store in Azure Blob Storage. See [references/recording-storage.md](references/recording-storage.md) |

Declare a dependency on Telephony with `TelephonyConstants.Feature.Area`.

### Enable the Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Telephony",
        "CrestApps.OrchardCore.Telephony.SoftPhone"
      ],
      "disable": []
    }
  ]
}
```

## Architecture

```text
Browser command --SignalR--> TelephonyHub --> ITelephonyService --> ITelephonyProviderResolver --> provider
Provider webhook or stream --> ProviderVoiceEvent --> INormalizedVoiceEventIngestor --> handlers
                                                         |
                                                         +--> CallStateChanged pushed to the soft phone
```

- `CrestApps.OrchardCore.Telephony.Abstractions` holds the provider contracts, request and result models, `TelephonyProviderOptions`, `TelephonySettings`, `TelephonyPermissions`, and `TelephonyConstants`. A provider module depends only on this package (plus `CrestApps.OrchardCore.ContactCenter.Abstractions` when it adapts Contact Center).
- `CrestApps.OrchardCore.Telephony.Core` holds the normalized voice-event pipeline (`INormalizedVoiceEventIngestor`, `INormalizedVoiceEventHandler`), the extension registry services, and the default dial and transfer policies.
- Command results only acknowledge a request. Live state comes from provider events normalized into `ProviderVoiceEvent`, so the server stays the source of truth.

### The Provider Contract

`ITelephonyProvider` declares only `Name` (a `LocalizedString`) and `Capabilities`. Each operation lives on its own contract. `TelephonyCapabilityContracts` maps every flag to the contract that must back it, and `DefaultTelephonyService` refuses an operation unless the provider both advertises the flag and implements the contract.

| Capability | Contract | Methods |
|---|---|---|
| `Dial`, `Hangup` | `ITelephonyCallControlProvider` | `DialAsync`, `HangupAsync` |
| `ReceiveCalls` | `ITelephonyInboundCallProvider` | `AnswerAsync`, `RejectAsync` |
| `Hold`, `Resume` | `ITelephonyHoldProvider` | `HoldAsync`, `ResumeAsync` |
| `Mute` | `ITelephonyMuteProvider` | `MuteAsync`, `UnmuteAsync` |
| `Transfer` | `ITelephonyTransferProvider` | `TransferAsync` |
| `AttendedTransfer` | `ITelephonyAttendedTransferProvider` | `StartAttendedTransferAsync` |
| `Merge` | `ITelephonyConferenceProvider` | `MergeAsync` |
| `SendDigits` | `ITelephonyDtmfProvider` | `SendDigitsAsync` |
| `Voicemail` | `ITelephonyVoicemailProvider` | `SendToVoicemailAsync` |
| `Directory` | `ITelephonyDirectoryProvider` | `GetDirectoryAsync` |
| `ExtensionDial`, `ExtensionConference` | `ITelephonyExtensionDialProvider` | `DialExtensionAsync`, `AddExtensionToConferenceAsync` |
| `BridgedDial` | `ITelephonyCallControlProvider` | `DialAsync` with `RequestMetadata.SoftPhoneCredentialId` set |

Optional contracts that are not capability-gated:

| Contract | Purpose |
|---|---|
| `ITelephonySoftPhoneCredentialsProvider` | `GetClientCredentialsAsync` issues browser bootstrap credentials |
| `ITelephonyCallStateProvider` | `GetCallStateAsync` reports provider-authoritative call state for reconciliation |
| `ITelephonyAudioProvider` | `AudioCapabilities` (`Browser`, `ExternalDevice`), `ConfiguredAudioMode`, `BrowserMediaAdapterName` |
| `ITelephonyConsultTransferProvider` | `GetConsultAsync`, `CompleteConsultAsync`, `CancelConsultAsync` for a transfer carried out as a consult call |
| `ITelephonyAuthenticationProvider` | Per-user OAuth for providers that need it (the built-in providers do not) |

Every operation returns `TelephonyResult` with `Succeeded`, `OutcomeUnknown`, `Error`, `ErrorCode`, and `Call`. Use `TelephonyResult.Unknown(...)` when a provider cannot confirm the outcome (for example a timeout after the request was sent) so callers never retry an operation that may have succeeded.

For the full provider walkthrough, event normalization rules, and transport models, see [references/custom-providers.md](references/custom-providers.md).

## Site Settings

Settings live under **Settings → Communication → Telephony** (group `TelephonyConstants.SettingsGroupId`, value `telephony`) and require `ManageTelephonySettings`. The **Soft Phone** tab selects the default provider and lists **Allowed short codes**; every enabled provider adds its own tab through a `SiteDisplayDriver<TSettings>` whose `SettingsGroupId` is `TelephonyConstants.SettingsGroupId`.

- Enabling the only configured provider makes it the default. Disabling the default provider clears `TelephonySettings.DefaultProviderName` and the soft phone is disabled until a new default is chosen.
- Settings export through the **Telephony Settings** and **Soft Phone Widget Settings** deployment steps and import through the built-in `Settings` recipe step. Provider credentials travel through each provider's own step.

```json
{
  "steps": [
    {
      "name": "Settings",
      "TelephonySettings": {
        "DefaultProviderName": "Telnyx",
        "AllowedShortCodes": ["611"]
      },
      "SoftPhoneWidgetSettings": {
        "Enabled": true,
        "DisplayOnAdmin": true,
        "EnableDiagnostics": false,
        "AccentColor": "#2f6fed",
        "RecentCallsCount": 30,
        "DefaultCountryCode": "US"
      }
    }
  ]
}
```

### Dial Destination Safety

Every dial and transfer path (keypad, transfer field, extension field, Contact Center dial and transfer) is checked by `IDialDestinationPolicy.Evaluate(address, DialDestinationContext)` before a provider is reached. The default policy refuses emergency short codes (`911`, `112`, `999`, `000` and others), premium-rate prefixes (`1900`, `1976`, `4470`), and anything that is neither a dialable international number nor an allowed short code. **Allowed short codes** can never open an emergency code. This is a refusal to originate, not an emergency-calling capability.

`ITransferTargetPolicy` decides what an agent may transfer to. Contact Center Voice replaces it with a curated-destination policy, and replaces `IOutboundLineResolver` so each user dials out from an assigned line (see `orchardcore-contact-center`).

## Soft Phone

With `CrestApps.OrchardCore.Telephony.SoftPhone` enabled, the **Soft Phone** tab configures `SoftPhoneWidgetSettings`:

| Setting | Property | Default |
|---|---|---|
| Enable the soft phone (kill switch) | `Enabled` | `true` |
| Show on the admin dashboard | `DisplayOnAdmin` | `true` |
| Enable the diagnostics tab | `EnableDiagnostics` | `false` (or add `?diag=1` to a URL for one session) |
| Default country | `DefaultCountryCode` | Automatic (culture, then browser locale, then United States) |
| Accent color | `AccentColor` | `#2f6fed` |
| Recent calls to display | `RecentCallsCount` | `30` (1 to 200) |

- The widget renders only for users with `UseTelephonySoftPhone` (display name "Use the telephony soft phone").
- Admin pages get the widget in the `Footer` zone automatically. For the front end, add the **Soft Phone** widget from **Design → Widgets**, optionally on an authenticated layer.
- To render it in a theme without a widget, render the `SoftPhoneWidget` shape through `IDisplayManager<SoftPhoneWidget>` and register the resources. `telephony-soft-phone` depends on `signalr`, `telephony-client`, and `intl-tel-input`, so ResourceManager resolves them.

```cshtml
<style asp-name="telephony-soft-phone" at="Head"></style>
<script asp-name="telephony-soft-phone" at="Foot"></script>
<script asp-name="telephony-phone-field" at="Foot"></script>
```

- The keypad normalizes a valid national number to E.164 using intl-tel-input so outbound compliance screening (`orchardcore-dnc-registry`, `orchardcore-phone-numbers`) sees a canonical number. The **Dial extension** toggle sends `DialRequest.IsExtension` and skips that screening.
- The Phone Field editor and display expose a `data-phone-dial` placeholder; the soft phone attaches a **Call with the soft phone** button that posts to `softphone/dial`. With the Extension feature, `SoftPhoneExtensionDialerFilter` enables the button on admin pages and the hub sends `DialRequested` to the standalone phone.
- Modules add footer tabs with a `DisplayDriver<SoftPhoneWidget>`; Contact Center adds the **Work** tab this way.
- Browser audio providers register `window.telephonySoftPhone.mediaAdapters[adapterName]`; the server provider must implement `ITelephonyAudioProvider`, advertise `Browser`, and return the same `BrowserMediaAdapterName`.

### Hub and Client Events

`TelephonyHub` authorizes every method against `UseTelephonySoftPhone` and joins a tenant-qualified user group. Main methods include `Dial`, `Hangup`, `Hold`, `Resume`, `Mute`, `Unmute`, `Transfer`, `Merge`, `SendDigits`, `Answer`, `Reject`, `Voicemail`, `DialExtension`, `AddExtensionToConference`, `GetConsult`, `CompleteConsult`, `CancelConsult`, `GetActiveCall`, `GetActiveCalls`, `GetInteractions`, `GetExtensionDirectory`, `GetDirectory`, `GetCapabilities`, `GetCredentials`, and `GetConnectionStatus`.

`ITelephonyClient` pushes `CallStateChanged`, `IncomingCall`, `IncomingCallAnswered`, `CredentialsIssued`, `RemotePartyChanged`, `DialRequested`, and `ReceiveError`. Providers report ringback state through `ITelephonyRemotePartyNotifier.NotifyAsync(providerName, providerCallId, RemotePartyState)`, which drives the `RemotePartyChanged` message and the soft phone's ringback tone.

## Incoming Calls

Telephony owns the incoming-call modal and media controls; it does not decide who receives a call. An orchestration module resolves the user and calls `IIncomingCallDispatcher.DispatchAsync(userId, call)`. Other modules add cards to the modal with `IIncomingCallContextProvider`:

```csharp
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;

namespace MyCompany.OrchardCore.Crm;

public sealed class CustomerIncomingCallContextProvider : IIncomingCallContextProvider
{
    public Task ContributeAsync(IncomingCallContributionContext context, CancellationToken cancellationToken = default)
    {
        context.Cards.Add(new IncomingCallCard
        {
            Title = "Jane Doe",
            Subtitle = context.Call.From,
            Icon = "fa-solid fa-user",
            Url = "/Admin/Contents/ContentItems/4k9.../Edit",
        });

        return Task.CompletedTask;
    }
}
```

Register it with `services.AddScoped<IIncomingCallContextProvider, CustomerIncomingCallContextProvider>()`. Each card renders **Answer & open** and **Open** actions. Setting `context.Properties["acceptUrl"]` and `context.Properties["declineUrl"]` makes the modal post to those URLs as the agent answers or ignores the call.

## Call History

`TelephonyCallHistoryVoiceEventHandler` and the hub persist every call through `ITelephonyInteractionStore`, indexed by `TelephonyInteractionIndex` (provider interaction id, provider name, user, direction, outcome, timestamps, duration). History survives removal of a provider. Updates use optimistic concurrency. Active-call restoration calls `GetActiveCalls`, which validates each in-progress record against `ITelephonyCallStateProvider` and deletes confirmed orphans.

## Extensions

The tenant-scoped extension registry maps a dialed number to an Orchard user, managed under **Interaction Center → Management → Extensions** with `ManageTelephonyExtensions`. The list is an Orchard Core `AdminList` named `TelephonyExtensions` with `Name`, `Number`, and `User` grid columns, so other modules can add columns through `IAdminListColumnsProvider` (see `orchardcore-admin-lists`). Extensions import through the `TelephonyExtension` recipe step. Details, the provider contract, and the recipe shape are in [references/extension-dialing.md](references/extension-dialing.md).

## Recording Storage

`IRecordingMediaStore` holds Contact Center call recordings and voice-media master copies, encrypted with the tenant data-protection key, by default under `App_Data/Sites/{tenant}/RecordingMedia`. Writes stream from `RecordingMediaWriteRequest.Content` (a `Stream`); `OpenReadAsync` returns a seekable decrypting stream. Enable `CrestApps.OrchardCore.Telephony.Azure` to move the ciphertext to Azure Blob Storage. See [references/recording-storage.md](references/recording-storage.md).

## Configuration Sections

| Section | Keys |
|---|---|
| `CrestApps_Telephony:Commands` | `Timeout` (default `00:00:10`, 1 second to 2 minutes). The deadline for one PBX command |
| `CrestApps_Telephony:Coordination` | `InteractionLockTimeout` (`00:00:05`), `InteractionLockExpiration` (`00:02:00`), `NewInteractionGracePeriod` (`00:00:15`), `ClientRecordedCallMaxAge` (`04:00:00`), `ClientRecordedCallSilenceTimeout` (`00:05:00`), `TokenRefreshLockTimeout` (`00:00:10`), `TokenRefreshLockExpiration` (`00:01:00`) |
| `CrestApps:Telephony:AzureRecordings` | `ConnectionString`, `ContainerName`, `BasePath`, `CreateContainer` |

```json
{
  "OrchardCore": {
    "CrestApps_Telephony": {
      "Commands": {
        "Timeout": "00:00:15"
      },
      "Coordination": {
        "InteractionLockTimeout": "00:00:10"
      }
    }
  }
}
```

Environment variables keep the single underscore, for example `OrchardCore__CrestApps_Telephony__Commands__Timeout=00:00:15`. Options are validated on tenant activation, and each lease expiration must exceed its lock timeout.

## Permissions

| Permission | Name | Use |
|---|---|---|
| `TelephonyPermissions.ManageTelephonySettings` | `ManageTelephonySettings` | Telephony settings screen and every provider tab |
| `TelephonyPermissions.UseSoftPhone` | `UseTelephonySoftPhone` | Soft phone widget, hub, `/softphone` page |
| `TelephonyPermissions.ManageExtensions` | `ManageTelephonyExtensions` | Extension registry |

## Upgrading from 2.x

- Remove `CrestApps.OrchardCore.Telephony.Admin` from recipes and deployment plans; the settings screen and menu are part of `CrestApps.OrchardCore.Telephony`. `TelephonyConstants.Feature.Admin`, `TelephonyAdminStartup`, and `AsteriskAdminStartup` are removed; depend on `TelephonyConstants.Feature.Area`.
- The Dialpad provider (`CrestApps.OrchardCore.Dialpad`) is removed. Move to Telnyx (`orchardcore-telephony-telnyx`), Asterisk (`orchardcore-telephony-asterisk`), or a custom provider, and update `TelephonySettings.DefaultProviderName`.
- Read `TelephonyProviderOptions` (and `TelnyxOptions`) through `IOptionsMonitor<T>.CurrentValue` or `IOptionsSnapshot<T>`. `IOptions<T>` keeps the first value and never sees a settings change. Custom providers call `IOptionsUpdateNotifier.RequestUpdate<TelephonyProviderOptions>()` after saving settings.
- `ITelephonyAuthenticationProvider.RevokeTokensAsync` and `ITelephonyAuthenticationService.DisconnectAsync` return `Task<TelephonyResult>`.
- `RecordingMediaWriteRequest.Content` is a `Stream`, not `byte[]`; `InteractionId` is `null` for voice-media master copies.
- Custom `ITelephonyUserAccessor` implementations add `ReloadCurrentUserAsync()` and `PersistCurrentUserAsync(Func<IUser, bool> mutate)`.
- `IVoiceMediaProvisioner` gains `GetAvailabilityAsync(mediaReference, cancellationToken)` returning `VoiceMediaAvailability`; the default answers `Unknown`, so override it to let a lost provider copy be re-uploaded from the master copy.
- The CrestApps SignalR module is deprecated in favor of `OrchardCore.SignalR`; replace `HubRouteManager` with `SignalRHubRoutes.GetHubPath<T>()` and `Html.SignalRHubUrl<T>()`.
- The extension list renders through the Orchard Core `AdminList` shape; whole-row template overrides only apply to the List layout. Use `SummaryAdminGrid` placement for the Grid layout.

## Troubleshooting

| Symptom | Check |
|---|---|
| Widget says no provider is configured | Enable a provider, configure it, and select it as the default on the **Soft Phone** tab |
| Widget never renders | Enable `CrestApps.OrchardCore.Telephony.SoftPhone`, keep **Enable the soft phone** on, and grant `UseTelephonySoftPhone` |
| A control is missing | The provider must advertise the `TelephonyCapabilities` flag and implement the matching contract |
| Settings change ignored until restart | Code reads `IOptions<TelephonyProviderOptions>`; switch to `IOptionsMonitor` |
| Dial refused for a short code | Add it to **Allowed short codes** (emergency codes can never be allowed) |
| Hub URL wrong behind a tenant prefix | Use `Html.SignalRHubUrl<TelephonyHub>()` |
| Call stays connected after the far end hung up | The provider must normalize its events into `ProviderVoiceEvent` and implement `ITelephonyCallStateProvider` |

## Related Skills

- `orchardcore-telephony-telnyx` and `orchardcore-telephony-asterisk` for the built-in providers.
- `orchardcore-contact-center` for queues, routing, the dialer, recording governance, and Contact Center voice provider adapters.
- `orchardcore-signalr`, `orchardcore-websockets`, `orchardcore-phone-numbers`, `orchardcore-dnc-registry`.
- `orchardcore-admin-lists`, `orchardcore-site-settings`, `orchardcore-configuration` for the framework mechanics.
