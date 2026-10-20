---
name: orchardcore-sms
description: Skill for configuring SMS messaging in Orchard Core. Covers SMS provider setup (Log, Twilio, Azure Communication Services), SmsSettings and TwilioSettings recipes, ISmsService and SmsMessage for programmatic sending including the From number, custom ISmsProvider implementation, SmsProviderOptions and TwilioOptions read through IOptionsMonitor, the SMS Task workflow activity with its From Phone Number option, SMS notifications, the Twilio health check, and SmsPermissions. Use this skill when requests mention Orchard Core SMS, Configure and Send SMS Messages, Available SMS Providers, Enabling SMS Features, Configuring Twilio, Configuring Azure Communication Services via Admin UI, custom SMS provider, AddSmsProvider, AddSmsProviderOptionsConfiguration, upgrading SMS code to Orchard Core 4, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include OrchardCore.Sms, OrchardCore.Sms.Azure, and OrchardCore.Notifications.Sms.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core SMS

## Guidelines

- Enable `OrchardCore.Sms` to get **Settings → Communication → SMS**, guarded by
  `OrchardCore.Sms.SmsPermissions.ManageSmsSettings`.
- Configure and select a default provider before sending; until then
  `ISmsService` returns a failed `Result`.
- Use `ISmsService` to send; implement `ISmsProvider` for new providers.
- `SmsProviderOptions`, `TwilioOptions`, and `AzureSmsOptions` are refreshed from
  site settings without a shell release; read them with
  `IOptionsMonitor<T>.CurrentValue`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

## Available SMS Providers

| Provider | Feature | Description |
|----------|---------|-------------|
| `Log` | `OrchardCore.Sms` | Writes messages to the log. Registered only when the host environment is `Development`. |
| `Twilio` | `OrchardCore.Sms` | Sends through Twilio. Requires an account SID, auth token, and phone number. |
| `Azure` | `OrchardCore.Sms.Azure` | Tenant-specific Azure Communication Services settings. |
| `DefaultAzure` | `OrchardCore.Sms.Azure` | Azure Communication Services from `OrchardCore:Sms:Azure` configuration. |

See `orchardcore-sms-azure` for the Azure providers.

## Enable and Configure Twilio

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Sms"
      ]
    },
    {
      "name": "Settings",
      "SmsSettings": {
        "DefaultProviderName": "Twilio"
      },
      "TwilioSettings": {
        "IsEnabled": true,
        "PhoneNumber": "+15555550123",
        "AccountSID": "your-account-sid"
      }
    }
  ]
}
```

The admin UI (**Twilio** tab) encrypts `AuthToken` with Data Protection, so
enter the token there instead of in a recipe. Then pick the default on the
**Providers** tab.

## Sending SMS Programmatically

```csharp
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Sms;

public sealed class NotificationController : Controller
{
    private readonly ISmsService _smsService;

    public NotificationController(ISmsService smsService)
    {
        _smsService = smsService;
    }

    [HttpPost]
    public async Task<IActionResult> SendOrderConfirmation(
        string phoneNumber,
        string orderId,
        CancellationToken cancellationToken)
    {
        var message = new SmsMessage
        {
            To = phoneNumber,
            Body = $"Your order {orderId} has been confirmed and is being processed.",
        };

        var result = await _smsService.SendAsync(message, cancellationToken);

        if (result.Succeeded)
        {
            return Ok(new { success = true });
        }

        return BadRequest(new { success = false, errors = result.Errors });
    }
}
```

`SmsMessage` has `To`, `Body`, and an optional `From`; when `From` is empty the
provider's configured phone number is used. The
`SendAsync(to, body, cancellationToken)` extension builds the message for
simple cases. Use E.164 numbers such as `+15555550123`.

## Creating a Custom SMS Provider (Simple)

`ISmsProvider` requires a `Name` and `SendAsync`. Register providers that need
no settings with `AddSmsProvider<T>("TechnicalName")`:

```csharp
using System.Net.Http.Json;
using Microsoft.Extensions.Localization;
using OrchardCore.Infrastructure;
using OrchardCore.Sms;

public sealed class WebhookSmsProvider : ISmsProvider
{
    public const string TechnicalName = "Webhook";

    private readonly IHttpClientFactory _httpClientFactory;

    internal readonly IStringLocalizer S;

    public WebhookSmsProvider(
        IHttpClientFactory httpClientFactory,
        IStringLocalizer<WebhookSmsProvider> stringLocalizer)
    {
        _httpClientFactory = httpClientFactory;
        S = stringLocalizer;
    }

    public LocalizedString Name => S["Webhook"];

