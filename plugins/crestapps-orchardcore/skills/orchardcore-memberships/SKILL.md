---
name: orchardcore-memberships
description: Skill for the CrestApps Orchard Core Memberships module (CrestApps.OrchardCore.Memberships), which sells access as a product. Covers the MembershipPart (RoleNames, DurationDays) that makes a product a membership, the Membership record and MembershipStatus (Active, Expired, Revoked), IMembershipStore and MembershipService (GrantAsync, RevokeOrderAsync, EndAsync, ExpireAsync), MembershipPeriods.Extend, MembershipOrderEventHandler built on OrderLineGrantHandlerBase, role grants through IRoleGrantService, the Memberships recipe and deployment step, MembershipCartLineHandler refusing guests, the hourly MembershipsBackgroundTask, gating content with Content Access Control and RolePickerPart, the Memberships sample recipe, Commerce > Memberships with ManageMemberships, and the storefront account/memberships page. Use it when requests mention selling memberships, paid roles, members-only content bought as a product, or granting something when an order is paid.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Memberships

Memberships sells access. Attach the **Membership** part to a product type; buying the product puts the buyer in the part's roles for its length, buying again adds time, and a cancellation, a full refund, the end date or the store takes the roles away. Whatever those roles gate (content restricted with Content Access Control, permissions, features) opens and closes with the membership.

Memberships is a one-off purchase through an order. For a renewing, billed-every-cycle plan with roles, use Subscriptions and its entitlements (`orchardcore-subscriptions`). Related skills: `orchardcore-carts` and `orchardcore-orders` (orders and `IOrderEventHandler`), `orchardcore-products`, `orchardcore-checkout`, `orchardcore-content-access-control`, `orchardcore-crestapps-roles`.

## Feature

| Feature ID | Depends on | Fills |
| --- | --- | --- |
| `CrestApps.OrchardCore.Memberships` (`MembershipsConstants.Feature`) | Commerce, `CrestApps.OrchardCore.Orders`, `OrchardCore.Roles` | `IOrderEventHandler`, `ICartLineHandler` |

Install `CrestApps.OrchardCore.Memberships` in the web project. The **Memberships sample** recipe (`MembershipsSample`) enables Contents, Roles, Content Access Control, Products, Carts, Orders, Checkout and Memberships, creates a `Member` role, a members-only **Members' Article** type with one article, and a $49 **Annual Membership** product.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContentAccessControl",
        "CrestApps.OrchardCore.Orders",
        "CrestApps.OrchardCore.Checkout",
        "CrestApps.OrchardCore.Memberships"
      ],
      "disable": []
    }
  ]
}
```

## Making a product a membership

`MembershipPart`: `RoleNames` (string array) and `DurationDays` (default 365; `0` or less never ends).

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "MembershipPlan",
          "DisplayName": "Membership Plan",
          "Settings": { "ContentTypeSettings": { "Creatable": true, "Listable": true, "Draftable": true, "Versionable": true } },
          "ContentTypePartDefinitionRecords": [
            { "PartName": "TitlePart", "Name": "TitlePart", "Settings": { "ContentTypePartSettings": { "Position": "0" } } },
            { "PartName": "ProductPart", "Name": "ProductPart", "Settings": { "ContentTypePartSettings": { "Position": "1" }, "ProductPartSettings": { "Type": "Service", "DefaultCurrency": "USD", "SalesMode": "Purchasable", "PriceDisplay": "Show" } } },
            { "PartName": "MembershipPart", "Name": "MembershipPart", "Settings": { "ContentTypePartSettings": { "Position": "2" } } }
          ]
        }
      ]
    },
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "annualmembership000000000a",
          "ContentType": "MembershipPlan",
          "DisplayText": "Annual Membership",
          "Latest": true,
          "Published": true,
          "TitlePart": { "Title": "Annual Membership" },
          "ProductPart": { "Price": 49, "Currency": "USD", "Sku": "MEMBER-ANNUAL", "IsPurchasable": true },
          "MembershipPart": { "RoleNames": [ "Member" ], "DurationDays": 365 }
        }
      ]
    }
  ]
}
```

## How it works

