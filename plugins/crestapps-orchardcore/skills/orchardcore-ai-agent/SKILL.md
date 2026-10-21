---
name: orchardcore-ai-agent
description: Skill for the CrestApps Orchard Core AI Agent module (CrestApps.OrchardCore.AI.Agent) and agent profiles. Covers the 50+ Orchard-aware AI tools it registers and the Orchard Core feature that enables each group (content, content definitions, features, tenants, recipes, users, roles, workflows, email, SMS, notifications, AI profiles, chat analytics), system tools such as getContentItemSchema and tool dependencies, recipe-schema-driven tools that need CrestApps.OrchardCore.Recipes, AI profiles of type Agent with AgentMetadata Availability (OnDemand or AlwaysAvailable) and AgentInvocationMetadata, attaching agents to other profiles, exposing agents through the A2A host and the MCP server, and the security model for agent tools. Use this skill when requests mention AI Agent tools, natural-language site management, agent profiles, searchForContentItems, createOrUpdateContentItem, importOrchardCoreRecipe, createTenant, enableSiteFeature, sendEmail, listWorkflowTypes, listAIProfiles or queryChatSessionMetrics.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Agent

The AI Agent module registers Orchard-aware tools that let an AI profile manage the site in natural language: content, content definitions, features, tenants, recipes, users, roles, workflows, communications, and AI profiles. Agent **profiles** (profiles of type Agent) are a separate concept that comes from the base AI module and can use these tools.

## Guidelines

- Feature ID `CrestApps.OrchardCore.AI.Agent`; it depends on `CrestApps.OrchardCore.AI` and `CrestApps.OrchardCore.Recipes`. Install `CrestApps.OrchardCore.AI.Agent` in the web/startup project.
- Each tool group registers only when its Orchard Core feature is enabled.
- Almost every tool is **selectable**: it appears grouped by category in the **Tools** section of the **Capabilities** tab, and nothing runs until a profile, template, or interaction selects it.
- `getContentItemSchema` is a **system** tool (auto-included, never in pickers). `createOrUpdateContentItem` depends on it and `importOrchardCoreRecipe` depends on `getOrchardCoreRecipeJsonSchema`, so selecting the main tool brings in its schema tool.
- AI profile sessions run the profile as configured and the tools do not re-check site permissions, so attach management tools only to profiles whose query permission is restricted (see `orchardcore-ai-tools`).

