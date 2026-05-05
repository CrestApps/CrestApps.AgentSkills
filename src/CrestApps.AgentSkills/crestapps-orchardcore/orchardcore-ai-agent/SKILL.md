---
name: orchardcore-ai-agent
description: Skill for the CrestApps Orchard Core AI Agent module that provides 50+ natural-language AI tools for managing an Orchard Core site. Covers content management, content definitions, feature toggling, tenant management, recipe execution, user and role lookup, workflow management, email/SMS/notification sending, AI profile inspection, and chat analytics. Use this skill when requests mention AI Agent tools, natural language site management, CrestApps.OrchardCore.AI.Agent, AI-powered content operations, or closely related Orchard Core AI tool integration and troubleshooting work. Strong matches include searchForContentItems, createOrUpdateContentItem, importOrchardCoreRecipe, createTenant, enableSiteFeature, sendEmail, sendNotification, listWorkflowTypes, listAIProfiles, queryChatSessionMetrics, and the code patterns, admin flows, recipe steps, and tool categories captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Agent - Prompt Templates

## Manage an Orchard Core Site with AI Tools

You are an Orchard Core expert. Generate configuration and guidance for enabling and using the CrestApps AI Agent module to manage an Orchard Core site through natural language.

### Guidelines

- Use feature ID `CrestApps.OrchardCore.AI.Agent`.
- The module depends on `CrestApps.OrchardCore.AI` and `CrestApps.OrchardCore.Recipes`.
- All tools are selectable per AI profile (toggled on/off individually).
- Tools are conditionally loaded based on which Orchard Core features are enabled.
- Install the NuGet package into the web/startup project.

### Feature Overview

| Feature | Feature ID | Description |
|---------|-----------|-------------|
| Orchard Core AI Agent | `CrestApps.OrchardCore.AI.Agent` | Provides AI-powered tools for managing content, definitions, features, tenants, recipes, users, roles, workflows, communications, AI profiles, and analytics through natural language |

### Install the NuGet Package

```shell
dotnet add package CrestApps.OrchardCore.AI.Agent
```

