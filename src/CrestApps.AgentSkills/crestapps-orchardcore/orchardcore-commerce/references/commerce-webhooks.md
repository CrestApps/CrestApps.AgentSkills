# Commerce Webhooks

| | |
| --- | --- |
| Feature | `CrestApps.OrchardCore.Commerce.Webhooks` (depends on Commerce and Webhooks, `CrestApps.OrchardCore.Webhooks`) |
| Contracts | `CrestApps.OrchardCore.Webhooks.Abstractions`, namespace `CrestApps.OrchardCore.Webhooks` (publisher, events); event names (`CommerceWebhookEvents`) in `CrestApps.OrchardCore.Abstractions`, namespace `CrestApps.OrchardCore.Commerce.Webhooks` |
| Admin | **Configuration > Webhooks** (`/Admin/webhooks`), permission **Manage webhooks** (`WebhookPermissions.ManageWebhooks`, which keeps the name `ManageCommerceWebhooks` so existing roles keep it) |

Subscriptions, signing, the delivery outbox and the admin belong to the generic Webhooks building block (see [orchardcore-webhooks](../../orchardcore-webhooks/SKILL.md)), which Commerce Webhooks turns on. Commerce Webhooks is the switch the commerce modules gate their events on: Orders, Customers, Inventory and Shipping declare and publish from startups behind `[RequireFeatures(CommerceConstants.Features.Webhooks)]`, so nothing is queued while it is off.

## Model

Both documents live in the `Webhooks` YesSql collection and are defined in `CrestApps.OrchardCore.Webhooks.Core.Models`; they keep their original YesSql type names, so existing subscriptions and deliveries stay readable.

- `WebhookSubscription` (`WebhookSubscriptionIndex`): `Name`, `Url`, `ProtectedSecret`, `Events` (names, or `*` for all including future ones), `IsEnabled`, `CreatedUtc`, `ModifiedUtc`.
- `WebhookDelivery` (`WebhookDeliveryIndex`): `EventId`, `EventName`, `Payload`, `Status` (`Pending`, `Delivered`, `Failed`), `Attempts`, `NextAttemptUtc`, `LastStatusCode`, `LastError`, timestamps.

Secrets are `whsec_` plus 24 random bytes, protected with Data Protection purpose `CrestApps.OrchardCore.Commerce.Webhooks.Secret` (`WebhookStorage.SecretProtectionPurpose`, unchanged so stored secrets stay readable). Data Protection keys must persist and be shared by every node or secrets can't be read back; a delivery whose secret can't be unprotected fails at once.

## Publishing

```csharp
public interface IWebhookPublisher
{
    Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken = default);

    Task PublishAsync(string eventName, object payload, string eventId, CancellationToken cancellationToken = default);
}
```

- `AddWebhookEvent(name, displayName, group)` (or an `IWebhookEventProvider`) declares an event and offers it in the admin picker. `WebhookPublisher` sends only events an enabled feature declares.
- The payload is `{ "id": "evt_...", "event": "...", "createdUtc": "...", "data": { } }`, with `data` serialized camelCase. Send the commerce API's shapes in `data` (for example `ApiOrder`) so receivers read one format.
- A stable `eventId` lets receivers drop repeats of an event that can be raised twice.
- Queuing errors are logged and swallowed; a webhook never fails the change that raised it.

## Delivery

- Sent through `IWebhookSender`: `Content-Type: application/json`, user agent `CrestApps-Webhooks/1.0`, and the `WebhookHeaders` `X-Webhook-Id`, `X-Webhook-Event`, `X-Webhook-Timestamp` (Unix seconds at this attempt), `X-Webhook-Signature`.
- Signature (`IWebhookSigner`, `HmacSha256WebhookSigner`) is `v1=` plus the lowercase hex HMAC-SHA256 of `{timestamp}.{body}`, keyed with the UTF-8 bytes of the whole secret including `whsec_`.
- Named `HttpClient` `CrestApps.Webhooks` (registered with `AddEgressGuardedHttpClient`), 15-second timeout, no redirects. The egress guard refuses hosts resolving only to loopback, private or link-local addresses (loopback allowed in Development), and such a delivery fails at once instead of retrying. Subscription URLs must be absolute `https` (plain `http` only to loopback in Development).
- Backoff after a non-`2xx`, timeout or error: 1 minute, 5 minutes, 30 minutes, 2 hours, 6 hours, 12 hours, 24 hours; failed after the eighth attempt.
- `WebhookDeliveriesBackgroundTask` runs every minute: sends up to 20 due deliveries, then deletes up to 20 finished deliveries older than 30 days, except events a feature keeps (`WebhookDeliveryRetentionOptions`; AI Webhooks keeps every AI event, commerce deliveries keep 30 days).
- **Send again** resets a delivery; **Send a test event** posts `webhook.ping` to one subscription.

## Events

| Event | Raised by | `data` |
| --- | --- | --- |
| `order.created` | Orders, after the order is saved and not refused | `order` |
| `order.paid` | Orders | `order` |
| `order.canceled` | Orders, every reason except `Replaced` | `order`, `reason` |
| `order.refunded` | Orders, once per refund | `order`, `refund` |
| `customer.created` | Customers (not for the `Customers` recipe step) | `customer` |
| `shipment.shipped`, `shipment.delivered` | Shipping | `shipment`, `orderNumber`, `customerEmail` |
| `stock.changed` | Inventory, after commit, when total available changed | `stock`, `previousAvailable` |
| `stock.low` | Inventory, `RanLow` and not `SoldOut` | `stock`, `previousAvailable` |
| `stock.sold-out` | Inventory | `stock`, `previousAvailable` |
| `webhook.ping` | **Send a test event** | `webhookId`, `name` |

## Receiving

Verify against the raw body before parsing, compare in constant time, refuse timestamps older than a few minutes, answer `2xx` fast and work afterwards, drop an `X-Webhook-Id` already handled, and read `status` and times in `data` (or fetch from the API) instead of relying on arrival order.

```csharp
using System.Security.Cryptography;
using System.Text;

public static class StoreWebhookVerifier
{
    public static bool IsFromStore(string secret, string timestamp, string body, string signature)
    {
        if (!long.TryParse(timestamp, out var seconds) ||
            Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds) > 300)
        {
            return false;
        }

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp + "." + body));
        var expected = "v1=" + Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature ?? string.Empty));
    }
}
```

Keep the signing secret in the receiver's secret store, never in a page or app.
