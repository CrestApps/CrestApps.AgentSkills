---
name: orchardcore-passkeys
description: Skill for passkeys, two-factor authentication and the user Security page in Orchard Core 4.0. Covers the OrchardCore.Users.Passkeys, OrchardCore.Users.Security and OrchardCore.Users.2FA features, passkey login, passkeys as multi-factor and as a two-factor method, the Security page at /Security and UserSecurity display drivers, the UserSecurityTwoFactor and UserSecurityPasskeys shapes, UserMenuItems-Security, UserSecuritySettings and TwoFactorLoginSettings recipes, the OrchardCore Users Passkeys configuration, admins removing passkeys, IUserPasskeyStore, User.Passkeys, UserByPasskeyIndex, rate limiting, and upgrading. Use this skill when requests mention Orchard Core passkeys, WebAuthn, passwordless login, two-factor authentication, 2FA, recovery codes, or the Security page. Strong matches include OrchardCore.Users.Passkeys, OrchardCore.Users.Security, UserSecurity, UserSecuritySettings, TwoFactorLoginSettings, IdentityPasskeyOptions, IUserPasskeyStore, UserPasskey, and UserByPasskeyIndex.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Passkeys, Two-Factor Authentication and the Security Page

You are an Orchard Core expert. Configure passkey sign-in and two-factor authentication, and extend the user Security page.

## Guidelines

- Enable `OrchardCore.Users.Passkeys` for passkeys; enable one or more two-factor methods (`OrchardCore.Users.2FA.AuthenticatorApp`, `OrchardCore.Users.2FA.Email`, `OrchardCore.Users.2FA.Sms`) for two-factor authentication. `OrchardCore.Users.Security` and `OrchardCore.Users.2FA` are enabled by dependency only.
- Passkeys are built on the ASP.NET Core Identity passkey support. The `UserStore` implements `IUserPasskeyStore<IUser>` whether or not the feature is enabled, so use the passkey methods of `UserManager<IUser>` instead of touching `User.Passkeys` directly.
- Users manage their security on the **Security** page (`/Security`, user menu → **Security**). Add a section to it with a `DisplayDriver<UserSecurity>`; never add a separate page and user menu item.
- The security pages use the admin theme unless `UserSecuritySettings.UseSiteTheme` is on. `TwoFactorLoginSettings.UseSiteTheme` no longer exists.
- Passkeys are a local login: with **Disable local login** on, users cannot log in with a passkey (or add one, unless passkeys are also a two-factor method).
- Set `OrchardCore:Users:Passkeys:ServerDomain` before users register passkeys on a site reached through several hosts; changing it later invalidates every registered passkey.
- Declare display drivers `internal sealed` unless another assembly must reference the type.

## Features

| Feature | ID | Depends on |
|---|---|---|
| User Security (enabled by dependency only) | `OrchardCore.Users.Security` | `OrchardCore.Users` |
| Two-Factor Authentication Services (enabled by dependency only) | `OrchardCore.Users.2FA` | `OrchardCore.Users`, `OrchardCore.Users.Security` |
| Two-Factor Authenticator App Method | `OrchardCore.Users.2FA.AuthenticatorApp` | `OrchardCore.Users`, `OrchardCore.Users.2FA` |
| Two-Factor Email Method | `OrchardCore.Users.2FA.Email` | `OrchardCore.Users`, `OrchardCore.Users.2FA`, `OrchardCore.Liquid`, `OrchardCore.Email` |
| Two-Factor SMS Method | `OrchardCore.Users.2FA.Sms` | `OrchardCore.Users`, `OrchardCore.Users.2FA`, `OrchardCore.Liquid`, `OrchardCore.Sms` |
| Passkeys | `OrchardCore.Users.Passkeys` | `OrchardCore.Users`, `OrchardCore.Users.Security` |

