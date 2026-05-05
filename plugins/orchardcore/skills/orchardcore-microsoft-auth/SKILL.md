---
name: orchardcore-microsoft-auth
description: Skill for configuring Microsoft authentication in Orchard Core. Covers Microsoft Account login, Microsoft Entra ID (Azure AD) integration, Azure app registration, multi-tenant support, recipe-based configuration, configuration overrides, and callback URL setup. Use this skill when requests mention Orchard Core Microsoft Authentication, Microsoft Account Authentication, Microsoft Account Settings, Microsoft Account Configuration Override, Microsoft Entra ID (Azure Active Directory) Authentication, Entra ID Settings, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Microsoft.Authentication, OrchardCore.Users.Registration, ConfigureMicrosoftAccountSettings, OrchardCoreBuilder. It also helps with Microsoft Entra ID (Azure Active Directory) Authentication, Entra ID Settings, Multi-Tenant App Registrations, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Microsoft Authentication - Prompt Templates

## Module Overview

The `OrchardCore.Microsoft.Authentication` module enables authentication via Microsoft Account and/or Microsoft Entra ID (Azure Active Directory). It provides two distinct features:

- **OrchardCore.Microsoft.Authentication.MicrosoftAccount** — Authenticates users with personal Microsoft Accounts.
- **OrchardCore.Microsoft.Authentication.AzureAD** — Authenticates users with Microsoft Entra ID (work, school, and personal accounts).

When a user authenticates, a local Orchard Core user is created and linked if the site allows registration, or the external login is linked to an existing user with a matching email.

## Microsoft Account Authentication

### Guidelines

- Create an app in the [Application Registration Portal](https://apps.dev.microsoft.com) and add the web platform.
- Generate a secret to use as `AppSecret` in Orchard Core.
- Enable implicit flow in the app registration.
- The default callback URL is `[tenant]/signin-microsoft`.
- If you want to allow both personal and work/school accounts, use Microsoft Entra ID with multi-tenant configuration instead.
- Enable `OrchardCore.Users.Registration` to allow new users to register through Microsoft login.
- Existing users can link their Microsoft account via the External Logins link in the User menu.

### Microsoft Account Settings

| Setting | Description |
|---|---|
| AppId | Application ID from the Application Registration Portal. |
| AppSecret | The application secret generated in the portal. |
| CallbackPath | Request path for the callback. Defaults to `/signin-microsoft`. |

### Microsoft Account Configuration Override

Override admin settings via `appsettings.json` by calling `ConfigureMicrosoftAccountSettings()` on `OrchardCoreBuilder`:

```json
{
  "OrchardCore_Microsoft_Authentication_MicrosoftAccount": {
    "AppId": "",
    "AppSecret": "",
    "CallbackPath": "/signin-microsoft",
    "SaveTokens": false
  }
}
```

## Microsoft Entra ID (Azure Active Directory) Authentication

### Guidelines

- Create an app registration in the [Azure Portal](https://portal.azure.com) under "Azure Active Directory" → "App registrations".
- Under "Authentication", enable both "Access tokens" and "ID tokens" for implicit grant and hybrid flows.
- Under "Token configuration", add the `email` optional claim (Token type: ID) so Orchard can match logins by email.
- Set the Redirect URI to `[your-app-url]/signin-oidc` (the default callback path).
- Note the **Application (client) ID** and **Directory (tenant) ID** for configuration.
- Enable `OrchardCore.Users.Registration` to allow new user registrations through Entra ID login.

### Entra ID Settings

| Setting | Description |
|---|---|
| Display Name | Text shown on the Orchard login screen (e.g., "My Company Microsoft account"). |
| AppId | Application (client) ID from the Azure Portal. |
| TenantId | Directory (tenant) ID. Use `common` or `organizations` for multi-tenant. |
| CallbackPath | Callback path within the app. Defaults to `/signin-oidc`. |

### Multi-Tenant App Registrations

Configure the `TenantId` based on your audience:

| Audience | Tenant Type | TenantId Value |
|---|---|---|
| Accounts in your directory only | Single tenant | Your Directory (tenant) ID |
| Accounts in any Microsoft Entra directory | Multi-tenant | `organizations` |
| Any Entra directory + personal Microsoft accounts | Multi-tenant | `common` |

**Warning**: Multi-tenant configurations broaden the potential user base significantly. Ensure this aligns with your security requirements.

### Entra ID Recipe Step

```json
{
  "steps": [
    {
      "name": "azureADSettings",
      "appId": "86eb5541-ba2b-4255-9344-54eb73cec375",
      "tenantId": "4cc363b6-5254-4b8c-bc1b-e951a5fc85ac",
      "displayName": "Orchard Core AD App",
      "callbackPath": "/signin-oidc"
    }
  ]
}
```

### Entra ID Configuration Override

Override admin settings via `appsettings.json` by calling `ConfigureAzureADSettings()` on `OrchardCoreBuilder`:

```json
{
  "OrchardCore_Microsoft_Authentication_AzureAD": {
    "DisplayName": "My Company Login",
    "AppId": "",
    "TenantId": "",
    "CallbackPath": "/signin-oidc",
    "SaveTokens": false
  }
}
```

## Azure App Registration Setup (Step-by-Step)

1. Go to the [Azure Portal](https://portal.azure.com) → "Azure Active Directory" → "App registrations" → "New registration".
2. Set **Name** (e.g., "My App"), choose **Supported account types**, and add a **Redirect URI** (`https://example.com/signin-oidc`).
3. Note the **Application (client) ID** and **Directory (tenant) ID**.
4. Under **Authentication**, enable "Access tokens" and "ID tokens" under "Implicit grant and hybrid flows".
5. Under **Token configuration**, click "Add optional claim" → Token type: "ID" → select `email` → "Add".
6. Configure the Orchard Core admin settings or use `appsettings.json` overrides with the noted IDs.

## User Registration

- Enable `OrchardCore.Users.Registration` for new user sign-ups through Microsoft login.
- Existing users can link accounts via the **External Logins** link in the user menu.
- When a local user with the same email exists, the external login is automatically linked after authentication.
