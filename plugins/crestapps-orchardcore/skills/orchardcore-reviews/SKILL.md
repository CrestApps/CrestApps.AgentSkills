---
name: orchardcore-reviews
description: Skill for the CrestApps Orchard Core Product Reviews commerce module. Covers verified-purchase ProductReview records with a 1 to 5 rating, moderation under Commerce Reviews, ReviewSettings RequireApproval, BuyersOnly and review requests, review photos in the media store, the ProductRating and ProductReviews shapes on product pages, average rating and review count written to the product listing search record, the Customer rating filter and sort, and the IProductListingEntryProvider and IProductSearchDocumentContributor seams. Use this skill when requests mention Orchard Core product reviews, star ratings, verified purchase, review moderation, review request emails, or filtering and sorting listings by rating. Strong matches include CrestApps.OrchardCore.Reviews, ProductReview, ReviewSettings, ReviewService, ProductRatingReader, ReviewPhotoStore, ReviewRequestService, ReviewSearchDocumentContributor, ReviewListingEntryProvider, ProductAttributeKeys.Rating, ManageProductReviews and SubmitProductReviews.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Product Reviews - Prompt Templates

## Let Buyers Review Products

You are an Orchard Core expert. Generate accurate setup and extension guidance for the CrestApps Product Reviews module, which lets buyers rate and review products they bought, moderated by the store, with stars on product pages and an average rating that listings filter and sort by.

### Guidelines

- Install `CrestApps.OrchardCore.Reviews` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle) in the web/startup project.
- Enable `CrestApps.OrchardCore.Reviews`. It depends on `CrestApps.OrchardCore.Commerce`, `CrestApps.OrchardCore.Products`, `CrestApps.OrchardCore.Orders` and `OrchardCore.Contents` (see `orchardcore-products`).
- Ratings in listings need the Products **Product Listing** feature `CrestApps.OrchardCore.Products.Listing` (`ListingStartup` runs only then).
- Review photos need Orchard Core's Media feature. Review requests need `OrchardCore.Email` and `CrestApps.OrchardCore.Customers` (see `orchardcore-customers`), plus the site **Base URL**.
- Do not add a fixed Rating field to products. The module writes customer ratings to the listing search record.
- Orders and carts are covered by `orchardcore-orders` and `orchardcore-carts`; the shared Commerce menu and storefront account by `orchardcore-commerce`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature and Services

| Item | Value |
|---|---|
| Feature ID | `CrestApps.OrchardCore.Reviews` |
| Records | `ProductReview` in collection `Reviews` (`ProductReviewIndex`) |
| Services | `ReviewService` (`GetEligibilityAsync`, `SubmitAsync`, `ModerateAsync`, `DeleteAsync`, `ListApprovedAsync`, `PageAsync`, `CountPendingAsync`), `ProductRatingReader`, `ReviewPhotoStore`, `ReviewRequestService` |
| Shapes | `ProductRating` (`Content:before:5`) and `ProductReviews` (`Content:60`) on a product's `Detail` display |
| Site settings | `ReviewSettings` (group `reviews`, **Settings > Commerce > Reviews**) |
| Listing seams filled | `IProductSearchDocumentContributor`, `IProductListingEntryProvider` |
| Background task | `ReviewRequestBackgroundTask` (hourly) |
| Permissions | `ManageProductReviews` ("Moderate product reviews"; Administrator, Store manager, Marketing), `SubmitProductReviews` ("Review products bought"; Authenticated) |
| Report | **Product reviews** under Reports > Commerce (with `CrestApps.OrchardCore.Reports`) |

## Model and Settings

`ProductReview` holds `ProductId`, `ProductTitle`, `UserId`, `AuthorName`, `Rating` (1 to 5), `Title` (120 chars), `Body` (4000 chars), `Status` (`Pending`, `Approved`, `Rejected`), `VerifiedPurchase`, `OrderId`, `Photos` (up to three media paths) and moderation fields. `ProductRating(Count, Average)` is the count and one-decimal average of approved reviews.

