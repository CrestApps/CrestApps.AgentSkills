# CrestApps.AgentSkills Development Instructions

**ALWAYS reference these instructions first and fall back to searching only if needed.**

## Project Overview

CrestApps.AgentSkills contains shared AI agent skills and MCP tooling for .NET applications and Orchard Core projects.

- **Target Framework**: .NET 10 (net10.0)
- **SDK Version**: .NET 10.0.100 (see `global.json`)
- **Package Management**: Central Package Management via `Directory.Packages.props`
- **Skill source of truth**: `src/CrestApps.AgentSkills/orchardcore/` (27+ Orchard Core skills)

### Three Distinct Package Architectures

1. **`CrestApps.AgentSkills.Mcp`** - Generic MCP engine
   - Framework-agnostic skill parser and MCP provider
   - Supports `.md` (front-matter) and `.yaml`/`.yml` skill formats
   - Services: `IAgentSkillFilesStore`, `IMcpPromptProvider`, `IMcpResourceProvider`
   - No bundled skills — expects consumer to provide skill directory
   
2. **`CrestApps.AgentSkills.OrchardCore`** - Dev-time skill distributor
   - Development dependency only (`IncludeBuildOutput=false`, `DevelopmentDependency=true`)
   - Uses MSBuild `.targets` to copy skills to solution root `.agents/skills/` on first build
   - No runtime code — purely for local AI authoring
   
3. **`CrestApps.AgentSkills.Mcp.OrchardCore`** - Runtime MCP server
   - Extends `CrestApps.AgentSkills.Mcp` with OrchardCore-specific wiring
   - Bundles skills via `contentFiles` (copied to bin output on restore)
   - Uses OrchardCore `FileSystemStore` for file access
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

**Note**: Tests use xUnit. Test classes follow pattern `<Subject>Tests.cs` with `sealed` modifier.

## Skill Validation

Each skill directory under `src/CrestApps.AgentSkills/orchardcore/` must contain a `SKILL.md` file with YAML front-matter (must include `name` and `description`).

### Skill Requirements

- **File name**: `SKILL.md` (uppercase, not `skill.md`)
- **Directory naming**: lowercase, hyphenated, prefixed with `orchardcore.` (e.g., `orchardcore.content-types`)
- **`name` field**: Must exactly match directory name
- **Front-matter**: Must start with `---` and contain closing `---`
- **References**: Optional `references/` subdirectory for additional `.md` files (not `examples/`)

### Skill Documentation Conventions (from CONTRIBUTING.md)

- All recipe step JSON blocks must be wrapped in root recipe format: `{ "steps": [...] }`
- All C# classes in code samples must use the `sealed` modifier
- Third-party module packages (non `OrchardCore.*`) must be installed in the web/startup project

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
  - `OrchardCore.FileStorage.FileSystem` (2.2.1) - File system access
  - `YamlDotNet` (16.3.0) - YAML parsing
  - `xunit` (2.9.3) - Testing framework

## Key Conventions

### Code Style

- Follow `.editorconfig` for formatting and naming rules
- All classes must be `sealed` unless explicitly designed for inheritance
- Use file-scoped namespaces
- Enable nullable reference types (`<Nullable>enable</Nullable>`)
- Prefix interface implementations with `I` (e.g., `IAgentSkillFilesStore`)

### MCP Architecture Pattern

Services are registered as **singletons** with caching for performance:
- `IAgentSkillFilesStore` / `IMcpResourceFileStore` - File system abstraction
- `IMcpPromptProvider` - Skill body content → MCP prompts (cached after first call)
- `IMcpResourceProvider` - Skill files + references → MCP resources (cached after first call)

Parsers are static utility classes:
- `SkillFrontMatterParser` - Extracts YAML from `.md` front-matter
- `SkillYamlParser` - Parses `.yaml`/`.yml` files
- `SkillFileParser` - Unified parser that detects format and delegates

### Working with Skills

**Warning**: The `CrestApps.AgentSkills.OrchardCore` package **always overwrites** files in `.agents/` folder in solution root. Treat generated files as read-only — modifications will be lost on next build.

**Adding a new skill**:
1. Open/confirm a "New Skill Request" issue first
2. Create directory under `src/CrestApps.AgentSkills/orchardcore/<skill-name>/`
3. Add `SKILL.md` with front-matter matching directory name
4. Run validation scripts and full build/test
5. Submit PR linking the issue (e.g., `Fix #123`)

## Coding Standards

- Prefer minimal, focused changes and keep documentation in sync with code updates
- Analysis level: `latest-Recommended` (see `Directory.Build.props` for suppressed warnings)
- Build acceleration enabled for Visual Studio (`AccelerateBuildsInVisualStudio=true`)
