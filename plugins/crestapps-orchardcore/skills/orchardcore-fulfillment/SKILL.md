---
name: orchardcore-fulfillment
description: Skill for the CrestApps Orchard Core Fulfillment Partners engine (CrestApps.OrchardCore.Fulfillment), which sends paid orders to outside fulfillment companies such as print-on-demand services. Covers the feature (depends on Commerce, Orders and Shipping), Settings > Commerce > Fulfillment partners, assigning lines to one partner with ClaimLinesAsync, reserving units in a pending shipment, sending once under the order's lock, FulfillmentRequest statuses and reasons, retries, the Needs attention notification, automatic sending with FulfillmentSettings and the background task, the order card and Commerce > Fulfillment requests, writing a partner with IFulfillmentProvider, FulfillmentOrder, FulfillmentLineClaim and FulfillmentSubmitResult, credentials, permissions and recipes. Use it when requests mention fulfillment partners, print on demand, dropshipping or a 3PL. Strong matches include IFulfillmentProvider, FulfillmentSender, FulfillmentRequest, ManageFulfillmentPartners and FulfillOrders.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Fulfillment Partners

You are an Orchard Core expert. Generate accurate setup, extension and troubleshooting guidance for the CrestApps **Fulfillment Partners** engine, which connects a store to the outside companies that make and ship its products (print-on-demand services, dropshippers, third-party warehouses). The engine assigns each order line to one partner, reserves the units in a Shipping shipment, sends paid orders once and leaves unclear outcomes to a person. It ships **no partner**: each company is a provider module implementing `IFulfillmentProvider`, from this repository or your own solution.

## Guidelines

