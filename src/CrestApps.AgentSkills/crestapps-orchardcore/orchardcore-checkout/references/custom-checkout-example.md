# Taking a payment from a custom module

A complete module that charges a registration fee through the CrestApps checkout and handles every outcome. Every checkout API used here was checked against the CrestApps.OrchardCore 3.0 source (`ICheckoutEngine`, `StartCheckoutRequest`, `CheckoutHandlerBase`, `CheckoutFlowStep`, `BillingItem`, `CheckoutConstants`, `ICheckoutSessionStore`), and the pattern mirrors the shipped consumers (`TransactionPayController`, `OrderCheckoutHandler`, `TransactionSettlementCheckoutHandler`, `BookingBalanceCharger`). `RegistrationFee` and `IRegistrationFeeStore` are the module's own types.

## Packages and manifest

The module project references the contracts; the web/startup project installs the modules.

```xml
<!-- Contoso.Registrations.csproj -->
<ItemGroup>
  <PackageReference Include="CrestApps.OrchardCore.Checkout.Abstractions" />
  <!-- Only when the module renders its own checkout step (CheckoutFlowDisplayDriver): -->
  <PackageReference Include="CrestApps.OrchardCore.Checkout.Core" />
</ItemGroup>

<!-- Web project -->
<ItemGroup>
  <PackageReference Include="CrestApps.OrchardCore.Checkout" />
  <PackageReference Include="CrestApps.OrchardCore.Stripe" />
</ItemGroup>
```

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Contoso Registrations",
    Author = "Contoso",
    Version = "1.0.0"
)]

[assembly: Feature(
    Id = "Contoso.Registrations",
    Name = "Contoso Registrations",
    Description = "Charges registration fees through the checkout.",
    Category = "Commerce",
    Dependencies =
    [
        "CrestApps.OrchardCore.Checkout",
    ]
)]
```

## The record

```csharp
namespace Contoso.Registrations.Models;

public sealed class RegistrationFee
{
    public string Id { get; set; }

    public string UserId { get; set; }

    public string Title { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; }

    public RegistrationFeeStatus Status { get; set; }

    public string CheckoutSessionId { get; set; }

    public DateTime? PaidUtc { get; set; }
}

public enum RegistrationFeeStatus
{
    Unpaid,
    AwaitingPayment,
    Paid,
}

public static class RegistrationFeeCheckout
{
    // The ReferenceType every handler filters on.
    public const string ReferenceType = "RegistrationFee";

    public const string StepKey = "RegistrationFee";
}
```

`IRegistrationFeeStore` (`FindByIdAsync`, `SaveAsync`) is your persistence. Implement it on the tenant's scoped YesSql `ISession` (a document, an index, or a `DocumentCatalog`) and do not call `SaveChangesAsync` inside it: the engine commits what a completing handler writes together with the `Completed` status, and discards it when a handler throws.

## Start the checkout

```csharp
using System.Security.Claims;
using Contoso.Registrations.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Contoso.Registrations.Controllers;

[Authorize]
public sealed class RegistrationFeesController : Controller
{
    private readonly IRegistrationFeeStore _fees;
    private readonly ICheckoutEngine _checkoutEngine;
    private readonly ICheckoutSessionStore _sessionStore;

    internal readonly IStringLocalizer S;

    public RegistrationFeesController(
        IRegistrationFeeStore fees,
        ICheckoutEngine checkoutEngine,
        ICheckoutSessionStore sessionStore,
        IStringLocalizer<RegistrationFeesController> stringLocalizer)
    {
        _fees = fees;
        _checkoutEngine = checkoutEngine;
        _sessionStore = sessionStore;
        S = stringLocalizer;
    }

