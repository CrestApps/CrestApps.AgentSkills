---
name: orchardcore-ai-chat-copilot
description: Skill for configuring the GitHub Copilot chat orchestrator in Orchard Core using the CrestApps AI Chat Copilot module. Covers GitHub OAuth and API key authentication modes, encrypted tenant and profile credentials, Copilot callback flow, model, effort, and allow-all selection on AI profiles, profile templates, and chat interactions. Use this skill when requests mention Orchard Core Copilot Orchestrator, GitHub Copilot SDK, GitHub OAuth, Copilot API Key, CopilotAuthController, CopilotOptionsConfiguration, Copilot Profiles, Copilot Templates, or closely related CrestApps implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.AI.Chat.Copilot, CopilotOptionsConfiguration, CopilotAuthController, OrchardCoreCopilotCredentialStore, CopilotSettingsDisplayDriver, AIProfileCopilotDisplayDriver, ChatInteractionCopilotDisplayDriver.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Chat Copilot - Prompt Templates

## Configure the GitHub Copilot Chat Orchestrator

You are an Orchard Core expert. Generate configuration, recipes, and extension code for the GitHub Copilot SDK-based **chat orchestrator**. This module is an alternative orchestration runtime, not an Orchard AI provider connection or deployment provider.

### Guidelines
- Enable `CrestApps.OrchardCore.AI.Chat.Copilot` to select GitHub Copilot orchestration on profiles, profile-source templates, and Chat Interactions.
- `CopilotOptionsConfiguration` binds `CrestApps:AI:Copilot` shell configuration and then overlays configured `CopilotSettings`.
- Tenant settings take precedence when `AuthenticationType` is non-default or `HasStoredConfiguration()` is true.
- Use `GitHubOAuth` for user or profile GitHub credentials and `ApiKey` for a directly configured model provider.
- `NotConfigured` intentionally disables the Copilot orchestrator for a tenant.
- OAuth mode requires a GitHub OAuth App, a Copilot-entitled identity, and the callback URL `/copilot/OAuthCallback`.
- OAuth settings protect the client secret; API-key settings protect the provider API key with ASP.NET Core Data Protection.
- API-key mode supports `openai`, `azure`, and `anthropic` provider types.
- Azure API-key mode requires an Azure API version and API key. Keep the base URL at the Azure resource URL.
- Select a model, effort `Default`, `Low`, `Medium`, or `High`, and whether to allow all tool executions per profile, template, or interaction.
- In OAuth mode, interactions normally use encrypted user credentials. Profile edit flows can store credentials on the profile for shared profile-scoped access.
- Profile credentials are shared by everyone who uses that profile. Treat them as an administrator-controlled capability.
- `CopilotAuthController` validates local return URLs and uses `__popup__` only for the profile-editor popup flow.
- Install CrestApps packages in the web/startup project.

### Feature Overview

| Feature | Feature ID | Purpose |
|---|---|---|
| AI Services | `CrestApps.OrchardCore.AI` | Shared AI profile and orchestration infrastructure |
| AI Copilot Orchestrator | `CrestApps.OrchardCore.AI.Chat.Copilot` | Copilot SDK orchestration and tenant settings |
| AI Chat | `CrestApps.OrchardCore.AI.Chat` | Profile chat UI |
| AI Chat Interactions | `CrestApps.OrchardCore.AI.Chat.Interactions` | Ad-hoc interaction UI and SignalR hub |

### Install and Enable

Install the package in the web/startup project:

