using System.Reflection;

namespace HsWin.Core.Scripting;

internal static class ScriptBootstrap
{
    private const string ResourceName = "HsWin.Core.Scripting.Resources.bootstrap.js";

    public static string Load()
    {
        var assembly = typeof(ScriptBootstrap).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Could not load embedded script resource '{ResourceName}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
