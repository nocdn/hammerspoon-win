namespace HsWin.Core.Applications;

public static class ProcessNameMatcher
{
    public static string Normalize(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        var trimmed = processName.Trim();
        var fileName = Path.GetFileName(trimmed);
        var extension = Path.GetExtension(fileName);

        return string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(fileName)
            : fileName;
    }
}