    [HttpPost("registrations/fees/{feeId}/pay")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(string feeId)
    {
        var fee = await FindOwnAsync(feeId);

        if (fee is null)
        {
            return NotFound();
        }

        if (fee.Status == RegistrationFeeStatus.Paid)
        {
            return RedirectToAction(nameof(Show), new { feeId });
        }

        // The session belongs to the signed-in user. ReturnUrl must be local, or StartAsync throws.
        var session = await _checkoutEngine.StartAsync(new StartCheckoutRequest
        {
            ReferenceType = RegistrationFeeCheckout.ReferenceType,
            ReferenceId = fee.Id,
            ReturnUrl = Url.Action(nameof(Show), new { feeId }),
            ReturnText = S["Back to my registration"],
            Title = S["Registration fee received"],
        }, HttpContext.RequestAborted);

        return RedirectToRoute(CheckoutConstants.RouteNames.Step, new
        {
            sessionId = session.SessionId,
            step = session.CurrentStep,
        });
    }

    // The page the checkout returns to. Your record is the source of truth; the session only says
    // whether a payment is still settling.
    [HttpGet("registrations/fees/{feeId}")]
    public async Task<IActionResult> Show(string feeId)
    {
        var fee = await FindOwnAsync(feeId);

        if (fee is null)
        {
            return NotFound();
        }

        var settling = false;

        if (fee.Status != RegistrationFeeStatus.Paid)
        {
            var latest = await _sessionStore.GetByReferenceAsync(RegistrationFeeCheckout.ReferenceType, fee.Id, cancellationToken: HttpContext.RequestAborted);

            settling = latest?.Status is CheckoutSessionStatus.AwaitingProvider or CheckoutSessionStatus.PaymentPending;
        }

        return View(new RegistrationFeeViewModel
        {
            Fee = fee,
            IsSettling = settling,
        });
    }

    private async Task<RegistrationFee> FindOwnAsync(string feeId)
    {
        var fee = await _fees.FindByIdAsync(feeId);

        return fee is not null && fee.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier) ? fee : null;
    }
}

public class RegistrationFeeViewModel
{
    public RegistrationFee Fee { get; set; }

    public bool IsSettling { get; set; }
}
```

`GetByReferenceAsync` does not enforce ownership, which is why the action checks the fee's owner first. Use the ownership-checked `ICheckoutSessionStore.GetAsync(sessionId)` for anything that resumes a checkout.

## Bill it and handle the outcome

```csharp
using Contoso.Registrations.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace Contoso.Registrations.Handlers;

public sealed class RegistrationFeeCheckoutHandler : CheckoutHandlerBase
{
    private readonly IRegistrationFeeStore _fees;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public RegistrationFeeCheckoutHandler(
        IRegistrationFeeStore fees,
        IClock clock,
        ILogger<RegistrationFeeCheckoutHandler> logger,
        IStringLocalizer<RegistrationFeeCheckoutHandler> stringLocalizer)
    {
        _fees = fees;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    // Runs once, inside ICheckoutEngine.StartAsync.
    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        var session = context.Session;

        if (!IsFee(session) || string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var fee = await _fees.FindByIdAsync(session.ReferenceId);

        if (fee is null || fee.Status == RegistrationFeeStatus.Paid)
        {
            _logger.LogWarning("Checkout '{SessionId}' was started for fee '{FeeId}', which is missing or paid.", session.SessionId, session.ReferenceId);

            return;
        }

        // The invoice is built in the session's currency; without it the site default is used.
        session.Currency = fee.Currency;

        // Billing items belong to a step. Nothing is asked here, so the step is concealed and the
        // customer opens straight on the payment step, which the checkout adds last.
        session.Steps.Add(new CheckoutFlowStep
        {
            Key = RegistrationFeeCheckout.StepKey,
            Title = S["Registration fee"],
            Description = fee.Title,
            Order = 0,
            CollectData = false,
            Conceal = true,
            BillingItems =
            [
                new BillingItem
                {
                    ItemId = fee.Id,
                    Description = fee.Title,
                    Amount = fee.Amount,
                    Quantity = 1,
                },
            ],
        });

        fee.Status = RegistrationFeeStatus.AwaitingPayment;
        fee.CheckoutSessionId = session.SessionId;

        await _fees.SaveAsync(fee);
    }

    // Conceal is not persisted, so it is set again every time the session loads.
    public override Task InitializingAsync(CheckoutFlowInitializingContext context)
    {
        foreach (var step in context.Flow.Session.Steps)
        {
            if (step.Key == RegistrationFeeCheckout.StepKey)
            {
                step.Conceal = true;
            }
        }

        return Task.CompletedTask;
    }

    // Every obligation is confirmed by its provider. This runs inside the transaction that marks the
    // session Completed, so throwing rolls everything back and the reconciliation sweep retries.
    public override async Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        if (context.Flow.Session is not CheckoutSession session || !IsFee(session))
        {
            return;
        }

        var fee = await _fees.FindByIdAsync(session.ReferenceId)
            ?? throw new InvalidOperationException($"Checkout '{session.SessionId}' pays for fee '{session.ReferenceId}', which does not exist.");

        if (fee.Status == RegistrationFeeStatus.Paid)
        {
            // Completion is idempotent; a second run changes nothing.
            return;
        }

        if (!session.TryGet<CheckoutInvoice>(out var invoice) || invoice.LineItems is not { Length: > 0 })
        {
            throw new InvalidOperationException($"Checkout '{session.SessionId}' bills nothing, so it cannot complete.");
        }

        fee.Status = RegistrationFeeStatus.Paid;
        fee.PaidUtc = _clock.UtcNow;
        fee.CheckoutSessionId = session.SessionId;

        await _fees.SaveAsync(fee);
    }