## Enable

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
        "OrchardCore.Recipes.Core",
        "OrchardCore.Users",
        "OrchardCore.Roles",
        "OrchardCore.Workflows"
      ],
      "disable": []
    }
  ]
}
```

Add `OrchardCore.Tenants`, `OrchardCore.Email`, `OrchardCore.Sms`, `OrchardCore.Notifications`, or `CrestApps.OrchardCore.AI.Chat.Analytics` only when you want those tool groups.

## Tool catalog

| Group | Requires | Tools |
|---|---|---|
| System and AI profiles | Base feature | `listTimeZones`, `listAIProfiles`, `viewAIProfile` |
| Recipes | `OrchardCore.Recipes.Core` | `applySiteSettings`, `getOrchardCoreRecipeJsonSchema`, `listOrchardCoreRecipeStepsAndSchemas`, `importOrchardCoreRecipe`, `listNonStartupRecipes`, `executeNonStartupRecipe` |
| Tenants | `OrchardCore.Tenants` | `listStartupRecipes`, `createTenant`, `getTenant`, `listTenant`, `enableTenant`, `disableTenant`, `removeTenant`, `reloadTenant`, `setupTenant` |
| Content | `OrchardCore.Contents` | `searchForContentItems`, `getSampleContentItemForContentType`, `publishContentItem`, `unpublishContentItem`, `getContentItemById`, `deleteContentItem`, `cloneContentItem`, `createOrUpdateContentItem`, `getLinkForContentItem` |
| Content schema (system) | `OrchardCore.Contents` + `CrestApps.OrchardCore.Recipes` | `getContentItemSchema` |
| Content definitions | `OrchardCore.ContentTypes` | `getContentTypeDefinition`, `getContentPartDefinition`, `listContentTypesDefinitions`, `listContentPartsDefinitions`, `listContentFieldDefinitions` |
| Content definition changes | `OrchardCore.ContentTypes` + `OrchardCore.Recipes.Core` | `removeContentTypeDefinition`, `removeContentPartDefinition`, `applyContentTypeDefinitionFromRecipe` |
| Features | `OrchardCore.Features` | `disableSiteFeature`, `enableSiteFeature`, `searchSiteFeature`, `listSiteFeature`, `getSiteFeature` |
| Notifications | `OrchardCore.Notifications` | `sendNotification` |
| Email | `OrchardCore.Email` | `sendEmail` |
| SMS | `OrchardCore.Sms` | `sendSmsMessage` |
| Users | `OrchardCore.Users` | `getUserInfo`, `searchForUsers` |
| Roles | `OrchardCore.Roles` | `getRoleInfo` |
| Workflows | `OrchardCore.Workflows` | `getWorkflowType`, `listWorkflowTypes` |
| Workflow changes | `OrchardCore.Workflows` + `OrchardCore.Recipes.Core` | `createOrUpdateWorkflow`, `listWorkflowActivities` |
| Analytics | `CrestApps.OrchardCore.AI.Chat.Analytics` | `queryChatSessionMetrics` |

### Schema-driven writes

- Call `getOrchardCoreRecipeJsonSchema` before `importOrchardCoreRecipe`. It returns the root `{ "steps": [...] }` schema and can expand one step while still listing every valid step name. Recipe steps contributed by enabled features (including CrestApps steps such as `Customers` or `IndexUsers`) are listed by these tools and by the MCP recipe-step resource.
- Call `getContentItemSchema` for the parent and nested content types before `createOrUpdateContentItem`, and send nested items (`BagPart`, `FlowPart`, widgets) inside the parent payload. With `CrestApps.OrchardCore.Recipes` enabled the payload is validated against the content type's recipe schema and corrective guidance is returned when it does not match.

## Agent profiles

An AI profile with **Profile type** `Agent` is a reusable specialist that other AI experiences can call.

| Field | Stored in | Meaning |
|---|---|---|
| **Description** | `AIProfile.Description` | What the agent does; used by orchestrators, A2A cards, and MCP tool descriptions |
| **Availability** | `AgentMetadata.Availability` | `OnDemand` (selectable per profile) or `AlwaysAvailable` (offered to every profile) |

Select on-demand agents in the **Agents** section of another profile's or template's **Capabilities** tab; the selection is stored in `AgentInvocationMetadata.Names`. Always-available agents are not listed there because they are always offered.

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Name": "content-editor-agent",
          "DisplayText": "Content Editor Agent",
          "Description": "Finds, drafts, and publishes site content on request.",
          "Type": "Agent",
          "ChatDeploymentName": "default-chat",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You manage site content. Confirm before deleting anything."
            },
            "AgentMetadata": {
              "Availability": "OnDemand"
            },
            "FunctionInvocationMetadata": {
              "Names": [ "searchForContentItems", "createOrUpdateContentItem", "publishContentItem" ]
            }
          }
        }
      ]
    }
  ]
}
```

Agent profiles can be exposed outside the site:

- **A2A** - every Agent profile is published by the A2A Host feature (`orchardcore-ai-a2a-host`).
- **MCP** - name the profile in `McpServerOptions.Agents` (or set `ExposeAllAgents`) to expose it as an MCP tool (`orchardcore-ai-mcp-server`).

## Security

- The tools call Orchard Core services directly (for example `deleteContentItem` calls `IContentManager.RemoveAsync`) and do **not** run their own content, feature, or tenant permission checks in the current source. The tool permission and the profile permission are the gates.
- Chat interactions re-check each selected tool's `AccessAITool_{toolName}` at send time. AI profile sessions do not, so restrict who may query a profile that carries management tools.
- Background callers (workflows, recipes) have no user and skip tool access checks.
- Keep destructive groups (tenants, features, content deletion, recipe import) on dedicated, tightly permissioned profiles.

## Troubleshooting

| Symptom | Check |
|---|---|
| A tool group is missing from the picker | Its Orchard Core feature is disabled, or the editing user lacks the tool permission. |
| `createOrUpdateContentItem` produces invalid items | Enable `CrestApps.OrchardCore.Recipes` so `getContentItemSchema` is registered and payloads are validated. |
| An agent is not offered on another profile | It is `AlwaysAvailable` (already included) or not of type Agent. |
| A2A clients see no agents | No profile has type Agent, or the A2A Host feature is disabled. |
