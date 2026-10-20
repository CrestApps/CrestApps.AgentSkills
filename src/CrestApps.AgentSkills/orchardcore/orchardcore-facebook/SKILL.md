---
name: orchardcore-facebook
description: Skill for configuring Facebook and Meta integrations in Orchard Core. Covers Meta App core components, Meta Login authentication, Social Plugins widgets and their ManageLiquidTemplates protection, Meta Pixel tracking, the Meta Conversions API and its workflow task, FacebookCoreSettings, FacebookLoginSettings, and FacebookPixelSettings recipes, and ConfigureFacebookSettings with the OrchardCore Facebook configuration section and AppSecret from configuration. Use this skill when requests mention Orchard Core Facebook/Meta Integration, Meta App Settings, Meta Login, Meta Social Plugins Widgets, Meta Pixel, Conversions API, server-side Meta events, or closely related Orchard Core implementation, setup, extension, upgrade, or troubleshooting work. Strong matches include OrchardCore.Facebook, OrchardCore.Facebook.Login, OrchardCore.Facebook.Widgets, OrchardCore.Facebook.Pixel, FacebookSettings, and FacebookPluginPart.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Facebook and Meta Integration

`OrchardCore.Facebook` is the Meta core feature. It is
`EnabledByDependencyOnly`; do not try to enable it directly. Enable a
dependent feature instead:

| Feature ID | Purpose |
|---|---|
| `OrchardCore.Facebook.Login` | External Meta authentication |
| `OrchardCore.Facebook.Widgets` | Meta social-plugin widgets |
| `OrchardCore.Facebook.Pixel` | Meta Pixel tracking and the Conversions API |

The Login and Widgets features bring in the core feature. Pixel is independent.

## Meta App Settings

Configure the core app at **Settings → Integrations → Meta App**. The backing
`FacebookSettings` properties are `AppId`, `AppSecret`, `FBInit`,
`FBInitParams`, `SdkJs`, and `Version`. The module's dedicated recipe step is
`FacebookCoreSettings`:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Facebook.Login"
      ]
    },
    {
      "name": "FacebookCoreSettings",
      "AppId": "meta-app-id",
      "FBInit": true,
      "FBInitParams": "status: true, xfbml: true, autoLogAppEvents: true",
      "SdkJs": "sdk.js",
      "Version": "v3.2"
    }
  ]
}
```

The app secret is stored data-protected when saved from the admin page, and
the login handler decrypts it. A secret written by a recipe is stored as is
and cannot be decrypted, so supply `AppSecret` through the admin page or
configuration instead. The step replaces all core values, so re-running it
clears a secret entered earlier.

## Configuration Overrides

Call `ConfigureFacebookSettings()` on the `OrchardCoreBuilder` to override the
stored core settings from configuration:

```csharp
builder.Services
    .AddOrchardCms()
    .ConfigureFacebookSettings();
```

```json
{
  "OrchardCore": {
    "Facebook": {
      "AppId": "meta-app-id",
      "AppSecret": "plain-app-secret",
      "FBInit": false,
      "FBInitParams": "status:true,xfbml:true,autoLogAppEvents:true",
      "SdkJs": "sdk.js",
      "Version": "v3.2"
    }
  }
}
```

The configured `AppSecret` is a plain value (for example from the
`OrchardCore__Facebook__AppSecret` environment variable or a key vault); Orchard
Core protects it before use.

## Meta Login

Enable `OrchardCore.Facebook.Login`, which depends on
`OrchardCore.Users.ExternalAuthentication`. Configure it at
**Settings → Security → Authentication → Meta**. The callback defaults to
`/signin-facebook`; register the tenant's full HTTPS callback URL with Meta.

```json
{
  "steps": [
    {
      "name": "FacebookLoginSettings",
      "CallbackPath": "/signin-facebook"
    }
  ]
}
```

The `FacebookLoginSettings` step persists the callback path only. To also set
`SaveTokens`, use the generic `Settings` step with a `FacebookLoginSettings`
object (`CallbackPath`, `SaveTokens`).

Enable `OrchardCore.Users.Registration` only when external users should be
able to create local accounts; external-user creation is controlled by
`ExternalRegistrationSettings` (see `orchardcore-users-roles`).

## Social Plugins Widgets

Enable `OrchardCore.Facebook.Widgets` to get the `FacebookPluginPart` and the
Chat, Comments, Continue With, Like, Quote, Save, and Share widgets.

The `FacebookPluginPart` body is an executable Liquid template. Editing it
requires the security-critical `ManageLiquidTemplates` permission, which is
also required to edit or publish any content containing the part, including
widgets embedded recursively inside other content items. Grant it only to
trusted roles.

Load the SDK in templates when a page needs it (resources `fb` and `fbsdk`):

```liquid
{% scriptresource name: "fbsdk" %}
```

## Meta Pixel and Conversions API

Enable `OrchardCore.Facebook.Pixel` and configure
**Settings → Integrations → Meta Pixel**:

- **Pixel Identifier** (`PixelId`) for browser-side tracking.
- **Conversions API Access Token** (`ConversionsApiAccessToken`), generated in Events Manager for the pixel's dataset; stored encrypted.
- **Test Event Code** (`ConversionsApiTestEventCode`), optional; events go to the Events Manager test console. Remove it before going live.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Facebook.Pixel",
        "OrchardCore.Workflows"
      ]
    },
    {
      "name": "Settings",
      "FacebookPixelSettings": {
        "PixelId": "your-pixel-id",
        "ConversionsApiTestEventCode": "TEST12345"
      }
    }
  ]
}
```

Enter the access token from the admin page so it is stored encrypted.

With `OrchardCore.Workflows` enabled, the **Meta Conversions API Event** task
(category **Meta**) sends a server-side event when a workflow runs:

- **Event Name**, for example `Purchase` or `Lead` (supports workflow expressions).
- **Event Source URL**, optional (supports expressions).
- **Action Source**, for example `Website`, `App`, or `Email`.
- **Event ID**, optional, to deduplicate with the same browser-side pixel event (supports expressions).

The task has `Done` and `Failed` outcomes. Server-side events are useful for
background processes and for events affected by ad blockers.

## Upgrading from 3.x

- Rename the `OrchardCore_Facebook` configuration section to `OrchardCore:Facebook` (the old name still works but is deprecated).
- A plain `AppSecret` from configuration is now honored; in 3.x it had to be an already-protected value and was otherwise ignored.
- Editing `FacebookPluginPart` content now requires `ManageLiquidTemplates`.
- `Permission.Description` values are `LocalizationSource`; see `orchardcore-permission-providers`.
