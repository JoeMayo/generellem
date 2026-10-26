using Generellem.DependencyInjection;

using Microsoft.Extensions.Options;

namespace Generellem.Services;

/// <summary>
/// Manages the location where configuration files go.
/// </summary>
public class GenerellemFiles(IOptions<GenerellemOptions> options) : IGenerellemFiles
{
    /// <summary>
    /// AppData subfolder from <see cref="GenerellemOptions.SubFolder"/>.
    /// Prevents one instance of Generellem from consuming or overwriting files of another instance.
    /// </summary>
    public string SubFolder => options.Value.SubFolder ?? string.Empty;

    public string GetAppDataPath(string fileName = "")
    {
        string folder =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Generellem",
                SubFolder);

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        return Path.Combine(folder, fileName);
    }
}