---
name: orchardcore-microsoft-auth
description: Skill for configuring Microsoft authentication in Orchard Core. Covers Microsoft Account login, Microsoft Entra ID (Azure AD) authentication, single and multi-tenant app registrations, current feature IDs, current settings paths, MicrosoftAccountSettings and AzureADSettings recipes, the generic Settings step, ConfigureMicrosoftAccountSettings and ConfigureAzureADSettings with the OrchardCore Authentication MicrosoftAccount and AzureAD configuration sections, AppSecret from configuration, and callback URL setup. Use this skill when requests mention Orchard Core Microsoft Authentication, Microsoft Account Authentication, Microsoft Account Settings, Microsoft Entra ID Authentication, Azure Active Directory login, Entra ID Settings, or closely related Orchard Core implementation, setup, extension, upgrade, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Microsoft Authentication

Enable one or both external-authentication features:

| Feature ID | Settings path |
|---|---|
| `OrchardCore.Microsoft.Authentication.MicrosoftAccount` | **Settings → Security → Authentication → Microsoft** |
| `OrchardCore.Microsoft.Authentication.AzureAD` | **Settings → Security → Authentication → Microsoft Entra ID** |

Both depend on `OrchardCore.Users.ExternalAuthentication`. Register each
tenant's exact HTTPS callback URL at the provider. To accept personal
Microsoft accounts together with work or school accounts, use Entra ID with
tenant `common` instead of the Microsoft Account feature.

## Microsoft Account

The default callback path is `/signin-microsoft`. The module's recipe step is
named `MicrosoftAccountSettings` and replaces `AppId`, `AppSecret`, and
`CallbackPath`:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Microsoft.Authentication.MicrosoftAccount"
      ]
    },
    {
      "name": "MicrosoftAccountSettings",
      "AppId": "microsoft-application-id",
      "CallbackPath": "/signin-microsoft"
    }
  ]
}
```

The handler decrypts the stored `AppSecret` with data protection, as saved
from the admin page; a recipe-imported secret is stored as is and fails to
decrypt. Supply the secret from the admin page or configuration, and note that
re-running the step clears it. The generic `Settings` step also accepts a
`MicrosoftAccountSettings` object with `SaveTokens`.

## Microsoft Entra ID

Register an app in the Azure Portal with redirect URI `/signin-oidc`, enable
access tokens and ID tokens under implicit and hybrid flows, and add the
optional `email` claim to the ID token so existing users can be matched by
email. The Entra recipe step is `AzureADSettings`:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Microsoft.Authentication.AzureAD"
      ]
    },
    {
      "name": "AzureADSettings",
      "DisplayName": "Sign in with Microsoft Entra ID",
      "AppId": "entra-application-id",
      "TenantId": "organizations",
      "CallbackPath": "/signin-oidc"
    }
  ]
}
```

| `TenantId` | Who can sign in |
|---|---|
| Directory (tenant) ID | Accounts in your directory only |
| `organizations` | Any work or school account |
| `common` | Work, school, and personal Microsoft accounts |

Multi-tenant values broaden who can authenticate; combine them with
`ExternalRegistrationSettings` or login scripts to control local accounts.

## Configuration Overrides

Call `ConfigureMicrosoftAccountSettings()` and/or `ConfigureAzureADSettings()`
on the `OrchardCoreBuilder`:

```csharp
builder.Services
    .AddOrchardCms()
    .ConfigureMicrosoftAccountSettings()
    .ConfigureAzureADSettings();
```

```json
{
  "OrchardCore": {
    "Authentication": {
      "MicrosoftAccount": {
        "AppId": "microsoft-application-id",
        "AppSecret": "plain-app-secret",
        "CallbackPath": "/signin-microsoft",
        "SaveTokens": false
      },
      "AzureAD": {
        "DisplayName": "Contoso account",
        "AppId": "entra-application-id",
        "TenantId": "organizations",
        "CallbackPath": "/signin-oidc",
        "SaveTokens": false
      }
    }
  }
}
```

The Microsoft Account `AppSecret` is a plain value here, for example from the
`OrchardCore__Authentication__MicrosoftAccount__AppSecret` environment
variable; Orchard Core protects it before use.

Enable `OrchardCore.Users.Registration` only when external identities may
create local Orchard users; see `orchardcore-users-roles` for
`ExternalRegistrationSettings` and the external login scripts.

## Upgrading from 3.x

- Rename `OrchardCore_Microsoft_Authentication_MicrosoftAccount` to `OrchardCore:Authentication:MicrosoftAccount` and `OrchardCore_Microsoft_Authentication_AzureAD` to `OrchardCore:Authentication:AzureAD` (old names still work but are deprecated).
- A plain Microsoft Account `AppSecret` from configuration is now honored; in 3.x it had to be already protected and was otherwise ignored.
