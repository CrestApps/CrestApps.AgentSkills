---
name: orchardcore-omnichannel-event-grid
description: Skill for receiving Azure Event Grid events in Orchard Core with the CrestApps Omnichannel - Azure Event Grid feature CrestApps.OrchardCore.Omnichannel.EventGrid. Covers the anonymous POST api/azure/webhook/eventgrid endpoint, aeg-sas-key and Microsoft Entra bearer-token authentication through EventGridOptions, the Event Grid schema requirement, the subscription validation handshake, the 1 MB limit, routing of Azure Communication Services SMSReceived texts onto SmsReceived with de-duplication, stored delivery reports and custom events dispatched to IOmnichannelEventHandler, and troubleshooting. Use this skill when requests mention Omnichannel Event Grid, Azure Event Grid webhooks, ACS inbound SMS, aeg-sas-key, or Event Grid subscription validation. Strong matches include EventGridOptions, AzureEventGridEndpoint, EventGridEventMapper, EventGridEventTypes, and IOmnichannelEventHandler.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Omnichannel Azure Event Grid

## Configure Event Grid Ingestion

You are an Orchard Core expert. Configure a secure Azure Event Grid webhook for
the CrestApps Omnichannel pipeline, most commonly to receive Azure
Communication Services (ACS) inbound texts.

### Guidelines

- Install `CrestApps.OrchardCore.Omnichannel.EventGrid` in the web/startup
  project and enable `CrestApps.OrchardCore.Omnichannel.EventGrid` (it depends
  only on `CrestApps.OrchardCore.Omnichannel`).
- The endpoint `POST ~/api/azure/webhook/eventgrid` is anonymous and disables
  antiforgery; every request, including the validation handshake, must pass the
  SAS key or bearer-token check or it gets `401 Unauthorized`.
- Only the **Event Grid event schema** is accepted. A CloudEvents subscription
  is answered with `400 Bad Request`.
- Keep the SAS key and Entra settings in secrets or environment configuration,
  never in recipes or source.
- For ACS, also enable `CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices`
  for the outbound providers (see `orchardcore-omnichannel`).

## Enable via Recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Omnichannel.EventGrid"
      ],
      "disable": []
    }
  ]
}
```

## Configure Authentication

`EventGridOptions` is bound from the tenant shell configuration section
`CrestApps:Omnichannel:EventGrid`. In the application's root `appsettings.json`
the section sits under the `OrchardCore` key:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Omnichannel": {
        "EventGrid": {
          "EventGridSasKey": "set-from-secrets",
          "AADIssuer": "https://sts.windows.net/<tenant-id>/",
          "AADAudience": "api://<application-id>",
          "AADMetadataAddress": "https://login.microsoftonline.com/<tenant-id>/.well-known/openid-configuration"
        }
      }
    }
  }
}
```

A tenant's own `App_Data/Sites/{tenant}/appsettings.json` uses the same keys
without the `OrchardCore` wrapper.

| `EventGridOptions` property | Use |
|---|---|
| `EventGridSasKey` | Fixed-time comparison with the `aeg-sas-key` request header. |
| `AADIssuer`, `AADAudience`, `AADMetadataAddress` | Validate a Microsoft Entra bearer token. All three are required; with only some set, a warning is logged and bearer tokens are rejected (SAS keeps working). |

The SAS key is checked first; a valid bearer token authorizes the request when
the SAS check fails.

## Create the Subscription

1. Deploy the site with the feature enabled and a public HTTPS address.
2. In Azure, add an event subscription to the topic or system topic (for ACS,
   the Communication Services resource's SMS events) with **Webhook** delivery
   and **Event Grid Schema**.
3. Target `https://<host>/api/azure/webhook/eventgrid`.
4. For SAS delivery, add a custom delivery property (header) named
   `aeg-sas-key` whose value equals `EventGridSasKey`; Event Grid does not send
   it on its own. For Entra delivery, configure token delivery for the same
   issuer and audience.
5. Configure authentication before creating the subscription, because the
   `Microsoft.EventGrid.SubscriptionValidationEvent` handshake is authenticated
   too. The endpoint echoes `{ "validationResponse": "<code>" }`; a validation
   event without a code gets `400`.

## What Happens to Each Event

| Event type | Behavior |
|---|---|
| `Microsoft.Communication.SMSReceived` | Mapped onto the `SMS` channel and raised as `OmnichannelConstants.Events.SmsReceived`, the same event Twilio and Telnyx raise, so SMS Automation and the SMS Messaging Channel both act on it. |
| `Microsoft.Communication.SMSDeliveryReportReceived` | Stored as an inbound message and passed to handlers; the sent message's delivery status is not updated. |
| Any other type | Stored as an inbound message (channel `Unknown` unless the data names one) and passed to every `IOmnichannelEventHandler` with its raw type. |

ACS inbound text fields map as `from` to customer address, `to` to service
address (which selects the SMS omnichannel address, so the number must exist as
one), `message` to content, `messageId` to provider message id (event id when
missing), and `receivedTimestamp` to the created time.

- Inbound texts are answered `200 OK` immediately and processed in the
  background, because an automated reply can outlast Event Grid's 30-second
  wait. A redelivered `messageId` is ignored. Storage concurrency conflicts are
  retried up to three attempts.
- A text without `from` or `to` is stored like an unmapped event with a warning.
- Payloads over 1 MB get `413 Payload Too Large`; malformed JSON gets `400`.

## Handle Custom Events

Unmapped events reach every registered `IOmnichannelEventHandler`. Filter by the
raw event type and validate the provider payload before acting.

```csharp
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace MyCompany.OrchardCore.Communications;

public sealed class ContosoEventGridHandler : IOmnichannelEventHandler
{
    public Task HandleAsync(OmnichannelEvent omnichannelEvent, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(omnichannelEvent.EventType, "Contoso.Orders.Shipped", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        // omnichannelEvent.Data holds the raw Event Grid data.
        return Task.CompletedTask;
    }
}
```

Register it with `services.AddScoped<IOmnichannelEventHandler, ContosoEventGridHandler>();`
and keep it idempotent because Event Grid delivers at least once.

## Troubleshooting

The endpoint logs one line per event (type and id), then what it did with it:
mapped to `SmsReceived` (with provider message id and length), delivery report
stored, redelivery ignored, concurrency retry (warning, error only when every
attempt fails), mapping failure (warning), or no mapping (debug). Logs never
include message text or phone numbers. Watch for `401`, `400` and `413` on the
route.

## Upgrading from 2.x

- The route moved from `Omnichannel/webhook/AzureEventGrid` to
  `api/azure/webhook/eventgrid`; update every subscription.
- ACS `SMSReceived` events are now routed to the SMS channel instead of only
  being stored, so remove custom handlers that duplicated that mapping.
