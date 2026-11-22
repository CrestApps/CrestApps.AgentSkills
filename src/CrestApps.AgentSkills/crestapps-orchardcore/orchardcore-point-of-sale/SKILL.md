---
name: orchardcore-point-of-sale
description: Skill for the CrestApps Orchard Core Point of Sale commerce module. Covers registers at inventory locations, shifts with an opening float, cash movements, declared tips and a counted close, counter sales as orders on the checkout framework, department sales without items (department keys), the Point of Sale Web Register admin page, the cash tender, the pos-card counter terminal method, Stripe Terminal readers, receipts, returns at the till, and the point-of-sale endpoints of the commerce JSON API. Use this skill when requests mention Orchard Core point of sale, POS, till app, web register, cash register, departments, register shifts, cash drawer, counter sales, Stripe Terminal or in-store returns. Strong matches include CrestApps.OrchardCore.PointOfSale, CrestApps.OrchardCore.PointOfSale.WebRegister, PosDepartment, RegisterKey, Register, RegisterShift, PosSaleService, PosShiftService, PosCashTenderProvider, StripeTerminalPaymentProvider, OrderChannels.PointOfSale, SellAtRegister and ManageRegisters.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Point of Sale - Prompt Templates

## Sell at the Counter

You are an Orchard Core expert. Generate accurate setup, API and extension guidance for the CrestApps Point of Sale module, which sells at a store's counter from a till app over the commerce API, or from the **Web register** admin page. Each sale is an ordinary order paid through the checkout framework, so prices, coupons, gift cards, tax, stock holds, order events and reports work as online.

### Guidelines

- Install `CrestApps.OrchardCore.PointOfSale` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle) in the web/startup project.
- Enable `CrestApps.OrchardCore.PointOfSale`, and `CrestApps.OrchardCore.PointOfSale.WebRegister` (Point of Sale Web Register, depends on Point of Sale) to sell in the browser without a till app. Point of Sale depends on `CrestApps.OrchardCore.Commerce`, `CrestApps.OrchardCore.Orders`, `CrestApps.OrchardCore.Inventory` and `CrestApps.OrchardCore.Checkout.Api` (the commerce JSON API, see `orchardcore-checkout` and `orchardcore-commerce`).
- Products come from `CrestApps.OrchardCore.Products` (see `orchardcore-products`). Registers sell from Inventory locations.
- Optional: `CrestApps.OrchardCore.Receipts` for printable and emailed receipts, `CrestApps.OrchardCore.Customers` for named buyers (see `orchardcore-customers`), gift cards, and `CrestApps.OrchardCore.Stripe` for Stripe Terminal readers (see `orchardcore-payments`).
- With Payroll's Pay Setup and Staff on, the card and cash tips declared when a shift closes become `TIPS` and `CTIPS` pay inputs (see `orchardcore-payroll`).
- Every API call is made by a signed-in staff member with a bearer token and **Sell at a register**. Send an `Idempotency-Key` header on calls that take one, so a dropped connection never rings a sale twice.
- Store credit and loyalty points are not offered at a register.
- Orders and stock locations are covered by `orchardcore-orders` and `orchardcore-inventory`; the shared Commerce menu by `orchardcore-commerce`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature and Services

