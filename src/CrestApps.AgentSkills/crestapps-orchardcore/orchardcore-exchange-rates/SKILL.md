---
name: orchardcore-exchange-rates
description: Skill for the CrestApps Orchard Core Exchange Rates module (CrestApps.OrchardCore.ExchangeRates), dated exchange rates to the store's reporting currency entered by hand or fetched daily by a provider. Covers the feature and its Commerce Foundation dependency, the ExchangeRate model and validation, ExchangeRateSettings, IExchangeRateService rate lookup with inverse pairs, IExchangeRateManager, writing an IExchangeRateProvider, the Exchange Rate Update background task, the ExchangeRates recipe and deployment step, the admin pages, the ManageExchangeRates permission, and how Invoicing records rates on invoices and ledger transactions. Use it when requests mention exchange rates, currency conversion, a reporting or base currency, multi-currency invoices or a central bank rate feed. Strong matches include ExchangeRatesConstants, ExchangeRateQuote, ExchangeRateUpdater, AddExchangeRates and ExchangeRateIndex.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Exchange Rates

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Exchange Rates module, the building block for what one currency was worth in another on a day.

## Guidelines

- Install `CrestApps.OrchardCore.ExchangeRates` in the web/startup project. From your module reference `CrestApps.OrchardCore.ExchangeRates.Abstractions` (model, `IExchangeRateService`, `IExchangeRateProvider`, constants) or `CrestApps.OrchardCore.ExchangeRates.Core` (`AddExchangeRates()`, stores, `ExchangeRatesPermissions`).
- A module that needs a rate asks `IExchangeRateService`; it never keeps its own rate table.
- A rate reads `1 {Currency} = {Rate} {BaseCurrency}` and holds from its `Date` until a later rate of the same pair. There is one rate per pair and day; codes are stored upper case.
- No provider is built in. Write an `IExchangeRateProvider` for a feed.
- All recipe JSON is wrapped in `{ "steps": [...] }`. All C# classes are `sealed`, except view models.

## Feature

| Feature | ID | Depends on |
| --- | --- | --- |
| Exchange Rates | `CrestApps.OrchardCore.ExchangeRates` | Commerce Foundation (`CrestApps.OrchardCore.Commerce.Foundation`) |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ExchangeRates"
      ],
      "disable": []
    }
  ]
}
```

## Model

`ExchangeRate` (`CrestApps.OrchardCore.ExchangeRates.Models`, a catalog item in the `ExchangeRates` collection with `ExchangeRateIndex`, declared `.ExclusiveToDocumentType().SingleRecordPerDocument()`):

| Property | Notes |
| --- | --- |
| `Currency` | ISO 4217 code converted, such as `EUR` |
| `BaseCurrency` | Code converted to, usually the reporting currency |
| `Rate` | Units of `BaseCurrency` one unit of `Currency` is worth; must be above zero |
| `Date` | Day the rate takes effect (`DateOnly`) |
| `Source` | `manual` (`ExchangeRatesConstants.ManualSource`) or the provider's name |
| `CreatedUtc` | Set when created |

`ExchangeRateHandler` refuses codes that are not three letters, the same currency on both sides, a rate of zero or less, no date, and a second rate of the same pair on the same day.

`ExchangeRateSettings`: `BaseCurrency` (the reporting currency), `Currencies` (the codes a provider fetches), `ProviderName`.

## Looking a rate up

```csharp
using CrestApps.OrchardCore.ExchangeRates.Services;

public sealed class ReportingAmounts
{
    private readonly IExchangeRateService _rates;

    public ReportingAmounts(IExchangeRateService rates)
    {
        _rates = rates;
    }

