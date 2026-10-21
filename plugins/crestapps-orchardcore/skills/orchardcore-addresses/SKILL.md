---
name: orchardcore-addresses
description: Skill for the CrestApps Orchard Core Addresses module (CrestApps.OrchardCore.Addresses, Addresses.Countries, Addresses.GooglePlaces). Covers the immutable Address value shared by customers, checkout, orders, shipping and taxation, per-country address formats from Google address data with AddAddressesCore, IAddressFormatProvider and AddressFormat, IAddressValidator and AddressProblem, IAddressFormatter, region codes, the AddressEditor shape with IAddressEditorService and AddressEditorOptions, CreateAddressEditorAsync and CreateAddressDisplayAsync, the addresses/formats endpoint, ICountryService and managed Country content items, address suggestions through IAddressAutocompleteProvider, the Google Places provider with its protected API key, the address-autocomplete rate-limit group, and the ManageAddressSuggestions permission. Use it when requests mention Orchard Core addresses, international address formats, address validation, a country list, address autocomplete or Google Places.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Addresses

Addresses (new in CrestApps.OrchardCore 3.0) gives the platform one address structure for every country. Each country's rules decide which parts it has, which are required, and how it is checked and printed. The data is a snapshot of Google's address data (CC BY 4.0) inside the package; a site never calls Google for formats.

## Where it fits in the commerce suite

| Module | Relationship |
| --- | --- |
| Shipping (`orchardcore-shipping`) | Requires Addresses; zones and rates use `Address` |
| Customers (`orchardcore-customers`) | Address book edited with the address editor |
| Checkout and Orders (`orchardcore-checkout`, `orchardcore-orders`) | Contact and shipping steps, immutable order copies, printed with `IAddressFormatter` |
| Taxation (`orchardcore-taxation`) | Jurisdictions match region codes; country lists come from `ICountryService` |
| CrestApps Content Fields (`orchardcore-crestapps-content-fields`) | Address Field (`CrestApps.OrchardCore.ContentFields.Address`) on any content type |

Addresses depends on nothing and can be used outside commerce.

## Features

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Addresses` | Addresses | none |
| `CrestApps.OrchardCore.Addresses.Countries` | Address Countries | Addresses, `OrchardCore.Contents`, `OrchardCore.ContentFields`, `OrchardCore.Title` |
| `CrestApps.OrchardCore.Addresses.GooglePlaces` | Address Suggestions (Google Places) | Addresses |

### Guidelines

- Install `CrestApps.OrchardCore.Addresses` in the web/startup project.
- Store `Address` values, never free-text address strings, and keep a copy (`Clone()`) on records that must not change.
- Store regions by code (`CA`, `13`, `D`) so tax jurisdictions match; `IAddressValidator` tidies names into codes.
- Validate posted addresses with `IAddressEditorService.ValidateAsync` and keep the returned `Address`.
- Turn on `OrchardCore.RateLimits` wherever a paid suggestion provider is set up.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Addresses",
        "CrestApps.OrchardCore.Addresses.Countries",
        "CrestApps.OrchardCore.Addresses.GooglePlaces",
        "OrchardCore.RateLimits"
      ],
      "disable": []
    }
  ]
}
```

## The Address value

`Address` (`CrestApps.OrchardCore.Addresses.Models`, in `CrestApps.OrchardCore.Addresses.Abstractions`) is immutable with init-only parts: `Name`, `Company`, `AddressLine1`, `AddressLine2`, `DependentLocality`, `City`, `Region`, `PostalCode`, `SortingCode`, `Country` (ISO alpha-2), `County`, `District` (for tax) and `Phone`.

```csharp
var address = new Address
{
    Name = "Avery Brooks",
    AddressLine1 = "1 Main Street",
    City = "Springfield",
    Region = "IL",
    PostalCode = "62701",
    Country = "US",
};
```

## Formats, validation and printing

Registered with `services.AddAddressesCore()` (the Addresses feature does this):

| Service | Use |
| --- | --- |
| `IAddressFormatProvider.GetFormatAsync(countryCode)` | `AddressFormat`: `Layout` (and `LatinLayout`), `Required`, `Uppercase`, `PostalCodePattern`, `PostalCodeExamples`, labels (`RegionLabel`, `PostalCodeLabel`), `Regions`; `FindRegion` by code, key or name. Unknown countries get a generic format. |
| `IAddressValidator.ValidateAsync(address)` | `AddressValidationResult` with `Problems` (`AddressProblem(Component, Kind)`, kinds `Missing`, `Invalid`, `Unknown`, `NotInRegion`), `IsValid`, and the tidied `Address` |
| `IAddressFormatter.GetLinesAsync(address, options)` | Lines in the country's order; `AddressFormatOptions.Envelope` capitalizes as the post wants, `IncludeCountry` adds the country name |

