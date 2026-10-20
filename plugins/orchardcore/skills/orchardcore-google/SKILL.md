---
name: orchardcore-google
description: Skill for configuring Google integrations in Orchard Core. Covers Google Analytics, Google Tag Manager, Google Authentication, credentials configuration, current feature IDs, current admin paths, generic Settings recipes, ConfigureGoogleSettings with the OrchardCore Authentication Google configuration section, ClientSecret from configuration or environment variables, and Google permissions. Use this skill when requests mention Orchard Core Google Integration, Google Analytics, Google Analytics Settings, Google Tag Manager, Google Tag Manager Settings, Google Authentication, sign in with Google, GoogleAuthenticationSettings, or closely related Orchard Core implementation, setup, extension, upgrade, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Google Integration

The Google module has these feature IDs:

| Feature | ID | Current admin path |
|---|---|---|
| Google Analytics | `OrchardCore.Google.Analytics` | **Settings → Integrations → Google Analytics** |
| Google Tag Manager | `OrchardCore.Google.TagManager` | **Settings → Integrations → Google Tag Manager** |
| Google authentication | `OrchardCore.Google.GoogleAuthentication` | **Settings → Security → Authentication → Google** |

Google authentication depends on
`OrchardCore.Users.ExternalAuthentication`. Create an OAuth client (web
application) in the Google Cloud Console and register the callback URL; its
default path is `/signin-google` under the tenant. Google+ is not an Orchard
Core dependency or setup step.

The Google features use site settings, so use the generic `Settings` recipe
step:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Google.Analytics",
        "OrchardCore.Google.TagManager",
        "OrchardCore.Google.GoogleAuthentication"
      ]
    },
    {
      "name": "Settings",
      "GoogleAnalyticsSettings": {
        "TrackingID": "G-EXAMPLE"
      },
      "GoogleTagManagerSettings": {
        "ContainerID": "GTM-EXAMPLE"
      },
      "GoogleAuthenticationSettings": {
        "ClientID": "google-oauth-client-id.apps.googleusercontent.com",
        "CallbackPath": "/signin-google",
        "SaveTokens": false
      }
    }
  ]
}
```

The handler expects the stored `ClientSecret` to be data-protected, as it is
when saved from the admin page. A secret written by a recipe is stored as is
and fails to decrypt, so supply it through the admin page or configuration.

## Configuration Overrides

Call `ConfigureGoogleSettings()` on the `OrchardCoreBuilder` to override the
Google authentication settings from configuration:

```csharp
builder.Services
    .AddOrchardCms()
    .ConfigureGoogleSettings();
```

```json
{
  "OrchardCore": {
    "Authentication": {
      "Google": {
        "ClientID": "google-oauth-client-id.apps.googleusercontent.com",
        "ClientSecret": "plain-client-secret",
        "CallbackPath": "/signin-google",
        "SaveTokens": false
      }
    }
  }
}
```

`ClientSecret` is a plain value here, for example from the
`OrchardCore__Authentication__Google__ClientSecret` environment variable;
Orchard Core protects it before use.

Enable `OrchardCore.Users.Registration` only when a Google external login may
create a local Orchard user; new external users are governed by
`ExternalRegistrationSettings` (see `orchardcore-users-roles`).

## Permissions

The static `OrchardCore.Google.Permissions` class holds
`ManageGoogleAuthentication`, `ManageGoogleAnalytics`, and
`ManageGoogleTagManager`; each feature registers its own provider. The removed
`GoogleAnalyticsPermissionsProvider.ManageGoogleAnalytics` member must be
replaced with `OrchardCore.Google.Permissions.ManageGoogleAnalytics`.

## Upgrading from 3.x

- Rename the `OrchardCore_Google` configuration section to `OrchardCore:Authentication:Google` (the old name still works but is deprecated).
- A plain `ClientSecret` from configuration is now honored; in 3.x it had to be already protected and was otherwise ignored.
- Replace `GoogleAnalyticsPermissionsProvider.ManageGoogleAnalytics` with `OrchardCore.Google.Permissions.ManageGoogleAnalytics`.
