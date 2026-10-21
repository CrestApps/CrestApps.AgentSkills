# Order Features

## Invoices and packing slips

`OrderDocumentBuilder` builds an `OrderDocumentViewModel` for the `OrderInvoice` and `OrderPackingSlip` shapes (override in Razor or Liquid). The header comes from `ReceiptSettings` (the Receipts module need not be on); addresses are printed with `IAddressFormatter`.

| Route | Name | Who |
| --- | --- | --- |
| `Admin/orders/{orderId}/invoice` | `OrdersInvoice` | `ManageOrders` |
| `Admin/orders/{orderId}/packing-slip?lines=lineId:qty,...&shipment=N` | `OrdersPackingSlip` | `ManageOrders` |
| `account/orders/{orderId}/invoice` | `AccountOrderInvoice` | The order's user with `ViewOwnOrders` |
| `orders/view/{token}/invoice` | `GuestOrderInvoice` | Anyone with the signed link |

## Orders the store creates

`DraftOrderService` prices chosen products as a cart for the buyer's account (so their own prices apply) and keeps a `Draft` with `Order.Invoice` (`OrderInvoice`: `CreatedBy`, `Note`, `DueUtc`, `SentUtc`, `AddsToOrderId`). Admin routes: `Admin/orders/drafts/create` (`OrdersCreate`), `Admin/orders/{orderId}/edit` (`OrdersEdit`), `Admin/orders/{orderId}/send-invoice` (`OrdersSendInvoice`). `IOrderService.ReplaceLinesAsync(order, pricing)` replaces a draft's lines and raises `LinesChangedAsync`. The buyer pays at `orders/view/{token}/pay` (`GuestOrderPay`) or `account/orders/{orderId}/pay`. **Add items** on a paid order starts a new order.

## Quotes

With `OrderSettings.AllowQuoteRequests`, `QuoteCartActionProvider` adds **Request a quote** (`cart/quote`). `QuoteService.RequestAsync` creates a draft with `Order.Quote` (`OrderQuote`: `RequestedUtc`, `Message`, `Status`, `SentUtc`, `ExpiresUtc`, `RespondedUtc`) and deletes the cart. Staff set `OrderLine.QuotedUnitPrice` (read as invariant text; quoted lines are excluded from discounts) and send it; `QuoteService.CanPay(order, now)` gates payment; `DeclineAsync` cancels as `Declined`. API: `POST me/quotes`, `POST me/orders/{orderId}/decline-quote`.

## Sales channels

`Order.Channel` (`OrderChannels`): `online`, `pos` (with `LocationId`, `RegisterId`, `ShiftId`, staff), `draft`. `OrderIndex` has `Channel`, `LocationId`, `ShiftId`; the orders list and API filter by them.

## Order bump and offers

`OrderBumpCheckoutFlowDisplayDriver` renders `CheckoutOrderBump` on the payment step for the `OrderSettings.OrderBumpProductId` product; `OrderCheckoutHandler` adds it as a line on completion. The confirmation renders `OrderConfirmationOffers` with cross-sells.

## Email templates

`LiquidOrderEmailTemplates` reads `OrderEmailTemplateSettings` (**Settings > Commerce > Emails**, group `order-emails`): a `Layout` with `{{ Content }}` and `Templates` by kind (`IsEnabled`, `Subject`, `Body`). The Liquid gets `Content`, `Subject`, `StoreName`, `Order`, and the email's values (`OrderUrl`, `PayUrl`, `TrackingUrl`, `Carrier`, `ShipmentNumber`, `ReturnNumber`, `ReturnStatus`, `Code`, `Amount`, `RecipientName`, `Message`, `RecoveryUrl`). Unparseable Liquid is refused on save; a template that throws falls back to the built email. Kinds: Orders `order-confirmation`, `order-invoice`, `order-quote`, `checkout-recovery`; Returns `order-return`; Gift Cards `gift-card`; Shipping `order-shipped`; Bookings `booking`.

