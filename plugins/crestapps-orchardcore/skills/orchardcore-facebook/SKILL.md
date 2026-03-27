---
name: orchardcore-facebook
description: Skill for configuring Facebook/Meta integrations in Orchard Core. Covers Meta App core components, Meta Login authentication, Social Plugins widgets (Like, Share, Comments, etc.), Meta Pixel tracking, and settings configuration. Use this skill when requests mention Orchard Core Facebook/Meta Integration, Meta Core Components, Meta App Settings, Meta Login Authentication, Meta Login Settings, Meta Social Plugins Widgets, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Facebook, OrchardCore.Facebook.Login, OrchardCore.Facebook.Widgets, OrchardCore.Facebook.Pixel, OrchardCore.Users.Registration, ResourceManager, ConfigureFacebookSettings, OrchardCoreBuilder, AppId, AppSecret, v3.2, sdk.js. It also helps with Meta Login Settings, Meta Social Plugins Widgets, Available Widget Types, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Facebook/Meta Integration - Prompt Templates

## Module Overview

The `OrchardCore.Facebook` module provides Facebook/Meta integrations for Orchard Core applications. It includes the following features:

- **OrchardCore.Facebook** — Core components: registers the Meta App with the site and loads the Facebook SDK.
- **OrchardCore.Facebook.Login** — Enables user authentication via Meta/Facebook accounts.
- **OrchardCore.Facebook.Widgets** — Adds Meta Social Plugin widgets (Like, Share, Comments, etc.).
- **OrchardCore.Facebook.Pixel** — Adds Meta Pixel tracking to the site.

## Meta Core Components

### Guidelines

- Enable the `OrchardCore.Facebook` feature as a prerequisite for all other Meta features.
- Obtain an `AppId` and `AppSecret` from the [Meta for Developers](https://developers.facebook.com/apps) page under Basic Settings.
- Configure settings via **Settings → Integrations → Meta App** in the admin dashboard.
- The SDK is registered with ResourceManager as resources `fb` and `fbsdk`, usable from Liquid or Razor templates.
- Set "Init on every page" to load the SDK globally, or leave disabled for on-demand loading.

### Meta App Settings

| Setting | Description |
|---|---|
| AppId | Meta application ID from the developer portal. |
| AppSecret | The application secret from the developer portal. |
| Javascript SDK Version | The Facebook SDK version to load (e.g., `v3.2`). |
| Javascript Sdk js | The SDK JS file to load (e.g., `sdk.js`). |
| Init on every page | If enabled, the SDK loads on every page; otherwise on demand. |
| Parameters for FB.init() | Comma-separated key-values passed to `FB.init()` (e.g., `status:true,xfbml:true,autoLogAppEvents:true`). |

## Meta Login Authentication

### Guidelines

- Enable the `OrchardCore.Facebook.Login` feature (requires `OrchardCore.Facebook` core feature).
- Enable the Meta Login Product in the [Meta for Developers](https://developers.facebook.com/apps) page for web apps.
- Set a valid **OAuth redirect URI** in the Meta developer portal.
- The default callback URL is `[tenant]/signin-facebook`.
- If the site allows registration, a local user is created and linked on first Meta login.
- If a local user with the same email exists, the external login is linked to that account.
- Enable `OrchardCore.Users.Registration` to allow new user sign-ups through Meta login.
- Existing users can link their Meta account via the External Logins link in the User menu.

### Meta Login Settings

| Setting | Description |
|---|---|
| CallbackPath | Request path for the OAuth callback. Defaults to `/signin-facebook`. |

## Meta Social Plugins Widgets

### Guidelines

- Enable the `OrchardCore.Facebook.Widgets` feature (requires `OrchardCore.Facebook` core feature).
- This feature adds a `FacebookPlugin` content part that integrates [Meta Social Plugins](https://developers.facebook.com/docs/plugins).
- The SDK must be loaded on the page (either via "Init on every page" or on-demand).

### Available Widget Types

| Widget | Description |
|---|---|
| Chat | Embeds a Messenger chat plugin on the page. |
| Comments | Adds a comments section powered by Facebook. |
| Continue With | Displays a "Continue with Facebook" button. |
| Like | Adds a Like button for the page or URL. |
| Quote | Enables quote sharing from the page. |
| Save | Adds a Save button for bookmarking content. |
| Share | Adds a Share button to share content on Facebook. |

## Meta Pixel Tracking

### Guidelines

- Enable the `OrchardCore.Facebook.Pixel` feature.
- Navigate to **Settings → Integrations → Meta Pixel** in the admin dashboard.
- Enter your **Pixel Identifier** obtained from the [Meta Events Manager](https://business.facebook.com/events_manager).
- The pixel tracking code is automatically injected on all front-end pages once configured.

### Meta Pixel Settings

| Setting | Description |
|---|---|
| Pixel Identifier | The Meta Pixel ID for tracking (e.g., `1234567890`). |

## Meta Settings Configuration Override

Override admin settings via `appsettings.json` by calling `ConfigureFacebookSettings()` on `OrchardCoreBuilder`:

```json
{
  "OrchardCore_Facebook": {
    "AppId": "",
    "AppSecret": "",
    "FBInit": false,
    "FBInitParams": "status:true,xfbml:true,autoLogAppEvents:true",
    "SdkJs": "sdk.js",
    "Version": "v3.2"
  }
}
```

## Using the Facebook SDK in Templates

### Razor

```cshtml
<script-resource name="fbsdk"></script-resource>
```

### Liquid

```liquid
{% scriptresource name: "fbsdk" %}
```

## User Registration

- Enable `OrchardCore.Users.Registration` for new user sign-ups through Meta login.
- Existing users can link accounts via the **External Logins** link in the user menu.
- When a local user with the same email exists, the external login is automatically linked after authentication.
