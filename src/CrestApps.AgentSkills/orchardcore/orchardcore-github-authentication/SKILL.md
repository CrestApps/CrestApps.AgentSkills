---
name: orchardcore-github-authentication
description: Skill for configuring GitHub OAuth authentication in Orchard Core. Covers GitHub external login settings, OAuth callback paths, recipe configuration, configuration overrides, ExternalAuthentication dependencies, and user registration behavior. Use this skill when requests mention Orchard Core GitHub Authentication, GitHub OAuth, sign in with GitHub, GitHubAuthenticationSettings, ConfigureGitHubSettings, GitHub callback URL, or closely related Orchard Core implementation setup extension or troubleshooting work. Strong matches include work with OrchardCore.GitHub.Authentication, OrchardCore.Users.ExternalAuthentication, OrchardCore.GitHub.Settings, GitHubAuthenticationOptions, AuthenticationOptionsConfiguration, OAuthPostConfigureOptions, and GitHubAuthenticationSettingsStep. It also helps with app registration, admin setup, recipes, and the source-backed patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core GitHub Authentication

The GitHub module provides an external OAuth login feature. The exact feature
identifier is `OrchardCore.GitHub.Authentication`; it depends on
`OrchardCore.Users.ExternalAuthentication`. It configures the GitHub handler
from site settings and allows an authenticated external identity to link to an
Orchard user according to the enabled Users features.

## Guidelines

- Enable `OrchardCore.GitHub.Authentication`, not merely the module name.
- Enable `OrchardCore.Users.Registration` when new visitors may create local users through external login.
- Register a GitHub OAuth App with a callback URL matching the tenant host and configured callback path.
- Treat `ClientSecret` as a secret and provide it through secure configuration in production.
- Use the default callback path unless there is a routing conflict.
- Configure each tenant independently through Settings or recipe data.
- `SaveTokens` should be enabled only when the application needs to use the provider tokens later.
- Do not use a GitHub personal access token as the OAuth client secret.
- All recipe JSON uses the root `{ "steps": [...] }` format.

## Enable the Feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.GitHub.Authentication"
      ],
      "disable": []
    }
  ]
}
```

The feature adds the GitHub settings display driver, admin navigation,
`AuthenticationOptionsConfiguration`, `GitHubAuthenticationOptionsConfiguration`,
and OAuth post-configuration.

## Register a GitHub OAuth App

1. Create an OAuth App in GitHub for the application environment.
2. Enter the public homepage URL for the relevant Orchard site.
3. Set the authorization callback URL to the application URL plus the callback path.
4. Copy the generated client ID and client secret.
5. Configure the Orchard tenant before testing login.

For a default callback path of `/signin-github`, a site hosted at
`https://portal.example.com` needs the callback URL
`https://portal.example.com/signin-github`. Check the configured value rather
than assuming it when a tenant overrides it.

## Configure Through the Admin

Navigate to **Configuration → Settings → GitHub Authentication**. The backing
`GitHubAuthenticationSettings` properties are:

| Property | Purpose |
|---|---|
| `ClientID` | OAuth App client identifier. |
| `ClientSecret` | OAuth App client secret. |
| `CallbackPath` | Application-local sign-in callback path. |
| `SaveTokens` | Whether the handler persists access tokens. |

The login UI comes from the external authentication system. Existing users can
link an external identity where their account settings and enabled user
features allow it.

## Configure Through a Recipe

The feature registers `GitHubAuthenticationSettingsStep` when
`OrchardCore.Recipes.Core` is available:

```json
{
  "steps": [
    {
      "name": "GitHubAuthenticationSettings",
      "ClientID": "your-client-id",
      "ClientSecret": "your-client-secret",
      "CallbackPath": "/signin-github",
      "SaveTokens": false
    }
  ]
}
```

Use deployment or a secure secret provider instead of committing actual
credentials in a shared recipe.

## Override Settings From Configuration

Call `ConfigureGitHubSettings()` during host construction to post-configure
the tenant options from `OrchardCore_GitHub`:

```csharp
builder.ConfigureGitHubSettings();
```

```json
{
  "OrchardCore_GitHub": {
    "ClientID": "your-client-id",
    "ClientSecret": "your-client-secret",
    "CallbackPath": "/signin-github",
    "SaveTokens": false
  }
}
```

Configuration wins after the persisted site settings have been loaded. This is
appropriate for environment-specific secrets and callback addresses.

## Diagnose Failed Login

Verify the active tenant URL, the callback URL in GitHub, and `CallbackPath`
first. Then check that the exact authentication feature and external
authentication dependency are enabled. A provider callback mismatch is an OAuth
application configuration problem, not an Orchard controller route to create.

## Registration and Account Linking

External authentication establishes the provider identity. Whether it can
create a local Orchard account is controlled by the enabled Users registration
features and their policies. Enable registration only for the audiences that
should be allowed to join the tenant. Existing local users should link GitHub
from the supported external-logins UI rather than creating duplicate accounts.

## Security Review Checklist

- Register distinct OAuth Apps for local development, staging, and production.
- Limit callback URLs to the exact HTTPS addresses used by each environment.
- Rotate a leaked client secret in GitHub and update the secure host configuration.
- Review token persistence before setting `SaveTokens` to `true`.
- Verify external-login and registration policies after a tenant recipe imports settings.
- Remove old callback URLs and OAuth Apps when a site host name changes.

GitHub authentication identifies the user; application permissions still come
from Orchard Core users, roles, and permissions. Do not grant administrative
roles solely because a user authenticated through a particular provider.
