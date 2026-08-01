---
name: orchardcore-email-azure
description: Skill for configuring Azure Communication Services email delivery in Orchard Core. Covers AzureEmailSettings, AzureEmailOptions, AzureEmailProvider, DefaultAzureEmailProvider, provider selection, recipes, IEmailService, and configuration overrides. Use this skill when requests mention Orchard Core Azure Email, Azure Communication Services Email, AzureEmailSettings, AzureEmailProvider, DefaultAzureEmailOptions, ACS email provider, or closely related Orchard Core implementation setup extension or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Azure Communication Services Email

`OrchardCore.Email.Azure` provides Azure Communication Services delivery on top
of the provider-neutral `OrchardCore.Email` API.

- The tenant provider is `AzureEmailProvider`, technical name `Azure`.
- The configuration-backed provider is `DefaultAzureEmailProvider`, technical
  name `DefaultAzure`.
- Application code sends through `IEmailService` and can therefore remain
  independent of the chosen provider.

Enable the features, configure a verified sender and connection string under
**Settings → Communication → Email**, then select `Azure` as the default when
multiple providers are enabled.

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

`DefaultAzureEmailOptions` binds
`OrchardCore_Email_AzureCommunicationServices`; it creates `DefaultAzure`
when its sender and connection string are valid. Keep ACS connection strings
out of committed recipes.

```csharp
using OrchardCore.Email;
using OrchardCore.Infrastructure;

namespace MyModule;

public sealed class WelcomeEmailService
{
    private readonly IEmailService _emailService;

    public WelcomeEmailService(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public Task<Result> SendAsync(string recipient)
    {
        return _emailService.SendAsync(new MailMessage
        {
            To = recipient,
            Subject = "Welcome",
            TextBody = "Welcome to the site.",
        });
    }
}
```

Use **Tools → Testing → Email Test** to test an enabled provider. Confirm the
sender identity, service restrictions, and quota in Azure before production
use.
