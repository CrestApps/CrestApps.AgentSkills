# CrestApps.AgentSkills Development Instructions

**ALWAYS reference these instructions first and fall back to searching only if needed.**

## Project Overview

CrestApps.AgentSkills contains shared AI agent skills and MCP tooling for .NET applications and Orchard Core projects.

- **Target Framework**: .NET 10 (net10.0)
- **SDK Version**: .NET 10.0.100 (see `global.json`)
- **Package Management**: Central Package Management via `Directory.Packages.props`
- **Skill source of truth**: `src/CrestApps.AgentSkills/orchardcore/` (46 Orchard Core skills)

### Four Source Projects

1. **`CrestApps.AgentSkills`** - Skill content container
   - Contains all Orchard Core skill files under `orchardcore/`
   - Not packable — purely a source container referenced by the other packages

2. **`CrestApps.AgentSkills.Mcp`** - Generic MCP engine
   - Framework-agnostic skill parser and MCP provider
   - Supports `.md` (front-matter) and `.yaml`/`.yml` skill formats
   - Key interfaces: `IAgentSkillFilesStore`, `IMcpPromptProvider`, `IMcpResourceProvider`
   - Key implementations: `DefaultAgentSkillFilesStore`, `SkillPromptProvider`, `SkillResourceProvider`
   - Key parsers: `SkillFrontMatterParser`, `SkillYamlParser`, `SkillFileParser`
   - No bundled skills — expects consumer to provide skill directory

3. **`CrestApps.AgentSkills.OrchardCore`** - Dev-time skill distributor
   - Development dependency only (`IncludeBuildOutput=false`, `DevelopmentDependency=true`)
   - Uses MSBuild `.targets` to copy skills to solution root `.agents/skills/` on first build
   - No runtime code — purely for local AI authoring

4. **`CrestApps.AgentSkills.Mcp.OrchardCore`** - Runtime MCP server for Orchard Core
   - Extends `CrestApps.AgentSkills.Mcp` with Orchard Core–specific convenience wrappers
   - Bundles skills via `contentFiles` (copied to bin output on restore)
   - Entry point: `OrchardCoreSkillMcpExtensions` (`AddOrchardCoreSkills`, `AddOrchardCoreAgentSkillServices`)
   - Services registered as singletons for caching

**Tests**: `test/CrestApps.AgentSkills.Mcp.Tests/`, `test/CrestApps.AgentSkills.Mcp.OrchardCore.Tests/`

## Build & Test

Run builds and tests from the repository root:

```bash
# Build (treat warnings as errors, run analyzers)
dotnet build -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false

# Run all tests
dotnet test -c Release --no-build --verbosity normal

# Run tests from a single project
dotnet test -c Release --no-build test/CrestApps.AgentSkills.Mcp.Tests/

# Run a specific test class
dotnet test -c Release --no-build --filter "FullyQualifiedName~SkillFrontMatterParserTests"

# Run a specific test method
dotnet test -c Release --no-build --filter "FullyQualifiedName~SkillFrontMatterParserTests.TryParse_ValidFrontMatter_ReturnsTrueAndExtractsFields"
```

**Note**: Tests use xUnit v3 (`xunit.v3`). Test classes follow the pattern `<Subject>Tests.cs` with the `sealed` modifier.

## Skill Validation

Each skill directory under `src/CrestApps.AgentSkills/orchardcore/` must contain a `SKILL.md` file with YAML front-matter (must include `name` and `description`).

### Skill Requirements

- **File name**: `SKILL.md` (uppercase, not `skill.md`)
- **Directory naming**: lowercase, hyphenated, prefixed with `orchardcore-` (e.g., `orchardcore-content-types`)
- **`name` field**: Must exactly match the directory name (e.g., `orchardcore-content-types`)
- **Front-matter**: Must start with `---` and contain closing `---`
- **References**: Optional `references/` subdirectory for additional `.md` files (not `examples/`)

### Skill Documentation Conventions (from CONTRIBUTING.md)

- All recipe step JSON blocks must be wrapped in root recipe format: `{ "steps": [...] }`
- All C# classes in code samples must use the `sealed` modifier, **except for View Models** which must not be `sealed` because they are used for model binding
- Third-party module packages (non `OrchardCore.*`) must be installed in the web/startup project
- Keep guidance concise, example-driven, and actionable
- Prefer ready-to-use patterns over abstract descriptions

### Skill Categories

Skills are organized by Orchard Core functional area:

