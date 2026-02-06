namespace CrestApps.OrchardCore.AgentSkills;

/// <summary>
/// Represents a builder for configuring agent skills.
/// Consumers should implement this interface to integrate with their agent framework.
/// </summary>
public interface IAgentBuilder
{
    /// <summary>
    /// Adds skills from the specified file system path.
    /// </summary>
    /// <param name="path">The path to load skills from.</param>
    void AddSkillsFromPath(string path);
}
