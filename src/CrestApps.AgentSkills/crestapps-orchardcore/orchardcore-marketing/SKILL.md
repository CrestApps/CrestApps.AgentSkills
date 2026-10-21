---
name: orchardcore-marketing
description: Skill for the CrestApps Orchard Core Marketing commerce module and how it works with checkout coupons and discounts. Covers CustomerPurchases kept per customer from paid and refunded orders, the purchases backfill, Segment rules by orders, spend, recency and country, Liquid email Campaigns sent in batches to customers who accept marketing, scheduled sending, test sends, the protected one-click unsubscribe link, and pairing campaigns with Checkout coupons limited per customer or generated codes. Use this skill when requests mention Orchard Core email marketing, customer segments, lapsed buyers, marketing campaigns, unsubscribe links, AcceptsMarketing, or promotion codes sent to customers. Strong matches include CrestApps.OrchardCore.Marketing, CustomerPurchases, CustomerPurchasesService, ICustomerPurchasesStore, Segment, SegmentRules, Campaign, CampaignService, CampaignCustomer, UnsubscribeController, MarketingBackgroundTask and ManageMarketing.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Marketing - Prompt Templates

## Email the Store's Customers

You are an Orchard Core expert. Generate accurate setup and extension guidance for the CrestApps Marketing module, which keeps what each customer has bought, groups customers into segments, and sends Liquid email campaigns to a segment's customers who accept marketing.

### Guidelines

- Install `CrestApps.OrchardCore.Marketing` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle) in the web/startup project.
- Enable `CrestApps.OrchardCore.Marketing`. It depends on `CrestApps.OrchardCore.Commerce`, `CrestApps.OrchardCore.Customers` and `CrestApps.OrchardCore.Orders` (see `orchardcore-customers` for `Customer.AcceptsMarketing`).
- Configure an email provider (`OrchardCore.Email` with SMTP or Azure) and set the site's **Base URL**. Sending needs both.
- Only customers who are `Active`, accept marketing and have an email are sent to. Do not bypass this in custom code.
- Coupons and discounts are not part of this module. They are the Checkout coupon catalog (**Commerce > Coupons**, feature `CrestApps.OrchardCore.Checkout`); see `orchardcore-checkout`. Campaigns carry their codes.
- Orders and carts are covered by `orchardcore-orders` and `orchardcore-carts`; the shared Commerce menu and storefront account by `orchardcore-commerce`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature and Services

| Item | Value |
|---|---|
| Feature ID | `CrestApps.OrchardCore.Marketing` |
| YesSql collection | `Marketing` |
| Records | `CustomerPurchases` (`CustomerPurchasesIndex`), `Segment` (`SegmentIndex`), `Campaign` (`CampaignIndex`), `PurchasesBackfill` |
| Stores | `ICustomerPurchasesStore`, `ISegmentStore`, `ICampaignStore` |
| Services | `CustomerPurchasesService` (`RecordAsync`, `BackfillAsync`), `CampaignService` (`RenderAsync`, `SendTestAsync`, `SendBatchAsync`, `CountAsync`) |
| Order seam filled | `IOrderEventHandler` (`CustomerPurchasesOrderHandler`) |
| Background task | `MarketingBackgroundTask` (every minute) |
| Unsubscribe | `GET` and `POST marketing/unsubscribe?token=` (`MarketingUnsubscribe` route and shape) |
| Permission | `ManageMarketing` (Administrator, Store manager, Marketing) |
| Admin | **Commerce > Segments** (`marketing/segments`), **Commerce > Campaigns** (`marketing/campaigns`) on the `AdminList` standard |

