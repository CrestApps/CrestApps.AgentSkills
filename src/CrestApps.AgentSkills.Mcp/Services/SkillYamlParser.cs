namespace CrestApps.AgentSkills.Mcp.Services;

/// <summary>
/// Parses skill definitions from YAML (<c>.yaml</c> / <c>.yml</c>) files.
/// Expects a YAML document with at least <c>name</c> and <c>description</c> fields.
/// An optional <c>body</c> field provides the skill body content and an optional
/// <c>mcp</c> field declares the MCP channel.
/// </summary>
public static class SkillYamlParser
{
    /// <summary>
    /// Attempts to parse a YAML skill file, extracting the required fields.
    /// </summary>
    /// <param name="content">The full content of the YAML file.</param>
    /// <param name="name">The parsed <c>name</c> field.</param>
    /// <param name="description">The parsed <c>description</c> field.</param>
    /// <param name="body">The parsed <c>body</c> field, or empty if not present.</param>
    /// <returns><c>true</c> if valid YAML with required fields was found; otherwise <c>false</c>.</returns>
    public static bool TryParse(string content, out string name, out string description, out string body)
    {
        return TryParse(content, out name, out description, out body, out _);
    }

    /// <summary>
    /// Attempts to parse a YAML skill file, extracting the required fields and the optional
    /// <c>mcp</c> channel declaration.
    /// </summary>
    /// <param name="content">The full content of the YAML file.</param>
    /// <param name="name">The parsed <c>name</c> field.</param>
    /// <param name="description">The parsed <c>description</c> field.</param>
    /// <param name="body">The parsed <c>body</c> field, or empty if not present.</param>
    /// <param name="mcp">The raw <c>mcp</c> value, or <c>null</c> when not declared.</param>
    /// <returns><c>true</c> if valid YAML with required fields was found; otherwise <c>false</c>.</returns>
    public static bool TryParse(string content, out string name, out string description, out string body, out string? mcp)
    {
        name = string.Empty;
        description = string.Empty;
        body = string.Empty;
        mcp = null;

        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            var skill = SkillDocument.Deserializer.Deserialize<SkillDocument.Model>(content);

            if (skill is null
                || string.IsNullOrWhiteSpace(skill.Name)
                || string.IsNullOrWhiteSpace(skill.Description))
            {
                return false;
            }

            name = skill.Name.Trim();
            description = skill.Description.Trim();
            body = skill.Body?.Trim() ?? string.Empty;
            mcp = string.IsNullOrWhiteSpace(skill.Mcp) ? null : skill.Mcp.Trim();

            return true;
        }
        catch
        {
            return false;
        }
    }
}
