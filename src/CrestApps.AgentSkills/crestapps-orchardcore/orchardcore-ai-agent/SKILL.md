---
name: orchardcore-ai-agent
description: Skill for the CrestApps Orchard Core AI Agent module (CrestApps.OrchardCore.AI.Agent) and agent profiles. Covers the Orchard-aware AI tools it registers and the feature that enables each group (content, content definitions, features, tenants, recipes, users, roles, workflows, email, SMS, notifications, AI profiles, AI conversations, AI quality, chat analytics), system tools and tool dependencies, AI profiles of type Agent with AgentMetadata Availability and AllowToolInvocation, AgentInvocationMetadata, the ready-made Content, Workflow and AI Operations agent templates, agents versus tools, exposing agents through A2A and MCP, and the security model for agent tools. Use this skill when requests mention AI Agent tools, natural-language site management, agent profiles, Let this agent use its tools, searchForContentItems, createOrUpdateContentItem, importOrchardCoreRecipe, enableSiteFeature, listWorkflowActivities, listAIConversations, summarizeAIQuality or queryChatSessionMetrics.
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
| AI Conversations | Base feature | `listAIConversations` (every channel, newest first, by profile, status and time), `viewAIConversation` (transcript, variables, extracted fields, analysis, evaluations, flow stages), `getAIContactMemory` |
| AI Quality | `CrestApps.OrchardCore.AI.Quality` | `summarizeAIQuality`, `listAITestCases`, `runAITests`, `listAIAlerts` |
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

`listWorkflowActivities` takes no parameters and returns every workflow activity with its name, display text,
category and whether it is an event or a task (it used to refuse calls without a `workflowTypeId`).

### Schema-driven writes

- Call `getOrchardCoreRecipeJsonSchema` before `importOrchardCoreRecipe`. It returns the root `{ "steps": [...] }` schema and can expand one step while still listing every valid step name. Recipe steps contributed by enabled features (including CrestApps steps such as `Customers` or `IndexUsers`) are listed by these tools and by the MCP recipe-step resource.
- Call `getContentItemSchema` for the parent and nested content types before `createOrUpdateContentItem`, and send nested items (`BagPart`, `FlowPart`, widgets) inside the parent payload. With `CrestApps.OrchardCore.Recipes` enabled the payload is validated against the content type's recipe schema and corrective guidance is returned when it does not match.

## Agent profiles

An AI profile with **Profile type** `Agent` is a reusable specialist that other AI experiences can call.

| Field | Stored in | Meaning |
|---|---|---|
| **Description** | `AIProfile.Description` | What the agent does; used by orchestrators, A2A cards, and MCP tool descriptions |
| **Availability** | `AgentMetadata.Availability` | `OnDemand` (selectable per profile) or `AlwaysAvailable` (offered to every profile) |
| **Let this agent use its tools** | `AgentMetadata.AllowToolInvocation` | Off: the agent runs as a single completion with no tools, whatever is ticked on its **Capabilities** tab (CrestApps.Core `AgentProxyTool`). On: it runs its ticked tools |

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
              "Availability": "OnDemand",
              "AllowToolInvocation": true
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

An agent called by another agent never delegates further, so delegation is one level deep.

### Ready-made agent templates

The module ships **Agent** profile templates (category **Site management**) on the **New AI Profile** picker,
greyed out until their features are enabled. Each sets `ProfileType: Agent`, `AgentAvailability: OnDemand` and
`AllowToolInvocation: true` and lists its tools in `ToolNames`:

| Template (`Name`) | Requires | Tools |
| --- | --- | --- |
| Content Agent | `OrchardCore.Contents`, `OrchardCore.ContentTypes` | The content management and content definition tools |
| Workflow Agent | `OrchardCore.Workflows` | The workflow tools |
| AI Operations Agent (`AIOperationsAgent`) | Base feature | `listAIProfiles`, `viewAIProfile`, `listAIConversations`, `viewAIConversation`, `getAIContactMemory`, `summarizeAIQuality`, `listAITestCases`, `runAITests`, `listAIAlerts`, `queryChatSessionMetrics` |

A tool whose feature is off is skipped when the agent runs. No template ticks the tenant, feature, recipe or
settings tools. Template front-matter keys are in `orchardcore-ai-profile-templates`.

### Agents or tools

Each delegation costs at least one more completion plus the caller reading the answer, and details can be
lost in the hand-off. The default orchestrator already trims large tool sets (scoping above 30 tools,
planning above 100). Use an agent when an area needs many tools and its own instructions; tick tools directly
when an assistant needs only a few. Never give one profile both an agent and that agent's tools.

Agent profiles can be exposed outside the site:

- **A2A** - every Agent profile is published by the A2A Host feature (`orchardcore-ai-a2a-host`).
- **MCP** - name the profile in `McpServerOptions.Agents` (or set `ExposeAllAgents`) to expose it as an MCP tool (`orchardcore-ai-mcp-server`).

## Security

- The tools call Orchard Core services directly (for example `deleteContentItem` calls `IContentManager.RemoveAsync`) and do **not** run their own content, feature, or tenant permission checks in the current source. The tool permission and the profile permission are the gates.
- Tools are authorized per user only in chat interactions, which re-check each selected tool's `AccessAITool_{toolName}` at send time. An AI profile, and every agent it calls, runs exactly as configured: the profile is the authorization boundary and its conversations may be anonymous. Restrict who may query a profile that carries management tools.
- An agent adds no access the calling profile could not have had by ticking the same tools, but an **Always available** agent is offered to every profile on the site, including public chat widgets. Keep agents that change the site **On demand**.
- Background callers (workflows, recipes) have no user and skip tool access checks.
- Keep destructive groups (tenants, features, content deletion, recipe import) on dedicated, tightly permissioned profiles.

## Troubleshooting

| Symptom | Check |
|---|---|
| A tool group is missing from the picker | Its Orchard Core feature is disabled, or the editing user lacks the tool permission. |
| `createOrUpdateContentItem` produces invalid items | Enable `CrestApps.OrchardCore.Recipes` so `getContentItemSchema` is registered and payloads are validated. |
| An agent is not offered on another profile | It is `AlwaysAvailable` (already included) or not of type Agent. |
| An agent answers without using its tools | **Let this agent use its tools** (`AllowToolInvocation`) is off. |
| A2A clients see no agents | No profile has type Agent, or the A2A Host feature is disabled. |
