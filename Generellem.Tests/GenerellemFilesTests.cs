using Generellem.DependencyInjection;
using Generellem.Services;

using Microsoft.Extensions.Options;

namespace Generellem.Services.Tests;

public class GenerellemFilesTests
{
    [Fact]
    public void GetAppDataPath_UsesSubFolderFromOptions()
    {
        string subFolder = "GenerellemFilesTests-" + Guid.NewGuid().ToString("N");
        GenerellemFiles files = new(Options.Create(new GenerellemOptions { SubFolder = subFolder }));
        string path = files.GetAppDataPath("generellem.db");

        try
        {
            Assert.EndsWith(Path.Combine("Generellem", subFolder, "generellem.db"), path);
            Assert.Equal(subFolder, files.SubFolder);
        }
        finally
        {
            string? folder = Path.GetDirectoryName(path);
            if (folder is not null && Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}