---
name: orchardcore-google
description: Skill for configuring Google integrations in Orchard Core. Covers Google Analytics, Google Tag Manager, Google Authentication, credentials configuration, current feature IDs, current admin paths, and generic Settings recipes. Use this skill when requests mention Orchard Core Google Integration, Google Analytics, Google Analytics Settings, Google Tag Manager, Google Tag Manager Settings, Google Authentication, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
---

# Orchard Core Google Integration

The Google module has these feature IDs:

| Feature | ID | Current admin path |
|---|---|---|
| Google Analytics | `OrchardCore.Google.Analytics` | **Settings → Integrations → Google Analytics** |
| Google Tag Manager | `OrchardCore.Google.TagManager` | **Settings → Integrations → Google Tag Manager** |
| Google authentication | `OrchardCore.Google.GoogleAuthentication` | **Settings → Security → Authentication → Google** |

Google authentication depends on
`OrchardCore.Users.ExternalAuthentication`. Register the OAuth callback URL
with Google; its default path is `/signin-google`. Google+ is not an
Orchard Core dependency or setup step.

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
        "ClientID": "google-oauth-client-id",
        "ClientSecret": "supply-from-a-secret-store",
        "CallbackPath": "/signin-google",
        "SaveTokens": false
      }
    }
  ]
}
```

Use `ConfigureGoogleSettings()` for configuration-backed overrides. Enable
`OrchardCore.Users.Registration` only when a Google external login may create
a local Orchard user.
