---
name: orchardcore-phone-numbers
description: Skill for using CrestApps Orchard Core phone-number parsing, validation, E.164 normalization, display formatting, region lookup, and time-zone lookup services. Covers the dependency-only CrestApps.OrchardCore.PhoneNumbers feature, IPhoneNumberService, the canonical PhoneNumber value type with TryParse, FromE164 and TryFromE164, FormatForDisplay, PhoneNumberComparisonKey for matching stored raw values, and how DNC screening, verifications, Omnichannel and the soft phone consume canonical numbers. Use this skill when requests mention Orchard Core phone numbers, E.164 formatting, libphonenumber, country calling codes, phone number display formatting, or phone number time zones. Strong matches include work with CrestApps.OrchardCore.PhoneNumbers, CrestApps.OrchardCore.PhoneNumbers.Abstractions, IPhoneNumberService, DefaultPhoneNumberService, PhoneNumber, PhoneNumberServiceExtensions, PhoneNumberComparisonKey, and TryFormatToE164.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Phone Numbers - Prompt Templates

## Normalize and Validate Phone Numbers

You are an Orchard Core expert. Generate code and configuration for the CrestApps Phone Numbers services. This infrastructure module provides parsing, validation, E.164 normalization, region lookup, country-code lookup, and phone-number time-zone lookup.

### Guidelines

- Install `CrestApps.OrchardCore.PhoneNumbers` in the web/startup project. Code that only consumes the contracts references `CrestApps.OrchardCore.PhoneNumbers.Abstractions` (namespace `CrestApps.OrchardCore.PhoneNumbers`).
- Enable `CrestApps.OrchardCore.PhoneNumbers`; it is marked dependency-only and normally activates through a dependent feature.
- Resolve `IPhoneNumberService` rather than parsing numbers with string manipulation.
- Store normalized phone numbers in E.164 format for comparisons, indexes, DNC lists, and external APIs.
- Pass canonical numbers between services as the `PhoneNumber` value type (built with `IPhoneNumberService.TryParse`), not as raw strings. DNC registries require it.
- Show numbers to people with `FormatForDisplay`; store and dial E.164.
- Supply an ISO 3166-1 alpha-2 region when input has no leading `+`.
- Do not infer a country from a local number without an explicit region context.
- Preserve user-facing formatting separately when required; E.164 is a canonical storage and transport value.
- Use `GetTimeZones` only with an E.164 number and allow for zero, one, or multiple IANA time-zone ids.
- This feature is a service module, not a content part or content field by itself.
- Add verification data through `CrestApps.OrchardCore.PhoneNumbers.Verifications`, not this module.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

### Feature Overview

| Item | Value |
|---|---|
| Package | `CrestApps.OrchardCore.PhoneNumbers` |
| Feature | Phone Numbers Services, ID `CrestApps.OrchardCore.PhoneNumbers` (dependency only, category Phone Verification) |
| Contracts package | `CrestApps.OrchardCore.PhoneNumbers.Abstractions` |
| Service | `IPhoneNumberService` (singleton) |
| Implementation | `DefaultPhoneNumberService` (libphonenumber) |
| Canonical value type | `PhoneNumber` (`readonly record struct`, always E.164 or empty) |
| Normalized standard | E.164 |

### Enable Phone Number Services

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.PhoneNumbers"
      ],
      "disable": []
    }
  ]
}
```

The feature has `EnabledByDependencyOnly = true`. Enabling DNC Registry or Phone Number Verifications activates it automatically; explicitly enabling it is useful for a custom module that consumes `IPhoneNumberService`.

## `IPhoneNumberService` Operations

| Method | Use |
|---|---|
| `TryFormatToE164` | Parse input and obtain canonical E.164 output |
| `FormatForDisplay` | Format a number for reading (grouped, without the country code when local to `regionCode`); unparseable input is returned unchanged |
| `IsValidNumber` | Validate input for a given region |
| `GetTimeZones` | Obtain IANA time-zone ids associated with an E.164 number |
| `GetRegionCode` | Determine an ISO alpha-2 region from an E.164 number |
| `GetCountryCode` | Obtain an international calling code from a region |
| `GetSupportedRegions` | Build an allow-list or region picker |

An E.164 number includes `+`, country calling code, and subscriber number, for example `+17024993350`.

## Normalize at the Boundary

Normalize as soon as user input reaches your application. Store the normalized value when parsing succeeds, and show a validation error otherwise.

```csharp
using CrestApps.OrchardCore.PhoneNumbers;

namespace MyCompany.OrchardCore.Contacts;

public sealed class ContactPhoneNormalizer
{
    private readonly IPhoneNumberService _phoneNumberService;

    public ContactPhoneNormalizer(IPhoneNumberService phoneNumberService)
    {
        _phoneNumberService = phoneNumberService;
    }

    public bool TryNormalize(string input, string regionCode, out string e164Number)
    {
        return _phoneNumberService.TryFormatToE164(input, regionCode, out e164Number);
    }
}
```

For a local number such as `702-499-3350`, pass `US`. For a number that already begins with `+`, a region can be omitted when calling the service.

Do not strip non-digits yourself before invoking the service. Formatting characters, prefixes, and regional numbering rules must be interpreted by the phone-number service.

## The `PhoneNumber` Value Type

`PhoneNumber` holds a number that is E.164 by construction, so a consumer never has to guess the format. Build it once, at the boundary:

```csharp
using CrestApps.OrchardCore.PhoneNumbers;

