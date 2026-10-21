---
name: orchardcore-fundraising
description: Skill for the CrestApps Orchard Core Fundraising commerce module. Covers the FundraisingCampaignPart that turns a product into a donation campaign with a goal, an end date, suggested amounts and a minimum, the CampaignProgress and DonateForm shapes, donor-chosen amounts and tributes on the cart line excluded from discounts, Donation records, tax receipts emailed and kept in the donor account, refunds, and FundraisingSettings. Use this skill when requests mention Orchard Core fundraising, donations, charity campaigns, donate buttons, donation goals, or tax-deductible receipts. Strong matches include CrestApps.OrchardCore.Fundraising, FundraisingCampaignPart, DonationLineHandler, Donation, IDonationStore, DonationService, FundraisingSettings and ManageFundraising.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Fundraising - Prompt Templates

## Raise Money with Campaigns

You are an Orchard Core expert. Generate accurate setup and extension guidance for the CrestApps Fundraising module, which turns a product into a campaign that donors give their own amount to.

### Guidelines

- Install `CrestApps.OrchardCore.Fundraising` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle) in the web/startup project.
- Enable `CrestApps.OrchardCore.Fundraising`. It depends on `CrestApps.OrchardCore.Commerce` and `CrestApps.OrchardCore.Orders`. Taking donations also needs `CrestApps.OrchardCore.Products`, Carts and `CrestApps.OrchardCore.Checkout` with a payment provider (see `orchardcore-products`, `orchardcore-checkout` and `orchardcore-payments`).
- A campaign is a product (`ProductPart`, typically product type `Service` with the price hidden) plus `FundraisingCampaignPart`. The donor's amount replaces the product price on the cart line.
- Amounts are in the product's currency. Donors see suggested amounts converted to their shopper currency.
- Donations are never discounted. The line sets `ExcludeFromDiscounts`, so coupons, company prices and role discounts skip it.
- Orders and carts are covered by `orchardcore-orders` and `orchardcore-carts`; the shared Commerce menu and storefront account by `orchardcore-commerce`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature and Services

| Item | Value |
|---|---|
| Feature ID | `CrestApps.OrchardCore.Fundraising` |
| Content part | `FundraisingCampaignPart` |
| Shapes | `CampaignProgress`, `DonateForm` (in the `ProductAction` display type) |
| Cart line handler | `DonationLineHandler` (`ICartLineHandler`) |
| Order handler | `DonationOrderHandler` (`IOrderEventHandler`) |
| Records | `Donation` in collection `Fundraising` (`IDonationStore`, `DonationIndex`) |
| Service | `DonationService` (`RecordAsync`, `RefundAsync`, `GetProgressAsync`, `ListByCampaignAsync`, `ListByOrderAsync`, `ListByDonorAsync`) |
| Cart line details | `donation.amount`, `donation.tribute` |
| Site settings | `FundraisingSettings` (group `fundraising`, **Settings > Commerce > Fundraising**) |
| Permission | `ManageFundraising` (Administrator, Store manager), also guards the settings group |
| Sample recipe | Fundraising sample: a Fundraising Campaign type and a campaign raising $10,000 |

## The Campaign Part

| Property | Default | Meaning |
|---|---|---|
| `Goal` | 0 | Target in the product's currency; zero for none |
| `EndsUtc` | null | End of the campaign; null for no end |
| `SuggestedAmounts` | 25, 50, 100 | Up to six suggested amounts |
| `MinimumAmount` | 1 | Smallest accepted gift |
| `TaxDeductible` | true | Issue a tax receipt for each donation |

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "FundraisingCampaign",
          "DisplayName": "Fundraising Campaign",
          "Settings": { "ContentTypeSettings": { "Creatable": true, "Listable": true, "Draftable": true, "Versionable": true } },
          "ContentTypePartDefinitionRecords": [
            { "PartName": "TitlePart", "Name": "TitlePart", "Settings": { "ContentTypePartSettings": { "Position": "0" } } },
            { "PartName": "ProductPart", "Name": "ProductPart", "Settings": { "ContentTypePartSettings": { "Position": "1" }, "ProductPartSettings": { "Type": "Service", "DefaultCurrency": "USD", "SalesMode": "Purchasable", "PriceDisplay": "Hidden" } } },
            { "PartName": "FundraisingCampaignPart", "Name": "FundraisingCampaignPart", "Settings": { "ContentTypePartSettings": { "Position": "2" } } }
          ]
        }
      ]
    },
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "4fndrsng0library2026000a",
          "ContentType": "FundraisingCampaign",
          "DisplayText": "Books for the community library",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "Books for the community library" },
          "ProductPart": { "Price": 25, "Currency": "USD", "Sku": "GIVE-LIBRARY", "IsPurchasable": true },
          "FundraisingCampaignPart": {
            "Goal": 10000,
            "EndsUtc": "2026-12-31T23:59:59Z",
            "SuggestedAmounts": [ 25, 50, 100 ],
            "MinimumAmount": 5,
            "TaxDeductible": true
          }
        }
      ]
    }
  ]
}
```

The part is in `CartOptions.ChooseOnProductPageParts`, so product cards send shoppers to the campaign page. There, `CampaignProgress` shows the amount raised toward the goal, the donor count and the days left, and `DonateForm` posts `details[donation.amount]` and `details[donation.tribute]` with the Add to cart form.

## The Cart Line

`DonationLineHandler`:

- **Prepare** parses the amount in the invariant culture (`DonationAmounts.Parse`), so "25,50" is never read as 2550, refuses amounts below `MinimumAmount`, and keeps the tribute (up to 120 characters).
- **Evaluate** sets the unit price to the chosen amount in the cart's currency, limits the quantity to one, sets `ExcludeFromDiscounts` (copied to the order line), and reports a problem once the campaign has ended.

## Donations, Receipts and Refunds

- `RecordAsync` runs when an order is paid. It writes a `Donation` per campaign line, numbered `{order number}-{line}`, with the paid amount and `CampaignAmount` converted to the campaign currency, and emails a receipt for each tax-deductible gift when receipts are emailed. Recording twice adds nothing.
- `RefundAsync` takes a refund off the order's donations in proportion.
- `GetProgressAsync` sums non-refunded gifts in the campaign's currency and counts donors.

`FundraisingSettings` holds `OrganizationName` (the site name when empty), `TaxId`, `ReceiptStatement` and `EmailReceipts` (on).

Donors find their gifts on `account/donations` (`AccountDonations`, **Donations** in the storefront account menu). Each tax-deductible gift links to a printable receipt at `account/donations/{id}/receipt` (`AccountDonationReceipt`), which only its donor can open.

## Troubleshooting

| Symptom | Check |
|---|---|
| Donate form missing | The product page must render the `ProductAction` display type and the part must be attached |
| Amount refused | Below `MinimumAmount`, or the campaign has ended |
| A coupon did not reduce a donation | Expected. Donation lines are excluded from discounts |
| No receipt email | `TaxDeductible` off, `EmailReceipts` off, or email is not configured |
| Progress counts the wrong total | Progress uses `CampaignAmount` in the campaign currency and subtracts refunds |
