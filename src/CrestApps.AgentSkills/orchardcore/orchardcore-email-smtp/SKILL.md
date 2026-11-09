---
name: orchardcore-email-smtp
description: Skill for configuring SMTP email delivery in Orchard Core. Covers SmtpSettings, keeping the password in the Secrets module with PasswordSecretName, SmtpOptions, DefaultSmtpOptions, SmtpEmailProvider, DefaultSmtpEmailProvider, SmtpEmailProviderBase, the OrchardCore Email Smtp configuration section, pickup directories and PickupDirectoryLocationBase, encryption methods, proxies, provider selection, recipes, IOptionsMonitor refresh, IEmailService, and legacy ISmtpService migration. Use this skill when requests mention Orchard Core SMTP Email, SmtpSettings, SmtpEmailProvider, SMTP configuration, MailKit email provider, pickup directory, DefaultSmtpOptions, DefaultSMTP, custom SMTP provider, upgrading SMTP code to Orchard Core 4, or closely related Orchard Core implementation setup extension or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core SMTP Email Provider

`OrchardCore.Email.Smtp` supplies the tenant `SMTP` provider and the
configuration-backed `DefaultSMTP` provider, both built on MailKit.
Application code uses `IEmailService` (see `orchardcore-email`), not an
SMTP-specific service.

## Tenant Provider (`SMTP`)

Enable the module and configure the **SMTP** tab of **Settings → Communication
→ Email**. Select the enabled SMTP provider as the email default when more than
one provider is enabled; enabling the only provider selects it automatically.

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
        "UseDefaultCredentials": false,
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

| Setting | Notes |
|---|---|
| `IsEnabled`, `DefaultSender` | The sender is used when `MailMessage.From`/`Sender` are empty. |
| `Host`, `Port` | `Port` defaults to `25`. `Host` is required for `Network` delivery. |
| `EncryptionMethod`, `AutoSelectEncryption` | `None`, `SslTls`, or `StartTls`. |
| `RequireCredentials`, `UseDefaultCredentials`, `UserName`, `Password` | The admin UI encrypts `Password` with Data Protection, so enter it there rather than in a recipe. |
| `PasswordSecretName` | 4.0. Name of a `TextSecret` holding the password; takes precedence over `Password`. |
| `ProxyHost`, `ProxyPort` | Required when the SMTP server is reached through a proxy. |
| `IgnoreInvalidSslCertificate` | Development and test servers only. |
| `DeliveryMethod`, `PickupDirectoryLocation` | `Network` or `SpecifiedPickupDirectory`. |

## Configuration-backed Provider (`DefaultSMTP`)

`DefaultSmtpOptions` binds `OrchardCore:Email:Smtp`. The provider appears only
when the section has a `DefaultSender` and either a `Host` or pickup delivery;
it cannot be edited in the admin UI.

```json
{
  "OrchardCore": {
    "Email": {
      "Smtp": {
        "DefaultSender": "site@example.com",
        "DeliveryMethod": "Network",
        "Host": "smtp.example.com",
        "Port": 587,
        "EncryptionMethod": "StartTls",
        "AutoSelectEncryption": false,
        "UseDefaultCredentials": false,
        "RequireCredentials": true,
        "UserName": "smtp-user",
        "Password": "supply-from-a-secret-store"
      }
    }
  }
}
```

Supply secrets through environment variables or a key vault, for example
`OrchardCore__Email__Smtp__Password`. In 4.0 a nonblank `Password` here logs a
`LegacySecretConfiguration` warning (event `8100`). `DefaultSmtpOptions` has no
secret reference, so to keep the password in the Secrets module use the tenant
`SMTP` provider with `PasswordSecretName` instead.

## Keeping the Password in a Secret (4.0)

The SMTP settings render the password with the `<secret-input>` editor. Without
the `OrchardCore.Secrets` feature it is protected in `SmtpSettings.Password`, as
before. With it, choose **Use a secret** and pick or create a `TextSecret`
(suggested name `Smtp.Password`); `PasswordSecretName` then wins and the
protected password is cleared when saved. An existing password can be moved
without retyping it from **Tools → Security → Migrate Secrets**. In a recipe,
create the secret with the `Secrets` step (value from the
`OrchardCore__Secrets__Smtp.Password` environment variable) and set
`"PasswordSecretName": "Smtp.Password"` in `SmtpSettings`. `SmtpOptions.Password`
is resolved from the secret first, then from the protected value. See
[orchardcore-secrets](../orchardcore-secrets/SKILL.md).

