---
name: orchardcore-or-tools
description: Skill for the CrestApps Orchard Core OR-Tools add-on module (CrestApps.OrchardCore.OrTools), which adds Google OR-Tools solvers to Workforce and Dispatch. Covers the OR-Tools Shift Scheduling feature (a CP-SAT model registered as the IShiftScheduleSolver named OR-Tools and set as ShiftPlanningOptions DefaultSolver) and the OR-Tools Dispatch feature (a routing model registered as the last IDispatchOptimizer), how each solver starts from the built-in answer, time limits and seeds, why the package is not in the CMS targets (native libraries per platform), installing it in the web project, and writing your own solver around the public DefaultShiftScheduleSolver and DefaultDispatchOptimizer. Use this skill when requests mention OR-Tools, CP-SAT, vehicle routing, optimizing a dispatch day or auto-scheduling shifts with a better solver. Strong matches include OrToolsShiftScheduleSolver, OrToolsDispatchOptimizer, OrToolsConstants, IShiftScheduleSolver, IDispatchOptimizer and ShiftPlanningOptions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core OR-Tools

## Better Shift Schedules and Dispatch Days

You are an Orchard Core expert. Generate accurate setup and extension guidance for the CrestApps OR-Tools add-on, which plugs Google OR-Tools (Apache 2.0) solvers into Workforce shift auto-scheduling and Dispatch day optimization.

### Guidelines

- Add the `CrestApps.OrchardCore.OrTools` package to the web/startup project yourself. It is **not** in `CrestApps.OrchardCore.Cms.Core.Targets`, because `Google.OrTools` brings native libraries for each platform (Windows, Linux and macOS, x64 and Arm64).
- Make sure the deployment target's runtime identifier is one OR-Tools ships native libraries for. The solvers don't catch a native library load failure, so on an unsupported platform **Auto-schedule** or **Optimize the day** fails with the exception instead of falling back; turn the feature off there.
- Enable only the feature you need. Each depends on the module it improves: Workforce Shift Planning or Dispatch.
- Turning a feature on never makes a schedule or a day worse: each solver runs the built-in solver first, gives its answer to OR-Tools as the starting point (a hint), and keeps the better of the two.
- No settings, permissions, admin pages, recipe steps or deployment steps of its own. The time limit and seed come from the problem the host module builds.
- Shift planning itself is covered by `orchardcore-workforce`; visits and the board by `orchardcore-dispatch`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Install

With central package management (versions in `Directory.Packages.props`):

```xml
<ItemGroup>
  <PackageReference Include="CrestApps.OrchardCore.Cms.Core.Targets" />
  <PackageReference Include="CrestApps.OrchardCore.OrTools" />
</ItemGroup>
```

### Features

