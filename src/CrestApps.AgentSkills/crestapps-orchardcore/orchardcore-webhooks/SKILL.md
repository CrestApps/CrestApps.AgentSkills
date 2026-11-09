---
name: orchardcore-webhooks
description: Skill for the CrestApps Orchard Core Webhooks building block (CrestApps.OrchardCore.Webhooks), used in both directions. Covers outbound signed webhooks with IWebhookPublisher, declaring events with AddWebhookEvent or IWebhookEventProvider, signing, the retry schedule, AddWebhookSender and IWebhookSender for posting to a URL of your own, the WebhookSubscriptions recipe step and Webhook subscriptions deployment step, WebhookDeliveryRetentionOptions for delivery log retention, how Commerce Webhooks and AI Webhooks build on it, and the Provider Webhook Inbox (CrestApps.OrchardCore.ContactCenter.ProviderInbox) for durable, de-duplicated inbound provider callbacks with IProviderWebhookInbox, IProviderWebhookInboxHandler, WebhookHandlerReplaySafety and the shared signature validators. Use this skill when requests mention Orchard Core webhooks, webhook subscriptions, publishing a webhook event, webhook delivery retries or retention, receiving provider callbacks durably, or the webhooks admin.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Webhooks

The Webhooks module is the one webhooks system of CrestApps.OrchardCore: **outbound** events the site posts to subscribed URLs, and the **inbound** provider webhook inbox that durably accepts callbacks from Twilio, Telnyx, Azure Event Grid, Stripe and other providers.

## Guidelines

- Install `CrestApps.OrchardCore.Webhooks` in the web/startup project. Contracts are in `CrestApps.OrchardCore.Webhooks.Abstractions`; services in `CrestApps.OrchardCore.Webhooks.Core`.
- Declare and publish events from a startup gated with `[RequireFeatures(WebhooksConstants.Feature.Webhooks)]`; the publisher only sends events an enabled feature declares, so a `*` subscription never receives events of a disabled feature.
- Never build your own signing, retries or subscription store; publish through `IWebhookPublisher` or send through `IWebhookSender`.
- An inbound handler must declare a `WebhookHandlerReplaySafety` other than `Unspecified` (refused at startup) because delivery is at least once.

## Features

| Feature | ID | Notes |
| --- | --- | --- |
| Webhooks | `CrestApps.OrchardCore.Webhooks` | Subscriptions, delivery outbox, deliveries log, admin at **Configuration > Webhooks** (`/Admin/webhooks`). Depends on nothing. |
| Provider Webhook Inbox | `CrestApps.OrchardCore.ContactCenter.ProviderInbox` | Enabled by dependency only. Keeps the ID it was released with inside Contact Center, but no longer depends on Contact Center. |

Permission **Manage webhooks** keeps its technical name `ManageCommerceWebhooks` (security critical), so existing roles keep it.

`CrestApps.OrchardCore.Commerce.Webhooks` and `CrestApps.OrchardCore.AI.Webhooks` keep their feature IDs and now depend on `CrestApps.OrchardCore.Webhooks`; neither keeps subscriptions, signing or retries of its own. The admin moved from **Commerce > Webhooks** to **Configuration > Webhooks**, and the old **Artificial Intelligence > Webhooks** page and **Manage AI webhooks** permission are gone. Stored subscriptions and deliveries keep their YesSql type names and the `Webhooks` collection, so nothing is rewritten on upgrade.

## Outbound webhooks

| Piece | Contract | Implementation |
| --- | --- | --- |
| Declare events | `IWebhookEventProvider`, or `services.AddWebhookEvent(name, displayName, group, samplePayload)` | Gathered by `IWebhookEventCatalog` |
| Publish | `IWebhookPublisher.PublishAsync(eventName, payload[, eventId])` | Queues one `WebhookDelivery` per matching subscription, sent once the request's work is saved |
| Sign | `IWebhookSigner` | `HmacSha256WebhookSigner`, `v1=` plus hex HMAC-SHA256 of `{timestamp}.{body}` |
| Send once | `IWebhookSender` | One signed POST through the egress-guarded `CrestApps.Webhooks` HTTP client |
| Retry | Delivery outbox | **Webhook deliveries** background task: 1 min, 5 min, 30 min, 2 h, 6 h, 12 h, 24 h, then failed; a host the egress guard refuses fails at once |