## Pickup Directory Delivery

Set `DeliveryMethod` to `SpecifiedPickupDirectory` to write `.eml` files
instead of sending. `PickupDirectoryLocation` must be `/` or a relative
subfolder such as `/Outbound`; absolute paths, invalid characters, and `..`
segments are rejected. It is resolved under `PickupDirectoryLocationBase`, a
configuration-only setting that supports Fluid and defaults to
`{{ AppData }}\Sites\{{ ShellSettings.Name }}\Emails`. The base applies to both
the tenant and the default provider.

```json
{
  "OrchardCore": {
    "Email": {
      "Smtp": {
        "DefaultSender": "site@example.com",
        "DeliveryMethod": "SpecifiedPickupDirectory",
        "PickupDirectoryLocationBase": "{{ AppData }}\\Drops\\{{ ShellSettings.Name }}",
        "PickupDirectoryLocation": "/Outbound"
      }
    }
  }
}
```

## Sending

```csharp
using OrchardCore.Email;
using OrchardCore.Infrastructure;

namespace MyModule;

public sealed class ReceiptService
{
    private readonly IEmailService _emailService;

    public ReceiptService(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public Task<Result> SendAsync(string recipient, CancellationToken cancellationToken = default)
    {
        return _emailService.SendAsync(new MailMessage
        {
            To = recipient,
            Subject = "Receipt",
            HtmlBody = "<p>Your receipt is ready.</p>",
            TextBody = "Your receipt is ready.",
        }, cancellationToken: cancellationToken);
    }
}
```

Test a selected enabled provider at **Tools → Testing → Email Test**.

## Options and Custom SMTP Providers

`SmtpOptions` is rebuilt from site settings after each committed change, so
read it with `IOptionsMonitor<SmtpOptions>.CurrentValue`. To add another
SMTP-based provider, derive from `SmtpEmailProviderBase<TOptions>` where
`TOptions : SmtpOptions`:

```csharp
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Email;
using OrchardCore.Email.Smtp.Services;

namespace MyModule;

public sealed class MarketingSmtpOptions : SmtpOptions;

public sealed class MarketingSmtpEmailProvider : SmtpEmailProviderBase<MarketingSmtpOptions>
{
    public const string TechnicalName = "MarketingSMTP";

    public MarketingSmtpEmailProvider(
        IOptionsMonitor<MarketingSmtpOptions> options,
        IEmailAddressValidator emailAddressValidator,
        ILogger<MarketingSmtpEmailProvider> logger,
        IStringLocalizer<MarketingSmtpEmailProvider> stringLocalizer)
        : base(options, emailAddressValidator, logger, stringLocalizer)
    {
    }

    public override LocalizedString DisplayName => S["Marketing SMTP"];
}
```

Configure `MarketingSmtpOptions` (for example by binding a configuration
section and setting `IsEnabled = options.ConfigurationExists()`), then add the
provider with `services.AddEmailProvider<MarketingSmtpEmailProvider>(MarketingSmtpEmailProvider.TechnicalName)`
or an `IConfigureOptions<EmailProviderOptions>` registered with
`AddEmailProviderOptionsConfiguration<T>()`. Reference the
`OrchardCore.Email.Smtp` package from the module that contains the provider.

## Upgrading from 3.x

- `OrchardCore_Email_Smtp` and `OrchardCore_Email` sections →
  `OrchardCore:Email:Smtp` (old names still bind but are deprecated).
- `IOptions<SmtpOptions>` → `IOptionsMonitor<SmtpOptions>.CurrentValue`.
- `class MyProvider : SmtpEmailProviderBase` with an `SmtpOptions` constructor
  argument → `SmtpEmailProviderBase<TOptions>` with
  `IOptionsMonitor<TOptions>`.
- `ISmtpService`, `SmtpResult`, `MailKitSmtpService`, `Body`, and `IsHtmlBody`
  are obsolete; use `IEmailService`, `Result`, `HtmlBody`, and `TextBody`.
- Saved passwords keep working. `SmtpSettings` gains `PasswordSecretName`;
  code reading `SmtpSettings.Password` directly must honor it (read
  `IOptionsMonitor<SmtpOptions>` or call `IServiceProvider.GetSecretValueAsync()`),
  because moving the password to a secret clears `Password`.
- `OrchardCore:Email:Smtp:Password` (and the legacy sections) still works but
  logs `LegacySecretConfiguration`; remove it once the password lives in a secret.
