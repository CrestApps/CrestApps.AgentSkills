using Microsoft.Extensions.Logging;

namespace CrestApps.AgentSkills.Mcp.Services;

/// <summary>
/// Resolves the raw <c>mcp</c> front-matter value into an effective <see cref="McpChannel"/>.
/// </summary>
public static class McpChannelResolver
{
    /// <summary>
    /// Resolves the effective MCP channel for a skill.
    /// An absent value falls back to <paramref name="defaultChannel"/>; an unrecognized value
    /// logs a warning and falls back to <paramref name="defaultChannel"/>.
    /// </summary>
    /// <param name="rawValue">The raw <c>mcp</c> value from the skill's front-matter, or <c>null</c>.</param>
    /// <param name="defaultChannel">The channel to fall back to when the value is absent or invalid.</param>
    /// <param name="skillName">The skill name, used for logging.</param>
    /// <param name="logger">The logger used to warn on invalid values.</param>
    /// <returns>The effective <see cref="McpChannel"/>.</returns>
    public static McpChannel Resolve(string? rawValue, McpChannel defaultChannel, string skillName, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return defaultChannel;
        }

        if (Enum.TryParse<McpChannel>(rawValue.Trim(), ignoreCase: true, out var channel)
            && Enum.IsDefined(channel))
        {
            return channel;
        }

        logger.LogWarning(
            "Skill '{SkillName}' declares an unrecognized mcp channel '{McpValue}'; falling back to the default channel '{DefaultChannel}'.",
            skillName, rawValue, defaultChannel);

        return defaultChannel;
    }
}