- Install `CrestApps.OrchardCore.Fulfillment` (and each partner's package) in the web/startup project.
- Enable `CrestApps.OrchardCore.Fulfillment`; it turns on Commerce, Orders and Shipping (see `orchardcore-orders`, `orchardcore-shipping`). Enable one feature per partner on top.
- Never call a partner from a page, a handler or a settings check. Partners are called only by `FulfillmentSender`, under the order's lock, with a bounded timeout.
- A provider returns **Retry later** or **Refused** only when the partner certainly did not create the order; anything unclear is **Needs attention**, which is never resent automatically.
- Send the order's `OrderId` as the partner's external reference when the partner supports one, so a retry cannot create the order twice.
- Keep partner credentials as protected site settings (`ProtectedSettingExtensions`), never in plain settings or recipes.
- Today, tracking numbers, cancellations and refunds do not come back from partners: a person types the tracking number on the reserved shipment.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

## Feature

| Feature | ID | Depends on |
| --- | --- | --- |
| Fulfillment Partners | `CrestApps.OrchardCore.Fulfillment` (`FulfillmentConstants.Features.Fulfillment`) | Commerce (`CrestApps.OrchardCore.Commerce`), Orders (`CrestApps.OrchardCore.Orders`), Shipping (`CrestApps.OrchardCore.Shipping`) |

Projects: `CrestApps.OrchardCore.Fulfillment.Abstractions` (`IFulfillmentProvider`, the models, `FulfillmentConstants`, `FulfillmentPermissions`, `FulfillmentNames`; the only package a provider needs), `CrestApps.OrchardCore.Fulfillment.Core` (`FulfillmentRequest`, `FulfillmentSender`, `FulfillmentSettings`, `IFulfillmentRequestHandler`, stores and the paid-order handler) and the module (admin, drivers, notifications, background task).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Fulfillment"
      ],
      "disable": []
    }
  ]
}
```

## Admin locations

| Menu | Route | Permission |
| --- | --- | --- |
| **Settings > Commerce > Fulfillment partners** (settings group `fulfillment-partners`, `FulfillmentConstants.SettingsGroupId`) | `Admin/Settings/fulfillment-partners` | `ManageFulfillmentPartners` |
| **Commerce > Fulfillment requests** | `FulfillmentAdminController.Index` | `FulfillOrders` (Shipping) |
| **Fulfillment partners** card on an order's admin page (`DetailAdmin`) | Order page | `FulfillOrders` |

The settings page lists every registered partner with a **Connected** or **Not connected** badge (from `IsConnectedAsync`), each provider's own settings card, and **Send paid orders automatically**. With no partner feature on, it says to turn one on under **Tools > Features**.

## How sending works

1. **Checks.** The order must be paid, not refunded in full, without an open dispute, not anonymized, have a shipping address and not be a pickup. Bundle lines are excluded before partners claim lines; if only bundles remain the request is **Nothing to send**.
2. **What is left.** Quantities come from `IShipmentService.GetRemainingAsync`, physical lines only. If three units were ordered and one was shipped by hand, the partner gets two.
3. **Assigning lines.** Every connected partner is asked `ClaimLinesAsync`. Claims larger than what is left are capped; claims for lines not left are ignored. Two partners claiming the same line makes the request **Needs attention** naming the line and partners, and neither is called. A partner that claims nothing gets **Nothing to send** and is not called. A partner whose claim throws is skipped for the others and is **Needs attention** for itself.
4. **Reserving.** Before calling the partner, a pending shipment holds the partner's lines (`IShipmentService.CreateAsync(..., ship: false)`, noted "Sent to *partner*"), saved in the same commit as the request's **Sending** status.
5. **Calling.** `SubmitAsync` receives only the lines assigned to that partner and is cancelled after `FulfillmentConstants.SubmitTimeout` (2 minutes). A timeout or exception is **Needs attention**, shipment kept.
6. **Outcome.** **Sent** keeps the shipment pending with the partner's order id in its note; typing the carrier and tracking number (`ShipAsync`) marks it shipped and updates `Order.FulfillmentStatus`. **Refused** or partner **Nothing to send** deletes the shipment and frees the units. **Retry later** keeps the shipment and retries with a growing wait, at most 10 times. **Needs attention** keeps the shipment so nobody ships the units by hand by mistake; **Send again** reuses it. A reserved shipment shipped by hand meanwhile makes a resend send nothing.

Concurrency: recording, assigning, reserving and calling happen under a distributed lock on the order held longer than the partner call may take; the request is re-read from the database after the lock (`FulfillmentRequestStore.FindCommittedAsync`), and a request another worker won returns `FulfillmentSendResult.TakenByAnotherWorker` instead of an error. A request stuck in **Sending** for 15 minutes (the site stopped mid-call) is flagged **Needs attention** (`FlagStuckAsync`).

### Statuses and reasons

| `FulfillmentRequestStatus` | Meaning |
| --- | --- |
| `Pending` | Recorded, waiting to be sent (also the state of a **Retry later**, shown as *Waiting to be sent*) |
| `Sending` | Shipment reserved, partner being called |
| `Sent` | The partner has the order (`ExternalOrderId`, `SentUtc`) |
| `NothingToSend` | Nothing left in the order is this partner's |
| `NeedsAttention` | Not sent, or not certain it arrived; the reason says why |

A request stores a reason code (`FulfillmentReasons`, such as `NotPaid`, `Refunded`, `OpenDispute`, `Pickup`, `NoAddress`, `Bundle`, `NothingLeft`, `NotClaimed`, `ConflictingClaims`, `TooMany`, `AlreadySent`, `ProviderDisabled`, `ShippedByHand`, `Refused`, `Unclear`, `Interrupted`, `PartnerNothingToSend`, `ClaimFailed`, `GaveUp`, `RetryLater`, `Waiting`, `Sent`) with `ReasonArguments`, never a sentence; `FulfillmentRequestText` localizes it. The partner's own `Message` is kept as it came.

`FulfillmentRequest` (collection `Fulfillment`, one per order and partner, `FulfillmentRequestIndex` by order, order number, partner, shipment, status, due time and last change): `OrderId`, `OrderNumber`, `ProviderName`, `Status`, `Lines` (`FulfillmentRequestLine`: `LineId`, `Title`, `Sku`, `Quantity`), `ShipmentId`, `ExternalOrderId`, `Reason`, `ReasonArguments`, `Message`, `Attempts`, `NextAttemptUtc`, `CreatedUtc`, `ModifiedUtc`, `SentUtc`. Requests are operational records like orders and shipments: no recipe or deployment step.

### Manual and automatic sending

- **Manual.** The order card has a button that records and sends a paid order under its lock, so two people sending at once share one request per partner. **Send again** where the partner may already have the order asks through the admin confirm dialog. The card is read-only once the order is no longer paid.
- **Automatic.** With `FulfillmentSettings.SendAutomatically`, `FulfillmentOrderEventHandler.PaidAsync` records a pending request for each connected partner in the payment's unit of work (reading saved settings only, calling no partner). The **Send orders to fulfillment partners** background task (`* * * * *`) sends due requests and flags stuck ones.

### Notifications

With `OrchardCore.Notifications` on, a request that becomes **Needs attention** notifies once the Administrator role and every role with `FulfillOrders`, in the notification center with a link to the order (type `FulfillmentNeedsAttention`, `FulfillmentConstants.NeedsAttentionNotificationType`, which people can turn off in their preferences). React in code with `IFulfillmentRequestHandler.NeedsAttentionAsync(request)`, called once per transition after the request is saved.

## Variant names

An order line's `Title` already names its variant (such as `Tee (White / S)`), so `FulfillmentOrderLine.Title` is the line title as is; do not append `VariantTitle` again (fixed in #1346 for partners and agent order views). Use `VariantId` and `Sku` to map to the partner's catalog.

## Add a partner provider

A provider is a module that depends on `CrestApps.OrchardCore.Fulfillment` and references `CrestApps.OrchardCore.Fulfillment.Abstractions`.

| Member | Contract |
| --- | --- |
| `Name` | Technical name stored requests refer to; never change it |
| `DisplayName` | `LocalizedString` shown to people |
| `IsConnectedAsync()` | Reads saved settings only; the settings page, order card and paid-order handler call it |
| `TestConnectionAsync(ct)` | Calls the partner with saved credentials (the engine does not call it; a provider's settings card offers it); refusals and outages are `FulfillmentConnectionResult.Failure(message)`, not exceptions |
| `ClaimLinesAsync(order, ct)` | Returns a `FulfillmentLineClaim(LineId, Quantity)` per line of the `FulfillmentOrder` it makes, typically from a part on the published product; creates nothing at the partner |
| `SubmitAsync(order, ct)` | Sends the order (only this partner's lines), honors the token, returns `FulfillmentSubmitResult.Sent(externalOrderId, message)`, `NothingToSend`, `RetryLater`, `Refused` or `NeedsAttention` |

`FulfillmentOrder` carries `OrderId`, `OrderNumber`, `ShippingAddress` (the Addresses `Address` the order stores), `Email`, `Phone` and `Lines` (`FulfillmentOrderLine`: `LineId`, `ContentItemId`, `VariantId`, `Sku`, `Title`, `Quantity`). `FulfillmentNames.Split(name)` splits a full name into the first word and the rest (a single word has no last name).

Register it scoped, add a `SiteDisplayDriver<TSettings>` on `FulfillmentConstants.SettingsGroupId` that checks `FulfillmentPermissions.ManageFulfillmentPartners`, call `services.AddSiteSettingsPermission(FulfillmentConstants.SettingsGroupId, FulfillmentPermissions.ManageFulfillmentPartners)`, keep secrets with `ProtectedSettingExtensions` (`ApplySecret`, `HasSecret`, `UnprotectSettingOrDefault`, namespace `CrestApps.OrchardCore.Core.Security`), and describe the settings to CrestApps Recipes with a `SiteSettingsSchemaBase`. A complete provider is in [references/provider-example.md](references/provider-example.md).

## Recipes and deployment

`FulfillmentSettings` travels with Orchard Core's `Settings` step and the **Fulfillment settings** deployment step (`AddSiteSettingsPropertyDeploymentStep<FulfillmentSettings, ...>`); CrestApps Recipes knows it through `FulfillmentSettingsSchema`. Partner credentials are protected: enter them again on another site.

```json
{
  "steps": [
    {
      "name": "Settings",
      "FulfillmentSettings": {
        "SendAutomatically": true
      }
    }
  ]
}
```

## Permissions

| Permission | Allows | Default roles |
| --- | --- | --- |
| `ManageFulfillmentPartners` ("Manage fulfillment partners and their credentials", security critical) | **Settings > Commerce > Fulfillment partners** and partner credentials | Administrator |
| `FulfillOrders` (Shipping, "Ship orders and update their shipments") | The order card, sending, **Send again**, **Commerce > Fulfillment requests**, and typing tracking numbers on the reserved shipment | Administrator, Store manager, Warehouse |

## Troubleshooting

| Symptom | Check |
| --- | --- |
| No partner listed on the settings page | No provider feature is enabled; the engine ships no partner |
| Partner shows **Not connected** | Its settings card is incomplete; `IsConnectedAsync` reads saved settings only, so a wrong but present key still shows **Connected** until a send fails |
| Order card has no send button | The order is not paid, is fully refunded, has an open dispute or was anonymized; the user needs `FulfillOrders` |
| Request **Nothing to send** | No partner claimed the remaining lines, everything left was shipped by hand, only bundle lines remain, or the order is a pickup |
| **Needs attention** with conflicting claims | Two partners claim the same line; fix the product's partner setup so only one claims it, then **Send again** |
| **Needs attention** after a timeout | The partner may have the order: check its dashboard before **Send again** (which reuses the reserved shipment) |
| Paid orders are not sent automatically | Turn on **Send paid orders automatically** and make sure background tasks run (**Tools > Background Tasks**) |
| Units look reserved but nothing shipped | The pending shipment holds them while the partner makes the order; type the tracking number on it when it ships |
| No notification for Needs attention | Enable `OrchardCore.Notifications`; the user's role needs `FulfillOrders` and the notification type must not be turned off |
| Line title shows the variant twice | A custom provider appends `VariantTitle`; the line `Title` already includes it |

Related skills: `orchardcore-orders` (order events, the order page), `orchardcore-shipping` (shipments, `FulfillOrders`), `orchardcore-commerce` (suite map), `orchardcore-products` (product parts a provider reads).
