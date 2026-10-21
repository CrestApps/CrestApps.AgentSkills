# A custom payment provider

A gateway that collects a one-time card payment in the browser, verifies it server-side, and refunds it. The contracts (`ICheckoutPaymentProvider`, `ICheckoutPaymentRefundProvider`, the context and result types, `CheckoutFlowPaymentMethod`) are the CrestApps.OrchardCore 3.0 ones; `IAcmeClient` stands for your gateway's SDK. The shipped `TestCheckoutPaymentProvider` and `StripeCheckoutPaymentProvider` follow the same shape.

## Provider

```csharp
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.Localization;

namespace Acme.Payments;

public sealed class AcmeCheckoutPaymentProvider : ICheckoutPaymentProvider, ICheckoutPaymentRefundProvider
{
    public const string ProviderKey = "acme";

    private readonly IAcmeClient _client;

    internal readonly IStringLocalizer S;

    public AcmeCheckoutPaymentProvider(IAcmeClient client, IStringLocalizer<AcmeCheckoutPaymentProvider> stringLocalizer)
    {
        _client = client;
        S = stringLocalizer;
    }

    public string Key => ProviderKey;

    public string DisplayName => S["Acme card"];

    public PaymentProviderCapabilities Capabilities { get; } = new()
    {
        SupportsOneTimePayments = true,
        SupportsEmbeddedElements = true,
        SupportsRefunds = true,
    };

    // The attempt is already persisted. Return the gateway's reference immediately so it is stored.
    public async Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
    {
        var attempt = context.Attempt;

        var intent = await _client.CreateIntentAsync(
            amountInMinorUnits: Money.ToMinorUnits(attempt.ExpectedAmount + attempt.ExpectedTaxAmount, attempt.Currency),
            currency: attempt.Currency,
            idempotencyKey: attempt.IdempotencyKey,
            cancellationToken);

        if (intent.Declined)
        {
            return PaymentBeginResult.Decline(intent.Message, intent.Id);
        }

        if (intent.Id is null)
        {
            return PaymentBeginResult.Failure(S["The payment could not be started."]);
        }

        var result = PaymentBeginResult.Success(intent.Id);

        // The browser confirms with the gateway's script, then the page polls the status endpoint.
        result.ClientSecret = intent.ClientSecret;
        result.RequiresAction = true;

        return result;
    }

    // The source of truth at completion. Report Unknown while the gateway has not decided.
    public async Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default)
    {
        var attempt = context.Attempt;

        if (string.IsNullOrEmpty(attempt.ProviderReference))
        {
            return new PaymentVerificationResult { Status = PaymentStatus.Unknown };
        }

        var intent = await _client.GetIntentAsync(attempt.ProviderReference, cancellationToken);

        return new PaymentVerificationResult
        {
            Status = intent.Succeeded ? PaymentStatus.Succeeded : intent.Failed ? PaymentStatus.Failed : PaymentStatus.Unknown,
            TransactionId = intent.Id,
            ReportsAuthoritativeAmount = true,

            // Split what the gateway took into net and tax; the checkout cross-checks both.
            Amount = attempt.ExpectedAmount,
            TaxAmount = attempt.ExpectedTaxAmount,
            TaxSnapshot = attempt.TaxSnapshot,
            Currency = intent.Currency,
            GatewayMode = intent.IsLive ? GatewayMode.Live : GatewayMode.Testing,
        };
    }

    public async Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.Attempt.ProviderReference))
        {
            return PaymentCancelResult.Success();
        }

        return await _client.CancelIntentAsync(context.Attempt.ProviderReference, cancellationToken)
            ? PaymentCancelResult.Success()
            : PaymentCancelResult.Failure(S["The payment could not be canceled at Acme."]);
    }

    // Called only by ICheckoutRefundService, after the refund is recorded and the payment locked.
    public async Task<PaymentRefundResult> RefundAsync(RefundPaymentContext context, CancellationToken cancellationToken = default)
    {
        var refund = await _client.RefundAsync(
            context.OriginalProviderReference ?? context.OriginalTransactionId,
            Money.ToMinorUnits(context.Amount, context.Currency),
            context.IdempotencyKey,
            cancellationToken);

        return refund.Succeeded
            ? PaymentRefundResult.Success(refund.Id, context.Amount, context.Currency, context.GatewayMode)
            : PaymentRefundResult.Failed(refund.Code, refund.Message);
    }
}
```

## Panel on the payment step

```csharp
using CrestApps.OrchardCore.Checkout;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace Acme.Payments;

public sealed class AcmePaymentMethodDisplayDriver : DisplayDriver<CheckoutFlowPaymentMethod>
{
    public override IDisplayResult Edit(CheckoutFlowPaymentMethod method, BuildEditorContext context)
        => View("AcmeCheckoutPaymentMethod", method)
            .Location("Content")
            .OnGroup(AcmeCheckoutPaymentProvider.ProviderKey);
}
```

`CheckoutFlowPaymentMethod` carries the `Flow` and `SavePaymentMethod` (true when the session keeps the card for later charges). The view loads the gateway script, which posts `{ "providerKey": "acme", "providerData": { ... } }` to the route named `CheckoutBeginPayment` (`checkout/{sessionId}/payment/begin`), confirms with the returned client secret, then polls `CheckoutPaymentStatus`.

## Registration

```csharp
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace Acme.Payments;

[RequireFeatures("CrestApps.OrchardCore.Checkout")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<AcmeCheckoutPaymentProvider>();
        services.AddScoped<ICheckoutPaymentProvider>(sp => sp.GetRequiredService<AcmeCheckoutPaymentProvider>());
        services.AddScoped<ICheckoutPaymentRefundProvider>(sp => sp.GetRequiredService<AcmeCheckoutPaymentProvider>());
        services.AddDisplayDriver<CheckoutFlowPaymentMethod, AcmePaymentMethodDisplayDriver>();
    }
}
```

## Webhooks

Map an endpoint that verifies the gateway signature, then calls `ICheckoutEngine.TryCompleteAsync(sessionId)` for the checkout the event names (put the session id in the gateway metadata at begin) and raises the matching `IPaymentEvent` methods so refund reconciliation, disputes and subscription lifecycle consumers react. De-duplicate events under a distributed lock, as the Stripe endpoint does. A lost webhook only delays completion: the reconciliation sweep verifies stale attempts every five minutes.

## Recurring and saved cards

- Recurring: implement `ICheckoutRecurringPaymentProvider` (`BeginRecurringAsync`, `CancelRecurringAsync`, `PauseRecurringAsync`, `UpdateRecurringAsync`) under the same key and set `SupportsRecurringPayments`. Be idempotent on the attempt's idempotency key so a retried begin resumes the same agreement.
- Saved cards: set `SupportsSavedPaymentMethods` (and `SupportsEmbeddedElements` for installment plans), keep the method when `CheckoutPaymentDataKeys.IsSet(context.ProviderData, CheckoutPaymentDataKeys.SavePaymentMethod)`, charge without the payer when `OffSession` is set using `SavedCustomerReference` and `SavedPaymentMethodReference`, and register an `ICheckoutSavedPaymentMethodProvider` with the same key that returns a `SavedPaymentMethod` (`ProviderKey`, `CustomerReference`, `PaymentMethodReference`, `Brand`, `Last4`, `ExpirationMonth`, `ExpirationYear`) from a settled `PaymentAttempt`.
- An offline method (records a promise, collects nothing) sets `DefersPayment`; it is then never offered on a checkout that settles an existing balance. See `orchardcore-pay-later` for `OfflineCheckoutPaymentProvider`.
