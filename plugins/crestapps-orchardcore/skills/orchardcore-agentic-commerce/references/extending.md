# Extending Agentic Commerce

A new binding is an endpoint plus a mapper over `IAgentCheckoutService` (or over `UcpOperations` / `AcpCheckoutOperations` for another UCP or ACP transport). Never call carts, orders or payment providers from an endpoint.

## Add a payment handler

A handler describes itself for the request's protocol, version, platform and test mode, and returns `null` when it can't be offered (always `null` for a platform in test mode, unless it only takes test credentials). `PrepareAsync` returns the checkout provider key and provider data the engine charges. Put the agent's credential only in `CheckoutPaymentDataKeys.DelegatedCredential`, never in a key a browser can send.

```csharp
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.Checkout.Services;

public sealed class ContosoPayHandler : IAgentPaymentHandler
{
    public const string HandlerName = "com.contoso.pay";

    public string GetName(string protocol)
        => HandlerName;

    public Task<AgentPaymentHandlerDescriptor> DescribeAsync(AgentPaymentHandlerContext context, CancellationToken cancellationToken = default)
    {
        // A platform in test mode only pays with test credentials.
        if (context.TestMode || context.Protocol != AgenticCommerceConstants.Protocols.Ucp)
        {
            return Task.FromResult<AgentPaymentHandlerDescriptor>(null);
        }

        return Task.FromResult(new AgentPaymentHandlerDescriptor
        {
            Id = "contoso_pay",
            Name = HandlerName,
            Version = "2026-01-01",
            Spec = "https://pay.contoso.com/ucp/spec",
            Schema = "https://pay.contoso.com/ucp/schema.json",
            Psp = "contoso",
            DisplayName = "Contoso Pay",
            InstrumentTypes = ["card"],
            Config = new JsonObject { ["environment"] = "production" },
        });
    }

    public Task<AgentPaymentPreparation> PrepareAsync(AgentPaymentHandlerContext context, AgentPayment payment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payment.Credential))
        {
            return Task.FromResult(AgentPaymentPreparation.Fail("The payment has no Contoso Pay token."));
        }

        return Task.FromResult(new AgentPaymentPreparation
        {
            Succeeded = true,
            ProviderKey = "contoso",
            ProviderData = new Dictionary<string, string>
            {
                [CheckoutPaymentDataKeys.DelegatedCredential] = payment.Credential.Trim(),
                [CheckoutPaymentDataKeys.DelegatedCredentialType] = "contoso_token",
            },
        });
    }
}

[Feature(AgenticCommerceConstants.Features.AgenticCommerce)]
[RequireFeatures("Contoso.Payments")]
public sealed class ContosoAgentPaymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IAgentPaymentHandler, ContosoPayHandler>();
}
```

The `ICheckoutPaymentProvider` with key `contoso` must:

- set `PaymentProviderCapabilities.AcceptsDelegatedCredentials` and, in `BeginAsync`, charge the credential on the server when `DelegatedCredential` is present: confirmed at once, idempotent on `attempt.IdempotencyKey`, one-time only (refuse `SavePaymentMethod` and recurring obligations), `RequiresAction` with a `RedirectUrl` when the bank asks for 3-D Secure, `Decline` otherwise;
- to take an agent's 3-D Secure result, set `AcceptsExternalAuthentication`, read `ThreeDSecureResult.From(context.ProviderData)` and, when the attempt already has a `ProviderReference`, finish that payment instead of starting another; set `PaymentBeginResult.CardBrand` when the bank asks for authentication.

A new credential kind belongs in `DelegatedCredentialTypes` (Checkout.Abstractions), and a new server-only key must be dropped by `PaymentEndpoints.SanitizeProviderData`. The platform's **Payment handlers** list filters by `descriptor.Name`; agents may name a handler by `Id` or `Name`. Reference implementations: `StripeSharedPaymentTokenHandler`, `GooglePayPaymentHandler`, `TestAgentPaymentHandler`.

## Add a UCP capability or extension

