using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CrestApps.AgentSkills.Mcp.Services;

/// <summary>
/// Shared YAML deserialization primitives for skill front-matter and YAML skill files.
/// Only top-level keys are read; nested and unknown keys (for example <c>license</c>,
/// <c>metadata</c>, <c>version</c>) are ignored.
/// </summary>
internal static class SkillDocument
{
    /// <summary>
    /// A shared deserializer configured to match the top-level skill keys and ignore everything else.
    /// </summary>
    public static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Represents the recognized top-level keys of a skill document.
    /// </summary>
    public sealed class Model
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Body { get; set; }

        public string? Mcp { get; set; }
    }
}
