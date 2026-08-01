---
name: orchardcore-email-azure
description: Skill for configuring Azure Communication Services email delivery in Orchard Core. Covers AzureEmailSettings, AzureEmailOptions, AzureEmailProvider, DefaultAzureEmailProvider, provider selection, recipes, and configuration overrides while using base Email abstractions. Use this skill when requests mention Orchard Core Azure Email, Azure Communication Services Email, AzureEmailSettings, AzureEmailProvider, DefaultAzureEmailOptions, ACS email provider, or closely related Orchard Core implementation setup extension or troubleshooting work. Strong matches include work with OrchardCore.Email.Azure, OrchardCore.Email, OrchardCore.Email.Azure.Models, OrchardCore.Email.Azure.Services, IEmailProvider, ISmtpService, AddEmailProviderOptionsConfiguration, and AzureEmailSettingsDisplayDriver. It also helps with tenant settings, verified senders, configuration-backed defaults, and the source-backed patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Azure Communication Services Email

`OrchardCore.Email.Azure` supplies Azure Communication Services delivery for
the base `OrchardCore.Email` provider system. It registers site settings and
the provider options configuration. Application code should use the base Email
abstractions so SMTP and Azure providers remain interchangeable.

## Guidelines

- Enable `OrchardCore.Email` before `OrchardCore.Email.Azure`.
- Select an enabled email provider after configuring it.
- The tenant-configured provider is `AzureEmailProvider` with technical name `Azure`.
- `DefaultAzureEmailProvider` uses configuration-backed `DefaultAzureEmailOptions`.
- Use a verified Azure Communication Services sender address as `DefaultSender`.
- Keep ACS connection strings out of recipes committed to source control.
- Bind production secrets from a secure configuration provider.
- An Azure provider configuration requires both `DefaultSender` and `ConnectionString`.
- All recipe JSON uses the root `{ "steps": [...] }` format.

## Enable the Provider

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Email",
        "OrchardCore.Email.Azure"
      ],
      "disable": []
    }
  ]
}
```

## Configure Per Tenant

Navigate to **Configuration → Settings → Email** and configure Azure
Communication Services. `AzureEmailSettings` contains:

| Property | Description |
|---|---|
| `IsEnabled` | Enables this tenant provider. |
| `DefaultSender` | Verified ACS sender address. |
| `ConnectionString` | Azure Communication Services connection string. |

Use a Settings recipe when secure recipe delivery is available:

```json
{
  "steps": [
    {
      "name": "Settings",
      "AzureEmailSettings": {
        "IsEnabled": true,
        "DefaultSender": "DoNotReply@your-domain.azurecomm.net",
        "ConnectionString": "endpoint=https://your-resource.communication.azure.com/;accesskey=your-key"
      }
    }
  ]
}
```

## Configure the Default Provider

The module binds `DefaultAzureEmailOptions` from
`OrchardCore_Email_AzureCommunicationServices`. It also binds the older
`OrchardCore_Email_Azure` section for compatibility. Prefer the current name:

```json
{
  "OrchardCore_Email_AzureCommunicationServices": {
    "DefaultSender": "DoNotReply@your-domain.azurecomm.net",
    "ConnectionString": "endpoint=https://your-resource.communication.azure.com/;accesskey=your-key"
  }
}
```

The options configuration sets `IsEnabled` only when both required values are
present. This provider is useful when every tenant shares centrally managed ACS
credentials; use tenant settings when tenants require separate accounts.

## Send Using the Base Email Abstractions

Keep application services provider-neutral:

```csharp
using OrchardCore.Email;

namespace MyModule;

public sealed class WelcomeEmailService
{
    private readonly ISmtpService _smtpService;

    public WelcomeEmailService(ISmtpService smtpService)
    {
        _smtpService = smtpService;
    }

    public Task<SmtpResult> SendAsync(string recipient)
    {
        return _smtpService.SendAsync(new MailMessage
        {
            To = recipient,
            Subject = "Welcome",
            Body = "Welcome to the site.",
        });
    }
}
```

Provider selection and credentials belong in configuration, not in this
service. Inspect the result and application logs when delivery fails.

## Operational Checks

Verify that the sender domain is provisioned in Azure, the connection string
belongs to the intended ACS resource, and the selected Orchard provider is
enabled. Test delivery to a controlled mailbox before enabling workflows or
user notifications that might generate large volumes of messages.

## Provider Model

The Azure module registers an email provider options configuration and an
`AzureEmailSettingsDisplayDriver`. The display driver makes tenant settings
available in the Email settings UI, while `AzureEmailOptionsConfiguration`
turns those settings into an enabled provider entry. This separation lets the
base Email module choose among enabled providers without application code
knowing where the credentials came from.

When an app needs a fixed default for all tenants, the
`DefaultAzureEmailProvider` is a safer operational choice than copying the same
connection string into every tenant. When independent tenants own their Azure
resources, configure the regular `AzureEmailProvider` per tenant instead.

## Sender and Recipient Constraints

Azure validates the sender against the configured Communication Services
resource. A successful Orchard provider selection does not guarantee that an
unverified sender or restricted destination will be accepted. Confirm domain
verification, sender identities, quota, and any Azure subscription restrictions
before adding email delivery to high-volume workflows.
