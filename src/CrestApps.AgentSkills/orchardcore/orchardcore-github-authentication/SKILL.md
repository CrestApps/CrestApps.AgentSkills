---
name: orchardcore-github-authentication
description: Skill for configuring GitHub OAuth authentication in Orchard Core. Covers GitHub external login settings, OAuth callback paths, the GitHubAuthenticationSettings recipe step with ConsumerKey and ConsumerSecret, the generic Settings step, ClientSecretSecretName references to the Secrets module, ConfigureGitHubSettings with the OrchardCore Authentication GitHub configuration section, ExternalAuthentication dependencies, and user registration behavior. Use this skill when requests mention Orchard Core GitHub Authentication, GitHub OAuth, sign in with GitHub, GitHubAuthenticationSettings, ConfigureGitHubSettings, GitHub callback URL, or closely related Orchard Core implementation setup extension upgrade or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core GitHub Authentication

Enable `OrchardCore.GitHub.Authentication`; it depends on
`OrchardCore.Users.ExternalAuthentication`. Configure it at
**Settings → Security → Authentication → GitHub**.

The persisted `GitHubAuthenticationSettings` properties are `ClientID`,
`ClientSecret`, `ClientSecretSecretName` (4.0), `CallbackPath`, and `SaveTokens`. The default callback path is
`/signin-github`. Register the tenant's full HTTPS callback URL as the
Authorization callback URL of the GitHub OAuth App.

The integration uses the `AspNet.Security.OAuth.GitHub` package
(`GitHubAuthenticationDefaults`, `GitHubAuthenticationOptions`,
`GitHubAuthenticationHandler`). The old `GithubDefault`, `GithubOptions`,
`GithubHandler`, and `IGithubAuthenticationService` APIs were removed in 3.0.

## Recipe Steps

The dedicated `GitHubAuthenticationSettings` recipe step deliberately uses
`ConsumerKey` and `ConsumerSecret`, which the handler maps to the persisted
client ID and secret:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.GitHub.Authentication"
      ]
    },
    {
      "name": "GitHubAuthenticationSettings",
      "ConsumerKey": "github-oauth-client-id",
      "CallbackPath": "/signin-github"
    }
  ]
}
```

The generic `Settings` step accepts the persisted shape, including
`SaveTokens`:

```json
{
  "steps": [
    {
      "name": "Settings",
      "GitHubAuthenticationSettings": {
        "ClientID": "github-oauth-client-id",
        "CallbackPath": "/signin-github",
        "SaveTokens": false
      }
    }
  ]
}
```

The handler decrypts the stored client secret with data protection, as saved
from the admin page. A secret imported by a recipe is stored as is and fails
to decrypt, so enter the secret on the admin page. Both steps replace the
stored values, so re-running them clears a secret entered earlier.

## Keeping the Client Secret in a Secret (4.0)

`ClientSecretSecretName` names a `TextSecret` of the Secrets module and takes
precedence over `ClientSecret`. With the `OrchardCore.Secrets` feature enabled,
the client secret editor offers **Use a secret** (suggested name
`GitHub.ClientSecret`); a saved secret can be moved without retyping it from
**Tools → Security → Migrate Secrets**. The generic `Settings` step can set
`"ClientSecretSecretName": "GitHub.ClientSecret"` after a `Secrets` step
created the secret from the `OrchardCore__Secrets__GitHub.ClientSecret`
environment variable; the dedicated `GitHubAuthenticationSettings` step has no
secret-name property. See [orchardcore-secrets](../orchardcore-secrets/SKILL.md).

## Configuration Overrides

```csharp
builder.Services
    .AddOrchardCms()
    .ConfigureGitHubSettings();
```

```json
{
  "OrchardCore": {
    "Authentication": {
      "GitHub": {
        "ClientID": "github-oauth-client-id",
        "ClientSecret": "",
        "CallbackPath": "/signin-github",
        "SaveTokens": false
      }
    }
  }
}
```

Unlike the Facebook, Google, Microsoft Account, and X secrets, the GitHub
`ClientSecret` is not listed among the secrets that 4.0 accepts as plain
values from configuration; verify the behavior before relying on it and keep
client secrets out of committed configuration. In 4.0 the GitHub login handler
reads the site settings, not the bound `GitHubAuthenticationSettings` options,
so prefer a secret selected on the admin page. A nonblank configured
`ClientSecret` logs a `LegacySecretConfiguration` warning (event `8100`).

Enable `OrchardCore.Users.Registration` only when external visitors may create
local accounts; new external users are governed by
`ExternalRegistrationSettings` (see `orchardcore-users-roles`). Authentication
does not grant Orchard roles or permissions.

## Upgrading from 3.x

- Rename the `OrchardCore_GitHub` configuration section to `OrchardCore:Authentication:GitHub` (the old name still works but is deprecated).
- Saved client secrets keep working. Code reading `GitHubAuthenticationSettings.ClientSecret` directly must also honor `ClientSecretSecretName`, since moving it to a secret clears `ClientSecret`.