The constants are on `OrchardCore.Users.UserConstants.Features` (`Passkeys`, `UserSecurity`, `TwoFactorAuthentication`, `AuthenticatorApp`, `EmailAuthenticator`, `SmsAuthenticator`).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Users.Passkeys",
        "OrchardCore.Users.2FA.AuthenticatorApp"
      ]
    },
    {
      "name": "Settings",
      "TwoFactorLoginSettings": {
        "RequireTwoFactorAuthentication": false,
        "AllowRememberClientTwoFactorAuthentication": true,
        "NumberOfRecoveryCodesToGenerate": 5
      },
      "UserSecuritySettings": {
        "UseSiteTheme": false
      }
    }
  ]
}
```

Both settings are edited in **Settings → Security → User Login**: `TwoFactorLoginSettings` on the **Two-Factor Authentication** tab, `UserSecuritySettings` (**Use site theme for the security pages**) on the **Security Page** tab. Both require `ManageUsers`. A role can also require two-factor authentication for its users (`RoleLoginSettings`).

## The Security Page

`OrchardCore.Users.Security` maps `SecurityController.Index` to `UserOptions.SecurityPath` (default `Security`) for any authenticated user and adds the **Security** item to the user menu (`UserMenuItems-Security` shape, position `Content:15` for `Detail` and `DetailAdmin`). The page builds the `UserSecurity` shape from `IDisplayManager<UserSecurity>`:

| Section | Shape | Location | Added by |
|---|---|---|---|
| Two-Factor Authentication (status, preferred method, methods, recovery codes) | `UserSecurityTwoFactor` | `Content:5` | `OrchardCore.Users.2FA` |
| Passkeys (list, add, rename, remove) | `UserSecurityPasskeys` | `Content:10` | `OrchardCore.Users.Passkeys` |

With no section the page shows "There are no security settings to manage." The former two-factor page path (`UserOptions.TwoFactorAuthenticationPath`, default `TwoFactor`) redirects to the Two-Factor Authentication section; the short-lived `/Passkeys` path was removed.

```json
{
  "OrchardCore": {
    "Users": {
      "SecurityPath": "Security",
      "TwoFactorAuthenticationPath": "TwoFactor"
    }
  }
}
```

The page also renders in the site theme, which does not style the `ocat-*` classes, so section templates should space their content with Bootstrap utilities.

### Adding a Section

```csharp
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Models;

namespace MyModule.Drivers;

internal sealed class SessionsUserSecurityDisplayDriver : DisplayDriver<UserSecurity>
{
    public override IDisplayResult Display(UserSecurity model, BuildDisplayContext context)
    {
        if (model.User is not User user)
        {
            return null;
        }

        return Initialize<SessionsSecurityViewModel>("UserSecuritySessions", viewModel =>
        {
            viewModel.UserId = user.UserId;
        }).Location("Content:20");
    }
}
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace MyModule;