if (_phoneNumberService.TryParse(model.PhoneNumber, model.RegionCode, out var phoneNumber))
{
    // phoneNumber.Value is "+17024993350", phoneNumber.Digits is "17024993350".
    var registered = await registry.GetRegisteredNumbersAsync([phoneNumber], cancellationToken);
}
```

| Member | Use |
|---|---|
| `TryParse(rawNumber, regionCode, out PhoneNumber)` | Extension on `IPhoneNumberService`; parses and normalizes in one step |
| `PhoneNumber.TryFromE164` / `FromE164` | Wrap a value that is already E.164 (`FromE164` throws otherwise) |
| `PhoneNumber.IsE164` | Shape check only (leading `+`, no leading zero, at most 15 digits) |
| `HasValue`, `Value`, `Digits` | Inspect the number; `default(PhoneNumber)` has no value |
| `GetRegionCode(PhoneNumber)` | Extension returning the region, or `null` for an empty number |

`PhoneNumber` serializes to JSON as its E.164 string through `PhoneNumberJsonConverter`.

To compare a stored raw value that may not have parsed, use `PhoneNumberComparisonKey.For(canonical, rawValue)` (the E.164 value when known, otherwise the digits) or `AllFor(...)` for every key a lookup should try.

## Validate Input

Use `IsValidNumber` when only validity is required:

```csharp
if (!_phoneNumberService.IsValidNumber(model.PhoneNumber, model.RegionCode))
{
    ModelState.AddModelError(nameof(model.PhoneNumber), "Enter a valid phone number.");
}
```

Prefer `TryFormatToE164` when the valid result also needs to be stored. It avoids a second parse and gives the comparison key required by DNC and verification workflows.

## Region and Calling-Code Lookups

`GetRegionCode` returns the ISO 3166-1 alpha-2 region for an E.164 number or `null` when unavailable. `GetCountryCode` returns the numeric calling code for a known region and `0` when the region is unknown.

```csharp
var regionCode = _phoneNumberService.GetRegionCode("+17024993350");
var callingCode = _phoneNumberService.GetCountryCode("US");
var regions = _phoneNumberService.GetSupportedRegions();
```

Treat the returned region as phone-number metadata, not an address or residency assertion. Numbers can be ported and countries can share calling codes.

## Phone-Number Time Zones

`GetTimeZones` returns IANA identifiers associated with an E.164 number:

```csharp
var timeZones = _phoneNumberService.GetTimeZones("+17024993350");
```

A result can be empty if it cannot be determined. It can also contain multiple entries where a calling range spans zones. Ask the user to choose when precise scheduling depends on one location. Do not silently treat a phone-number time zone as the account or tenant time zone.

## Persistence Pattern

Store raw input only when it is necessary for auditing or editor display. Use E.164 as the index and unique-comparison value:

```csharp
public sealed class ContactPhoneRecord
{
    public string DisplayNumber { get; set; } = string.Empty;

    public string NormalizedPhoneNumber { get; set; } = string.Empty;

    public string RegionCode { get; set; } = string.Empty;
}
```

Normalize and validate `NormalizedPhoneNumber` before a record is saved. Do not include a local number and its region in a global unique index because the same digits represent different numbers in different regions.

## Integrations

| Feature | How it uses Phone Numbers |
|---|---|
| `CrestApps.OrchardCore.PhoneNumbers.Verifications` | Stores and revalidates provider verification results on content items; see `orchardcore-phone-number-verifications` |
| `CrestApps.OrchardCore.DncRegistry` | Normalizes imports and checks do-not-call records |
| `CrestApps.OrchardCore.TimeZones` | Provides friendly editor labels for IANA zones, not phone-number discovery |
| `CrestApps.OrchardCore.Omnichannel.Managements` | Indexes every contact number in E.164 for caller and sender matching; see `orchardcore-omnichannel-management` |
| `CrestApps.OrchardCore.Telephony` | The soft phone keypad normalizes a valid national number to E.164 before dialing so outbound screening can canonicalize it; see `orchardcore-telephony` |

Phone Numbers itself does not manage external verification providers, send SMS, create a content type, or supply an admin page.

## Handling Failures

| Situation | Recommended action |
|---|---|
| Local number has no region | Require an explicit region picker |
| `TryFormatToE164` returns false | Reject or retain as unverified user input, never compare it as canonical data |
| `GetTimeZones` returns no values | Ask for location or fall back to user-selected preferences |
| Multiple time zones are returned | Ask the user to select one for schedule-sensitive behavior |
| Unknown region passed to `GetCountryCode` | Handle the `0` result and do not construct an E.164 prefix |

## Avoid These Patterns

- Do not use a fixed string length as validation.
- Do not assume `US` for every local number.
- Do not convert an E.164 number to an integer because it loses `+` and can overflow.
- Do not use a phone number’s region as proof of a customer’s country.
- Do not persist an external provider’s formatted display string as the comparison key.
- Do not call external verification APIs when local parsing is all that is requested.
