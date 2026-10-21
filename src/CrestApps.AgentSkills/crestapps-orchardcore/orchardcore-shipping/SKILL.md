---
name: orchardcore-shipping
description: Skill for the CrestApps Orchard Core Shipping module (CrestApps.OrchardCore.Shipping, Shipping.EasyPost, Shipping.Api). Covers ShippingZone and ShippingMethod with the Flat, Table, Free, LocalDelivery and Carrier rate providers, IShippingRateService, IShippingRateProvider with ShippingRateContext and CreateRate, ShippingPart weights and classes, ShippingSettings (weight unit, classes, carriers and tracking templates, pickup locations, ship-from address and parcel defaults), the checkout Shipping step and CheckoutShippingInfo, store pickup, shipments with IShipmentService and IShipmentEventHandler, ICarrierProvider and EasyPost labels, tracking and webhook, the ShippingZones recipe and deployment step, shipment workflow events, and the Shipping API. Use it when requests mention Orchard Core shipping rates, shipping zones, carriers, labels, tracking, store pickup, local delivery or fulfilment.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Shipping

Shipping (new in CrestApps.OrchardCore 3.0) prices shipping and records shipments. Zones say where; each zone's methods say how and what it costs through rate providers. Products carry weight and shipping class in the **Shipping** part.

## Where it fits in the commerce suite

| Module | Relationship |
| --- | --- |
| Commerce, Products (`orchardcore-commerce`, `orchardcore-products`) | Required |
| Orders (`orchardcore-orders`) | Required. Adds the Shipping step to an order's checkout and fills `Order.Shipping`, `ShippingAddress`, `FulfillmentStatus` |
| Addresses (`orchardcore-addresses`) | Required. Zones and rates work on the shared `Address` |
| Checkout (`orchardcore-checkout`) | The shipping line is a taxed billing item; the shipping address becomes the tax destination |
| Customers (`orchardcore-customers`) | Optional address book on the step |
| Inventory (`orchardcore-inventory`) | Pickup locations take units from a stock location |
| Markets (`orchardcore-markets`) | Methods in the market's currency, else converted |

## Features

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Shipping` | Shipping | Commerce, Products, Orders, Addresses, `OrchardCore.Contents` |
| `CrestApps.OrchardCore.Shipping.EasyPost` | EasyPost | Shipping |
| `CrestApps.OrchardCore.Shipping.Api` | Shipping API | Shipping, Checkout API |

### Guidelines

- Install `CrestApps.OrchardCore.Shipping` in the web/startup project and attach **ShippingPart** to product types that ship.
- Rates are always priced on the server; never trust a rate amount posted by the browser.
- Store carrier API keys with `ProtectSetting` and read them with `UnprotectSettingOrDefault`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Shipping"
      ],
      "disable": []
    },
    {
      "name": "ShippingZones",
      "Zones": [
        {
          "Name": "United States",
          "Countries": ["US"],
          "Methods": [
            { "Name": "Standard", "Description": "3 to 5 business days", "ProviderName": "Flat", "Properties": { "FlatRateSettings": { "Amount": 5, "PerItemAmount": 1 } } },
            { "Name": "Free over $100", "ProviderName": "Free", "Properties": { "FreeShippingSettings": { "MinimumSubtotal": 100 } } }
          ]
        },
        {
          "Name": "Everywhere else",
          "RestOfWorld": true,
          "Position": 99,
          "Methods": [
            { "Name": "International", "ProviderName": "Table", "Properties": { "TableRateSettings": { "Basis": 0, "Rows": [ { "From": 0, "Amount": 20 }, { "From": 2, "Amount": 35 } ] } } }
          ]
        }
      ]
    },
    {
      "name": "Settings",
      "ShippingSettings": {
        "WeightUnit": 0,
        "ShippingClasses": ["Bulky"],
        "Carriers": [
          { "Name": "UPS", "TrackingUrlTemplate": "https://www.ups.com/track?tracknum={0}" }
        ],
        "SendShippedEmails": true
      }
    }
  ]
}
```

