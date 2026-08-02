---
name: orchardcore-twitter
description: Skill for integrating X Twitter in Orchard Core. Covers TwitterClient, X API settings, sign in with X Twitter, TwitterSettings, TwitterSigninSettings, configuration overrides, recipes, and workflow status update activities. Use this skill when requests mention Orchard Core Twitter, Orchard Core X, TwitterClient, sign in with X, TwitterSettings, UpdateTwitterStatusTask, X workflow activity, or closely related Orchard Core implementation setup extension or troubleshooting work. Strong matches include work with OrchardCore.Twitter, OrchardCore.Twitter.Signin, OrchardCore.Twitter.Services, OrchardCore.Twitter.Workflows, ITwitterSettingsService, ITwitterSigninService, TwitterOptionsConfiguration, and UpdateTwitterStatusTask. It also helps with OAuth credentials, external authentication, recipes, and the source-backed patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core X Twitter Integration

The X Twitter module has two distinct features. `OrchardCore.Twitter` configures
the API client and workflow status activity. `OrchardCore.Twitter.Signin`
depends on it and `OrchardCore.Users.ExternalAuthentication` to add external
sign-in. Configure API credentials with the minimum provider permissions
required for the selected capability.

## Guidelines

- Enable `OrchardCore.Twitter` for the API client and status-update workflow task.
- Enable `OrchardCore.Twitter.Signin` only when X external login is required.
- Enable Users registration separately when external identities may create new local users.
- Store consumer secrets and access-token secrets in secure configuration.
- `TwitterClient` is registered through `AddHttpClient` with bounded retry behavior.
- `UpdateTwitterStatusTask` returns `Done` or `Failed` and stores the response in `TwitterResponse`.
- Use the feature’s admin settings or `ITwitterSettingsService` for tenant credentials.
- Do not log provider credentials or raw failure response content in production.
- All recipe JSON uses the root `{ "steps": [...] }` format.

## Enable X Integration

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Twitter"
      ],
      "disable": []
    }
  ]
}
```

For sign-in, add `OrchardCore.Twitter.Signin`:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Twitter",
        "OrchardCore.Twitter.Signin"
      ],
      "disable": []
    }
  ]
}
```

## Configure API Credentials

`TwitterSettings` contains the OAuth credentials used by `TwitterClient`:

| Property | Purpose |
|---|---|
| `ConsumerKey` | Application consumer key. |
| `ConsumerSecret` | Application consumer secret. |
| `AccessToken` | Account access token for API operations. |
| `AccessTokenSecret` | Account access-token secret. |

The recipe step name is `TwitterSettings`:

```json
{
  "steps": [
    {
      "name": "TwitterSettings",
      "ConsumerKey": "consumer-key",
      "ConsumerSecret": "consumer-secret",
      "AccessToken": "access-token",
      "AccessTokenSecret": "access-token-secret"
    }
  ]
}
```

For configuration overrides, call `ConfigureTwitterSettings()`. The extension
uses `OrchardCore_X` first and falls back to `OrchardCore_Twitter`:

```csharp
builder.ConfigureTwitterSettings();
```

```json
{
  "OrchardCore_X": {
    "ConsumerKey": "consumer-key",
    "ConsumerSecret": "consumer-secret",
    "AccessToken": "access-token",
    "AccessTokenSecret": "access-token-secret"
  }
}
```

## Sign In With X

The sign-in settings model exposes `CallbackPath` and `SaveTokens`. Register
the X application callback URL for the active tenant and configured path, then
set the values through the sign-in settings UI. The sign-in startup registers
the authentication and provider-specific options configurations plus the
external login display driver.

## Update X Status in a Workflow

Enable `OrchardCore.Workflows` in addition to `OrchardCore.Twitter`. The module
registers **Update X (Twitter) Status Task**. Its `StatusTemplate` is evaluated
through the workflow expression evaluator before `TwitterClient.UpdateStatus`
is called.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Twitter",
        "OrchardCore.Workflows"
      ],
      "disable": []
    }
  ]
}
```

Branch on **Failed** and avoid blindly retrying status updates in a workflow,
because a request may have reached the provider even if the response was lost.

## Use TwitterClient Programmatically

Inject the registered typed client rather than creating an unauthenticated
`HttpClient`:

```csharp
using OrchardCore.Twitter.Services;

namespace MyModule;

public sealed class SocialUpdateService
{
    private readonly TwitterClient _twitterClient;

    public SocialUpdateService(TwitterClient twitterClient)
    {
        _twitterClient = twitterClient;
    }

    public Task<HttpResponseMessage> PublishAsync(string status)
    {
        return _twitterClient.UpdateStatus(status);
    }
}
```

Check the response status before reporting success and respect X platform
limits and policies.