| Feature | ID | Depends on | Category |
|---|---|---|---|
| OR-Tools Shift Scheduling | `CrestApps.OrchardCore.OrTools.ShiftScheduling` | `CrestApps.OrchardCore.Workforce.ShiftPlanning` | Workforce |
| OR-Tools Dispatch | `CrestApps.OrchardCore.OrTools.Dispatch` | `CrestApps.OrchardCore.Dispatch` | Field Service |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.OrTools.ShiftScheduling",
        "CrestApps.OrchardCore.OrTools.Dispatch"
      ],
      "disable": []
    }
  ]
}
```

## What Each Feature Registers

| Feature | Registration | Effect |
|---|---|---|
| Shift Scheduling | `services.AddSingleton<IShiftScheduleSolver, OrToolsShiftScheduleSolver>()` and `ShiftPlanningOptions.DefaultSolver = "OR-Tools"` | **Auto-schedule** on a shift schedule uses OR-Tools unless the run names another solver. |
| Dispatch | `services.AddSingleton<IDispatchOptimizer, OrToolsDispatchOptimizer>()` | `IDispatchOptimizationService` uses the **last registered** optimizer, so **Optimize the day**, `POST api/dispatch/v1/optimize` and the `dispatchOptimizeAndExplain` AI tool use OR-Tools. |

Both solvers are named `OR-Tools` (`OrToolsConstants.SolverName`); the built-in shift solver is `Default` (`WorkforceConstants.DefaultSolver`). Results name the solver (always `OR-Tools` once the feature is on, even when the built-in answer was kept): the auto-schedule message ("The OR-Tools solver made this schedule in 1.3 seconds.") and the board's proposal ("Proposed by the OR-Tools solver."), from `ShiftScheduleProposal.Solver` and `DispatchProposal.Solver`.

## Shift Scheduling Model (CP-SAT)

`OrToolsShiftScheduleSolver` builds a CP-SAT model:

- A yes-or-no choice per worker, day and template, only where the worker works that day, holds the template's skills, is available for the whole shift, has no fixed shift that day or too close to it, and stays within their daily hours.
- Hard limits: one shift a day, rest between shifts on nearby days, weekly hours under the maximum, and days in a row.
- Objective, per interval and skill: people counted up to the minimum (weight 100) and up to the target (30), going over the maximum (-40), each hour short of a worker's weekly minimum (-15), each interval paid for (-1), and a shift inside a preferred window (+5).
- Runs for the problem's `TimeLimit` (from `ShiftPlanningOptions.SolverTimeLimitSeconds`, 30) less what the built-in solver took, with up to eight threads and the problem's `Seed`. `TimedOut` is set when the schedule wasn't proved optimal.

## Dispatch Routing Model

`OrToolsDispatchOptimizer` builds a routing model from the `DispatchProblem`:

- A route per technician from their home base; no trip back home is counted, as with the built-in optimizer.
- A time dimension of travel, the buffer between visits and each visit's length; each start on a five-minute step inside one of the technician's open stretches and the visit's window, with the gaps between stretches (breaks) as fixed breaks.
- Only the technicians a visit allows (`EligibleWorkerIds`), fixed visits on their technician at their start, every other visit optional at its `UnassignedPenalty` (1000 by default).
- Cost: travel minutes plus the penalty of every visit left out. Guided local search from the built-in plan for the problem's `TimeLimit` (the dispatch setting `OptimizerSeconds`, 5) less what the built-in optimizer took.

Nothing is changed until the dispatcher applies the proposal; every applied move still goes through the locked `IDispatchService.AssignAsync` checks.

## Writing Your Own Solver

`DefaultShiftScheduleSolver` (`CrestApps.OrchardCore.Workforce.Core`) and `DefaultDispatchOptimizer` (`CrestApps.OrchardCore.Dispatch.Core`) are public, so a custom solver can start from their answer the way the OR-Tools solvers do.

```csharp
public sealed class CautiousShiftScheduleSolver : IShiftScheduleSolver
{
    private readonly DefaultShiftScheduleSolver _builtIn = new();

    public string Name => "Cautious";

    public async Task<ShiftScheduleProposal> SolveAsync(ShiftScheduleProblem problem, CancellationToken cancellationToken = default)
    {
        var proposal = await _builtIn.SolveAsync(problem, cancellationToken);

        // Improve proposal.Shifts here, keeping it only when it is better.
        proposal.Solver = Name;

        return proposal;
    }
}

// Startup (in a feature that depends on CrestApps.OrchardCore.Workforce.ShiftPlanning):
// services.TryAddEnumerable(ServiceDescriptor.Singleton<IShiftScheduleSolver, CautiousShiftScheduleSolver>());
// services.Configure<ShiftPlanningOptions>(options => options.DefaultSolver = "Cautious");
```

```csharp
public sealed class LoggedDispatchOptimizer : IDispatchOptimizer
{
    private readonly DefaultDispatchOptimizer _builtIn = new();

    public string Name => "Logged";

    public async Task<DispatchPlan> SolveAsync(DispatchProblem problem, CancellationToken cancellationToken = default)
    {
        var plan = await _builtIn.SolveAsync(problem, cancellationToken);

        plan.Solver = Name;

        return plan;
    }
}

// Startup (in a feature that depends on CrestApps.OrchardCore.Dispatch; the last optimizer registered wins):
// services.AddSingleton<IDispatchOptimizer, LoggedDispatchOptimizer>();
```

## Gotchas

| Symptom | Check |
|---|---|
| The message still names the built-in solver | The feature isn't enabled, or another module set `ShiftPlanningOptions.DefaultSolver` later. When OR-Tools finds nothing better it keeps the built-in answer but still reports itself as the solver. |
| `DllNotFoundException` or `TypeInitializationException` on a server | The platform's OR-Tools native library isn't deployed; publish for a supported runtime identifier or disable the feature. |
| Optimize the day takes the full time limit | Expected: guided local search uses the remaining `OptimizerSeconds`. Lower it in **Settings > Dispatch**. |
| Package not found after adding the CMS targets | The add-on isn't in the targets; reference `CrestApps.OrchardCore.OrTools` directly. |

Related skills: `orchardcore-dispatch`, `orchardcore-workforce`, `orchardcore-scheduling`.
