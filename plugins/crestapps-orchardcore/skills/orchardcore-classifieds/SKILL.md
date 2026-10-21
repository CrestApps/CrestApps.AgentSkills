---
name: orchardcore-classifieds
description: Skill for the CrestApps Orchard Core Classifieds commerce module. Covers the ClassifiedPart that makes a content type visitors post from the site, ClassifiedPartSettings TermDays and RequireApproval, moderation, terms that expire and renew, the ClassifiedUpgradePart for paid Feature and Bump upgrades bought through the cart, the owner listings page, and search by words and distance on a Leaflet map. Use this skill when requests mention Orchard Core classifieds, user-posted listings, ads, directory entries, listing moderation, featured or bumped listings, or listing search by distance. Strong matches include CrestApps.OrchardCore.Classifieds, ClassifiedPart, ClassifiedUpgradePart, ClassifiedStatus, ClassifiedService, ClassifiedIndex, ClassifiedUpgradeLineHandler, PostClassifieds and ModerateClassifieds.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Classifieds - Prompt Templates

## Let Visitors Post Listings

You are an Orchard Core expert. Generate accurate setup and extension guidance for the CrestApps Classifieds module, which lets visitors post content (ads, directory entries, properties) that is moderated, runs for a term, and can be featured or bumped by a paid upgrade.

### Guidelines

- Install `CrestApps.OrchardCore.Classifieds` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle) in the web/startup project.
- Enable `CrestApps.OrchardCore.Classifieds`. It depends on `CrestApps.OrchardCore.Commerce` and `CrestApps.OrchardCore.Orders`.
- Paid upgrades are products. Enable `CrestApps.OrchardCore.Products`, `CrestApps.OrchardCore.Carts` and `CrestApps.OrchardCore.Checkout` to sell them (see `orchardcore-products` and `orchardcore-checkout`).
- Attach `ClassifiedPart` to the content type visitors post. Attach `ClassifiedUpgradePart` to a product type.
- Owners edit their listing with the type's storefront editor, including the part's place. Only staff see the status and dates, in the admin editor (`AdminAttribute.IsApplied`).
- Orders and carts are covered by `orchardcore-orders` and `orchardcore-carts`; the shared Commerce menu and storefront account by `orchardcore-commerce`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature and Services

| Item | Value |
|---|---|
| Feature ID | `CrestApps.OrchardCore.Classifieds` |
| Parts | `ClassifiedPart`, `ClassifiedUpgradePart` |
| Part settings | `ClassifiedPartSettings` (`TermDays` 30, `RequireApproval` true) |
| Index | `ClassifiedIndex` (type, owner, status, dates, coordinates; latest and published versions) |
| Service | `ClassifiedService` |
| Commerce seams filled | `ICartLineHandler` (`ClassifiedUpgradeLineHandler`), `IOrderEventHandler` (`ClassifiedUpgradeOrderHandler`) |
| Cart line detail | `classified.listing` |
| Permissions | `PostClassifieds` (Authenticated), `ModerateClassifieds` (Administrator, Store manager) |
| Background task | `ClassifiedsBackgroundTask` (every five minutes, expires listings) |
| Sample recipe | Classifieds sample: a Classified Ad type and a $5 upgrade that features an ad for 7 days |

## The Parts

`ClassifiedPart` holds `Status` (`ClassifiedStatus`: Pending, Approved, Rejected, Expired), `RejectReason`, `PostedUtc`, `ExpiresUtc`, `FeaturedUntilUtc`, `BumpedUtc`, `Place`, `Latitude` and `Longitude`. **Use where I am** on the storefront form fills the coordinates from the browser.

