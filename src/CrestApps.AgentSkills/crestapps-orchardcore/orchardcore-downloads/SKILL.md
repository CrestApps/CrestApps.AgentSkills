---
name: orchardcore-downloads
description: Skill for the CrestApps Orchard Core Digital Downloads commerce module. Covers the DigitalFilesPart, the private IDigitalFileStore under the tenant App_Data scanned through the Orchard Core file creation pipeline, DownloadEntitlement records granted when an order is paid and revoked on cancel or full refund, signed expiring download links with a download limit, DownloadSettings, order page and confirmation email sections, and the DigitalFiles recipe step. Use this skill when requests mention Orchard Core digital downloads, selling ebooks or files, download links, download limits, or digital product delivery. Strong matches include CrestApps.OrchardCore.Downloads, DigitalFilesPart, IDigitalFileStore, DownloadEntitlement, DownloadService, DownloadSettings, DownloadOrderEventHandler, DownloadEmailSectionProvider, ManageDigitalDownloads and DigitalFiles.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Digital Downloads - Prompt Templates

## Deliver Digital Products

You are an Orchard Core expert. Generate accurate setup, recipes and extension guidance for the CrestApps Digital Downloads module, which keeps a product's files privately, grants them to buyers when an order is paid, and serves them through signed, expiring links.

### Guidelines

- Install `CrestApps.OrchardCore.Downloads` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle) in the web/startup project.
- Enable `CrestApps.OrchardCore.Downloads`. It depends on `CrestApps.OrchardCore.Commerce`, `CrestApps.OrchardCore.Products`, `CrestApps.OrchardCore.Orders` and `OrchardCore.Contents`. Selling also needs Carts and `CrestApps.OrchardCore.Checkout` with a payment provider (see `orchardcore-products`, `orchardcore-checkout` and `orchardcore-payments`).
- Enable `CrestApps.OrchardCore.Customers` so a guest's Contact step asks for the email the links go to (see `orchardcore-customers`).
- Never put sellable files in the media library. Files go to `IDigitalFileStore`, which no URL serves.
- Enable an antivirus feature such as `OrchardCore.Antivirus.ClamAV` to scan uploads. Uploads run through Orchard Core's `FileCreationService` and every `IFileEventHandler` (see `orchardcore-file-upload-security` and `orchardcore-antivirus`).
- Set the site's **Base URL** so links in emails work outside a request.
- Orders and carts are covered by `orchardcore-orders` and `orchardcore-carts`; the shared Commerce menu and storefront account by `orchardcore-commerce`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature and Services

| Item | Value |
|---|---|
| Feature ID | `CrestApps.OrchardCore.Downloads` |
| Content part | `DigitalFilesPart` (`Files`, `DownloadLimit`, `LinkLifetimeDays`) |
| File store | `IDigitalFileStore` (`SaveAsync`, `FindAsync`, `OpenAsync`), folder `App_Data/.../DigitalFiles/{fileId}/{fileName}` |
| Entitlements | `DownloadEntitlement` in collection `Downloads`, `DownloadEntitlementIndex` |
| Service | `DownloadService` (`GrantAsync`, `ListAsync`, `CreateUrlAsync`, `ResolveAsync`, `RecordDownloadAsync`, `RevokeOrderAsync`, `RevokeAsync`, `ResetAsync`) |
| Site settings | `DownloadSettings` (group `downloads`, **Settings > Commerce > Downloads**): `DownloadLimit` 5, `LinkLifetimeDays` 30 |
| Order seams | `DownloadOrderEventHandler` (`OrderGrantHandlerBase`), `IOrderEmailSectionProvider`, `OrderDownloadsDisplayDriver` |
| Permission | `ManageDigitalDownloads` ("Manage digital files and downloads"; Administrator, Store manager), also guards the settings group |
| Recipe step | `DigitalFiles` (needs `OrchardCore.Recipes.Core`) |
| Report | **Downloads** under Reports > Commerce (with `CrestApps.OrchardCore.Reports`) |
| Sample recipe | Digital downloads sample: a Digital Product type, two ebooks with PDFs, an Ebooks listing, coupons WELCOME10 and EBOOKS5 |