| Item | Value |
|---|---|
| Feature IDs | `CrestApps.OrchardCore.PointOfSale` (Point of Sale); `CrestApps.OrchardCore.PointOfSale.WebRegister` (Point of Sale Web Register, depends on Point of Sale) |
| YesSql collection | `PointOfSale` (`Register`/`RegisterIndex`, `RegisterShift`/`RegisterShiftIndex`, `PosDepartment`/`PosDepartmentIndex`) |
| Stores | `IRegisterStore`, `IRegisterShiftStore`, `IPosDepartmentStore` |
| Services | `PosShiftService` (`MaySellAtAsync`, `SummarizeAsync`, `OpenAsync`, `MoveCashAsync`, `CloseAsync`, `DeclareTipsAsync`), `PosSaleService` (`StartAsync`, `PayAsync`, `GetChange`), `PosReceipts`, `PosDepartments.CheckAmount`, `PosDepartmentLines` (`Price`, `IsDepartmentLine`) |
| Cash tender | `PosCashTenderProvider` (`ICheckoutTenderProvider`, key `cash`) |
| Counter terminal | `PosCounterCardPaymentProvider` (`ICheckoutPaymentProvider` and `ICheckoutPaymentRefundProvider`, key `pos-card`) |
| Stripe reader | `StripeTerminalPaymentProvider` (key `stripe-terminal`), added by Stripe's `TerminalStartup` when both features are on |
| Session marker | `CheckoutCounterSale` (`RegisterId`, `ShiftId`, `LocationId`) |
| Order channel | `OrderChannels.PointOfSale` (`pos`); `OrderChannels.IsCounterSale(order)` |
| API | `api/commerce/v1/pos/...`, tag **Point of Sale** (`PosApiEndpoints`, `StripeTerminalApiEndpoints`) |
| Permissions | `SellAtRegister`, `ManageRegisters` (Administrator and Store manager have both) |
| Admin | **Commerce > Point of sale > Registers** (`point-of-sale/registers`), **Shifts** (`point-of-sale/shifts`) and **Departments** (`pos-departments`), all for **Manage registers and shifts**, on the `AdminList` standard; **Commerce > Point of sale > Web register** (`pos-register`, Web Register feature, **Sell at a register**) |

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
        "CrestApps.OrchardCore.PointOfSale",
        "CrestApps.OrchardCore.PointOfSale.WebRegister"
      ],
      "disable": []
    }
  ]
}
```

## Model

- **`Register`**: `Name`, `LocationId` (an inventory location), `Address` (sales are taxed by and billed to it), `Currency` (empty sells in the products' own currency), `Roles` (empty lets anyone with **Sell at a register**), `IsEnabled`, `Keys` (department keys, each a `RegisterKey`: `DepartmentId`, `Label`, `PresetAmount`; empty shows a key for every enabled department).
- **`PosDepartment`**: `Name`, `Number` (unique when given; order lines keep it as their SKU), `TaxCategoryCode` (empty uses the store's default rate), `Color` (hex color of its key), `MinimumAmount` and `MaximumAmount` (limits of one ring-up), `Position`, `IsEnabled`.
- **`RegisterShift`**: `RegisterId`, `RegisterName`, `LocationId`, `Currency`, `Status` (`Open` or `Closed`), who opened and closed it, `OpeningFloat`, `CashMovements` (`RegisterCashMovement`: `Amount` positive in, negative out, `Reason`), and at close `CountedCash`, `ExpectedCash`, `CloseNote`, `Summary` and the declared tips `CardTips` and `CashTips`.
- **`RegisterShiftSummary`**: `Sales`, `Total`, `Cash` (kept: handed over less change), `Card`, `Other` (gift cards and other balances), `Refunded`, `Tax` (the sales' `TaxAmount`) and `Departments` (each a `RegisterShiftDepartmentTotal`: `DepartmentId`, `Name`, `Quantity`, `Total` before tax, largest first), computed from the shift's paid orders (`OrderQuery.ShiftId`).
- `RegisterShift.GetExpectedCash(cashKept)` is the float plus cash kept for sales plus cash movements.

A register has one open shift at a time.

## How a Sale Works

`PosSaleService.StartAsync` prices the lines as a cart (`ICartService.PriceAsync`, without saving a cart) and creates the order with `IOrderService.CreateFromCartAsync`, setting `Channel` `pos`, `LocationId` (Inventory sells the stock there), `RegisterId`, `RegisterName`, `ShiftId`, `StaffUserId`, `StaffName`, the buyer when named, and `BillingAddress` from the register. It then starts the checkout with `ICheckoutEngine.StartAsync` and stores `CheckoutCounterSale`, an *In store* zero-rate `CheckoutShippingInfo` at the register address (so tax is charged where it is sold), and the buyer's `CheckoutContactInfo`. Counter sales skip the Customers Contact step and the Shipping step.

`PosSaleService.PayAsync` pays in order:

1. **Cash** through the `cash` tender, whose reference is the cash handed over in minor units (`PosCashTenderProvider.ToReference`). It pays up to what is due, after tax, only on a counter-sale session. Change is cash handed over less what the cash tenders paid.
2. **Card** for the rest: `pos-card` records an approval code from the store's own terminal (reference `pos_card_{code}`, verified as paid at once; refunds are recorded and made on the terminal), or `stripe-terminal` sends it to a Stripe reader.
3. **Completion** through `ICheckoutEngine.TryCompleteAsync`. A reader payment stays pending until Stripe reports the PaymentIntent succeeded.

Returns (`POST pos/returns`) refund through `IOrderOperationsService.RefundAsync` (Orders.Core), card first and cash last, restock at the order's location unless `restock` is `false`, and record the cash handed back as a negative cash movement on the register's open shift. Only register sales are returned at a till, unless the caller has **Manage orders**.

## Departments

A department rings up an amount when there is no item to track, as on a cash register ($10.00 keyed in, **Candy** pressed). Departments are module records, not content items, so they never appear in the online catalog, search, product feeds or agent catalogs.

A sale line with `departmentId` and `amount` (and optional `quantity`: 3 at 1.50) is checked by `PosDepartments.CheckAmount` (department enabled, amount above zero, whole cents, within its limits; otherwise `409`) and priced as keyed in by `PosDepartmentLines.Price`, without `ICartService`. On the order it is an `OrderLine` whose `ContentItemId` is the department id, `ContentType` is `PosDepartment` (`PosConstants.DepartmentLineType`), `Title`/`Sku` are the department's name and number, `TaxCategoryCode` is the department's tax category (the checkout taxes the line by it), and `ProductType` is `Undefined` (never shipped).

Downstream nothing needs a content item: Inventory holds no stock for it, refunds, receipts and reports read the line, store-wide coupons apply (product and category coupons don't match), review request emails skip it, and webhooks and the orders API show the department id as `productId`.

## Web Register

`CrestApps.OrchardCore.PointOfSale.WebRegister` adds **Commerce > Point of sale > Web register** (`pos-register`, `WebRegisterController`), for users with **Sell at a register** and **Access admin panel**. It lists the registers the user may sell at, or opens the only one at `pos-register/Till/{registerId}`. The page calls the controller's JSON actions with the admin cookie and an antiforgery token (`RequestVerificationToken` header); they call the same services as the API, so its sales are the same orders and checkouts.

| Action | Does |
|---|---|
| `GET Shift` | The open shift, as the shift report reads it, or none |
| `POST OpenShift`, `MoveCash`, `CloseShift` | `PosShiftService.OpenAsync`, `MoveCashAsync`, `CloseAsync` |
| `GET Product?sku=` | A published product or variant by SKU (`ProductSkuIndex`), priced at the register; works with a barcode scanner |
| `POST Sale` | `PosSaleService.StartAsync` with department and product lines; answers the checkout id, subtotal, discount, tax, total and amount due |
| `POST Pay` | `PosSaleService.PayAsync` with cash handed over and/or a counter-terminal card (`approvalCode`) |
| `POST Cancel` | Voids an unpaid sale (`ICheckoutEngine.CancelAsync`, which cancels its order) |
| `GET Receipt?orderId=` | The branded receipt with Receipts on, otherwise a plain 80 mm till receipt |

Amounts are keyed in cents first, and `×` multiplies. The web register does not name a customer, take codes or gift cards, use Stripe readers, or take returns; a till app does those through the API.

## Tips and Payroll

Closing a shift takes the tips the cashier declares (`cardTips`, added to card payments, and `cashTips`, cash kept), kept as `RegisterShift.CardTips` and `CashTips`. `PosShiftService.DeclareTipsAsync` corrects them from the shift page until payroll took them. With Pay Setup and Staff on, `PosTipsPayInputSource` (source kind `RegisterShift`) gives them to payroll for the staff member who opened the shift (card tips on pay code `TIPS`, cash tips on `CTIPS`) and stamps the shift (`RegisterShiftPayroll`) when a run takes them.

## Recipes, deployment and reporting

The `PosDepartments` step (`PosDepartmentsStep`) creates or updates departments, matched by `ItemId`, then by name, and validated like the admin screen (a name, a unique number, a hex color, the least amount no more than the most). The **Register departments** deployment step (*Commerce*) writes every department. Import departments before the registers whose keys name them.

The `Registers` step creates or updates registers, matched by `ItemId`, then by name, validated like the admin screen (a name, an existing store location, an address with street, city and two-letter country, a three-letter currency or none). Import stock locations first (`StockLocations`, see `orchardcore-inventory`). The **Registers** deployment step (*Commerce*) writes every register; shifts and sales stay on the site.

```json
{
  "steps": [
    {
      "name": "PosDepartments",
      "PosDepartments": [
        {
          "ItemId": "4dp00000000000000000000004",
          "Name": "Candy",
          "Number": "4",
          "TaxCategoryCode": "Candy",
          "Color": "#C2185B",
          "MaximumAmount": 50,
          "Position": 4,
          "IsEnabled": true
        }
      ]
    },
    {
      "name": "Registers",
      "Registers": [
        {
          "ItemId": "4rg00000000000000000000001",
          "Name": "Front till",
          "LocationId": "default",
          "Address": { "AddressLine1": "1 Main St", "City": "Springfield", "Region": "IL", "PostalCode": "62701", "Country": "US" },
          "Currency": "USD",
          "Roles": [ "Cashier" ],
          "IsEnabled": true,
          "Keys": [
            { "DepartmentId": "4dp00000000000000000000004", "Label": "Candy", "PresetAmount": null }
          ]
        }
      ]
    }
  ]
}
```

With **Data Pipelines Core** on (Report Builder), the **Point of Sale** data source (`PointOfSale`, permission `ManageRegisters`) has `Registers` and `RegisterShifts`; the Orders data set's `RegisterId` and `ShiftId` join them. Receipts use the `pos-receipt` email template.

## API Summary

All paths are under `api/commerce/v1/`. A register the caller may not sell at answers `404`.

| Method | Path | Purpose |
|---|---|---|
| `GET` | `pos/registers` | Registers the caller may sell at, with `openShiftId` |
| `GET` | `pos/registers/{registerId}/keys` | Its department keys in order (`departmentId`, `label`, `number`, `color`, `presetAmount`, `minimumAmount`, `maximumAmount`) |
| `POST` | `pos/registers/{registerId}/shifts` | Open a shift (`openingFloat`); `409 shift-open` |
| `GET` | `pos/shifts/{shiftId}` | Shift with summary, `tax`, `departments` totals and expected cash |
| `POST` | `pos/shifts/{shiftId}/cash` | Cash in or out (`amount`, `reason`) |
| `POST` | `pos/shifts/{shiftId}/close` | Close with `countedCash`, `note`, and declared `cardTips` and `cashTips` |
| `POST` | `pos/sales` | Ring up `lines` (product lines, or `departmentId` + `amount` + `quantity`) for a `customerId`, or `customerEmail` and `customerName`; `201` with `orderId`, `orderNumber`, `checkout` |
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
| `SellAtRegister` ("Sell at a register") | The POS API and the web register at registers whose `Roles` include the user's role (or have none): shifts, sales, payments, receipts and returns. Web register users also need **Access admin panel** |
| `ManageRegisters` ("Manage registers and shifts") | Registers, shifts and departments admin, selling at every register, and pairing Stripe readers |

## Troubleshooting

| Symptom | Check |
|---|---|
| `GET pos/registers` is empty | The register is disabled, or its `Roles` exclude the staff member's roles |
| `409 shift-closed` | Open a shift; a sale is paid and returned only while its shift is open |
| `409 not-available` on a sale | The product cannot be sold or is out of stock at the register's location |
| `409 not-available` on a reader payment | The card was declined. Send to the reader again or take another payment |
| Receipt answers `409 unsupported` | Enable `CrestApps.OrchardCore.Receipts` |
| Stripe reader endpoints missing | Enable `CrestApps.OrchardCore.Stripe` with Point of Sale |
| Department sale answers `409` | The department is disabled, or the amount is zero, has fractions of a cent, or is outside its minimum/maximum |
| **Web register** missing from the menu | Enable `CrestApps.OrchardCore.PointOfSale.WebRegister`; the user needs **Sell at a register** and **Access admin panel** |
| Department key missing on a register | The department is disabled, or the register's `Keys` list is set and does not include it |
| Tax charged at the wrong place | Set the register's `Address`; it is the in-store shipping address used for tax |
