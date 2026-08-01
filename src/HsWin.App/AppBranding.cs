using System.Reflection;

namespace HsWin.App;

internal static class AppBranding
{
    public const string DisplayName = "Hammerspoon (Windows Edition)";

    public static string Version
    {
        get
        {
            var assembly = typeof(AppBranding).Assembly;
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                var buildMetadataSeparator = informationalVersion.IndexOf('+');
                return (buildMetadataSeparator >= 0
                        ? informationalVersion[..buildMetadataSeparator]
                        : informationalVersion)
                    .Trim();
            }

            return assembly.GetName().Version?.ToString(3) ?? "unknown";
        }
    }
}
