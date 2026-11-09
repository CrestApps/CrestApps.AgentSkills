---
name: orchardcore-email-azure
description: Skill for configuring Azure Communication Services email delivery in Orchard Core. Covers AzureEmailSettings, AzureEmailOptions, DefaultAzureEmailOptions, AzureEmailProvider, DefaultAzureEmailProvider, the generic AzureEmailProviderBase, the OrchardCore Email Azure configuration section, IOptionsMonitor refresh, encrypted connection strings, ConnectionStringSecretName references to the Secrets module, provider selection, recipes, IEmailService, and configuration overrides. Use this skill when requests mention Orchard Core Azure Email, Azure Communication Services Email, AzureEmailSettings, AzureEmailProvider, DefaultAzureEmailOptions, DefaultAzure, ACS email provider, custom Azure email provider, upgrading Azure email code to Orchard Core 4, or closely related Orchard Core implementation setup extension or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Azure Communication Services Email

`OrchardCore.Email.Azure` provides Azure Communication Services (ACS) delivery
on top of the provider-neutral `OrchardCore.Email` API (see
`orchardcore-email`).

- The tenant provider is `AzureEmailProvider`, technical name `Azure`, edited
  on the **Azure Communication Services** tab of **Settings → Communication →
  Email**.
- The configuration-backed provider is `DefaultAzureEmailProvider`, technical
  name `DefaultAzure`, bound from `OrchardCore:Email:Azure`.
- Application code sends through `IEmailService` and can therefore remain
  independent of the chosen provider.

## Configuration-backed Provider (`DefaultAzure`)

`DefaultAzureEmailOptions` is enabled only when both `DefaultSender` and
`ConnectionString` are set. It cannot be edited in the admin UI.

```json
{
  "OrchardCore": {
    "Email": {
      "Azure": {
        "DefaultSender": "DoNotReply@your-domain.azurecomm.net",
        "ConnectionString": "endpoint=https://your-resource.communication.azure.com/;accesskey=..."
      }
    }
  }
}
```

Keep the connection string out of committed files: use
`OrchardCore__Email__Azure__ConnectionString` or a key vault. In 4.0 a nonblank
`ConnectionString` here logs a `LegacySecretConfiguration` warning (event
`8100`). `DefaultAzureEmailOptions` can't reference a secret; to keep the
connection string in the Secrets module, use the tenant `Azure` provider.

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

## Tenant Provider (`Azure`)

| `AzureEmailSettings` property | Description |
|---|---|
| `IsEnabled` | Enables the tenant provider. |
| `DefaultSender` | Verified ACS sender address. Required. |
| `ConnectionString` | ACS connection string. Required unless a secret is referenced. Stored encrypted with Data Protection. |
| `ConnectionStringSecretName` | 4.0. Name of a `TextSecret` holding the connection string; takes precedence over `ConnectionString`. |

The admin UI encrypts the connection string before saving it, and
`AzureEmailOptionsConfiguration` decrypts it. Enter it in the admin UI; a plain
connection string imported by a `Settings` recipe step cannot be decrypted.

With the `OrchardCore.Secrets` feature enabled, the connection string editor
(`<secret-input>`) offers **Use a secret**: pick or create a `TextSecret`
(suggested name `AzureEmail.ConnectionString`), and the protected value is
cleared on save. A saved connection string can be moved without retyping it
from **Tools → Security → Migrate Secrets**. A recipe can create the secret
with the `Secrets` step (value from the `OrchardCore__Secrets__AzureEmail.ConnectionString`
environment variable) and set `"ConnectionStringSecretName": "AzureEmail.ConnectionString"`
in `AzureEmailSettings`. See [orchardcore-secrets](../orchardcore-secrets/SKILL.md). When
the tenant provider is the only one enabled it becomes the default
automatically; disabling it clears the default if it was selected.

## Send With IEmailService

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

    public Task<Result> SendAsync(string recipient, CancellationToken cancellationToken = default)
    {
        return _emailService.SendAsync(new MailMessage
        {
            To = recipient,
            Subject = "Welcome",
            TextBody = "Welcome to the site.",
        }, cancellationToken: cancellationToken);
    }
}
```

Attachments must use an ACS-supported file extension; other attachments make
the send fail with an error result.

## Runtime Updates and Custom Providers

`AzureEmailOptions` participates in the signal-backed options refresh: saving
the settings invalidates `AzureEmailOptions`, `EmailProviderOptions`, and
`EmailOptions` after the YesSql session commits, and every node rebuilds them.
Inject `IOptionsMonitor<AzureEmailOptions>` and read `CurrentValue`.

`AzureEmailProviderBase<TOptions>` (`TOptions : AzureEmailOptions`) reuses one
`EmailClient` until the current connection string changes. Derive from it to add
another ACS-backed provider:

```csharp
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Email.Azure.Models;
using OrchardCore.Email.Azure.Services;

namespace MyModule;

public sealed class TransactionalAzureEmailOptions : AzureEmailOptions;

public sealed class TransactionalAzureEmailProvider : AzureEmailProviderBase<TransactionalAzureEmailOptions>
{
    public const string TechnicalName = "TransactionalAzure";

    public TransactionalAzureEmailProvider(
        IOptionsMonitor<TransactionalAzureEmailOptions> options,
        ILogger<TransactionalAzureEmailProvider> logger,
        IStringLocalizer<TransactionalAzureEmailProvider> stringLocalizer)
        : base(options, logger, stringLocalizer)
    {
    }

    public override LocalizedString DisplayName => S["Transactional Azure Communication Services"];
}
```

Configure `TransactionalAzureEmailOptions` (set `IsEnabled`, `DefaultSender`,
and `ConnectionString`), register the provider with
`services.AddEmailProvider<TransactionalAzureEmailProvider>(TransactionalAzureEmailProvider.TechnicalName)`
or an `IConfigureOptions<EmailProviderOptions>`, and reference the
`OrchardCore.Email.Azure` package from that module.

Use **Tools → Testing → Email Test** to test an enabled provider. Confirm the
sender identity, service restrictions, and quota in Azure before production
use.

## Upgrading from 3.x

- `OrchardCore_Email_Azure` and `OrchardCore_Email_AzureCommunicationServices`
  sections → `OrchardCore:Email:Azure` (old names still bind but are
  deprecated).
- `IOptions<AzureEmailOptions>` → `IOptionsMonitor<AzureEmailOptions>.CurrentValue`.
- `class MyProvider : AzureEmailProviderBase` with a `Func<AzureEmailOptions>`
  constructor argument → `AzureEmailProviderBase<TOptions>` with
  `IOptionsMonitor<TOptions>`.
- Saved connection strings keep working. Code reading
  `AzureEmailSettings.ConnectionString` directly must also honor
  `ConnectionStringSecretName` (read `IOptionsMonitor<AzureEmailOptions>`
  or call `IServiceProvider.GetSecretValueAsync()`).
- `OrchardCore:Email:Azure:ConnectionString` still works but logs
  `LegacySecretConfiguration`.
