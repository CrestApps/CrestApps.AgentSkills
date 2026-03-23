---
name: orchardcore-google
description: Skill for configuring Google integrations in Orchard Core. Covers Google Analytics with tracking IDs, Google Tag Manager with container IDs, Google Authentication with OAuth, credentials configuration, and recipe-based setup for each Google feature.
---

# Orchard Core Google Integration - Prompt Templates

## Module Overview

The `OrchardCore.Google` module provides Google service integrations for Orchard Core applications. It includes three distinct features:

- **OrchardCore.Google.Analytics** — Adds Google Analytics tracking to the front-end site.
- **OrchardCore.Google.TagManager** — Integrates Google Tag Manager on the front-end site.
- **OrchardCore.Google.Authentication** — Enables user authentication via Google accounts.

## Google Analytics

### Guidelines

- Enable the `OrchardCore.Google.Analytics` feature.
- Obtain a Tracking ID from the [Google Analytics](https://analytics.google.com/analytics/web) portal.
- Navigate to **Admin → Tracking Info → Tracking Code** in Google Analytics to find your Tracking ID.
- Configure the Tracking ID in Orchard Core via **Google → Google Analytics** in the admin dashboard.
- The tracking script is automatically injected on all front-end pages once configured.

### Google Analytics Settings

| Setting | Description |
|---|---|
| Tracking ID | The Google Analytics tracking identifier (e.g., `UA-XXXXXXXXX-X` or `G-XXXXXXXXXX`). |

## Google Tag Manager

### Guidelines

- Enable the `OrchardCore.Google.TagManager` feature.
- Create a Tag Manager account at [Google Tag Manager](https://tagmanager.google.com/).
- Copy the **Container ID** generated for your website (e.g., `GTM-XXXXXXX`).
- Configure the Container ID in Orchard Core via **Google → Google Tag Manager** in the admin dashboard.
- The Tag Manager snippet is automatically injected on all front-end pages once configured.

### Google Tag Manager Settings

| Setting | Description |
|---|---|
| Container ID | The Google Tag Manager container identifier (e.g., `GTM-XXXXXXX`). |

## Google Authentication

### Guidelines

- Enable the `OrchardCore.Google.Authentication` feature.
- Create a project in the [Google API Console](https://console.developers.google.com/projectselector/apis/library).
- Add the Google+ API to your project.
- Create OAuth credentials: select "Web server" for the calling location and "User data" for data access.
- Set the **authorized redirect URI** to `[tenant]/signin-google` (the default callback path).
- Download the credentials JSON file to obtain `ClientID` and `ClientSecret`.
- Configure settings via **Google → Google Authentication** in the admin dashboard.
- Enable `OrchardCore.Users.Registration` to allow new users to register through Google login.
- Existing users can link their Google account via the External Logins link in the User menu.

### Google Authentication Settings

| Setting | Description |
|---|---|
| ClientID | The `client_id` value from the downloaded Google credentials JSON file. |
| ClientSecret | The `client_secret` value from the downloaded Google credentials JSON file. |
| CallbackPath | Request path for the OAuth callback. Defaults to `/signin-google`. |

## Google Settings Configuration Override

Override admin settings via `appsettings.json` by calling `ConfigureGoogleSettings()` on `OrchardCoreBuilder`:

```json
{
  "OrchardCore_Google": {
    "ClientID": "",
    "ClientSecret": "",
    "CallbackPath": "/signin-google",
    "SaveTokens": false
  }
}
```

## Google OAuth Setup (Step-by-Step)

1. Go to [Google API Console](https://console.developers.google.com/projectselector/apis/library) and create or select a project.
2. Add the **Google+ API** to the project.
3. Navigate to **Credentials** → **Create Credentials** → **OAuth client ID**.
4. Select "Web application" as the application type.
5. Set the **Authorized redirect URI** to `https://your-site.com/signin-google`.
6. Configure the consent screen with your application details.
7. Download the credentials JSON file.
8. Copy `client_id` and `client_secret` into the Orchard Core Google Authentication settings.

## User Registration

- Enable `OrchardCore.Users.Registration` for new user sign-ups through Google login.
- Existing users can link accounts via the **External Logins** link in the user menu.
- When a local user with the same email exists, the external login is automatically linked after authentication.
