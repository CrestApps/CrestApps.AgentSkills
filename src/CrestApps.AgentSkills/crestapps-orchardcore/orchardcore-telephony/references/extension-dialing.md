# Internal Extension Dialing

Extension dialing lets one on-platform user call another by a short number (for example `1001`) and add an extension to an active call as a conference participant. It is capability-gated like every other soft-phone operation: a provider that cannot connect two of its own registered endpoints does not advertise it, the control is hidden, and the server fails closed.

## The Extension Registry

- A tenant-scoped, provider-neutral map from a dialed number to an Orchard user (`TelephonyExtension`, indexed by `TelephonyExtensionIndex`). Providers translate the resolved user into their own live endpoint, so extensions survive a provider switch.
- Managed under **Interaction Center → Management → Extensions** (admin routes `telephony/extensions`, `telephony/extension/create`) with the `ManageTelephonyExtensions` permission.
- Fields: **Extension number** (unique per tenant), **User**, optional **Display name**. The entry name is built from the number and display name so list search matches either. There is no enabled state; delete an extension to stop it ringing.
- Services: `ITelephonyExtensionManager`, `ITelephonyExtensionStore`, `ITelephonyExtensionResolver`, and `ITelephonyExtensionDirectory`.
- The list renders through the Orchard Core `AdminList` shape with name `TelephonyExtensions` and grid columns `Name`, `Number`, `User`, plus select and actions columns. Add a column with an `IAdminListColumnsProvider` for that list name (see `orchardcore-admin-lists`).

### Display Names

Wherever the soft phone shows an extension it also shows who it rings, resolved on the server in this order: the extension's **Display name** (when it differs from the user name), the user's display name as the site shows users (the CrestApps Users display-name settings, see `orchardcore-crestapps-users`), the user name, then the bare number.

The soft phone reads the directory once on connect through the hub's `GetExtensionDirectory` and refreshes it after five minutes. A user's own extensions are left out and refused as a target ("That's your own extension.") by the soft phone and the server.

## Placing an Extension Call

```text
Soft phone (Dial extension) --> TelephonyHub.DialExtension
        --> ITelephonyService resolves the extension to the target user (fails closed when unknown)
        --> ITelephonyExtensionDialProvider (capability and contract checked together)
        --> provider rings the caller, dials the target endpoint, and bridges the legs
```

- The value is sent verbatim, not normalized to E.164, and skips outbound compliance screening.
- The call is always resolved and bridged server-side; the browser does not know the target's ephemeral endpoint.
- If the target does not answer in the ring window, the caller reaches the target user's voicemail, ingested through the saved-recording pipeline into that user's soft-phone **Voicemail** tab.
- `AddExtensionToConference` rings the resolved target and joins their leg to the existing conversation. Hanging up the extension leg of a conference with the `conferenceParticipant` request metadata ends only that colleague's leg, and the result carries `participantLeft`.
- A transfer typed in extension mode sets `TransferRequest.IsExtension`; `DefaultTelephonyService` resolves `TransferRequest.TargetUserId` and fails closed when nobody owns the extension.

## Provider Contract

| Operation | Capability | Method |
|---|---|---|
| Call an extension | `ExtensionDial` | `DialExtensionAsync(ExtensionDialRequest)` |
| Add an extension to a conference | `ExtensionConference` | `AddExtensionToConferenceAsync(ExtensionConferenceRequest)` |

`ExtensionDialRequest` carries `Extension`, `TargetUserId`, `TargetDisplayName`, `CallerUserId`, `CallerDisplayName`, `From`, and `Metadata`. The service resolves the target user before the provider is called; the provider only turns that user into its live endpoint.

| Provider | Support |
|---|---|
| Telnyx | Supported. Both legs are Telnyx SIP-over-WebSocket registrations bridged server-side; conference-add joins a conference formed from the active call |
| Asterisk | Not advertised yet |

## Recipe

Extensions export through the **Telephony Extensions** deployment step and import through the `TelephonyExtension` recipe step. Each entry finds its user by `UserName` in the destination; an unknown user name or a number another extension already holds is reported and skipped.

```json
{
  "steps": [
    {
      "name": "TelephonyExtension",
      "Extensions": [
        {
          "ItemId": "9vt3k7x1m5q2c8z4b6n0r3h7y",
          "Number": "1001",
          "UserName": "agent.smith",
          "DisplayName": "Agent Smith"
        }
      ]
    }
  ]
}
```

When `Name` is omitted it is built from the number and display name; when `DisplayName` is omitted the user name is shown.
