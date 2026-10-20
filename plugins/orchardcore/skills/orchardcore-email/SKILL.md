---
name: orchardcore-email
description: Guidance for configuring and sending email in Orchard Core with the OrchardCore.Email module, SMTP or Azure Communication Services providers, IEmailService, MailMessage, Result, Liquid templates, the Email Task workflow activity, recipes, provider selection, the OrchardCore Email configuration section, IOptionsMonitor EmailOptions and EmailProviderOptions, custom IEmailProvider implementations, and EmailPermissions. Use this skill when requests mention OrchardCore.Email Module, Email Providers, Configuring SMTP Settings, Via Admin UI, Via Recipe, Via Configuration Provider, OrchardCore Email Smtp or Email Azure configuration, custom email provider, AddEmailProvider, AddEmailProviderOptionsConfiguration, IOptionsUpdateNotifier, ISmtpService migration, MailKitSmtpService migration, upgrading email code to Orchard Core 4, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# OrchardCore.Email Module

`OrchardCore.Email` supplies the provider-neutral email API. Enable an email
provider and configure one enabled provider as the default before application
code sends mail.

| Provider module | Tenant provider technical name | Configuration-backed default |
|---|---|---|
| `OrchardCore.Email.Smtp` | `SMTP` | `DefaultSMTP` (section `OrchardCore:Email:Smtp`) |
| `OrchardCore.Email.Azure` | `Azure` | `DefaultAzure` (section `OrchardCore:Email:Azure`) |

The email settings page is **Settings → Communication → Email**. It lists
enabled providers and selects `EmailSettings.DefaultProviderName`. Enabling the
last valid tenant provider selects it automatically; otherwise explicitly
choose the default. The test page is **Tools → Testing → Email Test** and lets
an administrator select an enabled provider for the test message. Access is
controlled by `OrchardCore.Email.EmailPermissions.ManageEmailSettings`.

Settings changes take effect without a tenant reload: `EmailOptions`,
`EmailProviderOptions`, `SmtpOptions`, and `AzureEmailOptions` are refreshed
through `IOptionsMonitor<T>` after the settings are committed.

## Enable and Configure SMTP

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Email",
        "OrchardCore.Email.Smtp"
      ]
    },
    {
      "name": "Settings",
      "SmtpSettings": {
        "IsEnabled": true,
        "DefaultSender": "noreply@example.com",
        "Host": "smtp.example.com",
        "Port": 587,
        "AutoSelectEncryption": false,
        "EncryptionMethod": "StartTls",
        "RequireCredentials": true,
        "UserName": "smtp-user",
        "DeliveryMethod": "Network"
      },
      "EmailSettings": {
        "DefaultProviderName": "SMTP"
      }
    }
  ]
}
```

`EncryptionMethod` values are `None`, `SslTls`, and `StartTls`. The admin UI
encrypts the SMTP password with Data Protection; enter it there, or use the
configuration-backed `DefaultSMTP` provider for secrets. See
`orchardcore-email-smtp` for pickup directories and all SMTP settings.

## Configuration-backed Providers

```json
{
  "OrchardCore": {
    "Email": {
      "Smtp": {
        "DefaultSender": "site@example.com",
        "Host": "smtp.example.com",
        "Port": 587,
        "EncryptionMethod": "StartTls",
        "RequireCredentials": true,
        "UserName": "smtp-user",
        "Password": "supply-from-a-secret-store"
      },
      "Azure": {
        "DefaultSender": "DoNotReply@your-domain.azurecomm.net",
        "ConnectionString": "supply-from-a-secret-store"
      }
    }
  }
}
```

Environment variables use the same path, for example
`OrchardCore__Email__Smtp__Password` or
`OrchardCore__Email__Azure__ConnectionString`. A `DefaultSMTP` or
`DefaultAzure` provider only appears when its section has valid values, and it
cannot be edited in the admin UI.

## Enable Azure Communication Services

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Email",
        "OrchardCore.Email.Azure"
      ]
    },
    {
      "name": "Settings",
      "EmailSettings": {
        "DefaultProviderName": "DefaultAzure"
      }
    }
  ]
}
```

Use a verified ACS sender. The tenant `Azure` provider stores its connection
string encrypted, so set it in the admin UI; see `orchardcore-email-azure`.

## Sending Email Programmatically

Inject `IEmailService`; `ISmtpService`, `SmtpResult`, and
`MailKitSmtpService` are not the current API. Use `HtmlBody` and/or `TextBody`;
`Body` and `IsHtmlBody` are obsolete.

```csharp
using OrchardCore.Email;
using OrchardCore.Infrastructure;

namespace MyModule;

public sealed class OrderConfirmationService
{
    private readonly IEmailService _emailService;

    public OrderConfirmationService(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public Task<Result> SendAsync(
        string recipientEmail,
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var message = new MailMessage
        {
            To = recipientEmail,
            Subject = $"Order confirmation {orderId}",
            HtmlBody = $"<p>Your order <strong>{orderId}</strong> has been confirmed.</p>",
            TextBody = $"Your order {orderId} has been confirmed.",
        };

        return _emailService.SendAsync(message, cancellationToken: cancellationToken);
    }
}
```

Pass a provider technical name only when deliberately overriding the configured
default:

```csharp
var result = await _emailService.SendAsync(
    message,
    providerName: "SMTP",
    cancellationToken: cancellationToken);

if (!result.Succeeded)
{
    foreach (var error in result.Errors)
    {
        logger.LogError("Email send failed: {Error}", error.Message.Value);
    }
}
```