### Enable the Feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Agent"
      ],
      "disable": []
    }
  ]
}
```

### How It Works

When the `CrestApps.OrchardCore.AI.Agent` feature is enabled, the module registers AI tools that can be invoked through any AI profile. Each tool is:

- **Selectable** - can be individually toggled on/off per AI profile from the admin UI.
- **Permission-aware** - respects Orchard Core's built-in permissions system.
- **Feature-gated** - only loads when its required Orchard Core feature is enabled.

### Tool Categories and Available Tools

The AI Agent module provides tools across these categories. Each tool is registered via `AddCoreAITool<T>()` and marked `.Selectable()`.

#### System Tools

Available when the base feature is enabled.

| Tool Name | Description |
|-----------|-------------|
| `listTimeZones` | Retrieves a list of available time zones in the system |

#### Recipe Tools

Requires `OrchardCore.Recipes.Core`.

| Tool Name | Description |
|-----------|-------------|
| `applySiteSettings` | Applies predefined system configurations and settings using AI assistance |
| `getOrchardCoreRecipeJsonSchema` | Returns a JSON Schema definition for Orchard Core recipes or a specific recipe step |
| `listOrchardCoreRecipeStepsAndSchemas` | Lists all available Orchard Core recipe steps and returns their JSON schema definitions |
| `importOrchardCoreRecipe` | Imports and runs an Orchard Core recipe within the site |
| `listNonStartupRecipes` | Retrieves all available recipes that are not executed during startup |
| `executeNonStartupRecipe` | Executes a recipe that is not configured to run at application startup |

#### Tenant Management Tools

Requires `OrchardCore.Tenants`.

| Tool Name | Description |
|-----------|-------------|
| `listStartupRecipes` | Retrieves startup recipes available for tenant setup |
| `createTenant` | Creates a new tenant in the Orchard Core application |
| `getTenant` | Retrieves detailed information about a specific tenant |
| `listTenant` | Returns information about all tenants in the system |
| `enableTenant` | Enables a tenant that is currently disabled |
| `disableTenant` | Disables a tenant that is currently active |
| `removeTenant` | Removes an existing tenant that can be safely deleted |
| `reloadTenant` | Reloads the configuration and state of an existing tenant |
| `setupTenant` | Sets up a new tenant with initial configuration |

#### Content Management Tools

Requires `OrchardCore.Contents`.

| Tool Name | Description |
|-----------|-------------|
| `searchForContentItems` | Searches for content items using filters and queries |
| `getSampleContentItemForContentType` | Generates a structured sample content item for a specified content type |
| `publishContentItem` | Publishes a draft or previously unpublished content item |
| `unpublishContentItem` | Unpublishes a currently published content item |
| `getContentItemById` | Retrieves a specific content item by its ID or type |
| `deleteContentItem` | Deletes a content item from the system |
| `cloneContentItem` | Creates a duplicate of an existing content item |
| `createOrUpdateContentItem` | Creates a new content item or updates an existing one |
| `getLinkForContentItem` | Retrieves a link for a content item |

#### Content Definition Tools

Requires `OrchardCore.ContentTypes`. The last three tools also require `OrchardCore.Recipes.Core`.

| Tool Name | Description |
|-----------|-------------|
| `getContentTypeDefinition` | Retrieves the definitions of all available content types |
| `getContentPartDefinition` | Retrieves the definitions of all available content parts |
| `listContentTypesDefinitions` | Lists available content type definitions |
| `listContentPartsDefinitions` | Lists available content part definitions |
| `listContentFieldDefinitions` | Lists available content field definitions |
| `removeContentTypeDefinition` | Removes a content type definition |
| `removeContentPartDefinition` | Removes a content part definition |
| `applyContentTypeDefinitionFromRecipe` | Creates or updates a content type definition via recipe |

#### Feature Management Tools

Requires `OrchardCore.Features`.

| Tool Name | Description |
|-----------|-------------|
| `disableSiteFeature` | Disables one or more site features |
| `enableSiteFeature` | Enables one or more site features |
| `searchSiteFeature` | Searches available features for a match |
| `listSiteFeature` | Retrieves all available site features |
| `getSiteFeature` | Retrieves information about a specific feature |

#### Communication Tools

Each tool requires its respective Orchard Core feature.

| Tool Name | Required Feature | Description |
|-----------|-----------------|-------------|
| `sendNotification` | `OrchardCore.Notifications` | Sends a notification message to a user |
| `sendEmail` | `OrchardCore.Email` | Sends an email message on behalf of the logged-in user |
| `sendSmsMessage` | `OrchardCore.Sms` | Sends an SMS message to a user |

#### User Management Tools

Requires `OrchardCore.Users`.

| Tool Name | Description |
|-----------|-------------|
| `getUserInfo` | Gets information about a user |
| `searchForUsers` | Searches the system for users |

#### Role Management Tools

Requires `OrchardCore.Roles`.

| Tool Name | Description |
|-----------|-------------|
| `getRoleInfo` | Gets information about a specific role |

#### Workflow Management Tools

Requires `OrchardCore.Workflows`. The last two tools also require `OrchardCore.Recipes.Core`.

| Tool Name | Description |
|-----------|-------------|
| `getWorkflowType` | Gets information about a specific workflow type |
| `listWorkflowTypes` | Lists all workflow types in the system |
| `createOrUpdateWorkflow` | Creates or updates a workflow definition |
| `listWorkflowActivities` | Lists all available workflow tasks and activities |

#### AI Profile Tools

Available when the base feature is enabled.

| Tool Name | Description |
|-----------|-------------|
| `listAIProfiles` | Lists AI profiles with optional filters for type, analytics, data extraction, and post-session processing |
| `viewAIProfile` | Retrieves detailed configuration for a specific AI profile by ID or name |

#### AI Analytics Tools

Requires the Chat Analytics feature.

| Tool Name | Description |
|-----------|-------------|
| `queryChatSessionMetrics` | Queries aggregated chat session analytics metrics with optional date range and profile filters for generating charts and reports |

### Tool Selectability per AI Profile

Each registered tool is marked as **selectable**, meaning administrators can control which tools an AI profile has access to. To configure tool access:

1. Navigate to **Artificial Intelligence → AI Profiles**.
2. Edit the desired profile.
3. In the **Tools** section, toggle individual tools on or off.

This allows creating specialized profiles. For example, a "Content Editor" profile might only have content management tools enabled, while an "Admin" profile has all tools enabled.

### Feature Dependencies and Tool Availability

Tools are **automatically registered** when their required Orchard Core features are enabled. The following table summarizes the feature dependencies:

| Tool Category | Required Features |
|---------------|------------------|
| System | Base feature only |
| Recipes | `OrchardCore.Recipes.Core` |
| Tenant Management | `OrchardCore.Tenants` |
| Content Management | `OrchardCore.Contents` |
| Content Definitions | `OrchardCore.ContentTypes` (and `OrchardCore.Recipes.Core` for create/update/remove) |
| Feature Management | `OrchardCore.Features` |
| Notifications | `OrchardCore.Notifications` |
| Email | `OrchardCore.Email` |
| SMS | `OrchardCore.Sms` |
| Users | `OrchardCore.Users` |
| Roles | `OrchardCore.Roles` |
| Workflows | `OrchardCore.Workflows` (and `OrchardCore.Recipes.Core` for create/update) |
| AI Profiles | Base feature only |
| AI Analytics | Chat Analytics feature |

### Enable Additional Features for More Tools

To make all tools available, enable the relevant Orchard Core features:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Agent",
        "OrchardCore.Contents",
        "OrchardCore.ContentTypes",
        "OrchardCore.Features",
        "OrchardCore.Tenants",
        "OrchardCore.Recipes.Core",
        "OrchardCore.Users",
        "OrchardCore.Roles",
        "OrchardCore.Workflows",
        "OrchardCore.Email",
        "OrchardCore.Sms",
        "OrchardCore.Notifications"
      ],
      "disable": []
    }
  ]
}
```

### Example - Create Content via AI

Once the AI Agent is enabled with content tools, an AI profile can handle requests like "Create a new Blog Post titled Getting Started" by invoking the `createOrUpdateContentItem` tool internally.

The tool constructs and saves the content item through the standard Orchard Core content management pipeline, respecting all permissions, content handlers, and workflow triggers.

### Example - Manage Tenants via AI

With tenant tools enabled, requests like "Create a new tenant called marketing-site" invoke the `createTenant` tool. The AI agent can also list, enable, disable, reload, and remove tenants.

### Security Considerations

- All tools respect Orchard Core's permission system. Users can only perform actions they are authorized for.
- Tools run in the context of the authenticated user's permissions.
- Use AI profile tool selectability to limit which operations are exposed.
- Audit trail integration captures tool invocations when the Audit Trail feature is enabled.
