---
name: orchardcore-github-authentication
description: Skill for configuring GitHub OAuth authentication in Orchard Core. Covers GitHub external login settings, OAuth callback paths, ConsumerKey and ConsumerSecret recipes, configuration overrides, ExternalAuthentication dependencies, and user registration behavior. Use this skill when requests mention Orchard Core GitHub Authentication, GitHub OAuth, sign in with GitHub, GitHubAuthenticationSettings, ConfigureGitHubSettings, GitHub callback URL, or closely related Orchard Core implementation setup extension or troubleshooting work.
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
`ClientSecret`, `CallbackPath`, and `SaveTokens`. The default callback path is
`/signin-github`. Register the tenant's full HTTPS callback URL with the
GitHub OAuth App.

### Orchard Core 3.0 API changes

The GitHub integration uses the `AspNet.Security.OAuth.GitHub` package. The
old `GithubDefault`, `GithubOptions`, `GithubHandler`, and
`IGithubAuthenticationService` APIs are removed. Use the package's
`GitHubAuthenticationDefaults`, `GitHubAuthenticationOptions`, and
`GitHubAuthenticationHandler` types, or the Orchard Core settings and
registration APIs described below.

The `GitHubAuthenticationSettings` recipe step deliberately uses
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
      "ConsumerSecret": "supply-from-a-secret-store",
      "CallbackPath": "/signin-github"
    }
  ]
}
```

Use `ConfigureGitHubSettings()` for host configuration overrides, and keep
client secrets out of committed configuration. Enable
`OrchardCore.Users.Registration` only when external visitors may create local
accounts. Authentication does not grant Orchard roles or permissions.
