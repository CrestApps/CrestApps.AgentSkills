---
name: orchardcore-facebook
description: Skill for configuring Facebook and Meta integrations in Orchard Core. Covers Meta App core components, Meta Login authentication, Social Plugins widgets, Meta Pixel tracking, FacebookCoreSettings, and FacebookLoginSettings recipes. Use this skill when requests mention Orchard Core Facebook/Meta Integration, Meta Core Components, Meta App Settings, Meta Login Authentication, Meta Login Settings, Meta Social Plugins Widgets, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
---

# Orchard Core Facebook and Meta Integration

`OrchardCore.Facebook` is the Meta core feature. It is
`EnabledByDependencyOnly`; do not try to enable it directly. Enable a
dependent feature instead:

| Feature ID | Purpose |
|---|---|
| `OrchardCore.Facebook.Login` | External Meta authentication |
| `OrchardCore.Facebook.Widgets` | Meta social-plugin widgets |
| `OrchardCore.Facebook.Pixel` | Meta Pixel tracking |

The Login and Widgets features bring in the core feature. Pixel is independent.

## Meta App Settings

Configure the core app at **Settings → Integrations → Meta App**. The backing
`FacebookSettings` properties are `AppId`, `AppSecret`, `FBInit`,
`FBInitParams`, `SdkJs`, and `Version`.

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
      "AppSecret": "supply-from-a-secret-store",
      "FBInit": true,
      "FBInitParams": "status: true, xfbml: true, autoLogAppEvents: true",
      "SdkJs": "sdk.js",
      "Version": "v3.2"
    }
  ]
}
```

Use `ConfigureFacebookSettings()` only when host configuration must override
the stored core settings. Keep the app secret out of source control.

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

`FacebookLoginSettings` persists the callback path. `SaveTokens` is a runtime
setting and is not part of this recipe step.

Enable `OrchardCore.Users.Registration` only when external users should be
able to create local accounts.

## Widgets and Pixel

Enable `OrchardCore.Facebook.Widgets` to add the `FacebookPluginPart` content
part to widget content types. Enable `OrchardCore.Facebook.Pixel` and configure
its pixel setting separately. Load `fbsdk` in templates when a page needs the
SDK:

```liquid
{% scriptresource name: "fbsdk" %}
```