- **AI & MCP**: ai, ai-chat, ai-chat-interactions, ai-mcp
- **Content Model**: content-types, content-parts, content-fields, content-items, content-queries, taxonomies
- **Templating**: theming, razor, liquid, shapes, placement, display-management
- **Infrastructure**: modules, features, setup, tenants, data-migrations, background-tasks, caching
- **Recipes & Deployment**: recipes, deployments, autoroute, site-settings
- **Security**: security, users-roles, openid
- **UI & Navigation**: navigation, menus, widgets, forms, admin
- **Search & Media**: search-indexing, media, graphql
- **Communication**: email, notifications, workflows
- **Tooling**: module-creator, theme-creator, tester
- **Other**: localization, seo, audit-trail

### Local Validation Scripts

**Bash:**
```bash
for dir in src/CrestApps.AgentSkills/orchardcore/*/; do
  name=$(basename "$dir")
  if [ ! -f "$dir/SKILL.md" ]; then echo "FAIL: $name missing SKILL.md"; fi
  if ! head -1 "$dir/SKILL.md" | grep -q "^---$"; then echo "FAIL: $name bad front-matter"; fi
  echo "OK: $name"
done
```

**PowerShell:**
```powershell
Get-ChildItem -Path "src\CrestApps.AgentSkills\orchardcore" -Directory | ForEach-Object {
    $skillFile = Join-Path $_.FullName "SKILL.md"
    if (-not (Test-Path $skillFile)) { Write-Host "FAIL: $($_.Name) missing SKILL.md" -ForegroundColor Red }
    elseif ((Get-Content $skillFile -First 1) -ne "---") { Write-Host "FAIL: $($_.Name) bad front-matter" -ForegroundColor Red }
    else { Write-Host "OK: $($_.Name)" -ForegroundColor Green }
}
```

## Packaging Notes

The solution is configured for preview packages by default (`VersionSuffix=preview` in `Directory.Build.props`).
For release builds, override the version (for example via CI) and publish with `dotnet pack`.

### Central Package Management

- All package versions are centrally managed in `Directory.Packages.props`
- Key dependencies:
  - `ModelContextProtocol` (0.8.0-preview.1) - MCP C# SDK
  - `YamlDotNet` (16.3.0) - YAML parsing
  - `Microsoft.Extensions.Logging.Abstractions` (10.0.3) - Logging abstractions
  - `xunit.v3` (3.2.2) - Testing framework

## Key Conventions

### Code Style

- Follow `.editorconfig` for formatting and naming rules
- All classes must be `sealed` unless explicitly designed for inheritance
- Use file-scoped namespaces
- Enable nullable reference types (`<Nullable>enable</Nullable>`)
- Prefix interfaces with `I` (e.g., `IAgentSkillFilesStore`)
- After completing work, clean up the code by removing any unused services injected through dependency injection, as well as any unused `using` statements

### MCP Architecture Pattern

Services are registered as **singletons** with caching for performance:

- `IAgentSkillFilesStore` - File system abstraction (implementation: `DefaultAgentSkillFilesStore`)
- `IMcpPromptProvider` - Skill body content → MCP prompts, cached after first call (implementation: `SkillPromptProvider`)
- `IMcpResourceProvider` - Skill files + references → MCP resources, cached after first call (implementation: `SkillResourceProvider`)

Parsers are static utility classes:

- `SkillFrontMatterParser` - Extracts YAML from `.md` front-matter
- `SkillYamlParser` - Parses `.yaml`/`.yml` files
- `SkillFileParser` - Unified parser that detects format and delegates

### MCP Registration API

**Generic (any .NET app), from `CrestApps.AgentSkills.Mcp`:**

```csharp
// Register services only (without attaching to an MCP server)
services.AddAgentSkillServices();
services.AddAgentSkillServices(options => options.Path = "/path/to/skills");

// Register services and attach prompts/resources to the MCP server builder
builder.AddAgentSkills();
builder.AddAgentSkills(options => options.Path = "/path/to/skills");
```

**Orchard Core–specific wrappers, from `CrestApps.AgentSkills.Mcp.OrchardCore`:**

```csharp
// Register services only
services.AddOrchardCoreAgentSkillServices();

// Register services and attach prompts/resources to the MCP server builder
builder.AddOrchardCoreSkills();
```

### Working with Skills

**Warning**: The `CrestApps.AgentSkills.OrchardCore` package **always overwrites** files in the `.agents/` folder at the solution root. Treat generated files as read-only — modifications will be lost on the next build.

**Adding a new skill**:

1. Open/confirm a "New Skill Request" issue first
2. Create directory under `src/CrestApps.AgentSkills/orchardcore/orchardcore-<skill-name>/`
3. Add `SKILL.md` with front-matter `name` matching the directory name exactly
4. Run validation scripts and full build/test
5. Submit PR linking the issue (e.g., `Fix #123`)

## Coding Standards

- Prefer minimal, focused changes and keep documentation in sync with code updates
- Analysis level: `latest-Recommended` (see `Directory.Build.props` for suppressed warnings)
- Build acceleration enabled for Visual Studio (`AccelerateBuildsInVisualStudio=true`)