    public async Task<decimal?> ToReportingCurrencyAsync(decimal amount, string currency, DateOnly day, CancellationToken cancellationToken)
    {
        var baseCurrency = await _rates.GetBaseCurrencyAsync(cancellationToken);

        if (string.IsNullOrEmpty(baseCurrency))
        {
            return null;
        }

        var quote = await _rates.GetRateAsync(currency, baseCurrency, day, cancellationToken);

        return quote is null ? null : Math.Round(amount * quote.Rate, 2, MidpointRounding.AwayFromZero);
    }
}
```

`GetRateAsync(currency, baseCurrency, date)` returns 1 for the same currency; else the latest rate of the pair on or before the day; else the inverse of the opposite pair's latest (to 10 decimal places); the newer of the two wins when both exist. It returns an `ExchangeRateQuote(Rate, Date, Source)` or `null`. Write rates through `IExchangeRateManager` (a catalog manager) so validation runs.

## Writing a provider

```csharp
using CrestApps.OrchardCore.ExchangeRates.Services;

public sealed class CentralBankExchangeRateProvider : IExchangeRateProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public CentralBankExchangeRateProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    // Stored in the settings and on every rate it fetches.
    public string Name => "CentralBank";

    public string DisplayName => "Central bank daily feed";

    public async Task<IReadOnlyDictionary<string, decimal>> GetRatesAsync(string baseCurrency, IReadOnlyCollection<string> currencies, DateOnly date, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();
        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        // Fetch what one unit of each currency is worth in baseCurrency on the day; leave out a currency the feed lacks.

        return rates;
    }
}

// In your module Startup:
// services.AddScoped<IExchangeRateProvider, CentralBankExchangeRateProvider>();
```

Choose it under **Settings > Commerce > Exchange rates** with the currencies to fetch. The **Exchange Rate Update** background task (`ExchangeRateUpdateBackgroundTask`, `20 * * * *`, hourly at twenty past) calls `ExchangeRateUpdater.UpdateAsync` for the site's day: a pair the day already has, entered by hand or fetched earlier, is never replaced, and a rate that fails validation is logged and skipped. Nothing runs without a provider, a base currency and at least one currency.

## Who uses the rates

Invoicing copies the issue day's rate to the reporting currency onto an invoice, debit note and its ledger transaction (`ExchangeRate`, `BaseCurrency` on `Invoice` and `Transaction`); a credit note carries its invoice's rate. A document in the reporting currency records 1; a currency without a rate records none (logged). The invoice and credit note data sets add **Reporting currency**, **Exchange rate**, **Total in reporting currency** and **Still due in reporting currency**. See `orchardcore-invoicing`.

## Admin menu and permission

| Location | Permission |
| --- | --- |
| **Commerce > Exchange rates** (list, add, edit, delete; both list layouts) | `ManageExchangeRates` (Manage exchange rates) |
| **Settings > Commerce > Exchange rates** (group `exchangeRates`) | `ManageExchangeRates` |

## Recipes and deployment

The `ExchangeRates` step (entries under `Rates`, matched by `ItemId`) and the **Exchange rates** deployment step move the rates. `ExchangeRateSettings` moves with the `Settings` step. With CrestApps Recipes, both have schemas.

```json
{
  "steps": [
    {
      "name": "Settings",
      "ExchangeRateSettings": {
        "BaseCurrency": "USD",
        "Currencies": [ "EUR", "CAD", "GBP" ],
        "ProviderName": "CentralBank"
      }
    },
    {
      "name": "ExchangeRates",
      "Rates": [
        { "ItemId": "4exchangerate0000000000001", "Currency": "EUR", "BaseCurrency": "USD", "Rate": 1.0842, "Date": "2026-11-20", "Source": "manual" },
        { "ItemId": "4exchangerate0000000000002", "Currency": "CAD", "BaseCurrency": "USD", "Rate": 0.7315, "Date": "2026-11-20", "Source": "manual" }
      ]
    }
  ]
}
```

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Invoices record no exchange rate | No `BaseCurrency` set, or no rate of the pair on or before the issue day (and no opposite pair) |
| The update task adds nothing | No provider registered or chosen, empty `Currencies`, or the day already has those pairs |
| "already has a rate on that day" | One rate per pair and day: edit the existing one |
| Fetched rate missing | The provider returned zero or a code not in `Currencies`; see the log |