The `ShippingZones` step creates or updates zones by `ItemId` or `Name` (names are unique); methods without a `MethodId` get one; provider settings go in `Properties` keyed by type name. The **Shipping Zones** deployment step exports every zone. `TableRateSettings.Basis` is `Weight` (0), `Subtotal` or `Quantity`.

## Model

| Type | Key members |
| --- | --- |
| `ShippingZone` (`INamedCatalog<ShippingZone>`) | `Name`, `Position`, `Countries` (ISO alpha-2), `Regions` (ISO 3166-2 such as `US-AK`), `RestOfWorld`, `Methods` |
| `ShippingMethod` | `MethodId`, `Name`, `Description`, `ProviderName`, `Enabled`, `Position`, `Currency` (empty for any), `TaxCategoryCode`, `ClassSurcharges`; provider settings in its properties |
| `ShippingPart` | `Weight` (site unit), `ShippingClass`, `VariantWeights`; `GetWeight(variantId)` |
| `ShippingSettings` (group `shipping`) | `WeightUnit` (`Kilogram`, `Pound`), `ShippingClasses`, `Carriers`, `SendShippedEmails`, `PickupLocations` (up to 20, with `Fee`, `Instructions`, `StockLocationId`), `ShipFrom`, default parcel size, `PackagingWeight` |

Provider settings: `FlatRateSettings` (`Amount`, `PerItemAmount`), `TableRateSettings` (`Basis`, `Rows` of `From` and `Amount`, `Maximum`), `FreeShippingSettings` (`MinimumSubtotal`), `LocalDeliverySettings` (`PostalCodes` with `981*` wildcards, `Amount`, `FreeFromSubtotal`, `MinimumSubtotal`), `CarrierRateSettings` (`Carriers`, `MaxRates`, `MarkupPercent`, `MarkupAmount`, `FallbackAmount`).

## Rates

`IShippingRateService.FindZoneAsync(address)` returns the first zone by `Position` covering the address, else the rest-of-world zone. `GetRatesAsync(ShippingRateRequest)` asks each enabled method's provider; class surcharges are added and amounts rounded and never negative. A `ShippingRateRequest` has `Destination`, `Currency` and `Lines` (`ShippingRequestLine` with weight and class), plus `Subtotal`, `TotalWeight` and `ItemCount`.

Add a rate provider:

```csharp
public sealed class PerKiloShippingProvider : IShippingRateProvider
{
    internal readonly IStringLocalizer S;

    public PerKiloShippingProvider(IStringLocalizer<PerKiloShippingProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Name => "PerKilo";

    public LocalizedString DisplayName => S["Per kilogram"];

    public LocalizedString Description => S["A base amount plus an amount for each kilogram."];

    public Task<IReadOnlyList<ShippingRate>> GetRatesAsync(ShippingRateContext context, CancellationToken cancellationToken = default)
    {
        var settings = context.Method.GetOrCreate<PerKiloRateSettings>();
        var amount = settings.BaseAmount + (settings.PerKilo * context.Request.TotalWeight);

        return Task.FromResult<IReadOnlyList<ShippingRate>>([context.CreateRate(amount)]);
    }
}

public sealed class PerKiloRateSettings
{
    public decimal BaseAmount { get; set; }

    public decimal PerKilo { get; set; }
}
```

Register with `services.AddScoped<IShippingRateProvider, PerKiloShippingProvider>();`, and add a `DisplayDriver<ShippingMethod>` that edits `PerKiloRateSettings` and renders only when `ProviderName` is `PerKilo`. `CreateRate(amount, option, name)` returns several rates from one method (`RateId` becomes `{MethodId}:{option}`).

## Checkout

