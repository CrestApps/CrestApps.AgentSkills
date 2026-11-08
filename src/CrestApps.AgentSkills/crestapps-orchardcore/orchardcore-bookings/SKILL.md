---
name: orchardcore-bookings
description: Skill for the CrestApps Orchard Core Bookings commerce module. Covers the BookingPart for Departure, Nightly and TimeSlot products, capacity per date in BookingSlot records, occupancy and weekend pricing, deposits with the balance charged to the saved card, cancellation tiers and refunds, request-to-book quotes, Traveler details, the booking search, bookings on the shared calendar with Block with calendar, the IBookingHandler, IBookingSlotFilter and IBookingPaymentHandler hooks, booking and payment workflow events, the Report Builder data source and the BookingDates recipe and deployment step. Use this skill when requests mention Orchard Core bookings, tours, room or rental stays, time slots, booking deposits or cancellation policies. Strong matches include CrestApps.OrchardCore.Bookings, BookingPart, BookingKind, BookingSlot, BookingCapacityService, BookingService, BookingDepositService, BookingCalendarHandler, ManageBookings and BookingDates.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.1"
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
- With `CrestApps.OrchardCore.Calendars` also on, bookings go on a **Bookings** calendar and a product can be blocked by another calendar. There is no extra feature: `CalendarStartup` carries `[RequireFeatures("CrestApps.OrchardCore.Calendars")]`. See `orchardcore-calendars`.
- Bookings sells dated products through the cart. For staff appointments booked without a cart (resources, working hours, public booking pages), use `orchardcore-scheduling`.
- Follow booking changes with `IBookingHandler` (inside the save) and money with `IBookingPaymentHandler`; never poll the stores.
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
| Commerce seams filled | `ICartLineHandler`, `IOrderEventHandler`, `ICheckoutTenderProvider`, `ICartActionProvider`, two `CheckoutFlow` display drivers; `ITransactionPaymentHandler` (balance payments) |
| Seams offered | `IBookingHandler`, `IBookingSlotFilter`, `IBookingPaymentHandler` |
| Balance tender key | `booking-balance` |
| Traveler content type | `Traveler` |
| Permission | `ManageBookings` (Administrator, Store manager, Customer support) |
| Recipe step | `BookingDates` (needs `OrchardCore.Recipes.Core`) |
| Deployment step | **Booking dates** (`BookingDatesDeploymentStep`, category Commerce) |
| Workflow events | Ten booking and payment events in the **Commerce** category (with `OrchardCore.Workflows`) |
| Report Builder | **Bookings** data source (with `OrchardCore.DataPipelines.Core`) |
| Calendar type | `Bookings` (event type `Booking`), with Calendars on |
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

`BookingKind` is `Departure` (0), `Nightly` (1) or `TimeSlot` (2). Defaults: `Capacity` 10, `MinNights` 1, `MaxNights` 28, `SlotMinutes` 60, `DaysAhead` 180, `BalanceDueDaysBefore` 30.

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
| `BlockingCalendarId` | With Calendars on, **Block with calendar**: dates or times busy on that calendar can't be booked |

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

