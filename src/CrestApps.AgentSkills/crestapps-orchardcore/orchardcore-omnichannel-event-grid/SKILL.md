---
name: orchardcore-omnichannel-event-grid
description: Skill for receiving and dispatching Omnichannel events through Azure Event Grid in Orchard Core using CrestApps modules. Covers webhook authentication, Event Grid subscription validation, inbound message persistence, event handler dispatch, and tenant configuration. Use this skill when requests mention Orchard Core Omnichannel Azure Event Grid, Event Grid webhooks, Event Grid subscription validation, aeg-sas-key, Microsoft Entra bearer tokens, or inbound omnichannel event dispatch. Strong matches include work with CrestApps.OrchardCore.Omnichannel.EventGrid, EventGridOptions, AzureEventGridEndpoint, IOmnichannelEventHandler, OmnichannelEvent, and OmnichannelMessage.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Omnichannel Azure Event Grid

## Configure Event Grid Ingestion

You are an Orchard Core expert. Configure secure Azure Event Grid ingestion for
the CrestApps Omnichannel pipeline, and implement handlers only for the events
your application understands.

### Guidelines

- Install `CrestApps.OrchardCore.Omnichannel.EventGrid` in the web/startup project.
- Enable the base Omnichannel feature and the exact Event Grid feature ID
  `CrestApps.OrchardCore.Omnichannel.EventGrid`.
- The module exposes an anonymous `POST` endpoint at
  `Omnichannel/webhook/AzureEventGrid`; it disables antiforgery because Azure
  Event Grid cannot supply an Orchard antiforgery token.
- Prefer a valid Microsoft Entra bearer token for Event Grid delivery. A configured
  `aeg-sas-key` request header is a supported shared-secret alternative. Do not
  expose an unauthenticated public webhook.
- Configure all three AAD settings when using bearer authentication. A partial
  AAD configuration cannot validate a token and the request is rejected.
- Keep SAS keys and token configuration in user secrets, Key Vault, or
  environment-specific configuration. Never place a production secret in a
  recipe or source file.
- The endpoint accepts Event Grid event arrays and rejects request bodies larger
  than 1 MiB with HTTP 413.
- The module saves each normal event as an inbound `OmnichannelMessage` in the
  `Omnichannel` YesSql collection, then invokes every registered
  `IOmnichannelEventHandler`.
- Event Grid payload normalization is deliberately generic. It checks common
  data names such as `from`, `to`, `content`, `channel`, and `timestamp`; a
  channel-specific handler should interpret provider-specific data.

## Feature and Package

| Item | Value |
|---|---|
| NuGet package | `CrestApps.OrchardCore.Omnichannel.EventGrid` |
| Base feature | `CrestApps.OrchardCore.Omnichannel` |
| Event Grid feature | `CrestApps.OrchardCore.Omnichannel.EventGrid` |
| Webhook route | `POST /Omnichannel/webhook/AzureEventGrid` |

Install the package in the web/startup project, then enable it with a recipe:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Omnichannel",
        "CrestApps.OrchardCore.Omnichannel.EventGrid"
      ],
      "disable": []
    }
  ]
}
```

## Configure Webhook Authentication

The feature binds `EventGridOptions` from
`CrestApps:Omnichannel:EventGrid` in tenant configuration.

```json
{
  "CrestApps": {
    "Omnichannel": {
      "EventGrid": {
        "EventGridSasKey": "${EVENT_GRID_SAS_KEY}",
        "AADIssuer": "https://sts.windows.net/<tenant-id>/",
        "AADAudience": "api://<application-id>",
        "AADMetadataAddress": "https://login.microsoftonline.com/<tenant-id>/.well-known/openid-configuration"
      }
    }
  }
}
```

| `EventGridOptions` property | Use |
|---|---|
| `EventGridSasKey` | Fixed-time comparison against the `aeg-sas-key` header |
| `AADIssuer` | Expected issuer for bearer token validation |
| `AADAudience` | Expected audience for bearer token validation |
| `AADMetadataAddress` | OpenID Connect metadata source for signing keys |

Prefer Microsoft Entra delivery for production:

1. For Entra delivery, set `AADIssuer`, `AADAudience`, and
   `AADMetadataAddress`, then configure Event Grid token delivery to match.
2. For shared-key delivery, set `EventGridSasKey` and configure Event Grid to
   send the same value in `aeg-sas-key`.
3. A request is authorized as soon as either configured mechanism succeeds.

## Create the Event Subscription

1. Enable the feature and deploy the public HTTPS endpoint first.
2. In Azure Event Grid, create an event subscription with **Webhook** delivery.
3. Set its target to
   `https://<host>/Omnichannel/webhook/AzureEventGrid`.
4. Configure the selected authentication mechanism.
5. Let Event Grid send its validation event before publishing application
   events.
6. Monitor HTTP 401, HTTP 400, and HTTP 413 responses in the application logs.

### Subscription Validation Flow

Event Grid sends an event whose type is
`Microsoft.EventGrid.SubscriptionValidationEvent`. The endpoint reads
`SubscriptionValidationEventData.ValidationCode` and responds with:

```json
{
  "validationResponse": "<validation-code>"
}
```

Do not register a custom `IOmnichannelEventHandler` to answer this handshake.
The endpoint handles it before persistence and event-handler dispatch.

## Inbound Event Processing

For every non-validation `EventGridEvent`, `AzureEventGridEndpoint`:

1. Creates an inbound `OmnichannelMessage`.
2. Reads common fields from the event data where they exist.
3. Falls back to preserving raw event JSON as message content when parsing
   fails.
4. Stores the message in `OmnichannelConstants.CollectionName`.
5. Creates an `OmnichannelEvent` with the Event Grid ID, event type, subject,
   binary data, and stored message.
6. Invokes registered `IOmnichannelEventHandler` implementations.

This endpoint does not map a provider event to a particular channel processor.
Use an event handler to recognize event types and validate the provider payload
before applying business behavior.

## Implement a Channel-Specific Handler

Implement `IOmnichannelEventHandler` in the consuming module and ignore events
outside its scope. Keep the handler `sealed` and use a file-scoped namespace.

```csharp
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace MyCompany.OrchardCore.Communications;

public sealed class ProviderEventHandler : IOmnichannelEventHandler
{
    public Task HandleAsync(OmnichannelEvent omnichannelEvent, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(omnichannelEvent.EventType, "Contoso.SmsReceived", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        // Validate and process the provider-specific payload here.
        return Task.CompletedTask;
    }
}
```

Register it with the appropriate Orchard Core lifetime in the consuming
feature. Event Grid invokes all registered handlers, so handlers must filter by
event type, channel, direction, or provider data before acting.

## Operational and Security Checklist

- Use HTTPS and a publicly reachable route for the Event Grid subscription.
- Do not treat successful Event Grid authentication as provider-payload
  validation when multiple publishers share a topic.
- Log only non-sensitive identifiers; do not log body content, SAS keys, or
  bearer tokens.
- Keep event handlers idempotent because Event Grid delivery can be retried.
- Treat `Channel = "Unknown"` as a signal that provider data needs explicit
  mapping in a handler.
- Keep the payload under 1 MiB or store large content externally and publish a
  reference event.
- Test subscription validation, an authorized application event, a missing
  credential, and malformed JSON before production rollout.