- **Paid.** `MembershipOrderEventHandler.GrantAsync` calls `MembershipService.GrantAsync(order)`: for each membership line it creates a `Membership` or extends the member's active one for that product with `MembershipPeriods.Extend(now, currentEnd, DurationDays, quantity)` (from the current end while it has not passed, else from now; `null` end for an unlimited part). It grants the roles through `IRoleGrantService` (`CrestApps.OrchardCore.Users.Abstractions`, `services.AddRoleGrants()` from Users.Core), which records each grant on the user by source (`Membership`) and source id (the membership); new memberships leave `AddedRoleNames` empty. Line roles and length are read from the product version the order bought. When the buyer is the signed-in user on the request, their sign-in is refreshed so the roles apply at once; paid by webhook, the sign-in catches up later. A guest's order grants nothing.
- **Canceled** order: `RevokeOrderAsync` ends the memberships it bought. **Refunded**: only when the order is refunded in full (`OrderRefundStatus.Refunded`).
- **Ending** (`EndAsync(membership, status, reason)`): revokes the membership's role grants; the member leaves a role only when the membership put them in it and nothing else (another membership, a subscription) still grants it. The security stamp is then updated so an existing sign-in is re-validated. `MembershipRoleGrantSource` reports the roles of memberships made before grants were recorded (from `AddedRoleNames`), so they are kept and removed as before.
- **Expiry**: `MembershipsBackgroundTask` (*Membership end dates*, `15 * * * *`) calls `ExpireAsync`.
- **Cart**: `MembershipCartLineHandler` refuses a membership line to a guest (*Sign in to buy ...*).

`Membership` (collection `Memberships`, `MembershipIndex`): `UserId`, `UserName`, `ContentItemId`, `Title`, `RoleNames`, `AddedRoleNames`, `OrderIds`, `StartsUtc`, `EndsUtc`, `Status`, `EndedReason`. `IMembershipStore`: `ListByUserAsync`, `ListByOrderAsync(userId, orderId)`, `ListEndedAsync`, `PageAsync(search, status, skip, take)`.

## Recipes and deployment

The `Memberships` step creates or updates memberships. The member is named by `UserName` (must exist on the site); a membership is matched by `ItemId`, then by member, product (`ContentItemId`) and `StartsUtc`. An active imported membership grants its `RoleNames` through role grants. The **Memberships** deployment step (*Commerce*) writes every membership without the member's user id or `AddedRoleNames`.

```json
{
  "steps": [
    {
      "name": "Memberships",
      "Memberships": [
        { "ItemId": "4ms00000000000000000000001", "UserName": "ana", "ContentItemId": "4gold0000000000000000000001", "Title": "Gold", "RoleNames": [ "Member" ], "StartsUtc": "2026-01-01T00:00:00Z", "EndsUtc": "2027-01-01T00:00:00Z", "Status": "Active" }
      ]
    }
  ]
}
```

## Gating content

Attach `RolePickerPart` with `RolePickerPartContentAccessControlSettings.IsContentRestricted = true` to the content type (Content Access Control). `RoleBasedContentItemAuthorizationHandler` then refuses **View content** to anyone not in one of the item's roles, anonymous visitors included, even when the permission is granted to everyone. Administrators see every item.

## Granting something else when an order is paid

The same pattern serves any "paid order grants X" module. Derive from `OrderGrantHandlerBase` (`CrestApps.OrchardCore.Orders.Core.Services`), which routes paid orders to `GrantAsync` and canceled or fully refunded orders to `RevokeAsync`. When the grant comes from a part on the product, `OrderLineGrantHandlerBase<TPart>` (as Memberships and Downloads use) reads each line's purchased product version and skips lines already granted:

```csharp
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;

public sealed class CourseSeatOrderHandler : OrderGrantHandlerBase
{
    protected override Task GrantAsync(Order order, CancellationToken cancellationToken)
    {
        // Enroll order.UserId in the courses on order.Lines. When the order is paid through the checkout this
        // runs inside the completing transaction: throwing rolls the completion back for a retry.
        return Task.CompletedTask;
    }

    protected override Task RevokeAsync(Order order, OrderRevokeReason reason, CancellationToken cancellationToken)
    {
        // reason is OrderRevokeReason.Canceled or Refunded.
        return Task.CompletedTask;
    }
}

// services.AddScoped<IOrderEventHandler, CourseSeatOrderHandler>();
```

## Screens and permissions

- **Commerce > Memberships** (admin route `memberships`, `ManageMemberships`, on Orchard Core's `AdminList`): member, roles, end date, status filter, **End now** (no refund).
- Storefront `account/memberships` (route `AccountMemberships`, shape `AccountMemberships`) lists the member's own memberships in the commerce account menu.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| Paid but content still hidden | Sign-in not refreshed (paid by webhook); sign out and in, or wait for the security stamp check |
| Role kept after a refund | Only a full refund ends a membership |
| Role kept after ending | Another active membership gives the same role, or the member had the role before |
| Guest cannot add to cart | By design; a membership needs an account |