[RequireFeatures(UserConstants.Features.UserSecurity)]
public sealed class UserSecurityStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<UserSecurity, SessionsUserSecurityDisplayDriver>();
    }
}
```

`Views/UserSecuritySessions.cshtml` renders a card-like section; post its forms to your own controller and redirect back to the `UserSecurity` route.

## Passkeys

Enabling `OrchardCore.Users.Passkeys` lets users:

- Add, rename and remove passkeys in the **Passkeys** section of the Security page (up to `PasskeyService.MaxPasskeyCount` = 100 per user, since they live in the user document).
- Log in with **Log in with a passkey** on the login page (`LoginFormPasskey` shape added to the `LoginForm`) or by picking a passkey in the autofill of the username field, without typing a user name.
- Use a passkey as their second factor when the two-factor services are enabled (provider name `UserConstants.PasskeyTwoFactorProvider` = `Passkey`).

### Passkeys and Two-Factor Authentication

- With the default `UserVerificationRequirement` of `required`, a passkey login counts as multi-factor (`amr` claims `pop` and `mfa`): users with two-factor enabled are not asked for a second factor, and users whose role requires two-factor are not forced to set up a method. Logging in with a password still asks for both.
- With `preferred` or `discouraged`, a passkey is a single factor and users with two-factor enabled are asked for their second factor afterwards.
- Adding a passkey does not enable two-factor authentication, unless two-factor is required for the user. Removing the last passkey removes the passkey method, and is refused when it is the only method of a user who must use two-factor.
- The passkey two-factor method shows as a status in the Two-Factor Authentication section (like the verified email and phone methods), with the Passkeys section below it to add one.

### Configuration

The relying party ID defaults to the request host. Other options bind from `OrchardCore:Users:Passkeys` (values shown are the defaults):

```json
{
  "OrchardCore": {
    "Users": {
      "Passkeys": {
        "ServerDomain": null,
        "AuthenticatorTimeout": "00:05:00",
        "ChallengeSize": 32,
        "UserVerificationRequirement": "required",
        "ResidentKeyRequirement": "preferred",
        "AttestationConveyancePreference": null,
        "AuthenticatorAttachment": null
      }
    }
  }
}
```

| Option | Values |
|---|---|
| `ServerDomain` | Common registrable domain when the site has several hosts (`example.com` for `www.example.com` and `example.com`). Changing it invalidates registered passkeys. |
| `AuthenticatorTimeout` | How long the browser waits; the `Identity.Passkey` state cookie lives one minute longer. |
| `UserVerificationRequirement` | `required`, `preferred`, `discouraged`. Only `required` (exact value) makes a passkey multi-factor. |
| `ResidentKeyRequirement` | `required`, `preferred`, `discouraged`; discoverable passkeys allow login without a user name. |
| `AttestationConveyancePreference` | `none`, `indirect`, `direct`, `enterprise`. |
| `AuthenticatorAttachment` | `platform` or `cross-platform`; unset allows any authenticator. |

- The server only accepts a passkey used from the same origin (scheme, host, port) as the request. Behind a TLS-terminating reverse proxy, enable forwarded headers (see `orchardcore-reverse-proxy`).
- Tenants sharing a host with different URL prefixes share the relying party ID: the browser may offer one tenant's passkey on another tenant's login page, which refuses it.

### Administration

On **Access Control → Users**, a user who has passkeys gets a **Remove passkeys** item in the row's actions menu (`UserPasskeyActionsMenu` shape, `ActionsMenu:11` in both `SummaryAdmin` and `SummaryAdminGrid`), shown to users who can `EditUsers` for that user. It asks through the confirm dialog, posts to `AdminPasskeyController.RemoveAll`, removes every passkey in one update, and disables two-factor when passkeys were the user's only method.

### Rate Limiting

With `OrchardCore.RateLimits` enabled, POST `Passkeys/Login` (route name `RateLimitRouteNames.PasskeyLogin`) is limited to 10 requests per IP address with a sliding window, under the `password-authentication` policy shared with the login page (see `orchardcore-rate-limits`).

### Storage and API

- `User.Passkeys` is an `IList<UserPasskey>` (`CredentialId`, `PublicKey`, `Name`, `CreatedUtc`, `LastUsedUtc`, `SignCount`, `Transports`, `IsUserVerified`, `IsBackupEligible`, `IsBackedUp`, `AttestationObject`, `ClientDataJson`). It holds no secret.
- `UserByPasskeyIndex` (created by a migration of `OrchardCore.Users`) stores `CredentialIdHash`, a Base64Url SHA-256 of the credential id (`UserByPasskeyIndex.HashCredentialId(byte[])`), one record per passkey. `UserManager.FindByPasskeyIdAsync()` uses it so a passkey is registered only once.
- The **All Users** deployment step does not export passkeys (they are bound to the site's domain), and the users audit trail cannot snapshot `User.Passkeys`.
- A passkey login does not run a full user update, so user validators and user-updated handlers do not run on each login.

```csharp
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;

public sealed class PasskeyReport
{
    private readonly UserManager<IUser> _userManager;

    public PasskeyReport(UserManager<IUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<int> CountAsync(IUser user)
        => (await _userManager.GetPasskeysAsync(user)).Count;
}
```

## Upgrading

- `Views/TwoFactorAuthentication/Index.cshtml` is now the `UserSecurityTwoFactor` shape: move an override to `UserSecurityTwoFactor.cshtml`.
- The `UserMenuItems-TwoFactor` shape is replaced by `UserMenuItems-Security`; rename overrides.
- `TwoFactorLoginSettings.UseSiteTheme` moved to `UserSecuritySettings.UseSiteTheme` (a migration moves the stored value). Recipes must set it under `UserSecuritySettings`.
- Links to `/TwoFactor` keep working through the redirect; link to the `UserSecurity` route instead.
- The preferred two-factor method form binds with the `UserSecurity` prefix (fixed in 4.0, it failed to save before). Custom `UserSecurityTwoFactor` overrides must keep the field names the shape renders.

## Troubleshooting

| Symptom | Cause |
|---|---|
| No **Log in with a passkey** button | `OrchardCore.Users.Passkeys` disabled, or **Disable local login** is on. |
| Passkey refused after moving the site | The relying party ID (host or `ServerDomain`) changed; users must register new passkeys. |
| Passkey refused behind a proxy | Forwarded headers are off, so the scheme or host differs from the browser origin. |
| Users still asked for a second factor after a passkey login | `UserVerificationRequirement` is not exactly `required`. |
| **Add passkey** missing | Local login disabled and passkeys are not a two-factor method, or the user reached 100 passkeys. |
| Security page renders in the admin theme | Turn on **Use site theme for the security pages** (`UserSecuritySettings.UseSiteTheme`). |

## Related Skills

- `orchardcore-users-roles` for login, registration and role settings.
- `orchardcore-rate-limits` for the login policies.
- `orchardcore-display-management` for display drivers.