## Recovery emails

`CheckoutRecoveryBackgroundTask` (every 15 minutes) emails a buyer once when an order awaits payment untouched or was canceled because they left, older than `RecoveryDelayHours` and within a week, with a signed `orders/resume/{token}` link (14 days) that resumes the checkout or rebuilds the cart. Requires the site's **Base URL**.

## Gift cards

Feature `CrestApps.OrchardCore.Orders.GiftCards`.

- `GiftCard` (`Orders` collection, `GiftCardIndex`): `Code`, `InitialValue`, `Balance`, `Currency`, `IsEnabled`, `ExpiresUtc`, recipient, `Note`, `OrderId`, `Activity`. `ChangeBalance` is the only way the balance moves.
- Codes: `GiftCardCodes.Generate()` makes 16 characters in groups of four, without 0, O, 1 or I; `Normalize` makes lookups ignore dashes, spaces and case.
- `GiftCardService.IssueAsync` and `AdjustAsync` (under the checkout tender lock).
- Paying: `GiftCardTenderProvider` is the `ICheckoutTenderProvider` with key `gift-card` (`OrderConstants.GiftCardTenderKey`), applied after tax.
- Selling: the migration creates a `GiftCard` content type with `GiftCardPart`; any product type with the part sells cards. `GiftCardOrderEventHandler.PaidAsync` issues one card per unit, disabled while an offline payment is owed. Cancels and full refunds disable unspent cards.
- Admin: **Commerce > Gift cards** (`Admin/gift-cards`, `ManageGiftCards`), on `AdminList`.

## Returns

Feature `CrestApps.OrchardCore.Orders.Returns`.

- `OrderReturn` (`OrderReturnIndex`): `Number` (such as `R-261019-K3QZ`), `Lines` (`OrderReturnLine`), `Resolution` (`Refund` or `Exchange`), `Comment`, `StoreMessage`, `RefundedAmount`, `RestockLocationId`, `Status` (`Requested`, `Approved`, `Received`, `Completed`, `Rejected`, `Canceled`).
- `OrderReturnPolicy.GetReturnable` and `IsOpen` (within `ReturnSettings.WindowDays` of payment).
- Buyer: `account/orders/{orderId}/return`, `orders/view/{token}/return`; `OrderReturns_Detail` and `OrderReturns_DetailAdmin` shapes.
- Store: `Admin/returns` (`ManageOrders`) through `OrderReturnService`: approve, reject, receive (restock lines at a location, raising `ReturnedAsync`), refund (through `OrderOperationsService.RefundAsync`), exchanged.
- `ReturnSettings` (group `returns`): `WindowDays` (30), `Reasons` (up to 20), `AllowExchanges`, `Instructions`.

## Reports

With Reports on (see `orchardcore-reports`), **Reports > Commerce** (`ManageOrders`) gains Sales, Sales by product, Sales by category, Customers, Cohorts and retention, Checkouts, Sales by country, Tax on orders, VAT one-stop shop (OSS), Discounts on orders, Refunds, Disputes and Orders to ship. Amounts stay in their own currency. With Orchard Core's Admin Dashboard, the **Sales Summary** widget (`SalesSummaryWidget`) shows recent sales.

## Workflow events and automation recipes

With Workflows on: **Order Paid**, **Order Canceled**, **Order Refunded** (input `Order`, `OrderId`, `OrderNumber`, `Total`, `Currency`, `CustomerEmail`, plus `Reason`/`Note` or `Amount`/`RefundedAmount`/`FullyRefunded`). Recipes **Automation: thank-you email** and **Automation: canceled order alert** add a `WorkflowType` from `OrderPaidEvent` or `OrderCanceledEvent` to an `EmailTask`.

## Signed links for your own module

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSignedLinks();
    }
}
```

Use `SignedTokenService` to sign and read a purpose-bound token and `SiteUrlGenerator` for the absolute URL from the site's base URL. Links depend on the tenant's Data Protection keys.
