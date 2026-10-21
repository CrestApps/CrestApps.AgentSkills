---
name: orchardcore-orders
description: Skill for the CrestApps Orchard Core Orders module (CrestApps.OrchardCore.Orders, Orders.GiftCards, Orders.Returns, Orders.Api). Covers checking a cart out as a draft order through the Checkout framework with CheckoutReferenceTypes.Order, OrderCheckoutHandler, order statuses and cancel reasons, IOrderService, IOrderEventHandler and OrderEventHandlerBase (created, paid, canceled, refunded, returned, lines changed), OrderGrantHandlerBase, OrderOperationsService refunds and cancels, guest order links with IOrderLinkService, confirmations and ICommerceEmailSender with AddCommerceEmailKind and Liquid email templates, IOrderEmailSectionProvider, invoices and packing slips, draft orders and quotes, sales channels, order bumps, gift cards, returns, order reports, workflow events, IDisplayManager of Order display types, OrderSettings, and the Orders API. Use it when requests mention Orchard Core orders, order events, refunds, invoices, gift cards, returns, quotes, order emails or the orders API.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orders

Orders (new in CrestApps.OrchardCore 3.0) checks a cart out through the Checkout framework and records what was bought. An order holds lines, not money: the payment, tax, attempts and refunds belong to the checkout session the order names.

## Where it fits in the commerce suite

| Module | Relationship |
| --- | --- |
| Carts (`orchardcore-carts`) | Required. The cart is frozen into a draft order. |
| Checkout (`orchardcore-checkout`) | Required. Takes payment, coupons, tenders and tax for the order. |
| Payments, Pay Later, Transactions (`orchardcore-payments`, `orchardcore-pay-later`, `orchardcore-transactions`) | Collect and refund the money |
| Customers (`orchardcore-customers`) | Adds the Contact step, links `Order.CustomerId`, store credit refunds |
| Inventory (`orchardcore-inventory`) | Holds, sells, releases and restocks through order events |
| Shipping (`orchardcore-shipping`) | Adds the Shipping step, `Order.Shipping`, shipments and `FulfillmentStatus` |
| Taxation (`orchardcore-taxation`) | Tax lines copied to `Order.TaxLines` |
| Receipts (`orchardcore-receipts`) | `ReceiptSettings` brand invoices and packing slips |

## Features

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Orders` | Orders | Commerce, Carts, Checkout, `CrestApps.OrchardCore.Resources` |
| `CrestApps.OrchardCore.Orders.GiftCards` | Gift Cards | Orders, `OrchardCore.Title` |
| `CrestApps.OrchardCore.Orders.Returns` | Returns | Orders |
| `CrestApps.OrchardCore.Orders.Api` | Orders API | Orders, Cart API, Checkout API |

### Guidelines

- Install `CrestApps.OrchardCore.Orders` in the web/startup project and enable a payment provider.
- React to orders with `IOrderEventHandler`; never poll order status.
- Cancel only through `IOrderService.CancelAsync`, and refund through `OrderOperationsService`, so every handler and ledger stays in step.
- Send customer emails through `ICommerceEmailSender` so the store's templates, layout and the buyer's culture apply.
- Resolve services that need `IOrderService` from `IServiceProvider` inside the handler method to avoid a dependency cycle.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Orders",
        "CrestApps.OrchardCore.Orders.GiftCards",
        "CrestApps.OrchardCore.Orders.Returns",
        "CrestApps.OrchardCore.Customers",
        "CrestApps.OrchardCore.Stripe"
      ],
      "disable": []
    }
  ]
}
```

## Checking out

1. The cart is priced again and refused if a line can't be bought.
2. `IOrderService.CreateFromCartAsync` freezes it into a **Draft** order (product version, title, SKU, price, quantity, tax codes, categories, `Order.Culture`) and runs `CreatedAsync` handlers, which may refuse it.
3. Earlier unpaid orders from the same cart are canceled as `Replaced`.
4. A checkout session starts with reference `CheckoutReferenceTypes.Order`. `OrderCheckoutHandler` bills one line per order line.
5. On completion the order is **Paid** with its total, tax lines (`Order.TaxLines`), discounts (`Order.Discounts`, `DiscountTotal`) and tenders (`Order.Tenders`); bought lines leave the cart and the confirmation is emailed once.

| Status | Meaning |
| --- | --- |
| Draft | From a cart before checkout, or made by the store for a buyer (`Order.Invoice` set) |
| Awaiting payment | Checkout started, not completed |
| Paid | Checkout completed |
| Canceled | `CancelReason`: `Replaced`, `PaymentFailed`, `CheckoutCanceled`, `CheckoutExpired`, `Abandoned`, `Unavailable`, `CanceledByStore`, `Declined` (a declined quote), `Rejected` |

Refunds are tracked beside the status in `Order.Refunds`, `RefundedAmount` and `RefundStatus` (`PartiallyRefunded`, `Refunded`). `AbandonedOrdersBackgroundTask` cancels stale unpaid orders every 15 minutes.

## Order events

```csharp
public interface IOrderEventHandler
{
    Task CreatedAsync(OrderCreatedContext context, CancellationToken cancellationToken = default);

    Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default);

    Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default);

    Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task ReturnedAsync(OrderReturnedContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task LinesChangedAsync(OrderLinesChangedContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
```