    public async Task<Result> SendAsync(
        SmsMessage message,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(TechnicalName);

        var response = await client.PostAsJsonAsync(
            "/api/sms/send",
            new { from = message.From, to = message.To, body = message.Body },
            cancellationToken);

        return response.IsSuccessStatusCode
            ? Result.Success()
            : Result.Failed(S["Failed to send SMS via webhook."]);
    }
}
```

```csharp
using OrchardCore.Sms;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(WebhookSmsProvider.TechnicalName, client =>
            client.BaseAddress = new Uri("https://sms.example.com"));

        services.AddSmsProvider<WebhookSmsProvider>(WebhookSmsProvider.TechnicalName);
    }
}
```

## Creating a Custom SMS Provider (Settings-backed)

When availability depends on site settings, build options from the settings,
read them through `IOptionsMonitor<T>`, and contribute the provider with
`IConfigureOptions<SmsProviderOptions>`. This mirrors the built-in Twilio
provider.

```csharp
using Microsoft.Extensions.Options;
using OrchardCore.Settings;
using OrchardCore.Sms;

public sealed class CustomSmsSettings
{
    public bool IsEnabled { get; set; }
}

public sealed class CustomSmsOptions
{
    public bool IsEnabled { get; set; }
}

public sealed class CustomSmsOptionsConfiguration : IConfigureOptions<CustomSmsOptions>
{
    private readonly ISiteService _siteService;

    public CustomSmsOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(CustomSmsOptions options)
        => options.IsEnabled = _siteService.GetSettings<CustomSmsSettings>().IsEnabled;
}

public sealed class CustomProviderOptionsConfiguration : IConfigureOptions<SmsProviderOptions>
{
    private readonly IOptionsMonitor<CustomSmsOptions> _customOptions;

    public CustomProviderOptionsConfiguration(IOptionsMonitor<CustomSmsOptions> customOptions)
    {
        _customOptions = customOptions;
    }

    public void Configure(SmsProviderOptions options)
    {
        options.TryAddProvider("CustomProvider", new SmsProviderTypeOptions(typeof(CustomSmsProvider))
        {
            IsEnabled = _customOptions.CurrentValue.IsEnabled,
        });
    }
}

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddSignalOptionsChangeTokenSource<CustomSmsOptions>()
            .AddTransient<IConfigureOptions<CustomSmsOptions>, CustomSmsOptionsConfiguration>()
            .AddSmsProviderOptionsConfiguration<CustomProviderOptionsConfiguration>();
    }
}
```

`CustomSmsProvider` injects `IOptionsMonitor<CustomSmsOptions>`. In the
settings display driver, after a successful save, inject
`IOptionsUpdateNotifier` (`OrchardCore.Environment.Options`) and call
`RequestUpdate<CustomSmsOptions>().RequestUpdate<SmsProviderOptions>()` so the
monitors refresh after the session commits.

## Workflow SMS Activity

With `OrchardCore.Sms` and `OrchardCore.Workflows` enabled, the **SMS Task**
activity (category **Messaging**, outcomes `Done` and `Failed`) sends a message.
Its fields support Liquid: **Phone Number** (recipient), the optional **From
Phone Number** (with country code, overriding the provider's number), and
**Body**. The persisted activity properties are `PhoneNumber`, `FromNumber`, and
`Body`; the send result is stored in `LastResult`.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Sms",
        "OrchardCore.Workflows"
      ]
    }
  ]
}
```

## SMS Notifications

Enable `OrchardCore.Notifications.Sms` to deliver user notifications by SMS
according to user preferences (see `orchardcore-notifications`).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Sms",
        "OrchardCore.Notifications",
        "OrchardCore.Notifications.Sms"
      ]
    }
  ]
}
```

## Health Checks

With `OrchardCore.HealthChecks` enabled, the SMS module adds a health check that
reports the status of the Twilio SMS service (see `orchardcore-health-checks`).

## Upgrading from 3.x

- `IOptions<SmsProviderOptions>`, `IOptions<TwilioOptions>`, and
  `IOptions<AzureSmsOptions>` → `IOptionsMonitor<T>.CurrentValue`; settings
  changes no longer release the shell.
- `OrchardCore.Sms.SmsPermissionProvider.ManageSmsSettings` was removed; use
  `OrchardCore.Sms.SmsPermissions.ManageSmsSettings`.
- `OrchardCore_Sms_AzureCommunicationServices` → `OrchardCore:Sms:Azure`.
- The SMS Task gained the optional **From Phone Number** (`FromNumber`) field
  and now honors workflow cancellation.
