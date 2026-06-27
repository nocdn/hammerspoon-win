using System.IO;

namespace HsWin.App.Tests;

public sealed class CliInstallServiceTests
{
    [Fact]
    public void PathContainsDirectoryMatchesExistingPathSegment()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var pathValue = string.Join(Path.PathSeparator, "C:\\Windows", directory, "C:\\Tools");

        Assert.True(CliInstallService.PathContainsDirectory(pathValue, directory));
    }

    [Fact]
    public void AddDirectoryToPathAppendsMissingDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var updated = CliInstallService.AddDirectoryToPath("C:\\Windows", directory);

        Assert.EndsWith($"{Path.PathSeparator}{Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)}", updated, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddDirectoryToPathDoesNotDuplicateExistingDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var pathValue = CliInstallService.AddDirectoryToPath("C:\\Windows", directory);

        var updated = CliInstallService.AddDirectoryToPath(pathValue, directory);

        Assert.Equal(pathValue, updated);
    }
}
