# Learning Commerce: Selling Courses, Paths and Seats

Supporting detail for `orchardcore-learning`. Module `CrestApps.OrchardCore.Learning.Commerce` (contracts in `Learning.Commerce.Abstractions` and `Learning.Commerce.Core`). It builds on Products, Orders and Accounts and adds no store: a course is a product, a purchase is an order line, a business buyer is an account.

| Feature | ID | Depends on |
|---|---|---|
| Learning Commerce | `CrestApps.OrchardCore.Learning.Commerce` | Learning, Product Listing (`CrestApps.OrchardCore.Products.Listing`), Orders (`CrestApps.OrchardCore.Orders`), Account Members (`CrestApps.OrchardCore.Accounts.Members`) |

## Courses as Products

- The migration attaches `ProductPart` and `LearningProductPart` to `Course` and, when Learning Paths is on (also when it is turned on later, through `LearningPathProductMigrations`), to `LearningPath`, with `ProductPageSettings.ShowDetails` and an optional price (`ProductPartSettings.PriceOptional`). A course without a price is not for sale; `CourseProductPlacementProvider` hides `ProductSummary` and `ProductSpecifications` on its page.
- `LearningProductPart` can be attached to any product type for a bundle of courses or a pack of seats.

| `LearningProductPart` | Meaning |
|---|---|
| `Grants` | List of `LearningProductGrant` |
| `EnrollsWithoutKeyOrApproval` | Lets a `Key` or `Approval` course be sold (otherwise the cart refuses it) |

| `LearningProductGrant` | Meaning |
|---|---|
| `Kind` | `Course` (enroll the buyer), `LearningPath` (start the buyer, needs Learning Paths), `Seats` (a licence pool for the buyer's account) |
| `TargetId` | Course or path; empty means the item itself |
| `AccessDays` | Access length from purchase (per seat enrollment for seats); empty for no end |
| `Seats` | Seats per unit (quantity 3 x 10 seats = 30) |

With no grant, buying a course enrolls the buyer and buying a path starts it.

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "4safetycourse000000course1",
          "ContentType": "Course",
          "LearningProductPart": {
            "EnrollsWithoutKeyOrApproval": false,
            "Grants": [
              { "Kind": "Course", "TargetId": "", "AccessDays": 365, "Seats": 1 }
            ]
          }
        }
      ]
    }
  ]
}
```

Set the price itself on `ProductPart` (see `orchardcore-products`).

## Cart Rules

`LearningCartLineHandler` (`ICartLineHandler`) refuses a learning product to a guest, a seat grant to anyone who is not an admin of a business account (`AccountMemberRole.Admin`), an unpublished course, a course whose prerequisites the buyer has not met (`CoursePrerequisites.MeetsAsync`), and a `Key` or `Approval` course unless `EnrollsWithoutKeyOrApproval` is true. Course and path lines are capped at quantity one; seat products take any quantity.

## Grants and Refunds

`LearningOrderEventHandler` derives from `OrderLineGrantHandlerBase<LearningProductPart>`: it reads the product version bought, grants each line once, and takes back each line on a cancellation or a full refund (a partial refund keeps everything).

1. On payment, inside the checkout unit of work, each line is recorded as a `LearningPurchase` (`Pending`, collection `LearningCommerce`) and a `LearningFulfillmentWork` item is queued (leased work queue, collection `LearningFulfillmentWork`).
2. `LearningPurchaseFulfillment` makes the grants after the request, and the **Learning purchase fulfilment** background task (`*/2 * * * *`) retries what is still due with back-off.
3. A grant that can never be made moves the purchase to `Failed` with `LearningPurchase.Error` and an order note.

| Grant | On payment | On full refund or cancellation |
|---|---|---|
| Course | `IEnrollmentService.EnrollAsync` with source `Learning.Commerce`, source id `{orderId}:{lineId}`, `IgnorePrerequisites` | `RevokeGrantAsync("Learning.Commerce", "{orderId}:{lineId}")`; a completed enrollment keeps its grant |
| Learning path | `ILearningPathService.StartAsync`, same source | Not taken back yet |
| Seats | `ILicensePoolService.CreateAsync` for the order's account or the buyer's | `ILicensePoolService.RevokeAsync` ends every seat enrollment |

## Licence Pools and Seats

A `LicensePool` holds an account's seats for one course; free seats are a quantity ledger (`LearningSeatLedger`).

| `ILicensePoolService` | Does |
|---|---|
| `GiveAsync(poolId, userId, actor)` | Gives a seat to an account member and enrolls them with source `Learning.LicenseSeat` |
| `InviteAsync(poolId, email, actor)` | A member gets the seat at once; anyone else gets an account invitation and the seat is held (`LicenseSeatInvitationObserver` assigns it on acceptance) |
| `RemoveAsync(seatId, actor)` | Takes a seat back and ends its enrollment |
| `RevokeAsync(poolId, reason)` | Revokes the pool |
| `GetRemainingAsync(pool)` | Free seats |

`ILicensePoolObserver` (`SeatsPurchasedAsync`, `SeatGivenAsync`, `RunningLowAsync` at a tenth or fewer); register with `services.AddLicensePoolObserver<T>()`.

**Course seats** (`/account/learning-seats`) is in the storefront account menu for account admins only.

## Notifications, Workflows and Data

| Area | What it adds |
|---|---|
| Notifications | **Course seat given** (`LearningSeatGiven`) |
| Workflows | Events **Course Seats Purchased**, **Course Seat Given**, **Licence Pool Running Low**; task **Give Course Seat** |
| Data | `LearningCommerce` data source with **Licence pools** and **Seat holders**, needs **View learning reports** |
| Personal data | Exports seats and purchases; erasure takes seats back and clears the person while keeping the records for the ledger |
| Advanced SEO | A course is a schema.org `Course` that keeps the product provider's `offers` |

Not built: subscriptions that grant learning, taking back a purchased path, selling certification paths or extra quiz attempts, and a recipe step for licence pools.
