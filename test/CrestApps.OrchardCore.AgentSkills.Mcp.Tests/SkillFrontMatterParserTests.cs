using CrestApps.OrchardCore.AgentSkills.Mcp.Services;
using Xunit;

namespace CrestApps.OrchardCore.AgentSkills.Mcp.Tests;

public sealed class SkillFrontMatterParserTests
{
    [Fact]
    public void TryParse_ValidFrontMatter_ReturnsTrueAndExtractsFields()
    {
        var content = "---\nname: test-skill\ndescription: A test skill for testing.\n---\n# Body Content\nSome text.";

        var result = SkillFrontMatterParser.TryParse(content, out var name, out var description, out var body);

        Assert.True(result);
        Assert.Equal("test-skill", name);
        Assert.Equal("A test skill for testing.", description);
        Assert.Equal("# Body Content\nSome text.", body);
    }

    [Fact]
    public void TryParse_MissingOpeningDelimiter_ReturnsFalse()
    {
        var content = "name: test-skill\ndescription: A test.\n---\n# Body";

        var result = SkillFrontMatterParser.TryParse(content, out _, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_MissingClosingDelimiter_ReturnsFalse()
    {
        var content = "---\nname: test-skill\ndescription: A test.\n# Body";

        var result = SkillFrontMatterParser.TryParse(content, out _, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_MissingNameField_ReturnsFalse()
    {
        var content = "---\ndescription: A test skill.\n---\n# Body";

        var result = SkillFrontMatterParser.TryParse(content, out _, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_MissingDescriptionField_ReturnsFalse()
    {
        var content = "---\nname: test-skill\n---\n# Body";

        var result = SkillFrontMatterParser.TryParse(content, out _, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_EmptyContent_ReturnsFalse()
    {
        var result = SkillFrontMatterParser.TryParse("", out _, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_NullContent_ReturnsFalse()
    {
        var result = SkillFrontMatterParser.TryParse(null!, out _, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_WhitespaceOnlyContent_ReturnsFalse()
    {
        var result = SkillFrontMatterParser.TryParse("   \n  \n  ", out _, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_AdditionalMetadataFieldsPreserved()
    {
        var content = "---\nname: my-skill\ndescription: My skill description.\nlicense: MIT\nmetadata:\n---\n# Content";

        var result = SkillFrontMatterParser.TryParse(content, out var name, out var description, out _);

        Assert.True(result);
        Assert.Equal("my-skill", name);
        Assert.Equal("My skill description.", description);
    }

    [Fact]
    public void TryParse_EmptyBody_ReturnsTrueWithEmptyBody()
    {
        var content = "---\nname: test-skill\ndescription: A test.\n---\n";

        var result = SkillFrontMatterParser.TryParse(content, out _, out _, out var body);

        Assert.True(result);
        Assert.Equal(string.Empty, body);
    }

    [Fact]
    public void TryParse_WindowsLineEndings_WorksCorrectly()
    {
        var content = "---\r\nname: test-skill\r\ndescription: A test skill.\r\n---\r\n# Body Content\r\nSome text.";

        var result = SkillFrontMatterParser.TryParse(content, out var name, out var description, out var body);

        Assert.True(result);
        Assert.Equal("test-skill", name);
        Assert.Equal("A test skill.", description);
        Assert.StartsWith("# Body Content", body);
    }
}