    // After the Completed status is committed. Exceptions are logged and never undo the payment.
    public override Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        if (context.Flow.Session is CheckoutSession session && IsFee(session))
        {
            // Send the confirmation email or raise your own event here.
        }

        return Task.CompletedTask;
    }

    public override Task FailedAsync(CheckoutFlowFailedContext context)
        => ReleaseAsync(context.Flow.Session as CheckoutSession);

    public override Task CanceledAsync(CheckoutFlowCanceledContext context)
        => ReleaseAsync(context.Flow.Session as CheckoutSession);

    public override Task ExpiredAsync(CheckoutFlowExpiredContext context)
        => ReleaseAsync(context.Flow.Session as CheckoutSession);

    // A checkout that ended unpaid lets the fee be paid again, unless a newer checkout took it over.
    private async Task ReleaseAsync(CheckoutSession session)
    {
        if (!IsFee(session) || string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var fee = await _fees.FindByIdAsync(session.ReferenceId);

        if (fee is null || fee.Status == RegistrationFeeStatus.Paid || fee.CheckoutSessionId != session.SessionId)
        {
            return;
        }

        fee.Status = RegistrationFeeStatus.Unpaid;
        fee.CheckoutSessionId = null;

        await _fees.SaveAsync(fee);
    }

    private static bool IsFee(CheckoutSession session)
        => string.Equals(session?.ReferenceType, RegistrationFeeCheckout.ReferenceType, StringComparison.Ordinal);
}
```

## Register it

```csharp
using Contoso.Registrations.Handlers;
using CrestApps.OrchardCore.Checkout.Handlers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace Contoso.Registrations;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRegistrationFeeStore, RegistrationFeeStore>();
        services.AddScoped<ICheckoutHandler, RegistrationFeeCheckoutHandler>();
    }
}
```

When checkout is optional for your module, move the handler registration into a startup marked `[RequireFeatures("CrestApps.OrchardCore.Checkout")]` and resolve `ICheckoutEngine` with `HttpContext.RequestServices.GetService<ICheckoutEngine>()`, as the Transactions pay page does.

## What each outcome looks like

| Outcome | Session status | Handler hook | Customer sees |
| --- | --- | --- | --- |
| Paid and fulfilled | `Completed` | `CompletingAsync`, then `CompletedAsync` | Confirmation headed with `Title`, **Continue** to `ReturnUrl` |
| Paid, your handler threw | `PaymentPending` | `CompletingAsync` retried by the sweep (every 5 minutes) | *It will be retried shortly*; the money is kept, never refunded |
| Gateway still deciding | `AwaitingProvider` / `PaymentPending` | none yet | The page stops polling; webhook or sweep completes it |
| Card declined when the payment begins | unchanged (still open) | none | The decline message; the next try gets a fresh idempotency key, so another card can be used |
| Provider reports an obligation failed when verified | `Failed` | `FailedAsync` | *This checkout is no longer available.* with **Back**; obligations that did settle are refunded through `ICheckoutRefundService` |
| Canceled (`ICheckoutEngine.CancelAsync`) | `Canceled` | `CanceledAsync(context.Reason)` | Same page |
| Abandoned past **Session lifetime** | `Expired` | `ExpiredAsync` | Same page |

## Optional: a step that asks something

A step that collects data is `CollectData = true` and visible, and its driver must save `SavedSteps[StepKey]`; until it does the payment step is not reachable and `TryCompleteAsync` answers `Blocked`.

```csharp
// In ActivatingAsync, beside the billing step:
session.Steps.Add(new CheckoutFlowStep
{
    Key = "RegistrationTerms",
    Title = S["Terms"],
    Order = 5,
    CollectData = true,
});
```

```csharp
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace Contoso.Registrations.Drivers;

