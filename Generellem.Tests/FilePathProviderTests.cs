using Generellem.Services;

namespace Generellem.DocumentSource.Tests;

public class FilePathProviderTests
{
    [Theory]
    [InlineData("\\bin")]
    [InlineData("\\obj")]
    [InlineData("/Users/dev/repo/bin")]
    [InlineData("/Users/dev/repo/obj")]
    [InlineData("C:\\repo\\bin\\Debug")]
    [InlineData("/Users/dev/repo/.git")]
    [InlineData("/Users/dev/repo/.vs")]
    public void IsPathExcluded_DetectsExcludedPaths(string path)
    {
        Mock<IGenerellemFiles> gemFiles = new();

        FilePathProvider provider = new(gemFiles.Object);

        var result = provider.IsPathExcluded(path);

        Assert.True(result);
    }

    [Theory]
    [InlineData("/Users/dev/repo/Documentation")]
    [InlineData("/Users/dev/repo/Generellem")]
    [InlineData("C:\\repo\\src")]
    public void IsPathExcluded_AllowsNormalPaths(string path)
    {
        Mock<IGenerellemFiles> gemFiles = new();
        FilePathProvider provider = new(gemFiles.Object);

        Assert.False(provider.IsPathExcluded(path));
    }
}
