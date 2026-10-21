---
name: orchardcore-point-of-sale
description: Skill for the CrestApps Orchard Core Point of Sale commerce module. Covers registers at inventory locations, shifts with an opening float, cash movements and a counted close, counter sales created as orders on the checkout framework, the cash tender, the pos-card counter terminal payment method, Stripe Terminal readers, receipts as data or Receipts HTML, returns at the till, and the point-of-sale endpoints of the commerce JSON API that a till app calls. Use this skill when requests mention Orchard Core point of sale, POS, till app, cash register, register shifts, cash drawer, counter sales, Stripe Terminal, card readers, or in-store returns. Strong matches include CrestApps.OrchardCore.PointOfSale, Register, RegisterShift, RegisterShiftSummary, PosSaleService, PosShiftService, PosCashTenderProvider, PosCounterCardPaymentProvider, StripeTerminalPaymentProvider, CheckoutCounterSale, OrderChannels.PointOfSale, SellAtRegister and ManageRegisters.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Point of Sale - Prompt Templates

## Sell at the Counter

You are an Orchard Core expert. Generate accurate setup, API and extension guidance for the CrestApps Point of Sale module, which sells at a store's counter from a till app over the commerce API. Each sale is an ordinary order paid through the checkout framework, so prices, coupons, gift cards, tax, stock holds, order events and reports work as online.

### Guidelines

- Install `CrestApps.OrchardCore.PointOfSale` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle) in the web/startup project.
- Enable `CrestApps.OrchardCore.PointOfSale`. It depends on `CrestApps.OrchardCore.Commerce`, `CrestApps.OrchardCore.Orders`, `CrestApps.OrchardCore.Inventory` and `CrestApps.OrchardCore.Checkout.Api` (the commerce JSON API, see `orchardcore-checkout` and `orchardcore-commerce`).
- Products come from `CrestApps.OrchardCore.Products` (see `orchardcore-products`). Registers sell from Inventory locations.
- Optional: `CrestApps.OrchardCore.Receipts` for printable and emailed receipts, `CrestApps.OrchardCore.Customers` for named buyers (see `orchardcore-customers`), gift cards, and `CrestApps.OrchardCore.Stripe` for Stripe Terminal readers (see `orchardcore-payments`).
- Every API call is made by a signed-in staff member with a bearer token and **Sell at a register**. Send an `Idempotency-Key` header on calls that take one, so a dropped connection never rings a sale twice.
- Store credit and loyalty points are not offered at a register.
- Orders and stock locations are covered by `orchardcore-orders` and `orchardcore-inventory`; the shared Commerce menu by `orchardcore-commerce`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature and Services

| Item | Value |
|---|---|
| Feature ID | `CrestApps.OrchardCore.PointOfSale` |
| YesSql collection | `PointOfSale` (`Register`/`RegisterIndex`, `RegisterShift`/`RegisterShiftIndex`) |
| Stores | `IRegisterStore`, `IRegisterShiftStore` |
| Services | `PosShiftService` (`MaySellAtAsync`, `SummarizeAsync`), `PosSaleService` (`StartAsync`, `PayAsync`, `GetChange`), `PosReceipts` |
| Cash tender | `PosCashTenderProvider` (`ICheckoutTenderProvider`, key `cash`) |
| Counter terminal | `PosCounterCardPaymentProvider` (`ICheckoutPaymentProvider` and `ICheckoutPaymentRefundProvider`, key `pos-card`) |
| Stripe reader | `StripeTerminalPaymentProvider` (key `stripe-terminal`), added by Stripe's `TerminalStartup` when both features are on |
| Session marker | `CheckoutCounterSale` (`RegisterId`, `ShiftId`, `LocationId`) |
| Order channel | `OrderChannels.PointOfSale` (`pos`); `OrderChannels.IsCounterSale(order)` |
| API | `api/commerce/v1/pos/...`, tag **Point of Sale** (`PosApiEndpoints`, `StripeTerminalApiEndpoints`) |
| Permissions | `SellAtRegister`, `ManageRegisters` (Administrator and Store manager have both) |
| Admin | **Commerce > Point of sale > Registers** (`point-of-sale/registers`) and **Shifts** (`point-of-sale/shifts`) on the `AdminList` standard |

