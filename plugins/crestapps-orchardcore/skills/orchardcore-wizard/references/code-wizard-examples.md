# Code-Driven Wizard Examples

A two-step onboarding wizard built in code. Start it at `/Wizard/Start/Onboarding`.

## Definition

```csharp
using CrestApps.OrchardCore.Wizard;

namespace MyCompany.Onboarding;

public sealed class OnboardingWizardDefinition : IWizardDefinition
{
    public const string Type = "Onboarding";

    public string WizardType => Type;

    public string DisplayName => "Onboarding";

    public bool RequiresAuthenticatedUser => true;
}
```

## Handler

Add steps once, in `ActivatingAsync`, and finalize in `CompletedAsync`. Early-return for other wizard types.

```csharp
using System.Text.Json;
using CrestApps.OrchardCore.Wizard;
using CrestApps.OrchardCore.Wizard.Handlers;

namespace MyCompany.Onboarding;

public sealed class OnboardingWizardHandler : WizardHandlerBase
{
    public const string ProfileStep = "profile";
    public const string PreferencesStep = "preferences";

    private readonly IOnboardingService _onboarding;

    public OnboardingWizardHandler(IOnboardingService onboarding)
    {
        _onboarding = onboarding;
    }

    public override Task ActivatingAsync(WizardFlowActivatingContext context)
    {
        if (context.Flow.Session.WizardType != OnboardingWizardDefinition.Type)
        {
            return Task.CompletedTask;
        }

        context.Flow.Session.Steps.Add(new WizardStep
        {
            Key = ProfileStep,
            Title = "Your profile",
            Order = 1,
            CollectData = true,
        });

        context.Flow.Session.Steps.Add(new WizardStep
        {
            Key = PreferencesStep,
            Title = "Preferences",
            Order = 2,
            CollectData = true,
        });

        return Task.CompletedTask;
    }

    public override async Task CompletedAsync(WizardFlowCompletedContext context)
    {
        if (context.Flow.Session.WizardType != OnboardingWizardDefinition.Type)
        {
            return;
        }

        var profile = context.Flow.Session.SavedSteps[ProfileStep].Deserialize<OnboardingProfile>();

        await _onboarding.ProvisionAsync(context.Flow.Session.OwnerId, profile);
    }
}

public sealed class OnboardingProfile
{
    public string DisplayName { get; set; }

    public string Company { get; set; }
}
```

`IOnboardingService` stands for your own service.

## Step Display Driver

Render only the step you own, prefill from `SavedSteps`, and write `SavedSteps[step.Key]` when the post is valid. The host places `Content` in the step area.

```csharp
using System.Text.Json;
using CrestApps.OrchardCore.Wizard;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace MyCompany.Onboarding;

public sealed class OnboardingProfileStepDisplayDriver : DisplayDriver<WizardFlow>
{
    public override IDisplayResult Edit(WizardFlow flow, BuildEditorContext context)
    {
        if (!IsOwnStep(flow))
        {
            return null;
        }

        return Initialize<OnboardingProfileStepViewModel>("OnboardingProfileStep", model =>
        {
            if (flow.Session.SavedSteps.TryGetPropertyValue(OnboardingWizardHandler.ProfileStep, out var node) && node is not null)
            {
                var saved = node.Deserialize<OnboardingProfile>();

                model.DisplayName = saved.DisplayName;
                model.Company = saved.Company;
            }
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(WizardFlow flow, UpdateEditorContext context)
    {
        if (!IsOwnStep(flow))
        {
            return null;
        }

        var model = new OnboardingProfileStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayName), "Enter your name.");
        }

        if (context.Updater.ModelState.IsValid)
        {
            flow.Session.SavedSteps[OnboardingWizardHandler.ProfileStep] = JsonSerializer.SerializeToNode(new OnboardingProfile
            {
                DisplayName = model.DisplayName,
                Company = model.Company,
            });

            flow.GetCurrentStep().State = WizardStepState.Completed;
        }

        return Edit(flow, context);
    }

    private static bool IsOwnStep(WizardFlow flow)
        => flow.Session.WizardType == OnboardingWizardDefinition.Type
        && flow.CurrentStepEquals(OnboardingWizardHandler.ProfileStep);
}

public class OnboardingProfileStepViewModel
{
    public string DisplayName { get; set; }

    public string Company { get; set; }
}
```

A second driver handles `PreferencesStep` the same way.

## Step View

`Views/OnboardingProfileStep.cshtml`:

```cshtml
@model MyCompany.Onboarding.OnboardingProfileStepViewModel

<div class="mb-3">
    <label asp-for="DisplayName" class="form-label">Name</label>
    <input asp-for="DisplayName" class="form-control" />
    <span asp-validation-for="DisplayName" class="text-danger"></span>
</div>

<div class="mb-3">
    <label asp-for="Company" class="form-label">Company</label>
    <input asp-for="Company" class="form-control" />
</div>
```

The host view provides the form, antiforgery token, stepper and Previous, Next and Finish buttons.

## Startup

```csharp
using CrestApps.OrchardCore.Wizard;
using CrestApps.OrchardCore.Wizard.Handlers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace MyCompany.Onboarding;

[RequireFeatures("CrestApps.OrchardCore.Wizard")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IWizardDefinition, OnboardingWizardDefinition>();
        services.AddScoped<IWizardHandler, OnboardingWizardHandler>();
        services.AddScoped<IDisplayDriver<WizardFlow>, OnboardingProfileStepDisplayDriver>();
    }
}
```

Add `"CrestApps.OrchardCore.Wizard"` to the module manifest's `Dependencies` so it is enabled with your feature.

## Per-Instance Sign-In Policy

```csharp
using CrestApps.OrchardCore.Wizard;

namespace MyCompany.Onboarding;

public sealed class PartnerWizardAccessPolicy : IWizardAccessPolicy
{
    public Task<bool> RequiresAuthenticatedUserAsync(string wizardType, string definitionId)
        => Task.FromResult(wizardType == "PartnerSignup" && definitionId == "enterprise");
}
```

Register it with `services.AddScoped<IWizardAccessPolicy, PartnerWizardAccessPolicy>();`. Any policy returning `true` makes the host challenge an anonymous visitor.