| `ReviewSettings` | Default | Meaning |
|---|---|---|
| `RequireApproval` | true | New and changed reviews wait for moderation |
| `BuyersOnly` | true | Only buyers of the product on a paid order may review |
| `SendReviewRequests` | false | Email buyers asking for reviews |
| `ReviewRequestDelayDays` | 14 | Days after the order before asking |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Media",
        "OrchardCore.Email",
        "CrestApps.OrchardCore.Customers",
        "CrestApps.OrchardCore.Reviews"
      ]
    },
    {
      "name": "Settings",
      "ReviewSettings": {
        "RequireApproval": true,
        "BuyersOnly": true,
        "SendReviewRequests": true,
        "ReviewRequestDelayDays": 10
      }
    }
  ]
}
```

## Submitting and Photos

- A signed-in user with `SubmitProductReviews` sees the form when `GetEligibilityAsync` allows it. The form posts to `POST /reviews/{productId}`.
- One review per user per product: reviewing again changes it and, with `RequireApproval`, sends it back for approval.
- A buyer's review is a verified purchase, recorded with its order.
- `ReviewPhotoStore` accepts up to three JPEG, PNG or WebP photos of at most 5 MB each (checked by their first bytes), saved under `reviews/{productId}/` in the media store. New photos replace and delete the old ones.

## Review Requests

`ReviewRequestService.SendDueAsync` reads paid orders from the last 60 days placed at least `ReviewRequestDelayDays` ago without `Order.ReviewRequestSentUtc`, up to 200 a run, and emails each buyer once with links to each product's reviews, only when the customer accepts marketing. Every due order is marked, so a buyer who does not accept marketing is not asked later.

## Moderation

**Commerce > Reviews** (`reviews`, `ManageProductReviews`) lists reviews on the `AdminList` standard, pending first, filtered by status, with **Approve**, **Reject** and **Delete**. Approving, rejecting, or deleting an approved review calls `IProductSearchIndexRefresher.RefreshAsync` so the listing rating updates at once.

## Ratings in Listings

- `ReviewSearchDocumentContributor` writes `ProductAttributeKeys.Rating` (average) and `ProductAttributeKeys.ReviewCount` to each product's search record. Products without approved reviews get neither.
- `ReviewListingEntryProvider` adds the **Customer rating** range facet (key `avg-rating`, ranges from 4, 3 and 2 stars) to listings with automatic filters, and **Customer rating: highest first** (key `avg-rating-desc`) to listings with automatic sorts. Unrated products sort last.
- A listing that picks its own filters and sorts adds them by attribute key `Product.Rating`.

The same two seams let any module add searchable attributes with automatic facets and sorts. The Reviews implementation is the pattern:

```csharp
using CrestApps.OrchardCore.Products.Core.Listing;
using Microsoft.Extensions.Localization;

namespace MyCompany.OrchardCore.Sustainability;

public sealed class EcoScoreListingEntryProvider : IProductListingEntryProvider
{
    internal readonly IStringLocalizer S;

    public EcoScoreListingEntryProvider(IStringLocalizer<EcoScoreListingEntryProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public IEnumerable<ProductListingFacet> GetFacets()
        =>
        [
            new ProductListingFacet
            {
                Key = "eco-score",
                AttributeKey = "Product.EcoScore",
                Label = S["Eco score"],
                Kind = ProductFacetKind.Range,
                ShowRanges = true,
                Ranges = [new ProductFacetRange { Min = 80 }, new ProductFacetRange { Min = 50 }],
            },
        ];

    public IEnumerable<ProductListingSort> GetSorts()
        =>
        [
            new ProductListingSort { Key = "eco-score-desc", Label = S["Eco score: highest first"], AttributeKey = "Product.EcoScore", Descending = true },
        ];
}
```

Pair it with an `IProductSearchDocumentContributor` that adds a `ProductAttributeValue` with that key, register both as scoped services in a startup with `[RequireFeatures("CrestApps.OrchardCore.Products.Listing")]`, and call `IProductSearchIndexRefresher.RefreshAsync` when the source data changes.

## Troubleshooting

| Symptom | Check |
|---|---|
| Review form missing | User must be signed in with `SubmitProductReviews`, and have bought the product on a paid order when `BuyersOnly` is on |
| Approved review not on the page | The product page must render the `Detail` display type |
| Rating filter missing in a listing | Enable Product Listing; the listing must use automatic filters, or add `Product.Rating` itself |
| Listing rating is stale | Moderate through **Commerce > Reviews**, which refreshes the product's search record |
| No review request emails | Enable `SendReviewRequests`, `OrchardCore.Email` and Customers, set the **Base URL**, and the buyer must accept marketing |
| Photo upload refused | Only JPEG, PNG or WebP up to 5 MB, three at most, with Media enabled |
