---
name: orchardcore-wizard
description: Skill for the CrestApps Orchard Core Wizard module, a reusable multi-step wizard or stepper framework. Covers content-driven wizards with the WizardPart and its settings (allowed step types or stereotypes, completion policy, sign-in), code-driven wizards with IWizardDefinition, WizardHandlerBase and IDisplayDriver of WizardFlow, WizardSession SavedSteps, the public Start, Step and Confirmation routes, idempotent completion under a lock with IWizardEngine.CompleteAsync and prepareUnderLock, IWizardAccessPolicy, the StartWizard and ContinueWizard permissions, and the wizard workflow events. Use this skill when requests mention Orchard Core wizards, steppers, multi-step forms, onboarding flows, guided sign-up, or surveys. Strong matches include CrestApps.OrchardCore.Wizard, WizardPart, WizardFlow, WizardSession, WizardStep, IWizardEngine, IWizardSessionStore, WizardCompletionResult and WizardCompletedEvent.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Wizard - Prompt Templates

## Build Multi-Step Wizards

You are an Orchard Core expert. Generate accurate setup and extension guidance for the CrestApps Wizard module, a decoupled multi-step wizard engine for sign-up, onboarding, surveys or any guided flow. A wizard is built from content items (no code) or from code; both share one engine, session store, public host controller, completion lock and workflow events.

### Guidelines

- Install `CrestApps.OrchardCore.Wizard` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle) in the web/startup project. Reference `CrestApps.OrchardCore.Wizard.Abstractions` from a module that only implements wizard contracts.
- Enable `CrestApps.OrchardCore.Wizard`. It is the only feature ID. The content-driven part activates when `OrchardCore.Contents` is enabled, and the workflow events activate when `OrchardCore.Workflows` is enabled (both through `[RequireFeatures]` startups).
- In a handler or driver, always early-return for wizard types and steps you do not own. Every registered handler and driver runs for every wizard.
- Add steps in `ActivatingAsync`, which runs once per new session, so resuming does not add steps again.
- A `CollectData` step is complete only when `WizardSession.SavedSteps` has an entry for its key.
- Put finalization side effects (provisioning, emails) in `CompletedAsync`. The engine runs completing and completed handlers exactly once.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature and Types

| Item | Value |
|---|---|
| Feature ID | `CrestApps.OrchardCore.Wizard` (category Content Management) |
| Definition | `IWizardDefinition` (`WizardType`, `DisplayName`, `RequiresAuthenticatedUser`) |
| Handler | `IWizardHandler`, or derive from `WizardHandlerBase` (namespace `CrestApps.OrchardCore.Wizard.Handlers`) |
| Renderer | `IDisplayDriver<WizardFlow>` |
| Flow | `WizardFlow` (`Session`, `GetSortedSteps`, `GetCurrentStep`, `SetCurrentStep`, `GetFirstStep`, `GetLastStep`, `GetNextStep`, `GetPreviousStep`, `CurrentStepEquals`) |
| Session | `WizardSession` (`SessionId`, `WizardType`, `DefinitionId`, `Status`, `Steps`, `CurrentStep`, `SavedSteps`, `OwnerId`, `Properties`) |
| Step | `WizardStep` (`Key`, `Title`, `Description`, `Order`, `CollectData`, `State`, `Data`, `Conceal`) |
| Engine | `IWizardEngine.CompleteAsync(flow, prepareUnderLock, cancellationToken)` returns `WizardCompletionResult` |
| Store | `IWizardSessionStore` (`GetAsync`, `NewAsync`, `SaveAsync`), indexed by `WizardSessionIndex` |
| Access | `IWizardAccessPolicy.RequiresAuthenticatedUserAsync(wizardType, definitionId)` |
| Permissions | `StartWizard`, `ContinueWizard` (granted to Anonymous, Authenticated and Administrator by default) |
| Rate-limit group | `wizard` (`WizardConstants.RateLimitGroups.Wizard`, for `OrchardCore.RateLimits`) |

Enums: `WizardSessionStatus` (`Pending`, `AwaitingExternal`, `Completed`, `Failed`, `Canceled`, `Expired`), `WizardStepState` (`NotStarted`, `InProgress`, `Completed`, `Failed`), `WizardCompletionStatus` (`Completed`, `AlreadyCompleted`, `Blocked`, `Failed`).

### Public Routes

| Route name | URL | Purpose |
|---|---|---|
| `WizardStart` | `GET/POST Wizard/Start/{wizardType}` | Start or resume (a per-wizard cookie remembers the pending session) and post a step |
| `WizardStep` | `GET Wizard/Step/{sessionId}/{step?}` | Show a saved step of a pending session |
| `WizardConfirmation` | `GET Wizard/Confirmation/{sessionId}` | Confirmation of a completed session |

On a valid post, the controller moves to the next step, or to the first incomplete data-collecting step, and when none is left calls `IWizardEngine.CompleteAsync` and redirects to the confirmation.

## Content-Driven Wizards