### Enable the Feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Products",
        "CrestApps.OrchardCore.Inventory",
        "CrestApps.OrchardCore.Orders",
        "CrestApps.OrchardCore.Checkout.Api",
        "CrestApps.OrchardCore.Receipts",
        "CrestApps.OrchardCore.PointOfSale"
      ],
      "disable": []
    }
  ]
}
```

## Model

- **`Register`**: `Name`, `LocationId` (an inventory location), `Address` (sales are taxed by and billed to it), `Currency` (empty sells in the products' own currency), `Roles` (empty lets anyone with **Sell at a register**), `IsEnabled`.
- **`RegisterShift`**: `RegisterId`, `RegisterName`, `LocationId`, `Currency`, `Status` (`Open` or `Closed`), who opened and closed it, `OpeningFloat`, `CashMovements` (`RegisterCashMovement`: `Amount` positive in, negative out, `Reason`), and at close `CountedCash`, `ExpectedCash`, `CloseNote` and `Summary`.
- **`RegisterShiftSummary`**: `Sales`, `Total`, `Cash` (kept: handed over less change), `Card`, `Other` (gift cards and other balances) and `Refunded`, computed from the shift's paid orders (`OrderQuery.ShiftId`).
- `RegisterShift.GetExpectedCash(cashKept)` is the float plus cash kept for sales plus cash movements.

A register has one open shift at a time.

## How a Sale Works

`PosSaleService.StartAsync` prices the lines as a cart (`ICartService.PriceAsync`, without saving a cart) and creates the order with `IOrderService.CreateFromCartAsync`, setting `Channel` `pos`, `LocationId` (Inventory sells the stock there), `RegisterId`, `RegisterName`, `ShiftId`, `StaffUserId`, `StaffName`, the buyer when named, and `BillingAddress` from the register. It then starts the checkout with `ICheckoutEngine.StartAsync` and stores `CheckoutCounterSale`, an *In store* zero-rate `CheckoutShippingInfo` at the register address (so tax is charged where it is sold), and the buyer's `CheckoutContactInfo`. Counter sales skip the Customers Contact step and the Shipping step.

`PosSaleService.PayAsync` pays in order:

1. **Cash** through the `cash` tender, whose reference is the cash handed over in minor units (`PosCashTenderProvider.ToReference`). It pays up to what is due, after tax, only on a counter-sale session. Change is cash handed over less what the cash tenders paid.
2. **Card** for the rest: `pos-card` records an approval code from the store's own terminal (reference `pos_card_{code}`, verified as paid at once; refunds are recorded and made on the terminal), or `stripe-terminal` sends it to a Stripe reader.
3. **Completion** through `ICheckoutEngine.TryCompleteAsync`. A reader payment stays pending until Stripe reports the PaymentIntent succeeded.

Returns (`POST pos/returns`) refund through `OrderOperationsService.RefundAsync`, card first and cash last, restock at the order's location unless `restock` is `false`, and record the cash handed back as a negative cash movement on the register's open shift. Only register sales are returned at a till, unless the caller has **Manage orders**.

## API Summary

All paths are under `api/commerce/v1/`. A register the caller may not sell at answers `404`.

| Method | Path | Purpose |
|---|---|---|
| `GET` | `pos/registers` | Registers the caller may sell at, with `openShiftId` |
| `POST` | `pos/registers/{registerId}/shifts` | Open a shift (`openingFloat`); `409 shift-open` |
| `GET` | `pos/shifts/{shiftId}` | Shift with summary and expected cash |
| `POST` | `pos/shifts/{shiftId}/cash` | Cash in or out (`amount`, `reason`) |
| `POST` | `pos/shifts/{shiftId}/close` | Close with `countedCash` and `note` |
| `POST` | `pos/sales` | Ring up `lines` for a `customerId`, or `customerEmail` and `customerName`; `201` with `orderId`, `orderNumber`, `checkout` |
| `POST` | `pos/sales/{checkoutId}/pay` | `cash`, then `card` (`approvalCode`) or `reader` (`readerId`); `status` is `completed`, `due` or `pending`; `{}` polls a reader |
| `GET` | `pos/sales/{orderId}/receipt` | Receipt HTML (`409 unsupported` without Receipts) |
| `POST` | `pos/sales/{orderId}/receipt/email` | Email the receipt to `email` |
| `POST` | `pos/returns` | `orderId`, `registerId`, `amount`, `reason`, `restock` |
| `GET` | `pos/terminal/readers` | Stripe readers (Sell at a register) |
| `POST` | `pos/terminal/readers` | Pair a reader by `registrationCode` (Manage registers and shifts); `simulated-wpe` in test mode |
| `POST` | `pos/terminal/readers/{readerId}/test-card` | Present a test card on a simulated reader (test mode only) |

Coupon codes and gift cards go on the sale's checkout with the checkout endpoints (`POST checkout/{id}/codes`, `POST checkout/{id}/gift-cards`), and `DELETE checkout/{id}` cancels an unpaid sale and releases its stock.

See [references/pos-api-examples.md](references/pos-api-examples.md) for a full shift, call by call, with request and response bodies.

## Permissions

| Permission | Allows |
|---|---|
| `SellAtRegister` ("Sell at a register") | The POS API at registers whose `Roles` include the user's role (or have none): shifts, sales, payments, receipts and returns |
| `ManageRegisters` ("Manage registers and shifts") | Registers and shifts admin, selling at every register, and pairing Stripe readers |

## Troubleshooting

| Symptom | Check |
|---|---|
| `GET pos/registers` is empty | The register is disabled, or its `Roles` exclude the staff member's roles |
| `409 shift-closed` | Open a shift; a sale is paid and returned only while its shift is open |
| `409 not-available` on a sale | The product cannot be sold or is out of stock at the register's location |
| `409 not-available` on a reader payment | The card was declined. Send to the reader again or take another payment |
| Receipt answers `409 unsupported` | Enable `CrestApps.OrchardCore.Receipts` |
| Stripe reader endpoints missing | Enable `CrestApps.OrchardCore.Stripe` with Point of Sale |
| Tax charged at the wrong place | Set the register's `Address`; it is the in-store shipping address used for tax |