```shell
dotnet add package CrestApps.OrchardCore.AI.Chat.Copilot
```

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Copilot",
        "CrestApps.OrchardCore.AI.Chat"
      ],
      "disable": []
    }
  ]
}
```

### Shell Configuration

Provide non-secret defaults through the section used by `CopilotOptionsConfiguration`:

```json
{
  "CrestApps": {
    "AI": {
      "Copilot": {
        "AuthenticationType": "ApiKey",
        "ProviderType": "openai",
        "BaseUrl": "https://api.openai.com/v1",
        "DefaultModel": "gpt-4o",
        "WireApi": "completions"
      }
    }
  }
}
```

Use environment variables or a secret store for `ApiKey` and OAuth client secrets. Tenant settings can override shell values.

### Configure GitHub OAuth

Use OAuth when sessions should use models entitled to a GitHub identity:

1. Create a GitHub OAuth App.
2. Set its callback URL to `https://your-domain.example/copilot/OAuthCallback`.
3. Configure the client ID and client secret in **Settings → Artificial Intelligence → Copilot**.
4. Select **GitHub Signed-in User** authentication.
5. Ask the user to select **Sign in with GitHub**.
6. Select an available Copilot model after authorization.

The OAuth flow requests `user:email` and `read:org`. `CopilotAuthController.AuthorizeGitHub` generates the authorization redirect and `OAuthCallback` exchanges the code, stores the encrypted credential, and returns to a safe local URL or closes the popup.

### Configure API-Key Authentication

Use API-key mode when the Copilot orchestration API should call a configured provider without a Copilot subscription:

1. Go to **Settings → Artificial Intelligence → Copilot**.
2. Select **API Key** authentication.
3. Select `openai`, `azure`, or `anthropic`.
4. Set the base URL, encrypted API key, default model, and wire API format.
5. For Azure, set the Azure API version.
6. Save the settings.

Use `completions` for the compatible chat-completions wire format. Use `responses` only with a provider and model that support the Responses API.

### Select Copilot on a Profile

1. Edit an AI Profile.
2. Select **GitHub Copilot Orchestrator**.
3. In OAuth mode, complete GitHub sign-in if necessary.
4. Select a model and effort level.
5. Decide whether **Allow all tool executions** is appropriate.
6. Save the profile.

Copilot does not use the ordinary connection and deployment fields. `AIProfileCopilotDisplayDriver` instead stores `CopilotSessionMetadata`. The orchestrator resolves profile credentials before user credentials.

### Reuse Settings with a Profile Template

For a profile-source template, configure the Copilot model, effort, and allow-all setting once. `AIProfileTemplateCopilotDisplayDriver` copies those values when the template creates a profile.

Avoid profile-scoped OAuth credentials unless the profile is intentionally a shared service identity. A user-level credential is safer for individual users.

### Configure a Chat Interaction

Enable the interactions feature when creating ad-hoc sessions:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Copilot",
        "CrestApps.OrchardCore.AI.Chat.Interactions"
      ],
      "disable": []
    }
  ]
}
```

`ChatInteractionCopilotDisplayDriver` and `CopilotChatInteractionSettingsHandler` save Copilot model, reasoning effort, and allow-all settings as `CopilotSessionMetadata`.

### Authentication Callback Endpoints

| Route | Handler | Purpose |
|---|---|---|
| `/copilot/Authorize` | `AuthorizeGitHub` | Starts OAuth and validates the local return URL |
| `/copilot/OAuthCallback` | `OAuthCallback` | Exchanges the authorization code and stores credentials |
| `/copilot/api/status` | `CopilotAuthEndpoints` GET handler | Returns current-user status and tenant configuration state |
| `/copilot/api/models` | `CopilotAuthEndpoints` GET handler | Lists available models for the signed-in user |
| `/copilot/api/disconnect` | `CopilotAuthEndpoints` POST handler | Removes current-user OAuth credentials after antiforgery validation |

### Permission Management

`CopilotPermissionProvider` supplies `ManageCopilotSettings`, granted to Administrators by default. Restrict it to trusted operators because it controls OAuth application credentials and provider API keys.

### Security Checklist

- Never commit OAuth client secrets, OAuth tokens, or API keys.
- Use protected tenant settings only through the settings editor or a secure deployment process.
- Validate every OAuth callback URL exactly, including the `/copilot/OAuthCallback` path.
- Prefer user-scoped OAuth credentials over profile-scoped credentials.
- Enable allow-all tool execution only for profiles whose selected tools and users are trusted.
- Use normal AI Profile and AI Tool permissions in addition to Copilot settings permissions.