- `ShippingCheckoutHandler` adds the **Shipping** step (`ShippingConstants.CheckoutStepKey`, after Contact) when a line is a `ProductType.Good`. The step finishes only with a rate priced on the server.
- The choice is kept as `CheckoutShippingInfo` (address, rate, method, zone, amount) and becomes a billing line with `TaxableKind = Shipping` and the method's `TaxCategoryCode`.
- The checkout tax destination is the shipping address, else the billing address.
- **Store pickup** offers `PickupLocations`: no quote, rate `pickup:{locationId}` with provider `Pickup`, the location's fee and address (taxed there); `StockLocationId` flows to `OrderShipping.LocationId` for Inventory.

## Shipments

`IShipmentService` (in the caller's unit of work): `GetRemainingAsync(order)`, `CreateAsync(order, lines, details, ship)` for a paid order, `ShipAsync`, `DeliverAsync`, `ReturnAsync`, `DeleteAsync`. Each change updates `Order.FulfillmentStatus` (`Unfulfilled`, `PartiallyFulfilled`, `Fulfilled`). A tracking link must be `http` or `https`; an empty one comes from the carrier's `TrackingUrlTemplate` (`{0}` is the number).

```csharp
public sealed class WarehouseShipmentHandler : ShipmentEventHandlerBase
{
    private readonly ILogger _logger;

    public WarehouseShipmentHandler(ILogger<WarehouseShipmentHandler> logger)
    {
        _logger = logger;
    }

    public override Task ShippedAsync(ShipmentContext context)
    {
        _logger.LogInformation("Order {OrderNumber} shipment {Number} left with {Carrier}.", context.Order.OrderNumber, context.Shipment.Number, context.Shipment.Carrier);

        return Task.CompletedTask;
    }
}
```

Register with `services.AddScoped<IShipmentEventHandler, WarehouseShipmentHandler>();`. Built in: the shipped email (`order-shipped` kind, once per shipment, ready-for-pickup wording for pickup orders), workflow events `ShipmentCreatedEvent`, `ShipmentShippedEvent`, `ShipmentDeliveredEvent`, `ShipmentReturnedEvent`, and webhooks. `OrderShipmentsDisplayDriver` adds `OrderShipments_DetailAdmin` and `OrderShipments_Detail` to the order pages.

## Carriers and EasyPost

`ICarrierProvider` quotes carrier rates (`QuoteAsync(CarrierShipmentRequest)` returning a `CarrierQuote`) and buys labels (`BuyLabelAsync(quoteId, rateId)` returning a `CarrierLabel`). `ICarrierProviderResolver` returns the default enabled provider chosen on the **Providers** tab of **Settings > Commerce > Shipping**. The `Carrier` rate provider quotes through it with markup, caches quotes for ten minutes, and falls back to `FallbackAmount` or nothing.

EasyPost (`CrestApps.OrchardCore.Shipping.EasyPost`) keeps `ApiKey` and `WebhookSecret` protected (purpose `EasyPostSettings`), registers its webhook automatically when saved with a public https Base URL, and receives `POST shipping/easypost/webhook` signed with `X-Hmac-Signature` to update tracking and deliver shipments. Labels are bought from the order (`shipping/shipments/{shipmentId}/label`, `FulfillOrders`).

## Shipping API

`CrestApps.OrchardCore.Shipping.Api` maps `checkout/{id}/pickup-locations`, `POST checkout/{id}/shipping-rates`, `PUT checkout/{id}/shipping`, the buyer's `me/orders/{orderId}/shipments`, and staff `orders/{orderId}/shipments`, `shipments/{shipmentId}/ship` and `/deliver` (`FulfillOrders`). See the API reference in `orchardcore-commerce`.

## Admin and permissions

**Commerce > Shipping** (`shipping/zones`) lists zones in the order they are tried; **Try rates** (`shipping/rates`) quotes an address. With Reports, **Shipping times** (`FulfillOrders`).

| Permission | Allows | Default roles |
| --- | --- | --- |
| `ManageShipping` | Zones, methods and **Settings > Commerce > Shipping** | Administrator, Store manager |
| `FulfillOrders` | Shipments on an order's page, labels | Administrator, Store manager, Warehouse |

Not included: return labels, dimensional weight, postal-code zones (use local delivery), pick lists.