### Enable the Feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Email",
        "CrestApps.OrchardCore.Customers",
        "CrestApps.OrchardCore.Orders",
        "CrestApps.OrchardCore.Marketing"
      ],
      "disable": []
    }
  ]
}
```

## Customers' Purchases

`CustomerPurchases` (its id is the customer's) holds `OrderCount`, `TotalSpent` (paid orders less refunds, in the store currency, converted with `IShopperCurrency`), `FirstOrderUtc`, `LastOrderUtc`, `Country` (the last order's billing or shipping country), and `OrderIds` and `RefundIds` that make recording idempotent.

- `CustomerPurchasesOrderHandler` calls `CustomerPurchasesService.RecordAsync` when an order is paid or refunded.
- `MarketingBackgroundTask` backfills orders paid before the feature was enabled, 200 per run, newest first, tracking progress in `PurchasesBackfill`.

## Segments

`Segment` has a `Name`, `Description` and `SegmentRules`. Every rule given must hold, on customers with at least one order.

| Rule | Meaning |
|---|---|
| `MinOrders`, `MaxOrders` | Order count range |
| `MinSpent`, `MaxSpent` | Lifetime spend range in the store currency |
| `LastOrderAtLeastDaysAgo` | Lapsed buyers |
| `LastOrderWithinDays` | Recent buyers |
| `FirstOrderWithinDays` | New customers |
| `Country` | Last order's country |

`ICustomerPurchasesStore.ListAsync(rules, utcNow, afterId, take)` pages a segment by document id, so customers added while a campaign sends come after the cursor. `CountAsync` counts a segment; the segment editor shows its customers and how many accept marketing (up to 10,000).

## Campaigns

`Campaign` holds `Name`, `SegmentId`, `Subject` and `Body` (Liquid), `Status` (`Draft`, `Scheduled`, `Sending`, `Sent`, `Canceled`), `ScheduledUtc`, the `Cursor`, and `Sent`, `Skipped` and `Failed` counts.

Liquid values:

| Value | Content |
|---|---|
| `Customer` | `CampaignCustomer`: `FirstName`, `LastName`, `Name`, `Email`, `OrderCount`, `TotalSpent`, `LastOrderUtc` |
| `StoreName` | The site's name |
| `UnsubscribeUrl` | The customer's own unsubscribe link. One is appended when the body has none |

```liquid
<p>Hi {{ Customer.FirstName }},</p>
<p>It has been a while since your last order at {{ StoreName }}.
Use <strong>COMEBACK15</strong> for 15% off your next order.</p>
<p><a href="{{ UnsubscribeUrl }}">Unsubscribe</a></p>
```

`CampaignService.SendBatchAsync` sends 50 customers at a time from the cursor, at most ten batches per background run. A scheduled campaign starts when `ScheduledUtc` passes. The text part is the HTML with its tags removed. The campaign editor's test send renders the message as the segment's first customer would get it.

## Unsubscribing

`UnsubscribeController` serves `marketing/unsubscribe?token=`. The token is the customer id protected by a time-limited data protector (400 days). A `POST` turns `AcceptsMarketing` off and needs no antiforgery token, so mail apps' one-click unsubscribe works.

## Coupons and Discounts for Campaigns

Discount codes come from Checkout's coupon catalog (`orchardcore-checkout`), which applies discounts before tax and spreads them across item lines so each line is taxed on what the buyer pays. Useful pairings with a campaign:

- A shared code with `MaxRedemptionsPerCustomer` 1 and an end date, typed into the campaign body.
- Generated single-use codes (`ICouponStore.GenerateCodesAsync`), exported as CSV from the coupon admin, for one code per recipient.
- A discount link `GET discount/{code}?redirect=/shop` in the body, which stores the code in a cookie and applies it at checkout.
- A coupon limited to one customer with `CustomerEmail` or `CustomerId`.

Ship coupons between sites with Checkout's `Coupons` recipe step:

```json
{
  "steps": [
    {
      "name": "Coupons",
      "Coupons": [
        { "Code": "COMEBACK15", "Description": "Lapsed buyer campaign", "Kind": "Percentage", "Percentage": 15, "MaxRedemptionsPerCustomer": 1, "EndsUtc": "2027-03-01T00:00:00Z", "IsEnabled": true }
      ]
    }
  ]
}
```

## Related Modules

- Reports: scheduled report emails (`orchardcore-reports`).
- Product Reviews: review requests to buyers who accept marketing (`orchardcore-reviews`).

## Troubleshooting

| Symptom | Check |
|---|---|
| Campaign stays in Sending with nothing sent | Configure `IEmailService` and the site **Base URL** |
| Segment counts zero | Purchases are still backfilling (200 orders a minute), or no customer matches every rule |
| Many customers skipped | They are not `Active`, do not accept marketing, or have no email |
| Unsubscribe link fails | The token is older than 400 days, or the link was altered |
| A code in the campaign is refused | Check the coupon's dates, limits, scope and currency in **Commerce > Coupons** |
