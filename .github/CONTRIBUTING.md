# Contributing to CrestApps Orchard Core Agent Skills

Thank you for your interest in contributing! Whether you're fixing bugs, adding new skills, or improving documentation, your help is appreciated.

Before getting started, please read through our [README](../README.md) to familiarize yourself with the project.

---

## Setting Up the Project Locally

Start by cloning the repository and switching to the `main` branch:

```bash
git clone https://github.com/CrestApps/CrestApps.OrchardCore.AgentSkills.git
cd CrestApps.OrchardCore.AgentSkills
git checkout main
```

### Command Line

1. Install the latest .NET SDK from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download).
2. Run the build using `dotnet build`.

### Visual Studio

1. Install Visual Studio 2022 or newer from [https://visualstudio.microsoft.com/downloads](https://visualstudio.microsoft.com/downloads).
2. Open the solution file (`.sln`) found at the root of the repo.
3. Wait for NuGet packages to restore.
4. Build the solution.

---

## Choosing What to Work On

We welcome contributions of all kinds! Here's how you can find something meaningful to contribute:

* Browse [open issues](https://github.com/CrestApps/CrestApps.OrchardCore.AgentSkills/issues) to see what's currently being worked on or needs help.

If you have an idea or improvement that's not tracked yet, please open a new issue first and discuss it with the maintainers before starting work.

---

## Adding New Skills

Skills must comply with the [agentskills.io specification](https://agentskills.io/specification). Each skill lives in its own directory under `src/skills/.agents/skills/` and must contain a `SKILL.md` file.

### Required File Structure

```
src/skills/.agents/skills/orchardcore.my-skill/
├── SKILL.md                    ← Required: skill definition with front-matter
└── references/                 ← Optional: additional reference/example files
    └── my-skill-examples.md
```

### SKILL.md Format

Every `SKILL.md` must start with YAML front-matter containing at least `name` and `description`:

```md
---
name: orchardcore.my-skill
description: A clear description of what this skill does and when to use it.
---

# Skill Title

Guidelines, code templates, and examples go here.
```

### Naming Conventions

* **Directory name**: lowercase, hyphenated, prefixed with `orchardcore.` (e.g., `orchardcore.content-types`)
* **`name` field**: must exactly match the directory name
* **`SKILL.md`**: must be uppercase (`SKILL.md`, not `skill.md`)
* **References directory**: `references/` (not `examples/`)

### Documentation Conventions

* All recipe step JSON blocks must be wrapped in the root recipe format: `{ "steps": [...] }`
* All C# classes in code samples must use the `sealed` modifier
* Third-party module packages (non `OrchardCore.*`) must be installed in the web/startup project

### Validation

Skills are validated automatically in CI. Before submitting a PR, verify your skill locally:

```bash
dotnet build -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false
dotnet test -c Release --verbosity normal
```

---

## Contribution Scope

* **Small Fixes (typos, minor bugs)**: Feel free to submit a pull request directly.
* **Features or Major Changes**: Please open an issue to discuss first. We want to make sure it aligns with the overall goals and doesn't duplicate existing efforts.

---

## Submitting a Pull Request (PR)

> New to pull requests? Check out [this guide](https://help.github.com/articles/using-pull-requests).

To submit a quality PR:

* Ensure your code follows our coding style and practices.
* Verify the project builds and all tests pass.
* Link your PR to a relevant issue using `Fix #issue_number` in the description.
* If you're not finished yet, mark your PR as a **draft**.
* Include screenshots or screen recordings for UI changes.
* Please [allow maintainers to edit your PR](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/working-with-forks/allowing-changes-to-a-pull-request-branch-created-from-a-fork) for easier collaboration.

---

## Review Process & Feedback

Every PR is reviewed by the core team. Here's how to keep the process smooth:

* Address feedback promptly and thoroughly.
* Apply [suggested changes](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/reviewing-changes-in-pull-requests/incorporating-feedback-in-your-pull-request#applying-suggested-changes) directly whenever possible.
* Don't manually resolve conversations — let the reviewer do that.
* Keep all related discussions inside the PR to keep things organized.
* When you've addressed feedback, use "Re-request review" to notify reviewers.

---

## Thank You!

We deeply appreciate your contributions. All PRs are reviewed with care to ensure they fit the quality and goals of the project.

Following these guidelines helps make sure your contribution is merged quickly and smoothly — and makes the process pleasant for everyone involved.