public sealed class RegistrationTermsCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    internal readonly IStringLocalizer S;

    public RegistrationTermsCheckoutFlowDisplayDriver(IStringLocalizer<RegistrationTermsCheckoutFlowDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override string StepKey => "RegistrationTerms";

    protected override Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        var accepted = flow.Session.SavedSteps[StepKey]?["accepted"]?.GetValue<bool>() == true;

        return Task.FromResult<IDisplayResult>(
            Initialize<RegistrationTermsStepViewModel>("RegistrationTermsStep", model => model.Accepted = accepted)
                .Location("Content:5"));
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        var model = new RegistrationTermsStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!model.Accepted)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Accepted), S["Accept the terms to continue."]);
        }
        else
        {
            flow.Session.SavedSteps[StepKey] = new JsonObject { ["accepted"] = true };
        }

        return await EditStepAsync(flow, context);
    }
}

public class RegistrationTermsStepViewModel
{
    public bool Accepted { get; set; }
}

// Startup: services.AddDisplayDriver<CheckoutFlow, RegistrationTermsCheckoutFlowDisplayDriver>();
```

`Views/RegistrationTermsStep.cshtml` renders the checkbox with `asp-for="Accepted"`; the checkout page supplies the form, the antiforgery token and the navigation buttons.

## Charging without the customer

A background task or an administrator charges a card kept by an earlier checkout (see `orchardcore-payments` for saved payment methods):

```csharp
var session = await _checkoutEngine.StartAsync(new StartCheckoutRequest
{
    ReferenceType = RegistrationFeeCheckout.ReferenceType,
    ReferenceId = fee.Id,

    // Server code only: the session, its attempts and the fee belong to this customer.
    OwnerId = fee.UserId,
}, cancellationToken);

var outcome = await _checkoutEngine.BeginPaymentAsync(session.SessionId, new BeginPaymentOptions
{
    ProviderKey = savedPaymentMethod.ProviderKey,
    ProviderData = CheckoutPaymentDataKeys.ForOffSessionCharge(savedPaymentMethod),
}, cancellationToken);

if (!outcome.Succeeded)
{
    await _checkoutEngine.CancelAsync(session.SessionId, "The charge was not accepted.", cancellationToken);

    // outcome.Declined and outcome.ProviderErrorMessage carry the gateway's answer.
    return;
}

var completion = await _checkoutEngine.TryCompleteAsync(session.SessionId, cancellationToken);

// completion.IsCompleted: your CompletingAsync ran. CheckoutCompletionStatus.Pending: the sweep finishes it.
```

The engine commits while it charges. Read your record again after these calls rather than saving a copy loaded before them.

## Testing the flow

Enable `CrestApps.OrchardCore.Checkout.TestProvider` in Development and drive it with HTTP requests that carry the signed-in cookie:

```http
POST /registrations/fees/{feeId}/pay
```

```http
POST /checkout/{sessionId}/payment/begin
Content-Type: application/json

{ "providerKey": "test" }
```

```http
POST /checkout/{sessionId}/payment/status
```

The status call answers `{ "status": "Completed", "completed": true }` and the fee is `Paid`. A fee of `10.02` exercises the decline path (the begin request answers `400` and the fee stays `AwaitingPayment` until the session is canceled or expires), and `10.05` the pending path (`"status": "Pending"`, completed by the sweep after two minutes). Cancel a session with `ICheckoutEngine.CancelAsync` to exercise `CanceledAsync`.
