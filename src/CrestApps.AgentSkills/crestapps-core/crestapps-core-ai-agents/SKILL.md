---
name: crestapps-core-ai-agents
description: Skill for agent profiles, agent availability, and task delegation through the CrestApps.Core orchestrator.
---

# CrestApps.Core AI Agents - Prompt Templates

## Create AI Agents

You are a CrestApps.Core expert. Generate code and guidance for specialized agent profiles in CrestApps.Core.

### Guidelines

- Agents are standard `AIProfile` records with `Type = AIProfileType.Agent`.
- Give every agent a strong `Description` because that is what the primary model uses for routing.
- Use `OnDemand` for most agents and `AlwaysAvailable` only when the agent should always be injected.
- Link on-demand agents to chat profiles through `AgentInvocationMetadata`.
- Assume agent execution is intentionally isolated and tools are disabled inside the delegated agent run.

### Agent Example

```csharp
var agent = new AIProfile
{
    Type = AIProfileType.Agent,
    Name = "translator",
    DisplayText = "Translator",
    Description = "Translates text between languages.",
    ChatDeploymentName = "gpt-4o-mini",
};

agent.Put(new AgentMetadata
{
    Availability = AgentAvailability.OnDemand,
});

await profileManager.CreateAsync(agent);
```

### Link the Agent to a Chat Profile

```csharp
chatProfile.Put(new AgentInvocationMetadata
{
    Names = ["translator", "code-reviewer"],
});

await profileManager.UpdateAsync(chatProfile);
```

### Availability Modes

| Mode | Use |
|---|---|
| `OnDemand` | Specialized agents assigned per profile |
| `AlwaysAvailable` | Global agents needed in every orchestration request |
