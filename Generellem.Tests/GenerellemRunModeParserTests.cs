using Generellem.DependencyInjection;

namespace Generellem.DependencyInjection.Tests;

public class GenerellemRunModeParserTests
{
    [Theory]
    [InlineData("Both", GenerellemRunMode.Both)]
    [InlineData("ingest", GenerellemRunMode.Ingest)]
    [InlineData("QUERY", GenerellemRunMode.Query)]
    public void TryParse_AcceptsModeNames(string text, GenerellemRunMode expected)
    {
        Assert.True(GenerellemRunModeParser.TryParse(text, out GenerellemRunMode mode));
        Assert.Equal(expected, mode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sometimes")]
    [InlineData("1")]
    public void TryParse_RejectsUnknownText(string? text)
    {
        Assert.False(GenerellemRunModeParser.TryParse(text, out _));
    }

    [Fact]
    public void TryReadArgs_ReadsModeFlag()
    {
        bool found = GenerellemRunModeParser.TryReadArgs(["--profile", "Ollama", "--mode", "query"], out GenerellemRunMode mode, out bool parsed);

        Assert.True(found);
        Assert.True(parsed);
        Assert.Equal(GenerellemRunMode.Query, mode);
    }

    [Fact]
    public void TryReadArgs_ReadsEqualsForm()
    {
        bool found = GenerellemRunModeParser.TryReadArgs(["--mode=Ingest"], out GenerellemRunMode mode, out bool parsed);

        Assert.True(found);
        Assert.True(parsed);
        Assert.Equal(GenerellemRunMode.Ingest, mode);
    }

    [Fact]
    public void TryReadArgs_LastFlagWins()
    {
        bool found = GenerellemRunModeParser.TryReadArgs(["--mode", "ingest", "--mode=query"], out GenerellemRunMode mode, out bool parsed);

        Assert.True(found);
        Assert.True(parsed);
        Assert.Equal(GenerellemRunMode.Query, mode);
    }

    [Fact]
    public void TryReadArgs_UnknownValueFallsBackToBoth()
    {
        bool found = GenerellemRunModeParser.TryReadArgs(["--mode", "later"], out GenerellemRunMode mode, out bool parsed);

        Assert.True(found);
        Assert.False(parsed);
        Assert.Equal(GenerellemRunMode.Both, mode);
    }

    [Fact]
    public void TryReadArgs_IsFalseWhenFlagIsAbsent()
    {
        bool found = GenerellemRunModeParser.TryReadArgs(["--profile", "Sqlite"], out _, out _);

        Assert.False(found);
    }
}