`ClassifiedUpgradePart` holds `Kind` (`ClassifiedUpgradeKind`: Feature = 0, Bump = 1) and `Days` (default 7).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Products",
        "CrestApps.OrchardCore.Carts",
        "CrestApps.OrchardCore.Orders",
        "CrestApps.OrchardCore.Checkout",
        "CrestApps.OrchardCore.Classifieds"
      ]
    },
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "ClassifiedAd",
          "DisplayName": "Classified Ad",
          "Settings": { "ContentTypeSettings": { "Creatable": true, "Listable": true, "Draftable": true } },
          "ContentTypePartDefinitionRecords": [
            { "PartName": "TitlePart", "Name": "TitlePart", "Settings": { "ContentTypePartSettings": { "Position": "0" } } },
            { "PartName": "HtmlBodyPart", "Name": "HtmlBodyPart", "Settings": { "ContentTypePartSettings": { "Position": "1" } } },
            {
              "PartName": "ClassifiedPart",
              "Name": "ClassifiedPart",
              "Settings": {
                "ContentTypePartSettings": { "Position": "2" },
                "ClassifiedPartSettings": { "TermDays": 30, "RequireApproval": true }
              }
            }
          ]
        },
        {
          "Name": "ListingUpgrade",
          "DisplayName": "Listing Upgrade",
          "Settings": { "ContentTypeSettings": { "Creatable": true, "Listable": true } },
          "ContentTypePartDefinitionRecords": [
            { "PartName": "TitlePart", "Name": "TitlePart", "Settings": { "ContentTypePartSettings": { "Position": "0" } } },
            { "PartName": "ProductPart", "Name": "ProductPart", "Settings": { "ContentTypePartSettings": { "Position": "1" }, "ProductPartSettings": { "Type": "Service", "DefaultCurrency": "USD", "SalesMode": "Purchasable", "PriceDisplay": "Show" } } },
            { "PartName": "ClassifiedUpgradePart", "Name": "ClassifiedUpgradePart", "Settings": { "ContentTypePartSettings": { "Position": "2" } } }
          ]
        }
      ]
    }
  ]
}
```

An upgrade product item then carries `"ClassifiedUpgradePart": { "Kind": 0, "Days": 7 }` next to its `ProductPart` price.

## ClassifiedService Lifecycle

| Method | Behavior |
|---|---|
| `PostAsync` | New listing waits for approval, or is approved and published when the type does not require approval |
| `ChangedAsync` | An owner's edit. With approval required, the listing is taken down until approved again |
| `ApproveAsync` / `RejectAsync` | Publishes for its term, or unpublishes with a reason. Both email the owner |
| `RenewAsync` | Adds a term from the current end, or starts a new term for an expired listing, and publishes |
| `UpgradeAsync` | Features for the upgrade's days (extending an existing feature) or bumps to the top |
| `ExpireDueAsync` | Unpublishes listings past their end and emails owners |
| `SearchAsync` | Published listings by type, words and distance. Bounding box in the index, then Haversine (`GeoDistance`). Featured first, then most recently bumped or posted |

`ClassifiedUpgradeLineHandler` stores the listing on the upgrade cart line as `classified.listing` and refuses a listing that is not the buyer's own. `ClassifiedUpgradeOrderHandler` applies the upgrade when the order is paid.

## Pages

| Route | Name | Purpose |
|---|---|---|
| `listings/post/{contentType}` | `ClassifiedPost` | Storefront editor for a new listing (`PostClassifieds`) |
| `listings/edit/{contentItemId}` | `ClassifiedEdit` | Owner edits a listing |
| `account/listings` | `ClassifiedsOwn` | Owner's listings with status, **Renew** and one button per upgrade (`ClassifiedsOwn` shape) |
| `account/listings/{id}/renew`, `account/listings/{id}/upgrade/{productId}` | `ClassifiedRenew`, `ClassifiedUpgrade` | Renew, or add an upgrade to the cart |
| `listings` | `ClassifiedsSearch` | Search by type, words and distance with a Leaflet and OpenStreetMap map loaded from cdnjs (`ClassifiedsSearch` shape) |
| Admin **Content > Posted listings** (`classifieds`) | | Moderation list by status with **Approve** and **Reject** (`ModerateClassifieds`) |

The owner's page is added to the storefront account menu as **My listings** (see `orchardcore-commerce` for the `commerce-account` menu).

## Troubleshooting

| Symptom | Check |
|---|---|
| Visitors cannot post | They must be signed in with `PostClassifieds` (granted to Authenticated by default) |
| Listing never appears | `RequireApproval` is on. Approve it under **Content > Posted listings** |
| Listing disappears after an owner edit | Edits to a type that needs approval send it back for approval |
| Upgrade refused in the cart | The listing on the line must belong to the buyer |
| Listings never expire | `ClassifiedsBackgroundTask` runs only for tenants that have served a request |
| Distance search returns nothing | The listing needs `Latitude` and `Longitude`, filled by **Use where I am** or the editor |
