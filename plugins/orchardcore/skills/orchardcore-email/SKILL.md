---
name: orchardcore-email
description: Guidance for configuring and sending email in Orchard Core with the OrchardCore.Email module, SMTP or Azure Communication Services providers, IEmailService, MailMessage, Result, Liquid templates, workflow activities, recipes, and provider selection. Use this skill when requests mention OrchardCore.Email Module, Email Providers, Configuring SMTP Settings, Via Admin UI, Via Recipe, Via Configuration Provider, ISmtpService migration, MailKitSmtpService migration, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
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
| `OrchardCore.Email.Smtp` | `SMTP` | `DefaultSMTP` |
| `OrchardCore.Email.Azure` | `Azure` | `DefaultAzure` |

The email settings page is **Settings → Communication → Email**. It lists
enabled providers and selects `EmailSettings.DefaultProviderName`. Enabling the
last valid tenant provider selects it automatically; otherwise explicitly
choose the default. The test page is **Tools → Testing → Email Test** and lets
an administrator select an enabled provider for the test message.

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
        "EncryptionMethod": "STARTTLS",
        "RequireCredentials": true,
        "UserName": "smtp-user",
        "Password": "use-a-secret-provider",
        "DeliveryMethod": "Network"
      },
      "EmailSettings": {
        "DefaultProviderName": "SMTP"
      }
    }
  ]
}
```

`OrchardCore_Email_Smtp` configures the `DefaultSMTP` provider. Use the
settings UI for a tenant-specific `SMTP` provider. Do not commit credentials.

## Enable and Configure Azure Communication Services

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
      "AzureEmailSettings": {
        "IsEnabled": true,
        "DefaultSender": "DoNotReply@your-domain.azurecomm.net",
        "ConnectionString": "supply-from-a-secure-source"
      },
      "EmailSettings": {
        "DefaultProviderName": "Azure"
      }
    }
  ]
}
```

`OrchardCore_Email_AzureCommunicationServices` configures `DefaultAzure`.
Use a verified ACS sender and secure configuration for connection strings.

## Sending Email Programmatically

Inject `IEmailService`; `ISmtpService`, `SmtpResult`, and
`MailKitSmtpService` are not the v3 API. Use `HtmlBody` and/or `TextBody`;
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

    public Task<Result> SendAsync(string recipientEmail, string orderId)
    {
        var message = new MailMessage
        {
            To = recipientEmail,
            Subject = $"Order confirmation {orderId}",
            HtmlBody = $"<p>Your order <strong>{orderId}</strong> has been confirmed.</p>",
            TextBody = $"Your order {orderId} has been confirmed.",
        };

        return _emailService.SendAsync(message);
    }
}
```

Pass a provider technical name only when deliberately overriding the configured
default:

```csharp
var result = await _emailService.SendAsync(message, providerName: "SMTP");

if (!result.Succeeded)
{
    foreach (var error in result.Errors)
    {
        logger.LogError("Email send failed: {Error}", error.Message.Value);
    }
}
```

`MailMessage` supports `From`, `To`, `Cc`, `Bcc`, `ReplyTo`, `Sender`,
`Subject`, `HtmlBody`, `TextBody`, and `Attachments`.

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

With `OrchardCore.Email` and `OrchardCore.Workflows` enabled, use the **Send
Email** workflow activity. Its body is rendered as HTML, so provide HTML there
and keep provider configuration outside the workflow definition.
