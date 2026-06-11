using System.Security.Cryptography;
using System.Text;

namespace HsWin.Core.Logging;

public static class LogSanitizer
{
    public static string DescribeCommand(string command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var length = command.Length;
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(command)))[..16];
        return $"length={length} sha256={fingerprint}";
    }

    public static string DescribeUrl(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return $"invalid-url length={url.Length}";
        }

        var builder = new StringBuilder();
        builder.Append(uri.Scheme);
        builder.Append("://");
        builder.Append(uri.Host);
        if (!uri.IsDefaultPort)
        {
            builder.Append(':');
            builder.Append(uri.Port);
        }

        builder.Append(uri.AbsolutePath);

        if (!string.IsNullOrEmpty(uri.Query))
        {
            var keys = uri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2)[0])
                .Where(key => !string.IsNullOrEmpty(key))
                .ToArray();

            builder.Append(keys.Length > 0 ? $"?keys={string.Join(",", keys)}" : "?<redacted-query>");
        }

        return builder.ToString();
    }
}