| Event | Runs | Notes |
| --- | --- | --- |
| `CreatedAsync` | After the order is made | `context.Refuse(message)` cancels it as `Unavailable`; the cart shows the problems |
| `PaidAsync` | In the unit of work that marks it paid | Throwing rolls the completion back for the checkout to retry |
| `CanceledAsync` | On every cancel | `context.Restock` says whether stock goes back |
| `RefundedAsync` | After a refund is recorded | `context.Restock` only for a full refund the store asked to restock |
| `ReturnedAsync` | When a return's items arrive | Lines with `Restock` and `LocationId` |
| `LinesChangedAsync` | When a draft's lines are replaced | `context.Refuse(problem)` keeps the old lines |

Derive from `OrderEventHandlerBase` and override only what you need:

```csharp
public sealed class GiftWrapOrderHandler : OrderEventHandlerBase
{
    private readonly IServiceProvider _serviceProvider;

    public GiftWrapOrderHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
    {
        var notes = _serviceProvider.GetRequiredService<IGiftWrapNotes>();

        foreach (var line in context.Order.Lines.Where(line => line.Details?.ContainsKey("giftwrap.message") == true))
        {
            await notes.AddAsync(context.Order, line, cancellationToken);
        }
    }
}
```

Register with `services.AddScoped<IOrderEventHandler, GiftWrapOrderHandler>();`. To give the buyer something a paid order bought (files, a membership) and take it back on a cancel or full refund, derive from `OrderGrantHandlerBase` and implement `GrantAsync(Order, CancellationToken)` and `RevokeAsync(Order, OrderRevokeReason, CancellationToken)`. After a partial refund the buyer keeps what they bought.

## Managing an order

`OrderOperationsService` (permission `ManageOrders`) backs the order page and the staff API:

- **Refund** an amount or everything: online payments through `ICheckoutRefundService` (newest first), Pay Later through `ITransactionCommandService.RefundAsync`, then balances (gift card, store credit, points) through `ICheckoutTenderLedger.ReturnAsync`. With `asStoreCredit`, the amount goes to the buyer through `IOrderStoreCreditRefunder` instead.
- **Cancel** as `CanceledByStore`, optionally refunding everything and restocking.
- **Send the confirmation again**, and **Notes** that record who did what.

Refunds started anywhere are recorded on the order by `OrderRefundRecorder` (an `IPaymentRefundHandler`). Gateway disputes land in `Order.Disputes` and payment risk in `Order.PaymentRisk`.

## Order pages and display types

Orders build the order with `IDisplayManager<Order>` in `OrderConstants.DisplayTypes.DetailAdmin` (admin and My Orders) and `Detail` (guest and account). Add a section with a `DisplayDriver<Order>`:

```csharp
public sealed class GiftWrapOrderDisplayDriver : DisplayDriver<Order>
{
    public override IDisplayResult Display(Order order, BuildDisplayContext context)
        => Initialize<GiftWrapOrderViewModel>("GiftWrapOrder", model => model.OrderId = order.ItemId)
            .Location(OrderConstants.DisplayTypes.DetailAdmin, "Content:20");
}
```

## Customer emails

```csharp
services.AddCommerceEmailSender()
    .AddCommerceEmailKind(
        "giftwrap-sent",
        LocalizationSource.Create<Startup>("Gift wrapped"),
        LocalizationSource.Create<Startup>("{{ Message }}: the gift message."));
```

`ICommerceEmailSender` builds a `CommerceEmail` (`Kind`, `To`, `Order`, `Culture`, `Reference`) in the buyer's culture, applies the store's Liquid template and layout for its kind (**Settings > Commerce > Emails**), sends it, and returns `false` instead of throwing on failure. Orders registers `order-confirmation`, `order-invoice`, `order-quote` and `checkout-recovery`. Add a section to the confirmation email with `IOrderEmailSectionProvider`.

## Guest access and the storefront account

- `IOrderLinkService` signs a 60-day link (`orders/view/{token}`) opened read-only, with a pay link while something is owed.
- `orders/find` emails a guest their order links; `orders/track` finds an order by number and email. Both are rate limited (`find-order` group).
- `account/orders` and `account/orders/{orderId}` show the signed-in user's orders; a guest's confirmation can create an account from the order's email.
- **Settings > Commerce > Orders** (`OrderSettings`): `SendConfirmations`, `SendRecoveryEmails` with `RecoveryDelayHours`, `AllowQuoteRequests`, and the order bump (`OrderBumpProductId`, `OrderBumpPercentOff`, `OrderBumpHeadline`).

Drafts and quotes, invoices and packing slips, gift cards, returns, sales channels, reports, workflow events and automation recipes are in [references/order-features.md](references/order-features.md).

## Orders API

`CrestApps.OrchardCore.Orders.Api` maps `POST checkout` (checks the caller's cart out, holds stock, returns `X-Checkout-Token`), `me/orders`, `me/orders/{orderId}`, returns under it (with Returns), `me/quotes`, `checkout/{id}/gift-cards` (with Gift Cards) and the staff `orders` endpoints (list, get, by number, refund, cancel) with `ManageOrders`. `ApiOrder` carries `channel`, `culture` and grouped `taxes`. See the API reference in `orchardcore-commerce`.

## Permissions

| Permission | Default roles | Allows |
| --- | --- | --- |
| `ManageOrders` | Administrator, Store manager, Customer support | Orders, drafts, invoices, packing slips, returns, order settings and reports |
| `ViewOwnOrders` | Authenticated | My Orders and the account order pages |
| `ManageGiftCards` | Administrator, Store manager | **Commerce > Gift cards** |