1. Enable `CrestApps.OrchardCore.Wizard`, `OrchardCore.Contents` and `OrchardCore.Flows`. The manifest does not declare Flows, but the WizardPart editor uses its `flowpart-edit` script and style resources for adding and ordering steps.
2. Attach `WizardPart` to a host content type (for example `Registration`).
3. Create step content types (ordinary types with fields), optionally with a stereotype such as `WizardStep`.
4. In the part settings choose the allowed step **content types** or **stereotype**, the **completion policy** and **requires authenticated user**.
5. Create a host item, add steps with **Add step** (works like `BagPart`) and order the cards.
6. Link to `/Wizard/Start/Content?definitionId={contentItemId}`. All content wizards share the wizard type `Content`.

`WizardPartSettings`: `ContainedContentTypes`, `ContainedStereotypes`, `DisplayType`, `CollapseContainedItems`, `RequiresAuthenticatedUser`, `CompletionPolicy` (`WizardCompletionPolicy` `None` = 0, `Draft` = 1 or `Publish` = 2; default `Publish`).

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "Registration",
          "DisplayName": "Registration",
          "Settings": { "ContentTypeSettings": { "Creatable": true, "Listable": true } },
          "ContentTypePartDefinitionRecords": [
            { "PartName": "TitlePart", "Name": "TitlePart", "Settings": { "ContentTypePartSettings": { "Position": "0" } } },
            {
              "PartName": "WizardPart",
              "Name": "WizardPart",
              "Settings": {
                "ContentTypePartSettings": { "Position": "1" },
                "WizardPartSettings": {
                  "ContainedStereotypes": [ "WizardStep" ],
                  "CompletionPolicy": 1,
                  "RequiresAuthenticatedUser": false
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

Each step renders a fresh content item seeded from the authored step's default values, with the type's standard editor. Responses are saved in the session and, per the completion policy, created as drafts or published inside the completion lock, so a persistence failure marks the wizard failed instead of losing data. Only types still in the allowlist render at runtime, and the administrator is responsible for exposing only safe step types to the wizard's audience.

## Code-Driven Wizards

Register a definition, a handler that adds and finalizes steps, and a display driver that renders the current step and writes `SavedSteps`:

```csharp
services.AddSingleton<IWizardDefinition, OnboardingWizardDefinition>();
services.AddScoped<IWizardHandler, OnboardingWizardHandler>();
services.AddScoped<IDisplayDriver<WizardFlow>, OnboardingProfileStepDisplayDriver>();
```

Then link to `/Wizard/Start/Onboarding`. A complete definition, handler, step driver and view are in [references/code-wizard-examples.md](references/code-wizard-examples.md).

Driver zones used by the default host: `Steps` (the stepper), `Content` (the step editor) and `Actions` (navigation buttons, `WizardFlowButtons`); the confirmation display uses the `Confirmation` display type. Set the session property `WizardConstants.SuppressDefaultChromePropertyKey` to `true` to render your own stepper and buttons while reusing the controller and engine.

## Completing Under a Lock

`IWizardEngine.CompleteAsync` takes a distributed lock for the session, reloads the authoritative session, checks every `CollectData` step has data, and runs completing and completed handlers exactly once even under concurrent or repeated posts. A missing step returns `Blocked` with `BlockingStepKey`. Use `prepareUnderLock` to validate and record external evidence, such as a payment return, inside the same lock:

```csharp
var result = await _wizardEngine.CompleteAsync(
    flow,
    prepareUnderLock: async authoritativeFlow =>
    {
        // Return false to stop completion when the external evidence is not valid.
        return await _receipts.ValidateAndRecordAsync(authoritativeFlow.Session, cancellationToken);
    },
    cancellationToken);

if (!result.IsCompleted)
{
    // Blocked, or Failed: send the user back to result.BlockingStepKey when it is set.
}
```

## Access Control

The host challenges anonymous visitors when the definition's `RequiresAuthenticatedUser` is `true` or any `IWizardAccessPolicy` returns `true` for the wizard type and `definitionId`. The content-driven part ships such a policy for its per-item setting. Register your own with `services.AddScoped<IWizardAccessPolicy, MyWizardAccessPolicy>();`. Starting needs `StartWizard`; posting, showing steps and the confirmation need `ContinueWizard`. Remove these from Anonymous to make every wizard sign-in only.

## Workflow Events

With `OrchardCore.Workflows` enabled, four events appear under the **Wizard** category: `WizardStartedEvent`, `WizardStepDisplayedEvent`, `WizardCompletedEvent` and `WizardFailedEvent`. Each has an optional `WizardType` filter (empty matches all, or `Onboarding`, `Content`, and so on) and supplies `SessionId`, `WizardType`, `DefinitionId`, `CurrentStep`, `Status` and the `Session` object as input, correlated by session id.

## Troubleshooting

| Symptom | Check |
|---|---|
| `Wizard/Start/{type}` returns 404 | No `IWizardDefinition` with that `WizardType` is registered, or the feature is off |
| WizardPart not in the part list | Enable `OrchardCore.Contents`; the content startup requires it |
| Add step or drag-to-reorder does nothing in the editor | Enable `OrchardCore.Flows`, which provides the `flowpart-edit` resources |
| Workflow events missing | Enable `OrchardCore.Workflows` |
| Steps duplicate after resume | Add steps only in `ActivatingAsync` |
| Wizard never completes | A `CollectData` step has no `SavedSteps` entry; the driver's `UpdateAsync` must write it |
| Another wizard's step renders | A handler or driver does not early-return for foreign wizard types or steps |
| Anonymous users get 403 | `StartWizard` or `ContinueWizard` was removed from Anonymous |