Each date names the product by `ContentItemId` and gives `Date` (`yyyy-MM-dd`) or `DaysFromToday`, plus optional `Time` (`HH:mm`), `Capacity` (default 10) and `Price`. A date the product already has is left unchanged, so the step is safe to rerun. Place it after the `Content` step that creates the products. The **Booking dates** deployment step (`BookingDatesDeploymentStep`, category Commerce) exports upcoming open dates in this shape; past and closed dates and what is held or sold stay on the site.

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
- `BookingStatus`: `Held` (released after `BookingService.HoldTime`, 60 minutes, unpaid), `Requested`, `Confirmed`, `Completed`, `NoShow`, `Canceled`, `Expired`.
- `BookingCapacityService` also asks every `IBookingSlotFilter` in `GetRemainingAsync` (a closed date shows as full) and in `HoldAsync` (it can't be held).
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

## Booking Hooks

| Seam | Called | Use it to |
|---|---|---|
| `IBookingHandler` | By `BookingStore` inside the save: `SavingAsync(BookingSavingContext)` and `DeletingAsync(Booking)` | Follow status changes and moves; what it writes commits with the booking. Use a `ShellScope` deferred task for after-commit work. |
| `IBookingSlotFilter` | By `BookingCapacityService` (remaining and hold) | Close dates or times for reasons Bookings doesn't track (`GetClosedSlotKeysAsync(contentItemId, part, slotKeys, timeZone)` returns the closed keys) |
| `IBookingPaymentHandler` | When money moves: `PaymentReceivedAsync`, `PaymentFailedAsync`, `RefundedAsync` | Follow full payments, deposits, balance payments, failed balance charges and refunds |

`BookingSavingContext` has `Booking`, `IsNew`, `PreviousStatus`, `PreviousStartsUtc`, `PreviousEndsUtc`, and the computed `StatusChanged` and `Rescheduled` (the snapshot is a `BookingChangeMarker` kept in the booking's properties; bookings saved before the hooks existed have `null` previous values on their first save). `BookingPaymentContext` has `Bookings` (only confirmed ones for a balance payment, a failure or a refund), `OrderId`, `OrderNumber`, `PaymentKind` (`FullPayment`, `Deposit`, `Balance`; `null` for a refund), `Amount` and `Currency` (for the whole order), `Reason`, `IsFinal` and `Reference`. Each payment, declined try and refund is reported once.

```csharp
public sealed class CanceledBookingHandler : IBookingHandler
{
    private readonly ILogger _logger;

    public CanceledBookingHandler(ILogger<CanceledBookingHandler> logger)
    {
        _logger = logger;
    }

    public Task SavingAsync(BookingSavingContext context, CancellationToken cancellationToken = default)
    {
        if (context.StatusChanged && context.Booking.Status == BookingStatus.Canceled)
        {
            _logger.LogInformation("Booking {BookingId} was canceled.", context.Booking.ItemId);
        }

        return Task.CompletedTask;
    }

    public Task DeletingAsync(Booking booking, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

// Startup: services.AddScoped<IBookingHandler, CanceledBookingHandler>();
```

```csharp
public sealed class ClosedOnTuesdaysSlotFilter : IBookingSlotFilter
{
    public Task<IReadOnlyCollection<string>> GetClosedSlotKeysAsync(string contentItemId, BookingPart part, IReadOnlyList<string> slotKeys, TimeZoneInfo timeZone, CancellationToken cancellationToken = default)
    {
        // Slot keys are yyyy-MM-dd or yyyy-MM-ddTHH:mm in the given time zone.
        IReadOnlyCollection<string> closed = slotKeys
            .Where(key => DateTime.ParseExact(key[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture).DayOfWeek == DayOfWeek.Tuesday)
            .ToList();

        return Task.FromResult(closed);
    }
}

// Startup: services.AddScoped<IBookingSlotFilter, ClosedOnTuesdaysSlotFilter>();
```

The store resolves handlers on first use, so a handler may depend on services that depend on the store. A payment handler that throws is logged and never undoes the payment.

## Workflow Events

With `OrchardCore.Workflows` on, these events are in the **Commerce** category, correlated by booking id, and triggered in a deferred task after the session commits. Booking events come from `BookingWorkflowHandler` (an `IBookingHandler`; one save can raise several, such as Created and Confirmed); payment events come from `BookingPaymentWorkflowHandler`, once for each booking the money is for.

| Activity | Display name | Raised when |
|---|---|---|
| `BookingCreatedEvent` | Booking Created | A booking is made (held, requested or confirmed) |
| `BookingConfirmedEvent` | Booking Confirmed | It becomes `Confirmed`, such as when the order is paid |
| `BookingRescheduledEvent` | Booking Rescheduled | It moves to other dates |
| `BookingCanceledEvent` | Booking Canceled | It becomes `Canceled` |
| `BookingCompletedEvent` | Booking Completed | It is marked complete |
| `BookingNoShowEvent` | Booking No-Show | It is marked a no-show |
| `BookingExpiredEvent` | Booking Expired | A hold or request runs out |
| `BookingPaymentReceivedEvent` | Booking Payment Received | Paid in full or by deposit, or a payment is applied to a balance |
| `BookingPaymentFailedEvent` | Booking Payment Failed | Charging the balance to the kept card fails |
| `BookingRefundedEvent` | Booking Refunded | A refund is recorded on an order with confirmed bookings |

Input: `Booking`, `BookingId`, `OrderId`, `OrderNumber`, `ContentItemId`, `Title`, `StartsUtc`, `EndsUtc`, `Status`, `PreviousStatus`, `CustomerName`, `CustomerEmail`. Payment events add `Amount` and `Currency` (for the whole order, so an order with two bookings raises the event twice with the same amount), `PaymentKind`, `Reason`, `IsFinal` and `PaymentReference`.

## Bookings on the Calendar

Whenever Bookings and `CrestApps.OrchardCore.Calendars` are both on:

- Bookings registers the `Bookings` calendar type (`ManagedByCode()`, event type `Booking`). `BookingCalendarHandler` (an `IBookingHandler`) creates the **Bookings** calendar the first time (`Source` and `SourceId` both `Bookings`, private by default) and on every booking save creates, moves or cancels its event: `Source = Bookings`, `SourceId` = booking id, `Editable = false`, titled with the product and buyer, busy, buyer as attendee; `Tentative` while held or requested, `Confirmed` once booked, `Cancelled` when canceled or expired, and deleted with the booking. It writes with `SkipConflictCheck` and `SendInvitations = false` inside the booking's session.
- **Sync to calendar** (`bookings-calendar/sync`, `ManageBookings`) adds or updates the event of every booking from today on, for bookings made before Calendars was on. It changes only what differs, so rerunning is harmless, and answers `404` while Calendars is off. **Commerce > Bookings > Calendar** shows **Sync to calendar**, and **Open in calendar** once the calendar exists.
- **Block with calendar** on the Booking part (`BlockingCalendarId`, any calendar except the Bookings one) is enforced by `BookingCalendarSlotFilter`, which reads `ICalendarAvailabilityService.GetBusyAsync` with tentative events included and busy-time sources left out. A time slot closes when anything busy overlaps it; a departure date or a night closes when anything busy falls on that day. Use it to keep a room, a guide or a vehicle from being sold twice across products.

## Report Builder Data Source

With `OrchardCore.DataPipelines.Core` on, the **Bookings** data source (`Bookings`) has the data sets `Bookings` (one row per booking, default date when booked), `BookingSlots` (one row per product date) and `BookingBalances` (one row per balance owed after a deposit). They need `ManageBookings`. Travelers' details, cancel reasons, cancellation policies, cards and payment provider errors are never exposed. See `orchardcore-report-builder`.

## Storefront and Admin

| Surface | Route or menu |
|---|---|
| Booking search (`BookingSearch`) | `GET bookings`, finds availability for people between two dates |
| Request to book (`BookingRequest`) | `bookings/request` |
| Bookings admin | **Commerce > Bookings** (`bookings`, `bookings/{action}/{itemId?}`), with a booking detail, dates per product, and a **Calendar** (`ManageBookings`) |
| Sync to calendar | `bookings-calendar/sync` (admin, with Calendars on) |

## Troubleshooting

| Symptom | Check |
|---|---|
| Product card has no Add to cart | Expected. Booking products are chosen on the product page |
| A departure date cannot be booked | Add the date with the admin Dates page or the `BookingDates` step |
| Order refused at checkout | The dates are gone, or a `RequestToBook` product was checked out without a quote |
| Balance never charged | The deposit must be paid with a provider that saves cards, and `BookingsBackgroundTask` must run (tenant has served a request) |
| `BookingDates` step not found | Enable `OrchardCore.Recipes.Core` and place the step inside `{ "steps": [...] }` |
| Traveler editors missing | The `Traveler` type needs fields, and the part needs `AskForTravelers` |
| Bookings missing from the calendar | Calendars must be on; run **Sync to calendar** for bookings made before it was enabled |
| Bookings calendar not visible | It is private; share it, or use `ViewAllCalendars` |
| Dates show as full with places left | An `IBookingSlotFilter` closed them, such as **Block with calendar** finding busy time that day |
| No booking workflow event | `OrchardCore.Workflows` must be on; events fire after the session commits |

Related skills: `orchardcore-products`, `orchardcore-checkout`, `orchardcore-payments`, `orchardcore-orders`, `orchardcore-calendars`, `orchardcore-scheduling`, `orchardcore-report-builder`.
