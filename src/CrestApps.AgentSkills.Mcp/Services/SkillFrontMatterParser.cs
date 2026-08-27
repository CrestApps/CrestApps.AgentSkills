namespace CrestApps.AgentSkills.Mcp.Services;

/// <summary>
/// Parses YAML front-matter from SKILL.md files.
/// Front-matter is delimited by <c>---</c> markers at the start of the file.
/// The front-matter block is deserialized with YamlDotNet, reading only the top-level
/// <c>name</c>, <c>description</c>, and (optional) <c>mcp</c> keys; nested/unknown keys
/// (for example <c>license</c>, <c>metadata</c>, <c>version</c>) are ignored.
/// </summary>
public static class SkillFrontMatterParser
{
    private const string FrontMatterDelimiter = "---";

    /// <summary>
    /// Attempts to parse a SKILL.md file, extracting the front-matter fields and body content.
    /// </summary>
    /// <param name="content">The full content of the SKILL.md file.</param>
    /// <param name="name">The parsed <c>name</c> field from front-matter.</param>
    /// <param name="description">The parsed <c>description</c> field from front-matter.</param>
    /// <param name="body">The body content after the closing <c>---</c> delimiter.</param>
    /// <returns><c>true</c> if valid front-matter with required fields was found; otherwise <c>false</c>.</returns>
    public static bool TryParse(string content, out string name, out string description, out string body)
    {
        return TryParse(content, out name, out description, out body, out _);
    }

    /// <summary>
    /// Attempts to parse a SKILL.md file, extracting the front-matter fields, body content,
    /// and the optional <c>mcp</c> channel declaration.
    /// </summary>
    /// <param name="content">The full content of the SKILL.md file.</param>
    /// <param name="name">The parsed <c>name</c> field from front-matter.</param>
    /// <param name="description">The parsed <c>description</c> field from front-matter.</param>
    /// <param name="body">The body content after the closing <c>---</c> delimiter.</param>
    /// <param name="mcp">The raw <c>mcp</c> front-matter value, or <c>null</c> when not declared.</param>
    /// <returns><c>true</c> if valid front-matter with required fields was found; otherwise <c>false</c>.</returns>
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

        var trimmedContent = content.TrimStart();

        if (!trimmedContent.StartsWith(FrontMatterDelimiter, StringComparison.Ordinal))
        {
            return false;
        }

        // Find the closing delimiter.
        var firstDelimiterEnd = trimmedContent.IndexOf('\n');
        if (firstDelimiterEnd < 0)
        {
            return false;
        }

        var afterFirstDelimiter = firstDelimiterEnd + 1;
        var closingIndex = trimmedContent.IndexOf(
            $"\n{FrontMatterDelimiter}",
            afterFirstDelimiter,
            StringComparison.Ordinal);

        if (closingIndex < 0)
        {
            return false;
        }

        var frontMatter = trimmedContent[afterFirstDelimiter..closingIndex];
        var bodyStart = closingIndex + 1 + FrontMatterDelimiter.Length;

        // Skip any trailing newline after the closing delimiter.
        if (bodyStart < trimmedContent.Length && trimmedContent[bodyStart] == '\r')
        {
            bodyStart++;
        }

        if (bodyStart < trimmedContent.Length && trimmedContent[bodyStart] == '\n')
        {
            bodyStart++;
        }

        body = bodyStart < trimmedContent.Length
            ? trimmedContent[bodyStart..]
            : string.Empty;

        // Deserialize the front-matter block with YamlDotNet, reading only top-level keys.
        SkillDocument.Model? model;

        try
        {
            model = SkillDocument.Deserializer.Deserialize<SkillDocument.Model>(frontMatter);
        }
        catch
        {
            return false;
        }

        if (model is null
            || string.IsNullOrWhiteSpace(model.Name)
            || string.IsNullOrWhiteSpace(model.Description))
        {
            name = string.Empty;
            description = string.Empty;
            return false;
        }

        name = model.Name.Trim();
        description = model.Description.Trim();
        mcp = string.IsNullOrWhiteSpace(model.Mcp) ? null : model.Mcp.Trim();

        return true;
    }
}
