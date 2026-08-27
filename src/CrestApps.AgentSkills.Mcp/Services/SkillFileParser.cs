namespace CrestApps.AgentSkills.Mcp.Services;

/// <summary>
/// Unified skill file parser that detects the file format (Markdown or YAML)
/// and delegates to the appropriate parser.
/// Supports <c>.md</c>, <c>.yaml</c>, and <c>.yml</c> extensions.
/// </summary>
public static class SkillFileParser
{
    /// <summary>
    /// Attempts to parse a skill file by detecting its format from the file name.
    /// </summary>
    /// <param name="fileName">The file name or path used to determine the format (e.g., <c>SKILL.md</c> or <c>SKILL.yaml</c>).</param>
    /// <param name="content">The full content of the skill file.</param>
    /// <param name="name">The parsed <c>name</c> field.</param>
    /// <param name="description">The parsed <c>description</c> field.</param>
    /// <param name="body">The parsed body content.</param>
    /// <returns><c>true</c> if the file was parsed successfully with all required fields; otherwise <c>false</c>.</returns>
    public static bool TryParse(
        string fileName,
        string content,
        out string name,
        out string description,
        out string body)
    {
        return TryParse(fileName, content, out name, out description, out body, out _);
    }

    /// <summary>
    /// Attempts to parse a skill file by detecting its format from the file name,
    /// additionally surfacing the optional <c>mcp</c> channel declaration.
    /// </summary>
    /// <param name="fileName">The file name or path used to determine the format (e.g., <c>SKILL.md</c> or <c>SKILL.yaml</c>).</param>
    /// <param name="content">The full content of the skill file.</param>
    /// <param name="name">The parsed <c>name</c> field.</param>
    /// <param name="description">The parsed <c>description</c> field.</param>
    /// <param name="body">The parsed body content.</param>
    /// <param name="mcp">The raw <c>mcp</c> value, or <c>null</c> when not declared.</param>
    /// <returns><c>true</c> if the file was parsed successfully with all required fields; otherwise <c>false</c>.</returns>
    public static bool TryParse(
        string fileName,
        string content,
        out string name,
        out string description,
        out string body,
        out string? mcp)
    {
        name = string.Empty;
        description = string.Empty;
        body = string.Empty;
        mcp = null;

        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        if (fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return SkillFrontMatterParser.TryParse(content, out name, out description, out body, out mcp);
        }

        if (fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
        {
            return SkillYamlParser.TryParse(content, out name, out description, out body, out mcp);
        }

        return false;
    }

    /// <summary>
    /// Gets all supported skill file names in priority order.
    /// </summary>
    public static IReadOnlyList<string> SupportedSkillFileNames { get; } =
    [
        "SKILL.md",
        "SKILL.yaml",
        "SKILL.yml",
    ];
}
