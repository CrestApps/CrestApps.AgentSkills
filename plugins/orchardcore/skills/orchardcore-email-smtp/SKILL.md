---
name: orchardcore-email-smtp
description: Skill for configuring SMTP email delivery in Orchard Core. Covers SmtpSettings, SmtpOptions, SmtpEmailProvider, DefaultSmtpEmailProvider, pickup directories, provider selection, recipes, IEmailService, and legacy ISmtpService migration. Use this skill when requests mention Orchard Core SMTP Email, SmtpSettings, SmtpEmailProvider, SMTP configuration, MailKit email provider, pickup directory, DefaultSmtpOptions, or closely related Orchard Core implementation setup extension or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core SMTP Email Provider

`OrchardCore.Email.Smtp` supplies the tenant `SMTP` provider and the
configuration-backed `DefaultSMTP` provider. Application code uses
`IEmailService`, not an SMTP-specific service.

Enable the module and configure **Settings → Communication → Email**. Select
the enabled SMTP provider as the email default when more than one provider is
enabled. The last valid enabled provider is selected automatically.

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
        "Password": "supply-from-a-secret-store",
        "DeliveryMethod": "Network"
      },
      "EmailSettings": {
        "DefaultProviderName": "SMTP"
      }
    }
  ]
}
```

For centrally managed defaults, configure `DefaultSmtpOptions` through
`OrchardCore_Email_Smtp`; its technical name is `DefaultSMTP`. Store passwords
outside source control.

For pickup delivery set `DeliveryMethod` to `SpecifiedPickupDirectory` and use
a valid relative `PickupDirectoryLocation`. The configuration-backed provider
also supports `PickupDirectoryLocationBase`.

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

    public Task<Result> SendAsync(string recipient)
    {
        return _emailService.SendAsync(new MailMessage
        {
            To = recipient,
            Subject = "Receipt",
            HtmlBody = "<p>Your receipt is ready.</p>",
            TextBody = "Your receipt is ready.",
        });
    }
}
```

`ISmtpService`, `SmtpResult`, `MailKitSmtpService`, `Body`, and `IsHtmlBody`
are obsolete names or members. Test a selected enabled provider at
**Tools → Testing → Email Test**.
