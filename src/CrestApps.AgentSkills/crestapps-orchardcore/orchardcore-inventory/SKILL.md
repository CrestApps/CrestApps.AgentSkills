---
name: orchardcore-inventory
description: Skill for the CrestApps Orchard Core Inventory module (CrestApps.OrchardCore.Inventory, Inventory.StockAlerts, Inventory.PurchaseOrders, Inventory.Api). Covers InventoryPart, stock items per product, variant and location, the append-only StockMovement ledger, IInventoryService (adjust, count, transfer, receive, reserve, commit, release, restock), StockChangeResult errors, stock locations and IFulfillmentLocationProvider, stock holds for orders through InventoryOrderEventHandler, IProductAvailabilityProvider and pre-orders, IStockLevelHandler and IStockLevelObserver, low-stock notifications and the Low Stock workflow event, the In stock listing filter, StockCsv import and export, the Stock recipe and deployment step, InventorySettings, back-in-stock alerts, purchase orders, stock reports, and the Inventory API. Use it when requests mention Orchard Core inventory, stock levels, stock holds, backorders, pre-orders, warehouses or locations, low stock alerts or purchase orders.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Inventory

Inventory (new in CrestApps.OrchardCore 3.0) counts the units of each product and variant on hand. Counts live outside the content item as stock items, and every change is a movement in an append-only ledger from which the count can be rebuilt.

## Where it fits in the commerce suite

| Module | Relationship |
| --- | --- |
| Commerce, Products (`orchardcore-commerce`, `orchardcore-products`) | Required. Inventory implements Products' `IProductAvailabilityProvider` and adds the In stock filter. |
| Orders (`orchardcore-orders`) | Optional. Order events hold, sell, release and restock stock. |
| Carts (`orchardcore-carts`) | Caps quantities and refuses sold-out lines through availability |
| Shipping (`orchardcore-shipping`) | Pickup locations take units from an inventory location |
| Commerce Webhooks (`orchardcore-commerce`) | `stock.changed`, `stock.low`, `stock.sold-out` events |

## Features

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Inventory` | Inventory | Commerce, Products, `OrchardCore.Contents` |
| `CrestApps.OrchardCore.Inventory.StockAlerts` | Back-in-Stock Alerts | Inventory, `OrchardCore.Email` |
| `CrestApps.OrchardCore.Inventory.PurchaseOrders` | Purchase Orders | Inventory, `CrestApps.OrchardCore.Resources` |
| `CrestApps.OrchardCore.Inventory.Api` | Inventory API | Inventory |

### Guidelines

- Install `CrestApps.OrchardCore.Inventory` in the web/startup project and attach **InventoryPart** to each product type you count.
- Change stock only through `IInventoryService`; it works in the caller's YesSql session and is committed with the caller's work.
- Stock items are concurrency-checked: a racing change throws `ConcurrencyException` at commit. Retry, never open a separate unit of work (SQLite would deadlock).
- React to stock changes with `IStockLevelHandler`, which runs after commit in its own scope.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Inventory",
        "CrestApps.OrchardCore.Inventory.StockAlerts"
      ],
      "disable": []
    }
  ]
}
```

## Model

All records live in the `Inventory` YesSql collection.

| Record | Key members |
| --- | --- |
| `StockItem` | `ContentItemId`, `VariantId`, `Sku`, `Title`, `OnHand`, `Reserved`, `Available` (`OnHand - Reserved`), `LowStockThreshold`, `AllowBackorder`, `ReleaseDateUtc`, `Archived`, `Levels` per location (`StockLocationLevel`) |
| `StockMovement` | `Kind` (`Received`, `Adjusted`, `Counted`, `Sold`, `Returned`, `Transferred`), signed `Quantity`, `OnHandAfter`, `Reason`, `OrderId`, `LocationId`, `PurchaseOrderId`, actor, `Utc` |
| `StockReservation` | `OrderId`, `LocationId`, `Quantity`, `ReturnedQuantity`, `Status` (`Held`, `Committed`, `Released`), `ExpiresUtc` |
| `InventoryPart` | `TrackInventory` (default true), `AllowBackorder`, `LowStockThreshold`, `ReleaseDateUtc` (**Ships from**, makes a backorder a pre-order) |
| `InventorySettings` | `DefaultLowStockThreshold` (5), `HoldMinutes` (60), `Locations` (up to 50), `LowStockNotificationRoles` |

The default location id is `InventoryConstants.DefaultLocationId` (`default`). `InventoryProductSynchronizer` keeps one stock item per variant (or one per product) in step as products are saved; removed variants and untracked products are archived, never deleted.

## `IInventoryService`

| Method | Use |
| --- | --- |
| `AdjustAsync(stockItemId, kind, quantity, reason, actor)` | `Received` (positive) or `Adjusted` (any non-zero); location overload |
| `SetCountAsync(stockItemId, count, reason, actor)` | Records a `Counted` movement, even when unchanged; location overload |
| `TransferAsync(stockItemId, fromLocationId, toLocationId, quantity, reason, actor)` | Moves available units between locations |
| `ReceiveAsync(stockItemId, locationId, quantity, purchaseOrderId, reason, actor)` | Receives purchase order units |
| `ReserveAsync(orderId, lines, expiresUtc)` | Holds all counted lines or none; returns a `StockShortage` |
| `CommitAsync(orderId, lines)` | Sells what the order holds, once |
| `ReleaseAsync(orderId, reason)`, `ReleaseExpiredAsync(take)` | Lets holds go |
| `RestockAsync(orderId, reason, actor)` and `RestockAsync(orderId, lines, locationId, reason, actor)` | Returns sold units, never more than bought |
| `RebuildAsync(stockItemId)` | Sets `OnHand` from the movements |