`MailMessage` supports `From`, `To`, `Cc`, `Bcc`, `ReplyTo`, `Sender`,
`Subject`, `HtmlBody`, `TextBody`, and `Attachments`. The
`SendAsync(to, subject, htmlBody, textBody)` extension builds a message for
simple cases.

`IEmailService.SendAsync()` and `IEmailProvider.SendAsync()` accept a
`CancellationToken`. Pass the request token through custom providers and
event handlers so shutdown and request cancellation can stop delivery.

## Reading Email Options

Options backed by site settings change at runtime. Inject
`IOptionsMonitor<T>` and read `CurrentValue` on each use; `IOptions<T>` keeps
the value from when the scope was created.

```csharp
using Microsoft.Extensions.Options;
using OrchardCore.Email;

namespace MyModule;

public sealed class EmailProviderInfo
{
    private readonly IOptionsMonitor<EmailOptions> _emailOptions;

    public EmailProviderInfo(IOptionsMonitor<EmailOptions> emailOptions)
    {
        _emailOptions = emailOptions;
    }

    public string DefaultProviderName
        => _emailOptions.CurrentValue.DefaultProviderName;
}
```

## Custom Email Providers

Implement `IEmailProvider` (`DisplayName` plus `SendAsync`). Register a provider
without settings with `services.AddEmailProvider<T>("TechnicalName")`. For a
provider whose availability depends on settings, implement
`IConfigureOptions<EmailProviderOptions>` and register it with
`services.AddEmailProviderOptionsConfiguration<T>()`. Read provider settings
through `IOptionsMonitor<TOptions>`, register
`services.AddSignalOptionsChangeTokenSource<TOptions>()`, and request a refresh
from the settings display driver after a successful save:

```csharp
_optionsUpdateNotifier
    .RequestUpdate<MyEmailOptions>()
    .RequestUpdate<EmailProviderOptions>()
    .RequestUpdate<EmailOptions>();
```

`IOptionsUpdateNotifier` lives in `OrchardCore.Environment.Options`; the
monitors refresh after the YesSql session commits. To extend the built-in
providers, derive from `SmtpEmailProviderBase<TOptions>` or
`AzureEmailProviderBase<TOptions>`. A complete provider example is in
[email-examples.md](references/email-examples.md).

## Liquid Templates

Render a Liquid template before assigning it to `HtmlBody`:

```csharp
var html = await _liquidTemplateManager.RenderStringAsync(
    "<p>Welcome, {{ UserName }}!</p>",
    System.Text.Encodings.Web.HtmlEncoder.Default,
    new { UserName = userName });

await _emailService.SendAsync(new MailMessage
{
    To = recipientEmail,
    Subject = "Welcome",
    HtmlBody = html,
    TextBody = $"Welcome, {userName}!",
});
```

## Email Events

Implement `IEmailServiceEvents`, or derive from `EmailServiceEventsBase`, to
observe validation and delivery. Event methods receive a cancellation token;
`ValidatedAsync` and `ValidatingAsync` also receive
`MailMessageValidationContext`.

```csharp
using OrchardCore.Email;
using OrchardCore.Email.Services;

namespace MyModule;

public sealed class EmailAuditHandler : EmailServiceEventsBase
{
    private readonly ILogger<EmailAuditHandler> _logger;

    public EmailAuditHandler(ILogger<EmailAuditHandler> logger)
    {
        _logger = logger;
    }

    public override Task SendingAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending email with subject {Subject}.", message.Subject);
        return Task.CompletedTask;
    }

    public override Task FailedAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Email delivery failed for subject {Subject}.", message.Subject);
        return Task.CompletedTask;
    }
}
```

Register the handler as `IEmailServiceEvents`.

```csharp
services.AddScoped<IEmailServiceEvents, EmailAuditHandler>();
```

## Workflows

With `OrchardCore.Email` and `OrchardCore.Workflows` enabled, use the **Email
Task** activity (category **Messaging**, outcomes `Done` and `Failed`). It
supports Liquid in `Author`, `Sender`, `ReplyTo`, `Recipients`, `Cc`, `Bcc`,
`Subject`, `TextBody`, and `HtmlBody`, and its `BodyFormat` is `All`, `Text`,
or `Html`. Keep provider configuration outside the workflow definition.

## Upgrading from 3.x

- Configuration sections: `OrchardCore_Email_Smtp` and `OrchardCore_Email` →
  `OrchardCore:Email:Smtp`; `OrchardCore_Email_Azure` and
  `OrchardCore_Email_AzureCommunicationServices` → `OrchardCore:Email:Azure`.
  The old names still bind but are deprecated.
- `IOptions<EmailOptions>`, `IOptions<EmailProviderOptions>`,
  `IOptions<SmtpOptions>`, `IOptions<AzureEmailOptions>` →
  `IOptionsMonitor<T>.CurrentValue`.
- `SmtpEmailProviderBase` → `SmtpEmailProviderBase<TOptions>` taking
  `IOptionsMonitor<TOptions>`; `AzureEmailProviderBase` →
  `AzureEmailProviderBase<TOptions>` taking `IOptionsMonitor<TOptions>`
  instead of `Func<AzureEmailOptions>`.
- `OrchardCore.Email.Permissions.ManageEmailSettings` was removed; use
  `OrchardCore.Email.EmailPermissions.ManageEmailSettings`.
