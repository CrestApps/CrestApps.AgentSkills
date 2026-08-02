---
name: orchardcore-microsoft-auth
description: Skill for configuring Microsoft authentication in Orchard Core. Covers Microsoft Account login, Microsoft Entra ID authentication, current feature IDs, current settings paths, MicrosoftAccountSettings and AzureADSettings recipes, and callback URL setup. Use this skill when requests mention Orchard Core Microsoft Authentication, Microsoft Account Authentication, Microsoft Account Settings, Microsoft Entra ID Authentication, Entra ID Settings, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
---

# Orchard Core Microsoft Authentication

Enable one or both external-authentication features:

| Feature ID | Settings path |
|---|---|
| `OrchardCore.Microsoft.Authentication.MicrosoftAccount` | **Settings → Security → Authentication → Microsoft** |
| `OrchardCore.Microsoft.Authentication.AzureAD` | **Settings → Security → Authentication → Microsoft Entra ID** |

Both depend on `OrchardCore.Users.ExternalAuthentication`. Register each
tenant's exact HTTPS callback URL at the provider.

## Microsoft Account

The default callback path is `/signin-microsoft`. The module's recipe handler
is named `MicrosoftAccountSettings`:

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
      "AppSecret": "supply-from-a-secret-store",
      "CallbackPath": "/signin-microsoft"
    }
  ]
}
```

## Microsoft Entra ID

The Entra recipe handler is `AzureADSettings`:

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

Use the specific configuration extension for environment-backed secrets and
enable `OrchardCore.Users.Registration` only when external identities may
create local Orchard users.