Results are `StockChangeResult`; errors are `NotFound`, `InvalidQuantity`, `Busy`, `NotEnoughAtLocation`. An order's units come from one location that has them all (the requested one first, then locations in listed order), otherwise from each in turn.

## Orders and availability

With Orders on, `InventoryOrderEventHandler` reserves on **Created** for `HoldMinutes` (refusing the order with *Only 1 of Mug left.* when short), sells on **Paid** in the checkout's unit of work, releases on **Canceled** (restocking a paid order when asked), restocks on a full **Refunded** when asked, re-holds on **Lines changed**, and restocks returned lines on **Returned**. `ExpiredStockHoldsBackgroundTask` releases expired holds every five minutes.

`InventoryAvailabilityProvider` answers availability across all locations; when a request names a cart, that cart's own holds count as available to it. Uncounted products are left out and can always be bought. With Product Bundles, `BundleAvailabilityProvider` answers for bundles from their parts.

## Reacting to stock levels

```csharp
public sealed class ReorderStockLevelHandler : IStockLevelHandler
{
    private readonly ILogger _logger;

    public ReorderStockLevelHandler(ILogger<ReorderStockLevelHandler> logger)
    {
        _logger = logger;
    }

    public Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
    {
        if (context.RanLow)
        {
            _logger.LogInformation("Stock for {Sku} is {Available}; reorder it.", context.Item.Sku, context.Item.Available);
        }

        return Task.CompletedTask;
    }
}
```

Register with `services.AddScoped<IStockLevelHandler, ReorderStockLevelHandler>();`. `ChangedAsync` runs when the level (`InStock`, `Low`, `Out`) changed; `AvailableChangedAsync` (default no-op) when available units changed. The context has `PreviousAvailable`, `PreviousLevel`, `Level`, `RanLow`, `SoldOut` and `BackInStock`. A rolled-back change raises nothing.

Built in: low-stock notifications to `LowStockNotificationRoles` (with Notifications), the **Low Stock** workflow event `LowStockEvent` (with Workflows), search record refresh (with Product Listing) and webhooks. The **Automation: low stock alert** recipe adds a workflow from `LowStockEvent` to an email.

## Recipes, deployment and CSV

```json
{
  "steps": [
    {
      "name": "Stock",
      "Items": [
        { "Sku": "MUG", "OnHand": 24 },
        { "ContentItemId": "4rgryp179mr10m6yvbt42xym8n", "VariantId": "4ydepe37jp6sn68kq7pvr4dh5d", "OnHand": 5 }
      ]
    }
  ]
}
```

Rows match by `ContentItemId` and `VariantId`, else by SKU; products named by id that aren't counted yet are synced first. The **Stock** deployment step exports every non-archived item. **Commerce > Inventory** exports and imports `StockCsv` (`Sku`, `Title`, `ContentItemId`, `VariantId`, `OnHand`, `Reserved`, `Available`, `LowStockThreshold`), up to 2 MB and 10,000 rows, all or nothing. With Content Transfer, `InventoryPart_*` columns import policy and counts (`Stock`, `VariantStock` such as `TEE-S=4; M / Red=2`).

## Back-in-stock alerts and purchase orders

- **StockAlerts:** `StockAlertForm` shows on a sold-out product's page; `POST stock/alerts` records a `StockAlert` (limited per email and per IP); `BackInStockAlertHandler` emails waiting requests when `BackInStock`. Alerts take part in personal data export and erasure.
- **PurchaseOrders:** **Commerce > Purchase orders** (`purchase-orders`) manages `PurchaseOrder` records (`Number`, `Supplier`, `LocationId`, `Currency`, `Lines` with `Ordered`, `Received`, `UnitCost`, `Status`: `Draft`, `Ordered`, `PartiallyReceived`, `Received`, `Canceled`). `PurchaseOrderReceiver.ReceiveAsync` receives through `IInventoryService.ReceiveAsync`. Suppliers are not emailed.

## Inventory API

`CrestApps.OrchardCore.Inventory.Api` maps `GET products/{id}/availability` (guests; counts only when low), and staff `inventory`, `inventory/{stockItemId}` (`ViewInventory`) with `receive`, `adjust`, `count`, `transfer` (`ManageInventory`, idempotency keys). A collision with a checkout answers `409 concurrency`. See the API reference in `orchardcore-commerce`.

## Admin, reports and permissions

- **Commerce > Inventory** (`InventoryAdminController`, an `AdminList`) lists items fewest available first, with *Low stock*, *Out of stock* and *Backordered* filters, and each item's movement history and forms.
- **Settings > Commerce > Inventory** (group `inventory`) is opened by `ManageInventory` without **Manage settings**.
- With Reports: **Stock on hand** and **Stock movements** (`ViewInventory`).

| Permission | Allows | Default roles |
| --- | --- | --- |
| `ManageInventory` | Changing stock, syncing products, Inventory settings, purchase orders; implies `ViewInventory` | Administrator, Store manager, Warehouse |
| `ViewInventory` | Listing stock and history, viewing purchase orders | Administrator, Store manager, Warehouse, Customer support |
