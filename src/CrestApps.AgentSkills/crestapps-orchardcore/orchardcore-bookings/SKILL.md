---
name: orchardcore-bookings
description: Skill for the CrestApps Orchard Core Bookings commerce module. Covers the BookingPart for Departure, Nightly and TimeSlot products, capacity per date in BookingSlot records, occupancy pricing, weekend prices, deposits with the balance charged to the saved card, cancellation tiers and refunds, request-to-book quotes, Traveler details at checkout, the booking search and calendar, and the BookingDates recipe step. Use this skill when requests mention Orchard Core bookings, tours, appointments, room or rental stays, time slots, booking deposits, or booking cancellation policies. Strong matches include CrestApps.OrchardCore.Bookings, BookingPart, BookingKind, BookingSlot, BookingSchedule, BookingCapacityService, BookingService, BookingDepositService, BookingBalanceTenderProvider, BookingCartLineHandler, ManageBookings and BookingDates.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Bookings - Prompt Templates

## Sell Products by Date

You are an Orchard Core expert. Generate accurate setup, recipes and extension guidance for the CrestApps Bookings module, which sells products by date as fixed departures, nightly stays or time slots.

### Guidelines

- Install `CrestApps.OrchardCore.Bookings` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle) in the web/startup project.
- Enable `CrestApps.OrchardCore.Bookings`. It depends on `CrestApps.OrchardCore.Commerce`, `CrestApps.OrchardCore.Orders`, `CrestApps.OrchardCore.Customers` and `CrestApps.OrchardCore.Transactions`, and needs a sellable product (`CrestApps.OrchardCore.Products`, Carts and `CrestApps.OrchardCore.Checkout`) to be useful.
- A booking product is an ordinary product (`ProductPart`) with the attachable `BookingPart`. See `orchardcore-products` for the product itself and `orchardcore-checkout` for the checkout it is paid through.
- Store dates in the site's time zone. Slot keys are `yyyy-MM-dd`, or `yyyy-MM-ddTHH:mm` for a time slot.
- Do not decrement capacity yourself. `BookingCapacityService` holds, commits and releases dates under a distributed lock per product.
- A deposit requires a payment provider that can save a card for off-session charges (Stripe). See `orchardcore-payments`.
- Orders and carts are covered by `orchardcore-orders` and `orchardcore-carts`; the shared Commerce menu and storefront account by `orchardcore-commerce`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature and Services

| Item | Value |
|---|---|
| Feature ID | `CrestApps.OrchardCore.Bookings` |
| Content part | `BookingPart` |
| YesSql collection | `Bookings` (`BookingSlot`, `Booking`, `BookingBalance`) |
| Stores | `IBookingSlotStore`, `IBookingStore`, `IBookingBalanceStore` |
| Services | `BookingSchedule`, `BookingCapacityService`, `BookingService`, `BookingDepositService`, `BookingBalanceCharger` |
| Commerce seams filled | `ICartLineHandler`, `IOrderEventHandler`, `ICheckoutTenderProvider`, `ICartActionProvider`, two `CheckoutFlow` display drivers |
| Balance tender key | `booking-balance` |
| Traveler content type | `Traveler` |
| Permission | `ManageBookings` (Administrator, Store manager, Customer support) |
| Recipe step | `BookingDates` (needs `OrchardCore.Recipes.Core`) |
| Sample recipe | `BookingsSample` (a tour, an appointment and a room) |