## The Part and Settings

`DigitalFilesPart.Files` holds `DigitalFile` items (`FileId`, `FileName`, `Size`). `DownloadLimit` and `LinkLifetimeDays` override the site settings when set; `0` means no limit or links that never expire.

The part editor uploads each file straight to the store through `POST /Admin/downloads/files/upload` (up to 1 GB, `ManageDigitalDownloads`), then saving the product keeps the ids the store knows. `GET /Admin/downloads/files/{fileId}` opens a stored file for staff. Removing a file from a product does not delete it, so past orders keep their downloads.

When an `IFileEventHandler` refuses a file, or the scanner cannot be reached, nothing is kept and the upload answers `400` with the reason.

## Granting and Revoking

`DownloadOrderEventHandler` derives from `OrderGrantHandlerBase` (Orders.Core):

- **Paid**: `DownloadService.GrantAsync(order)` creates one entitlement per file of each line's product, from the product version that was bought, inside the checkout's unit of work so the confirmation email can carry the links. Granting again adds only what is missing.
- **Canceled**: revokes the order's entitlements.
- **Refunded**: revokes only once the order is refunded in full.

From the order's admin page, staff can **Take back** one file (`DownloadsRevoke`) or **Give downloads back** (`DownloadsReset`), which resets the count, extends the link by the site lifetime and restores a revoked right.

## Links

- A link is `/downloads/{token}` (route `DigitalDownload`). The token is the entitlement id protected with data protection purpose `CrestApps.OrchardCore.Downloads.Link`, so links can be shown again; validity is decided by the entitlement.
- Opening a link shows a **Download** button. The download is a `POST`, so mail link scanners never use up a download.
- `RecordDownloadAsync` counts under a lock per entitlement.
- A dead link answers `410` (expired, used up, canceled or refunded), or `404` when it names nothing.

Buyers see their links on their order page, in the confirmation email (**Your downloads**, through `DownloadEmailSectionProvider`) and on `account/downloads` (`AccountDownloads`).

## Ship Files with a Recipe

Each file names its product and gives its bytes as `Base64`, or `Text` for a small text file, up to 10 MB. A product that already has a file of that name keeps it, so rerunning adds nothing. Place the step after the `Content` step that creates the products.

```json
{
  "steps": [
    {
      "name": "DigitalFiles",
      "Files": [
        { "ProductId": "4dgtlprod0trailguide00000a", "FileName": "trail-field-guide.pdf", "Base64": "JVBERi0xLjQK..." },
        { "ProductId": "4dgtlprod0notes0000000000a", "FileName": "read-me.txt", "Text": "Thanks for buying." }
      ]
    }
  ]
}
```

A product type for downloads uses `ProductPart` with product type `Digital` plus `DigitalFilesPart`. An order of digital products only has no shipping step.

## Extending

- Add a section to the order confirmation email from another module by implementing the Orders `IOrderEmailSectionProvider` seam, as `DownloadEmailSectionProvider` does.
- Grant other entitlements on payment by deriving from `OrderGrantHandlerBase`, which already handles paid, canceled and full-refund events.
- Resolve `DownloadService` to list or revoke an order's entitlements from custom code instead of writing to the `Downloads` collection.

## Troubleshooting

| Symptom | Check |
|---|---|
| Upload answers `400` | An `IFileEventHandler` (antivirus) refused the file or the scanner is unreachable |
| No links in the confirmation email | The order must be paid; the product version bought must have files |
| Email links point to the wrong host | Set the site **Base URL** |
| Link answers `410` | The entitlement expired, was used up, or the order was canceled or fully refunded. Use **Give downloads back** |
| Guest gets no links | Enable Customers so checkout collects the guest's email |
| `DigitalFiles` step error | The product must exist (run after `Content`), bytes must be valid base64 and at most 10 MB |
