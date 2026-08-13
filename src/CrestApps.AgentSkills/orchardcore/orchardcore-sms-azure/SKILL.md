---
name: orchardcore-sms-azure
description: Skill for configuring Azure Communication Services SMS in Orchard Core. Covers AzureSmsSettings, AzureSmsOptions, AzureSmsProvider, DefaultAzureSmsProvider, provider selection, configuration-backed defaults, and base SMS sending abstractions. Use this skill when requests mention Orchard Core Azure SMS, Azure Communication Services SMS, AzureSmsSettings, AzureSmsProvider, DefaultAzureSmsOptions, ACS text messaging, or closely related Orchard Core implementation setup extension or troubleshooting work. Strong matches include work with OrchardCore.Sms.Azure, OrchardCore.Sms, OrchardCore.Sms.Azure.Models, OrchardCore.Sms.Azure.Services, ISmsService, ISmsProvider, AddAzureSmsProvider, and AzureSettingsDisplayDriver. It also helps with phone-number configuration, tenant providers, secure connection strings, and the source-backed patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Azure Communication Services SMS

`OrchardCore.Sms.Azure` adds Azure Communication Services as a provider for the
base `OrchardCore.Sms` module. It supplies tenant settings and a
configuration-backed default provider. Use the base `ISmsService` from
application code so another provider can be selected later without rewriting
the sending service.

## Guidelines

- Enable `OrchardCore.Sms` before `OrchardCore.Sms.Azure`.
- Select a configured and enabled SMS provider before attempting delivery.
- Configure an ACS connection string and a phone number capable of sending SMS.
- `AzureSmsProvider` is the tenant-configured provider; `DefaultAzureSmsProvider` reads host configuration.
- Keep ACS connection strings in secure configuration, never in client-side code.
- `AzureSmsOptions.ConfigurationExists()` requires both `PhoneNumber` and `ConnectionString`.
- Use E.164 recipient phone numbers and validate them before sending.
- Test with authorized destination numbers where the Azure account requires it.
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
      ],
      "disable": []
    }
  ]
}
```

The Azure startup calls `AddAzureSmsProvider()`, registers
`AzureSmsOptionsConfiguration`, provider options configuration, and the Azure
settings display driver.

## Configure Per Tenant

Navigate to **Settings → Communication → SMS** and configure
Azure Communication Services. `AzureSmsSettings` contains:

| Property | Description |
|---|---|
| `IsEnabled` | Enables the Azure provider for the tenant. |
| `ConnectionString` | ACS connection string. |
| `PhoneNumber` | ACS sending number in E.164 format. |

After saving, select Azure in the SMS providers UI. The provider options
configuration determines which tenant providers are available; merely enabling
the feature does not select it.

## Configure the Default Azure Provider

`DefaultAzureSmsOptions` binds from
`OrchardCore_Sms_AzureCommunicationServices` and becomes enabled only when a
phone number and connection string are supplied:

```json
{
  "OrchardCore_Sms_AzureCommunicationServices": {
    "PhoneNumber": "+18005551234",
    "ConnectionString": "endpoint=https://your-resource.communication.azure.com/;accesskey=your-key"
  }
}
```

Use this provider when host configuration centrally supplies ACS credentials.
Use tenant settings when each tenant has independent connection data. Do not
place the access key in a deployment recipe or repository configuration file.

## Send With ISmsService

Send through the base service:

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

Check `Result.Succeeded` and handle errors without logging the full message
body or unnecessary personal data.

## Integrate Workflows and Notifications

The base SMS module can add a **Send SMS** workflow task when Workflows is
enabled. Enable `OrchardCore.Notifications.Sms` separately when user
notifications should offer SMS according to each user’s preferences. The Azure
module remains only the delivery provider; it does not itself create a new
workflow activity.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Sms",
        "OrchardCore.Sms.Azure",
        "OrchardCore.Workflows"
      ],
      "disable": []
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

The Azure module contributes providers to the base SMS provider options rather
than replacing `ISmsService`. The regular Azure provider reads tenant
`AzureSmsSettings`; the default provider reads `DefaultAzureSmsOptions` from
host configuration. Select the provider intentionally in the SMS settings UI
after confirming its options are enabled.

For shared hosting, the configuration-backed provider can simplify operations.
For isolated tenants, prefer per-tenant Azure settings and access controls so a
tenant cannot send through another tenant's Communication Services resource.
