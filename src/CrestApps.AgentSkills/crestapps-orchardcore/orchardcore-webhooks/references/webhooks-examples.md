# Webhooks Examples

## Inbound handler on the provider webhook inbox

```csharp
public sealed class MyProviderInboxHandler : IProviderWebhookInboxHandler
{
    public const string Name = "MyProvider.Event";

    public string TechnicalName => Name;

    // Delivery is at least once, so say how a repeat is made harmless. Unspecified is refused at startup.
    public WebhookHandlerReplaySafety ReplaySafety => WebhookHandlerReplaySafety.DeduplicatedByEventId;

    public Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        // Process the normalized payload the endpoint stored.
        return Task.CompletedTask;
    }
}

[RequireFeatures(WebhooksConstants.Feature.ProviderInbox)]
public sealed class InboxStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IProviderWebhookInboxHandler, MyProviderInboxHandler>();
}
```

Add `WebhooksConstants.Feature.ProviderInbox` to your feature's dependencies (the inbox feature is enabled by dependency only).

`WebhookHandlerReplaySafety` values: `NaturallyIdempotent`, `DeduplicatedByEventId`, `GuardedByDurableStore` (`Unspecified` is refused).

## Accepting a delivery in the endpoint

```csharp
var acceptance = await inbox.AcceptAsync(new ProviderWebhookInboxDelivery
{
    ProviderName = "MyProvider",
    DeliveryId = providerEventId,
    HandlerName = MyProviderInboxHandler.Name,
    Payload = json,
}, context.RequestAborted);
```

- `Accepted` or `Duplicate`: answer `2xx`.
- `Busy`, or an exception: nothing was stored; answer `409` or `503`.

## Contracts

| Contract | Purpose |
| --- | --- |
| `IProviderWebhookInbox` | `AcceptAsync`, `DispatchAsync`, `DispatchDueAsync`. |
| `IProviderWebhookInboxHandler` | Processes one stored payload. |
| `IProviderWebhookIngressLimiter` | Concurrency and per-provider rate limits before buffering a body, and signed timestamp freshness. |
| `IWebhookProviderNameNormalizer` | Maps provider aliases to one canonical name (Telephony's `IProviderIdentityResolver` when Telephony or Contact Center is on). |
| `IWebhookSignatureValidator` | Constant-time signature checks. |
| `IProviderWebhookInboxDispatchGate` | Holds dispatch back; a pass runs only when every gate admits it. |

## Signature validators

In `CrestApps.OrchardCore.Webhooks.Core.Signatures`:

- `HmacWebhookSignatureValidator` with configurable algorithm, digest encoding and prefix; `Sha256Hex` and `Sha1Base64` (Twilio) are built in.
- `SharedKeyWebhookSignatureValidator` for a shared key in a header, such as Event Grid's `aeg-sas-key`.
- `WebhookSignatures.FixedTimeEquals` for constant-time comparison.

## Who uses the inbox

| Module | Provider name | Handler |
| --- | --- | --- |
| SMS and the Messaging Workspace | `Twilio`, `AzureEventGrid`, `Telnyx` | `sms-inbound` |
| Contact Center voice and Telnyx | the telephony provider | the provider's voice event handler |
| Stripe | `Stripe` | `Stripe.Event` (a tenant without the inbox processes Stripe events inline) |

When Contact Center is enabled, its retention policy also purges completed and dead-lettered deliveries (`WebhookInboxMessageRetentionDays`).