An `IUcpProfileContributor` adds the capability for each version; an extension names its parent in `Extends`, so negotiation drops it when the parent isn't agreed. An `IUcpCheckoutExtension` writes its members on every answer the platform agreed it for (`WritingAsync`), may refuse a complete request with a message (`CompletingAsync`; the answer gets `ucp.status = "error"`), and keeps evidence once completed (`CompletedAsync`; `UcpCheckoutContext.Items` carries state between the two). If the extension reads checkout input, extend `UcpCheckoutMapper.ReadInput`/`ReadUpdate`.

```csharp
public sealed class GiftNoteProfileContributor : IUcpProfileContributor
{
    public const string Name = "com.contoso.gift_note";

    public Task ContributeAsync(UcpProfileContext context)
    {
        context.Capabilities.Add(new UcpCapability
        {
            Name = Name,
            Version = context.Version,
            Spec = "https://contoso.com/ucp/gift-note",
            Schema = "https://contoso.com/ucp/gift_note.json",
            Extends = [UcpConstants.Capabilities.Checkout],
        });

        return Task.CompletedTask;
    }
}

public sealed class GiftNoteCheckoutExtension : IUcpCheckoutExtension
{
    public Task WritingAsync(UcpCheckoutContext context)
    {
        if (context.Request.Has(GiftNoteProfileContributor.Name))
        {
            context.Json["gift_note"] = new JsonObject { ["allowed"] = true };
        }

        return Task.CompletedTask;
    }

    public Task<AgentCheckoutMessage> CompletingAsync(UcpCheckoutContext context)
        => Task.FromResult<AgentCheckoutMessage>(null);

    public Task CompletedAsync(UcpCheckoutContext context)
        => Task.CompletedTask;
}
```

```csharp
[Feature(AgenticCommerceConstants.Features.Ucp)]
public sealed class GiftNoteStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IUcpProfileContributor, GiftNoteProfileContributor>();
        services.AddScoped<IUcpCheckoutExtension, GiftNoteCheckoutExtension>();
    }
}
```

References: `Ap2ProfileContributor`, `Ap2CheckoutExtension`, `UcpShoppingProfileContributor`. Add an ACP discovery entry with `IAcpDiscoveryContributor`.

## Send a protocol's order updates

`AgentOrderUpdates` calls the `IAgentOrderPublisher`s whose `Protocol` equals `AgentCheckout.Protocol` for orders on the `agent` channel. Queue a direct delivery with `IWebhookOutbox`; an `IWebhookDeliveryFormatter` with the same `FormatterName` signs it at every attempt (see `orchardcore-webhooks`). Put only ids in `Data`: the formatter reads secrets itself when it sends.

```csharp
public sealed class ContosoOrderPublisher : IAgentOrderPublisher
{
    private readonly IWebhookOutbox _outbox;

    public ContosoOrderPublisher(IWebhookOutbox outbox)
    {
        _outbox = outbox;
    }

    public string Protocol
        => "contoso";

    public Task PublishAsync(AgentCheckout checkout, Order order, bool created, CancellationToken cancellationToken = default)
        => _outbox.EnqueueAsync(new WebhookDirectDelivery
        {
            OwnerId = checkout.PlatformId,
            Url = "https://agents.contoso.com/orders",
            EventName = created ? "order.created" : "order.updated",
            Body = JsonSerializer.Serialize(new { id = order.ItemId, status = order.Status.ToString() }),
            FormatterName = "contoso-order",
            Data = new Dictionary<string, string> { ["platformId"] = checkout.PlatformId },
        }, cancellationToken);
}
```

References: `UcpOrderPublisher` with `UcpOrderWebhookFormatter` (RFC 9421 with `IMerchantSigningKeyService.GetSigningKeyAsync`), `AcpOrderPublisher` with `AcpOrderWebhookFormatter` (`TimestampedHmacSignature`).

## Traps

- **The agent principal must not own the store checkout.** Requests run as `agent-platform:{id}` and `ICheckoutEngine.StartAsync` takes the current user as owner; `AgentCheckoutService` resets the owner to the linked buyer (or `null`) and the hand-off link gives a guest session to the buyer's browser.
- **Money only moves through `ICheckoutEngine`.** Handlers prepare provider data; they never call a gateway.
- **Totals must add up** (`AgentCheckoutService.BuildTotals`), and AP2 matches the mandate total in minor units.
- **Verify signatures against the read body** (`AgentRequest.Body`), never a re-serialized object.
- **Test mode**: every real handler returns `null` for a test-mode platform.