```csharp
public sealed class WarehouseAddressService
{
    private readonly IAddressValidator _validator;
    private readonly IAddressFormatter _formatter;

    public WarehouseAddressService(IAddressValidator validator, IAddressFormatter formatter)
    {
        _validator = validator;
        _formatter = formatter;
    }

    public async Task<IReadOnlyList<string>> GetLabelLinesAsync(Address address)
    {
        var result = await _validator.ValidateAsync(address);

        if (!result.IsValid)
        {
            return [];
        }

        return await _formatter.GetLinesAsync(result.Address, new AddressFormatOptions { Envelope = true });
    }
}
```

Validation checks the form of an address, not that the place exists.

## The address editor

Every address form (address book, admin, checkout, Address Field) uses the `AddressEditor` shape: country first, then fields in the country's order with its labels, required marks and region list. `IAddressEditorService` (`CrestApps.OrchardCore.Addresses.Core.Editing`):

| Method | Use |
| --- | --- |
| `BuildAsync(address, options)` | Editor model for an address or a new one |
| `PopulateAsync(model, options)` | Lay a posted model out again for its country |
| `ValidateAsync(model, modelState, prefix, options)` | Add problems to `ModelState` under `prefix`, in the visitor's language; returns the tidied address |
| `GetLayoutAsync(countryCode)` | The `AddressEditorLayout` |

`AddressEditorOptions`: `ShowName`, `RequireName`, `ShowCompany`, `ShowPhone`, `Countries` (all the site serves when `null`), `DefaultCountry`.

```csharp
// GET
var editor = await _addressEditor.BuildAsync(customerAddress, options);
model.AddressEditor = await _shapeFactory.CreateAddressEditorAsync(editor, prefix: "Address");

// POST
var posted = new AddressEditorViewModel();
await TryUpdateModelAsync(posted, "Address");
var result = await _addressEditor.ValidateAsync(posted, ModelState, "Address", options);
```

A display driver can return `Initialize<AddressEditorViewModel>("AddressEditor", ...)` with its own prefix. `CreateAddressDisplayAsync(address)` gives the `AddressDisplay` shape. When the country changes, the editor script fetches `~/addresses/formats/{countryCode}` (anonymous, cached an hour); without script the server re-lays the form on post. `AddressEditorViewModel.HasAnyValue()` tells a blank optional address from one started.

## Country lists

`ICountryService.GetCountriesAsync()` returns the ISO list from the .NET runtime, or with **Address Countries** on, the published `Country` content items (seeded from the ISO list, unique codes in `CountryPart`'s `Code` field, `CountryIndex`), falling back to ISO while there are none. Read and write the code with `GetCountryCode()` and `SetCountryCode()`; `CountryCodeUtility` normalizes codes. Managed countries decide which countries are offered and their names, not how addresses are written. Taxation stores codes, so toggling either feature never orphans tax data.

## Address suggestions

```csharp
public interface IAddressAutocompleteProvider
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(AddressSuggestionRequest request, CancellationToken cancellationToken = default);

    Task<Address> ResolveAsync(string suggestionId, string sessionToken, CancellationToken cancellationToken = default);
}
```

Register your provider as a scoped `IAddressAutocompleteProvider` in your own feature; the first that `IsAvailableAsync` answers. It runs only on the server, so keys never reach the browser. `AddressSuggestionRequest` has `Text`, `Country`, `SessionToken` and `Language`.

| Endpoint | Answers |
| --- | --- |
| `GET addresses/suggest?q=&country=&session=` | `[{ "id", "text" }]`; `q` under 3 or over 120 characters answers `[]` |
| `GET addresses/resolve?id=&session=` | The address parts, or `404` |

Both answer `404` with no provider and `429` over the limit. They are in the `address-autocomplete` rate-limit group (`AddressConstants.AutocompleteRateLimitGroup`); with `OrchardCore.RateLimits` on, a built-in limit allows 60 calls a minute per visitor IP, and a `Group` policy can add stricter limits (see `orchardcore-rate-limits`). Behind a proxy, enable Orchard Core's Reverse Proxy Configuration. The editor shows suggestions only when `data-suggest-url` is set, which happens only when a provider is available.

### Google Places

`GooglePlacesAddressAutocompleteProvider` calls the Places API (New) with the named `HttpClient` `GooglePlaces` (5-second timeout). The key is set under **Settings > Address suggestions** (group `address-suggestions`, permission `ManageAddressSuggestions`) and kept protected in `GooglePlacesSettings.ApiKey` (purpose `CrestApps.OrchardCore.Addresses.GooglePlaces.ApiKey`); it is never shown again, a blank field keeps it, and **Remove the key** clears it. Restrict the key to the Places API (New). Errors and timeouts are logged and give no suggestions.

## Permissions

| Permission | Allows | Default roles |
| --- | --- | --- |
| `ManageAddressSuggestions` | **Settings > Address suggestions** | Administrator |
