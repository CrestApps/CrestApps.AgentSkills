---
name: crestapps-core-ai-profiles
description: Skill for designing CrestApps.Core AI Profiles that control prompts, tools, retrieval, memory, and session behavior.
---

# CrestApps.Core AI Profiles - Prompt Templates

## Create Reusable AI Profiles

You are a CrestApps.Core expert. Generate code and guidance for AI Profiles in CrestApps.Core.

### Guidelines

- Use Chat Interactions for fast ad hoc validation.
- Move to AI Profiles when the experience needs stable reusable behavior.
- Keep model choice in deployments and behavior choice in the profile.
- Use profile descriptions for agent scenarios and routing decisions.
- Let profiles own prompt behavior, tools, retrieval, memory, and post-session behavior.

### AI Profile vs Other Building Blocks

| Concept | Question it answers |
|---|---|
| Connection | How do I talk to a provider |
| Deployment | Which model should run |
| Chat Interactions | Let me test this setup quickly |
| AI Profile | How should this AI experience behave |

### Typical Lifecycle

1. Create a provider connection.
2. Create one or more deployments.
3. Verify the deployment with Chat Interactions.
4. Create an AI Profile once the behavior should be reusable.
5. Attach tools, documents, data sources, or memory.

### Example Agent Profile

```csharp
var profile = new AIProfile
{
    Type = AIProfileType.Agent,
    Name = "code-reviewer",
    DisplayText = "Code Reviewer",
    Description = "Reviews code for bugs, security issues, and correctness.",
    ChatDeploymentName = "gpt-4o",
};

profile.Put(new AgentMetadata
{
    Availability = AgentAvailability.OnDemand,
});

await profileManager.CreateAsync(profile);
```

### Design Guidance

- Use profiles to capture reusable behavior and lifecycle rules.
- Use separate chat and utility deployments when the workflow benefits from it.
- Keep system instructions, tools, and retrieval aligned to one purpose.
- Prefer multiple focused profiles over one overloaded profile.
