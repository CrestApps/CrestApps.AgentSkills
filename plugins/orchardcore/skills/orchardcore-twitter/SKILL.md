---
name: orchardcore-twitter
description: Skill for integrating X Twitter in Orchard Core. Covers TwitterClient, X API settings, sign in with X Twitter, TwitterSettings, TwitterSigninSettings, ConfigureTwitterSettings with the OrchardCore X configuration section, ConsumerSecret and AccessTokenSecret from configuration or environment variables, recipes, and workflow status update activities. Use this skill when requests mention Orchard Core Twitter, Orchard Core X, TwitterClient, sign in with X, TwitterSettings, UpdateTwitterStatusTask, X workflow activity, or closely related Orchard Core implementation setup extension or troubleshooting work. Strong matches include work with OrchardCore.Twitter, OrchardCore.Twitter.Signin, OrchardCore.Twitter.Services, OrchardCore.Twitter.Workflows, ITwitterSettingsService, ITwitterSigninService, TwitterOptionsConfiguration, and UpdateTwitterStatusTask. It also helps with OAuth credentials, external authentication, recipes, and the source-backed patterns captured in this skill.
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
- Use the feature’s admin settings, `ConfigureTwitterSettings()`, or `ITwitterSettingsService` for tenant credentials.
- Sign in with X reuses the API `ConsumerKey` and `ConsumerSecret`; its own settings only hold `CallbackPath` and `SaveTokens`.
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

Configure them at **Settings → Integrations → X (Twitter)**. `TwitterSettings` contains the OAuth credentials used by `TwitterClient`:

| Property | Purpose |
|---|---|
| `ConsumerKey` | Application consumer key. |
| `ConsumerSecret` | Application consumer secret. |
| `AccessToken` | Account access token for API operations. |
| `AccessTokenSecret` | Account access-token secret. |

The recipe step name is `TwitterSettings` (the generic `Settings` step with a
`TwitterSettings` object works too). The client decrypts the stored secrets
with data protection, as saved from the admin page, so secrets imported by a
recipe are stored as is and fail to decrypt. Prefer the admin page or
configuration for `ConsumerSecret` and `AccessTokenSecret`; the step replaces
all four values, so re-running it clears secrets entered earlier.

```json
{
  "steps": [
    {
      "name": "TwitterSettings",
      "ConsumerKey": "consumer-key",
      "AccessToken": "access-token"
    }
  ]
}
```

For configuration overrides, call `ConfigureTwitterSettings()` on the
`OrchardCoreBuilder` and use the `OrchardCore:X` section:

```csharp
builder.Services
    .AddOrchardCms()
    .ConfigureTwitterSettings();
```

```json
{
  "OrchardCore": {
    "X": {
      "ConsumerKey": "consumer-key",
      "ConsumerSecret": "plain-consumer-secret",
      "AccessToken": "access-token",
      "AccessTokenSecret": "plain-access-token-secret"
    }
  }
}
```

`ConsumerSecret` and `AccessTokenSecret` are plain values here (for example
from `OrchardCore__X__ConsumerSecret` environment variables or a key vault);
Orchard Core protects them before use.

## Sign In With X

Configure **Settings → Security → Authentication → Sign In with X (Twitter)**.
The sign-in settings model exposes `CallbackPath` and `SaveTokens`; the
consumer key and secret come from the API settings above. Set `CallbackPath`
explicitly (for example `/signin-x`) and register the same full tenant URL as
the X app's callback URL. The sign-in startup registers
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

## Upgrading from 3.x

- Rename the `OrchardCore_X` or `OrchardCore_Twitter` configuration section to `OrchardCore:X` (old names still work but are deprecated).
- Plain `ConsumerSecret` and `AccessTokenSecret` values from configuration are now honored; in 3.x they had to be already protected and were otherwise ignored.
- `TwitterClient` no longer fails after its first request; it no longer overwrites the shared settings' encrypted secrets with decrypted values.