```csharp
[RequireFeatures(WebhooksConstants.Feature.Webhooks)]
public sealed class WebhooksStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWebhookEvent("invoice.sent", "Invoice sent", "Invoices", new { invoiceId = "inv_1" });
        services.AddScoped<IInvoiceHandler, InvoiceWebhookHandler>();
    }
}

public sealed class InvoiceWebhookHandler : IInvoiceHandler
{
    private readonly IWebhookPublisher _publisher;

    public InvoiceWebhookHandler(IWebhookPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task SentAsync(Invoice invoice, CancellationToken cancellationToken)
        => _publisher.PublishAsync("invoice.sent", new { invoiceId = invoice.Id }, cancellationToken);
}
```

`IInvoiceHandler` and `Invoice` stand for your own module's types.

A feature that posts to its own URL instead of to subscriptions (such as the AI **Send Signed Webhook** workflow task) calls `services.AddWebhookSender()` and sends through `IWebhookSender`, so it signs and guards egress the same way.

Built-in events include the commerce events in `CommerceWebhookEvents` (`order.paid`, `stock.low`, ...) and the AI events in `AIWebhookEvents` (`conversation.started`, `conversation.ended`, `conversation.analyzed`, `conversation.transferred`, `conversation.evaluated`, `alert.triggered`, `alert.resolved`). The receiving side (body, headers, signature check) is the same for every event; see the commerce webhooks reference in [orchardcore-commerce](../orchardcore-commerce/SKILL.md).

### Delivery log retention

`WebhookDeliveriesBackgroundTask` deletes delivered and failed deliveries older than `WebhookDeliveryRetentionOptions.DeliveryRetention` (30 days; zero or less keeps all). Pending deliveries and events in `KeptEventNames` are never deleted. AI Webhooks adds every AI event (`AIWebhookEvents.All`), so AI deliveries, including the history copied from the old AI log by `AIWebhookIndexMigrations`, are kept.

```csharp
services.Configure<WebhookDeliveryRetentionOptions>(options =>
{
    options.DeliveryRetention = TimeSpan.FromDays(90);
    options.KeptEventNames.Add("order.refunded");
});
```

### Recipes and deployment

Subscriptions travel with the **Webhook subscriptions** deployment step and the `WebhookSubscriptions` recipe step, matched by `ItemId`. The protected secret is never exported; a recipe may carry a plain `Secret`, and a new subscription without one gets a new secret. `Url` must be a full `https://` URL not on the site's own network, and `Events` needs at least one event or `*`.

```json
{
  "steps": [
    {
      "name": "WebhookSubscriptions",
      "Subscriptions": [
        {
          "ItemId": "4k5y9m2x7q1c8w3n0p6r5t2v9z",
          "Name": "ERP",
          "Url": "https://erp.example.com/hooks",
          "Events": [ "order.paid", "order.refunded" ],
          "IsEnabled": true
        }
      ]
    }
  ]
}
```

## Provider webhook inbox

1. The endpoint authenticates and parses the request.
2. It calls `IProviderWebhookInbox.AcceptAsync` with a `ProviderWebhookInboxDelivery` (`ProviderName`, `DeliveryId`, `HandlerName`, `Payload`). The delivery is committed before the endpoint answers; a repeat of the same provider and delivery id is `Duplicate` on every node.
3. The endpoint answers, then starts processing with `AfterResponseWork` (from `CrestApps.OrchardCore.Core`).
4. The **Provider Webhook Inbox Dispatch** background task retries unfinished deliveries with exponential backoff and dead-letters after 10 failed attempts.

Deliveries are items of the platform's leased work queue (`ILeasedWorkQueue<T>`), with a five-minute lease; completed deliveries are kept at least seven days as idempotency tombstones. Inbox documents keep the `ContactCenter` YesSql collection. A feature can hold the dispatcher back with `IProviderWebhookInboxDispatchGate`.

Answer `2xx` for `Accepted` and `Duplicate`; for `Busy`, or when `AcceptAsync` throws, answer `409` or `503` so the provider sends again.

See [Webhooks examples](references/webhooks-examples.md) for a full inbox handler, the endpoint call and the signature validators.
