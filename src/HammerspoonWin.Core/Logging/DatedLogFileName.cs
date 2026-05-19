using System.Globalization;

namespace HammerspoonWin.Core.Logging;

public static class DatedLogFileName
{
    public const string DateFormat = "MM-dd-yyyy-HH-mm";

    public static string CreateUniquePath(string directory, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);

        var stem = timestamp.ToString(DateFormat, CultureInfo.InvariantCulture);
        var candidate = Path.Combine(directory, $"{stem}.log");
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var suffix = 2;
        while (true)
        {
            candidate = Path.Combine(directory, $"{stem}-{suffix}.log");
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }
}
