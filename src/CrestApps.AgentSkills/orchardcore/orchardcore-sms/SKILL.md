---
name: orchardcore-sms
description: Skill for configuring SMS messaging in Orchard Core. Covers SMS provider setup (Log, Twilio, Azure Communication Services), ISmsService for programmatic sending, custom SMS provider implementation, workflow SMS activities, notification integration, and provider configuration.
---

# Orchard Core SMS - Prompt Templates

## Configure and Send SMS Messages

You are an Orchard Core expert. Generate code and configuration for SMS messaging including provider setup, programmatic sending, custom provider implementation, workflow integration, and SMS notifications.

### Guidelines

- Enable `OrchardCore.Sms` to access SMS settings and provider configuration.
- The built-in providers are `Log` (debugging only) and `Twilio` (production).
- The `OrchardCore.Sms.Azure` feature adds Azure Communication Services as an additional provider.
- After enabling the SMS feature, you must configure the default provider before sending messages.
- Use `ISmsService` to send SMS messages programmatically.
- Use `ISmsProvider` to implement custom SMS providers.
- Register simple providers with `AddSmsProvider<T>("TechnicalName")`.
- Register complex providers with settings using `AddSmsProviderOptionsConfiguration<T>()`.
- When both `SMS` and `Workflows` features are enabled, a "Send SMS" workflow task becomes available.
- Enable `OrchardCore.Notifications.Sms` for user SMS notifications based on preferences.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

### Available SMS Providers

| Provider | Feature | Description |
|----------|---------|-------------|
| `Log` | `OrchardCore.Sms` | Writes SMS messages to application logs. For debugging only — never use in production. |
| `Twilio` | `OrchardCore.Sms` | Sends SMS via the Twilio service. Requires a Twilio account with SID, auth token, and phone number. |
| `Azure` | `OrchardCore.Sms.Azure` | Tenant-specific Azure Communication Services SMS. Configure per tenant via admin settings. |
| `DefaultAzure` | `OrchardCore.Sms.Azure` | Default Azure Communication Services configuration shared across all tenants. Configured via `appsettings.json`. |

### Enabling SMS Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Sms"
      ],
      "disable": []
    }
  ]
}
```

To also enable Azure Communication Services:

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

### Configuring Twilio

Navigate to **Settings → Communication → SMS**, click the **Twilio** tab, enable the checkbox, and provide your Twilio account info. Then in the **Providers** tab, select Twilio as the default provider.

### Configuring Azure Communication Services via Admin UI

Enable the `OrchardCore.Sms.Azure` feature. Navigate to **Settings → Communication → SMS** and click the **Azure Communication Services** tab to configure the connection string and phone number per tenant.

### Configuring Default Azure Communication Services via appsettings.json

The `DefaultAzure` provider is configured through the configuration provider and applies to all tenants. It only appears when the configuration is present:

```json
{
  "OrchardCore_Sms_AzureCommunicationServices": {
    "PhoneNumber": "+18005551234",
    "ConnectionString": "endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key"
  }
}
```

The `DefaultAzure` provider cannot be configured through admin settings — use configuration providers only.

### Sending SMS Programmatically with ISmsService

Inject `ISmsService` and call `SendAsync` to send an SMS message:

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
    public async Task<IActionResult> SendOrderConfirmation(string phoneNumber, string orderId)
    {
        var message = new SmsMessage
        {
            To = phoneNumber,
            Body = $"Your order {orderId} has been confirmed and is being processed.",
        };

        var result = await _smsService.SendAsync(message);

        if (result.Succeeded)
        {
            return Ok(new { success = true, message = "SMS sent successfully." });
        }

        return BadRequest(new { success = false, errors = result.Errors });
    }
}
```

### Creating a Custom SMS Provider (Simple)

For providers that do not require settings, implement `ISmsProvider` and register with `AddSmsProvider<T>`:

```csharp
using OrchardCore.Sms;

public sealed class WebhookSmsProvider : ISmsProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public WebhookSmsProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<SmsResult> SendAsync(SmsMessage message)
    {
        var client = _httpClientFactory.CreateClient("SmsWebhook");

        var response = await client.PostAsJsonAsync("/api/sms/send", new
        {
            to = message.To,
            body = message.Body,
        });

        if (response.IsSuccessStatusCode)
        {
            return SmsResult.Success;
        }

        return SmsResult.Failed("Failed to send SMS via webhook.");
    }
}
```

Register in `Startup.cs`:

```csharp
using OrchardCore.Sms;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSmsProvider<WebhookSmsProvider>("Webhook");
    }
}
```

### Creating a Custom SMS Provider (Complex, with Settings)

For providers with configurable settings, implement `IConfigureOptions<SmsProviderOptions>`:

```csharp
using Microsoft.Extensions.Options;
using OrchardCore.Settings;
using OrchardCore.Sms;

public sealed class CustomProviderOptionsConfiguration : IConfigureOptions<SmsProviderOptions>
{
    private readonly ISiteService _siteService;

    public CustomProviderOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(SmsProviderOptions options)
    {
        var typeOptions = new SmsProviderTypeOptions(typeof(CustomSmsProvider));

        var site = _siteService.GetSiteSettingsAsync().GetAwaiter().GetResult();
        var settings = site.As<CustomSmsSettings>();

        typeOptions.IsEnabled = settings.IsEnabled;

        options.TryAddProvider("CustomProvider", typeOptions);
    }
}
```

Register in `Startup.cs`:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSmsProviderOptionsConfiguration<CustomProviderOptionsConfiguration>();
    }
}
```

### Workflow SMS Activity

When both `OrchardCore.Sms` and `OrchardCore.Workflows` features are enabled, a **Send SMS** workflow task becomes available. This task allows sending SMS messages as part of a workflow without writing code.

Enable both features:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Sms",
        "OrchardCore.Workflows"
      ],
      "disable": []
    }
  ]
}
```

### SMS Notifications

Enable the `OrchardCore.Notifications.Sms` feature to allow sending user notifications via SMS based on user preferences. This integrates with the Orchard Core notification system.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Sms",
        "OrchardCore.Notifications",
        "OrchardCore.Notifications.Sms"
      ],
      "disable": []
    }
  ]
}
```
