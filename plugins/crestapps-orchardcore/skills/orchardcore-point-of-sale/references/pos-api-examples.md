# Point of Sale API Examples

A shift as a till app makes it. Paths are relative to `https://your-site/api/commerce/v1/`. Every call carries the staff member's bearer token; send an `Idempotency-Key` header on calls that take one. Responses omit some fields.

## 1. Pick a register and open a shift

```http
GET pos/registers
```

```json
[
  { "id": "4r9k2x7m1q", "name": "Downtown till 1", "locationId": "downtown", "currency": "USD", "address": { "city": "Springfield", "country": "US" }, "openShiftId": null }
]
```

When `openShiftId` is set, continue that shift. Otherwise open one with the float put in the drawer:

```http
POST pos/registers/4r9k2x7m1q/shifts
Content-Type: application/json
Idempotency-Key: 6f1d2c1e-open-shift

{ "openingFloat": 100.00 }
```

`201 Created` with the shift. A second open shift answers `409` with code `shift-open`.

## 2. Ring up a sale

```http
POST pos/sales
Content-Type: application/json
Idempotency-Key: 6f1d2c1e-sale-1

{
  "registerId": "4r9k2x7m1q",
  "lines": [ { "productId": "4b2kx7m9qz1c", "quantity": 2 } ],
  "customerEmail": "avery@example.com",
  "customerName": "Avery Brooks"
}
```

Each line (`PosSaleLine`) takes `productId`, `variantId`, `priceId`, `quantity` and `addOns`, as when adding to a cart. Name a known buyer with `customerId` (an id that is not a customer answers `400`), a walk-in buyer with `customerEmail` and `customerName`, or neither.

```json
{
  "orderId": "4d1k8s0p2w",
  "orderNumber": "1043",
  "checkout": {
    "id": "7c2m9x1k",
    "status": "pending",
    "totals": {
      "subtotal": { "amount": 24.0, "currency": "USD" },
      "tax": { "amount": 1.98, "currency": "USD" },
      "total": { "amount": 25.98, "currency": "USD" },
      "amountDue": { "amount": 25.98, "currency": "USD" }
    }
  }
}
```

Stock is held at the register's location while the sale is paid. `409 shift-closed` without an open shift; `409 not-available` when a product cannot be sold or has run out. Add codes or gift cards with `POST checkout/7c2m9x1k/codes` or `POST checkout/7c2m9x1k/gift-cards`; cancel with `DELETE checkout/7c2m9x1k`. The same staff account that rang the sale pays it.

## 3. Take the payment

```http
POST pos/sales/7c2m9x1k/pay
Content-Type: application/json
Idempotency-Key: 6f1d2c1e-pay-1

{ "cash": 30.00 }
```

```json
{
  "status": "completed",
  "amountDue": { "amount": 0.0, "currency": "USD" },
  "change": { "amount": 4.02, "currency": "USD" },
  "receipt": {
    "orderNumber": "1043",
    "staff": "jordan",
    "lines": [ { "description": "Contoso mug", "quantity": 2, "unitPrice": { "amount": 12.0, "currency": "USD" }, "total": { "amount": 24.0, "currency": "USD" } } ],
    "total": { "amount": 25.98, "currency": "USD" },
    "payments": [ { "method": "Cash", "amount": { "amount": 25.98, "currency": "USD" } } ],
    "change": { "amount": 4.02, "currency": "USD" }
  }
}
```

| `status` | Meaning |
|---|---|
| `completed` | Paid and placed. Hand back `change`, print `receipt` |
| `due` | `amountDue` remains. Take more cash or a card |
| `pending` | A Stripe reader is waiting. Poll by paying with `{}` |

Send all cash handed over in one `cash` value. Cash plus a counter-terminal card in one call:

```json
{ "cash": 20.00, "card": { "approvalCode": "483920" } }
```

Send the rest to a Stripe reader:

```json
{ "reader": { "readerId": "tmr_..." } }
```

A declined reader payment answers `409 not-available`. A sale is paid in the shift it was rung up in; after that shift closes, paying answers `409 shift-closed`.

## 4. Receipt

The completing answer holds the receipt as data (`ApiPosReceipt`) for printers with their own layout. With Receipts enabled:

```http
GET pos/sales/4d1k8s0p2w/receipt
```

```http
POST pos/sales/4d1k8s0p2w/receipt/email
Content-Type: application/json

{ "email": "avery@example.com" }
```

The email call answers `204`. Without Receipts both answer `409 unsupported`.

## 5. Cash in and out

```http
POST pos/shifts/9s4m2k7x/cash
Content-Type: application/json
Idempotency-Key: 6f1d2c1e-cash-1

{ "amount": -200.00, "reason": "Banked at noon" }
```

## 6. Return at the till

```http
POST pos/returns
Content-Type: application/json
Idempotency-Key: 6f1d2c1e-return-1

{ "orderId": "4d1k8s0p2w", "registerId": "4r9k2x7m1q", "reason": "Wrong size", "restock": true }
```

```json
{
  "orderId": "4d1k8s0p2w",
  "orderNumber": "1043",
  "refunded": { "amount": 25.98, "currency": "USD" },
  "cashBack": { "amount": 25.98, "currency": "USD" },
  "messages": [ "..." ]
}
```

Omit `amount` to refund everything. The card is refunded first and cash last; `cashBack` is taken out of the open shift's drawer, so a return needs an open shift.

## 7. Close the shift

```http
POST pos/shifts/9s4m2k7x/close
Content-Type: application/json

{ "countedCash": 124.50, "note": "Short 1.48" }
```

```json
{
  "id": "9s4m2k7x",
  "registerName": "Downtown till 1",
  "status": "closed",
  "openingFloat": { "amount": 100.0, "currency": "USD" },
  "sales": 1,
  "total": { "amount": 25.98, "currency": "USD" },
  "cash": { "amount": 25.98, "currency": "USD" },
  "card": { "amount": 0.0, "currency": "USD" },
  "expectedCash": { "amount": 125.98, "currency": "USD" },
  "countedCash": { "amount": 124.5, "currency": "USD" },
  "difference": { "amount": -1.48, "currency": "USD" }
}
```

`GET pos/shifts/9s4m2k7x` returns the same while the shift is open.

## Pair a Stripe reader

A manager with **Manage registers and shifts** pairs a reader once by the code it shows:

```http
POST pos/terminal/readers
Content-Type: application/json

{
  "registrationCode": "sunny-river-moon",
  "label": "Downtown till 1",
  "address": { "addressLine1": "1 Main Street", "city": "Springfield", "region": "IL", "postalCode": "62701", "country": "US" }
}
```

```json
{ "id": "tmr_...", "label": "Downtown till 1", "deviceType": "bbpos_wisepos_e", "status": "online", "locationId": "tml_...", "action": null, "actionStatus": null }
```

Give `address` to have a Stripe location created, or `stripeLocationId` for one made in the Stripe Dashboard. In test mode, `simulated-wpe` pairs a simulated reader and `POST pos/terminal/readers/{readerId}/test-card` presents a test card while a sale waits.

The reader provider creates a `card_present` PaymentIntent (idempotency key `terminal-pi-{attemptId}`) and tells the reader in `ProviderData["readerId"]` to process it; verify reads the PaymentIntent, cancel cancels it, and refunds go through `IStripeRefundService`.
