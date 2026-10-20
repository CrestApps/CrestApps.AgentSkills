---
name: orchardcore-sms-azure
description: Skill for configuring Azure Communication Services SMS in Orchard Core. Covers AzureSmsSettings, AzureSmsOptions, DefaultAzureSmsOptions, AzureSmsProvider, DefaultAzureSmsProvider, AzureSmsProviderBase, the OrchardCore Sms Azure configuration section, IOptionsMonitor refresh, encrypted connection strings, provider selection, configuration-backed defaults, the From number, and base SMS sending abstractions. Use this skill when requests mention Orchard Core Azure SMS, Azure Communication Services SMS, AzureSmsSettings, AzureSmsProvider, DefaultAzureSmsOptions, ACS text messaging, upgrading Azure SMS configuration to Orchard Core 4, or closely related Orchard Core implementation setup extension or troubleshooting work. Strong matches include work with OrchardCore.Sms.Azure, OrchardCore.Sms, ISmsService, ISmsProvider, AddAzureSmsProvider, and AzureSettingsDisplayDriver.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Azure Communication Services SMS

`OrchardCore.Sms.Azure` adds Azure Communication Services as a provider for the
base `OrchardCore.Sms` module (see `orchardcore-sms`). It supplies tenant
settings and a configuration-backed default provider. Use the base
`ISmsService` from application code so another provider can be selected later
without rewriting the sending service.

## Guidelines

- `OrchardCore.Sms.Azure` depends on `OrchardCore.Sms`.
- Select a configured and enabled SMS provider before attempting delivery.
- Configure an ACS connection string and a phone number capable of sending SMS.
- `AzureSmsProvider` (`Azure`) is the tenant-configured provider;
  `DefaultAzureSmsProvider` (`DefaultAzure`) reads host configuration.
- Keep ACS connection strings in secure configuration, never in client-side code.
- `AzureSmsOptions.ConfigurationExists()` requires both `PhoneNumber` and `ConnectionString`.
- Recipients must be valid phone numbers (E.164); the provider rejects invalid
  formats, empty recipients, and empty bodies with a failed `Result`.
- All recipe JSON uses the root `{ "steps": [...] }` format.

## Enable Azure SMS

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Sms",
        "OrchardCore.Sms.Azure"
      ]
    }
  ]
}
```

The Azure startup calls `AddAzureSmsProvider()`, which registers
`AzureSmsOptionsConfiguration`, a signal-backed change token source for
`AzureSmsOptions`, and the provider options configuration, plus the Azure
settings display driver.

## Configure Per Tenant

Navigate to **Settings → Communication → SMS** and open the **Azure
Communication Services** tab. `AzureSmsSettings` contains:

| Property | Description |
|---|---|
| `IsEnabled` | Enables the Azure provider for the tenant. |
| `ConnectionString` | ACS connection string, stored encrypted with Data Protection. |
| `PhoneNumber` | ACS sending number in E.164 format. |

Enter the connection string in the admin UI; `AzureSmsOptionsConfiguration`
decrypts the stored value, so a plain connection string imported by a recipe
cannot be read. After saving, select `Azure` on the **Providers** tab; enabling
the feature does not select it. Saved changes refresh `AzureSmsOptions` and
`SmsProviderOptions` through `IOptionsMonitor<T>` without a shell release.

## Configure the Default Azure Provider

`DefaultAzureSmsOptions` binds `OrchardCore:Sms:Azure` and becomes enabled only
when a phone number and connection string are supplied:

```json
{
  "OrchardCore": {
    "Sms": {
      "Azure": {
        "PhoneNumber": "+18005551234",
        "ConnectionString": "endpoint=https://your-resource.communication.azure.com/;accesskey=your-key"
      }
    }
  }
}
```

Provide the key through `OrchardCore__Sms__Azure__ConnectionString` or a key
vault rather than a committed file. Use this provider when host configuration
centrally supplies ACS credentials, then select it with a recipe:

```json
{
  "steps": [
    {
      "name": "Settings",
      "SmsSettings": {
        "DefaultProviderName": "DefaultAzure"
      }
    }
  ]
}
```

## Send With ISmsService

```csharp
using OrchardCore.Infrastructure;
using OrchardCore.Sms;

namespace MyModule;

public sealed class AppointmentReminderService
{
    private readonly ISmsService _smsService;

    public AppointmentReminderService(ISmsService smsService)
    {
        _smsService = smsService;
    }

    public Task<Result> SendAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        return _smsService.SendAsync(
            new SmsMessage
            {
                To = phoneNumber,
                Body = "Your appointment is tomorrow.",
            },
            cancellationToken);
    }
}
```

Set `SmsMessage.From` to send from another number owned by the ACS resource;
otherwise the configured `PhoneNumber` is used. Check `Result.Succeeded` and
handle errors without logging the full message body or unnecessary personal
data.

## Integrate Workflows and Notifications

The base SMS module adds the **SMS Task** workflow activity when Workflows is
enabled, including the optional **From Phone Number** field. Enable
`OrchardCore.Notifications.Sms` separately when user notifications should offer
SMS according to each user's preferences. The Azure module remains only the
delivery provider; it does not add a workflow activity.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Sms",
        "OrchardCore.Sms.Azure",
        "OrchardCore.Workflows"
      ]
    }
  ]
}
```

## Diagnose Delivery

Verify the selected provider, tenant settings, ACS resource connection string,
sending phone number, and recipient format. Confirm Azure region and
subscription restrictions before treating an application-level send result as a
code defect.

## Provider Selection Notes

The Azure module contributes providers to `SmsProviderOptions` rather than
replacing `ISmsService`. For shared hosting, the configuration-backed provider
can simplify operations. For isolated tenants, prefer per-tenant Azure settings
and access controls so a tenant cannot send through another tenant's
Communication Services resource. Custom ACS providers can derive from
`AzureSmsProviderBase<TOptions>` (`TOptions : AzureSmsOptions`), whose
constructor takes `IOptionsMonitor<TOptions>`, `IPhoneFormatValidator`, a
logger, and a string localizer.

## Upgrading from 3.x

- `OrchardCore_Sms_AzureCommunicationServices` → `OrchardCore:Sms:Azure`
  (environment variable `OrchardCore__Sms__Azure__ConnectionString`). The old
  name still binds but is deprecated.
- `IOptions<AzureSmsOptions>` → `IOptionsMonitor<AzureSmsOptions>.CurrentValue`.
