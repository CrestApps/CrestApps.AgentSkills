---
name: orchardcore-email-smtp
description: Skill for configuring SMTP email delivery in Orchard Core. Covers SmtpSettings, SmtpOptions, SmtpEmailProvider, DefaultSmtpEmailProvider, MailKit delivery, pickup directories, configuration overrides, recipes, and base Email module abstractions. Use this skill when requests mention Orchard Core SMTP Email, SmtpSettings, SmtpEmailProvider, SMTP configuration, MailKit email provider, pickup directory, DefaultSmtpOptions, or closely related Orchard Core implementation setup extension or troubleshooting work. Strong matches include work with OrchardCore.Email.Smtp, OrchardCore.Email, OrchardCore.Email.Smtp.Services, ISmtpService, IEmailProvider, SmtpEncryptionMethod, SmtpDeliveryMethod, and AddSmtpEmailProvider. It also helps with provider selection, admin settings, secure configuration, and the source-backed patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core SMTP Email Provider

`OrchardCore.Email.Smtp` supplies SMTP delivery on top of the base
`OrchardCore.Email` abstractions. It registers SMTP provider options and the
settings display driver; the base Email feature owns message abstractions,
provider selection, templates, and programmatic sending APIs.

## Guidelines

- Enable `OrchardCore.Email` and `OrchardCore.Email.Smtp`.
- Configure and select an enabled email provider before sending application email.
- `SmtpEmailProvider` has technical name `SMTP`; the configuration-backed provider is `DefaultSmtpEmailProvider`.
- Keep SMTP passwords in a secret store or environment configuration.
- Set `DefaultSender` to a domain the SMTP service permits.
- Use `STARTTLS` or SSL/TLS whenever the relay supports it.
- Enable invalid certificate acceptance only for controlled non-production diagnostics.
- Use `SpecifiedPickupDirectory` only when mail files rather than network delivery are intended.
- All recipe JSON uses the root `{ "steps": [...] }` format.

## Enable SMTP

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Email",
        "OrchardCore.Email.Smtp"
      ],
      "disable": []
    }
  ]
}
```

## Configure Through Settings or a Recipe

Open **Configuration → Settings → Email** and configure SMTP. The main
`SmtpSettings` fields are `Host`, `Port`, `AutoSelectEncryption`,
`RequireCredentials`, `UseDefaultCredentials`, `EncryptionMethod`, `UserName`,
`Password`, proxy fields, `DeliveryMethod`, and `PickupDirectoryLocation`.

```json
{
  "steps": [
    {
      "name": "Settings",
      "SmtpSettings": {
        "DefaultSender": "noreply@example.com",
        "Host": "smtp.example.com",
        "Port": 587,
        "AutoSelectEncryption": false,
        "EncryptionMethod": "STARTTLS",
        "RequireCredentials": true,
        "UserName": "smtp-user",
        "Password": "smtp-password",
        "DeliveryMethod": "Network"
      }
    }
  ]
}
```

The SMTP options are usable only when `DefaultSender` exists and either a host
is set for `Network` delivery or the delivery method is
`SpecifiedPickupDirectory`.

## Configuration-Backed Default Provider

`DefaultSmtpOptions` binds legacy `OrchardCore_Email` first and then
`OrchardCore_Email_Smtp`. The latter is the preferred current configuration
section:

```json
{
  "OrchardCore_Email_Smtp": {
    "DefaultSender": "noreply@example.com",
    "Host": "smtp.example.com",
    "Port": 587,
    "AutoSelectEncryption": false,
    "EncryptionMethod": "STARTTLS",
    "RequireCredentials": true,
    "UserName": "smtp-user",
    "Password": "smtp-password",
    "DeliveryMethod": "Network"
  }
}
```

## Pickup Directory Delivery

For local testing or a downstream pickup process, use a controlled base path
and a relative target path:

```json
{
  "OrchardCore_Email_Smtp": {
    "DefaultSender": "noreply@example.com",
    "DeliveryMethod": "SpecifiedPickupDirectory",
    "PickupDirectoryLocationBase": "{{ AppData }}\\Sites\\{{ ShellSettings.Name }}\\Emails",
    "PickupDirectoryLocation": "/Outbound"
  }
}
```

Do not use a traversal path or an arbitrary absolute `PickupDirectoryLocation`.
The base location and relative destination are intentionally separated.

## Send Through the Base Email API

Use the Email module API instead of binding application code to the SMTP
implementation. This allows the active provider to change:

```csharp
using OrchardCore.Email;

namespace MyModule;

public sealed class ReceiptService
{
    private readonly ISmtpService _smtpService;

    public ReceiptService(ISmtpService smtpService)
    {
        _smtpService = smtpService;
    }

    public Task<SmtpResult> SendAsync(string recipient)
    {
        var message = new MailMessage
        {
            To = recipient,
            Subject = "Receipt",
            Body = "<p>Your receipt is ready.</p>",
            IsHtmlBody = true,
        };

        return _smtpService.SendAsync(message);
    }
}
```

Check `SmtpResult.Succeeded` and record its errors without logging message
secrets or recipients unnecessarily.