### Enable the Feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Products",
        "CrestApps.OrchardCore.Carts",
        "CrestApps.OrchardCore.Orders",
        "CrestApps.OrchardCore.Customers",
        "CrestApps.OrchardCore.Checkout",
        "CrestApps.OrchardCore.Bookings"
      ],
      "disable": []
    }
  ]
}
```

## Booking Kinds and the Part

`BookingKind` is `Departure` (0), `Nightly` (1) or `TimeSlot` (2).

| Property | Meaning |
|---|---|
| `Kind` | Departure (fixed dates the store adds), Nightly (check-in to check-out), TimeSlot (times inside opening hours) |
| `Capacity` | Seats per departure, units per night, or bookings per time slot. A departure date can override it |
| `PricePerPerson` | Price is per person (a tour) rather than per unit (a room) |
| `Occupancies` | `BookingOccupancy` (`Id`, `Label`, `People`, `PriceAdjustment`), such as a double room or a single supplement |
| `MinNights`, `MaxNights`, `WeekendPrice` | Stay length, and the price of a Friday or Saturday night |
| `Hours`, `SlotMinutes` | `BookingOpeningHours` per day of week (`Day`, `Opens`, `Closes`) and the slot length |
| `DaysAhead` | How far ahead stays and slots can be booked |
| `DepositPercent`, `BalanceDueDaysBefore` | Share paid now, and when the rest is due |
| `CancellationTiers` | `BookingCancellationTier` (`DaysBefore`, `FeePercent`) |
| `RequestToBook` | The store confirms each booking before it is paid |
| `AskForTravelers` | Checkout asks each person's details from the `Traveler` content type |

Example content item part data, as in the sample recipe:

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "4bkngtour0alpinelakes0000a",
          "ContentType": "Tour",
          "DisplayText": "Alpine Lakes Tour",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "Alpine Lakes Tour" },
          "ProductPart": { "Price": 1000, "Currency": "USD", "Sku": "TOUR-ALPINE", "IsPurchasable": true },
          "BookingPart": {
            "Kind": 0,
            "Capacity": 12,
            "PricePerPerson": true,
            "Occupancies": [
              { "Id": "4bkngocc0double000000000000", "Label": "Double room", "People": 2, "PriceAdjustment": 0 },
              { "Id": "4bkngocc0single000000000000", "Label": "Single room", "People": 1, "PriceAdjustment": 300 }
            ],
            "DepositPercent": 15,
            "BalanceDueDaysBefore": 45,
            "CancellationTiers": [
              { "DaysBefore": 60, "FeePercent": 10 },
              { "DaysBefore": 30, "FeePercent": 50 }
            ],
            "RequestToBook": false,
            "AskForTravelers": true
          }
        }
      ]
    }
  ]
}
```

`BookingPart` is added to `CartOptions.ChooseOnProductPageParts`, so product cards link to the product page. The `BookingPicker` shape renders in the `ProductAction` display type and posts with the Add to cart form as `details[booking.date]`, `details[booking.end]` and `details[booking.occupancy]` (constants in `BookingsConstants.Details`). The query strings `?date=` and `?end=` preselect them.

## Dates and Capacity

`BookingSlot` is one date of a product: `SlotKey`, `StartsUtc`, `Capacity`, `Held`, `Sold`, an optional `Price` in the product's currency, and `Closed`.

- A departure is bookable only on dates the store added (admin or the `BookingDates` step).
- A night or time slot is created the first time it is held, at the part's capacity.
- `BookingSchedule` holds the rules as pure functions: `GetSlotKeys`, `Check` (past, beyond `DaysAhead`, stay length, opening hours), `GetTimes`, `GetUnitPrice`, `GetSeatsPerUnit`, `GetCancellationFeePercent` and `GetRefund`.
- `BookingCapacityService.HoldAsync` holds all dates or none, `CommitAsync` turns a hold into seats sold when the order is paid, `ReleaseAsync` gives them back, and `GetRemainingAsync` counts the cart's own unpaid holds as available.

### Add Dates with a Recipe

Each date names the product by `ContentItemId` and gives `Date` (`yyyy-MM-dd`) or `DaysFromToday`, plus optional `Time` (`HH:mm`), `Capacity` (default 10) and `Price`. A date the product already has is left unchanged, so the step is safe to rerun. Place it after the `Content` step that creates the products.

```json
{
  "steps": [
    {
      "name": "BookingDates",
      "Dates": [
        { "ContentItemId": "4bkngtour0alpinelakes0000a", "Date": "2027-06-14", "Capacity": 12 },
        { "ContentItemId": "4bkngtour0alpinelakes0000a", "DaysFromToday": 110, "Capacity": 12, "Price": 1150 }
      ]
    }
  ]
}
```

## Cart, Orders and Booking Status

- `BookingCartLineHandler` (order 10, before add-ons) validates dates in Prepare, prices one unit from each date's price, weekend price or the product price (converted with `CartLineEvaluateContext.ConvertAsync`), sets `PeoplePerUnit` so per-person add-ons charge each person, and caps `MaxQuantity` to what is left on the fullest date.
- `BookingOrderEventHandler` uses `BookingService`: Created makes a `Booking` per line and holds dates; Paid confirms, commits and emails the buyer; Canceled releases; LinesChanged rebooks.
- `BookingStatus`: `Held` (released after 60 minutes unpaid), `Requested`, `Confirmed`, `Completed`, `NoShow`, `Canceled`, `Expired`.
- `BookingsBackgroundTask` runs every five minutes, releasing expired holds and charging balances that are due.

## Deposits and the Balance

- `BookingDepositService.GetOfferAsync` computes the deposit. The balance is due at 09:00 local time, `BalanceDueDaysBefore` days before the earliest booking that takes a deposit. A booking already inside that window is paid in full.
- `BookingDepositCheckoutFlowDisplayDriver` shows **Pay a deposit** on the Payment step, chooses the `booking-balance` tender and sets `CheckoutSession.SavePaymentMethod`.
- `BookingBalanceTenderProvider.DebitAsync` records the balance as an outstanding Transactions ledger entry with reminders and a pay link, and a `BookingBalance` record.
- `BookingBalanceCharger.ChargeAsync` reads the saved card through `ICheckoutSavedPaymentMethodProvider` and charges it off-session (`CheckoutPaymentDataKeys.ForOffSessionCharge`). A decline is retried daily, three times in all.

## Cancellation and Requests to Book

- `BookingService.QuoteCancellationAsync` returns the fee (policy percent for the days left), what was paid (the unpaid balance tender is not counted) and the refund.
- `CancelAsync` refunds through `IOrderRefunder` (or a staff amount), waives the remaining balance, releases dates, notes the order and emails the buyer.
- With `RequestToBook`, `BookingCartActionProvider` adds **Request to book** to the cart. `bookings/request` (`BookingRequest` route) calls `IOrderRequestService` to make a quote, the booking is held as `Requested`, and the buyer pays from the quote link once staff send it.

## Travelers

Create a `Traveler` content type with the fields to collect (for example a required `FullName` text field). `BookingTravelersCheckoutFlowDisplayDriver` adds one editor per person to the Contact step for parts with `AskForTravelers`, and stores the content on `Booking.Travelers`.

## Storefront and Admin

| Surface | Route or menu |
|---|---|
| Booking search (`BookingSearch`) | `GET bookings`, finds availability for people between two dates |
| Request to book (`BookingRequest`) | `bookings/request` |
| Bookings admin | **Commerce > Bookings** (`bookings`), with a booking detail, dates per product, and a **Calendar** |

## Troubleshooting

| Symptom | Check |
|---|---|
| Product card has no Add to cart | Expected. Booking products are chosen on the product page |
| A departure date cannot be booked | Add the date with the admin Dates page or the `BookingDates` step |
| Order refused at checkout | The dates are gone, or a `RequestToBook` product was checked out without a quote |
| Balance never charged | The deposit must be paid with a provider that saves cards, and `BookingsBackgroundTask` must run (tenant has served a request) |
| `BookingDates` step not found | Enable `OrchardCore.Recipes.Core` and place the step inside `{ "steps": [...] }` |
| Traveler editors missing | The `Traveler` type needs fields, and the part needs `AskForTravelers` |
