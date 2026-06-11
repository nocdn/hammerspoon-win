using HsWin.Core.Logging;

namespace HsWin.Core.Tests;

public sealed class LogSanitizerTests
{
    [Fact]
    public void DescribeCommandDoesNotReturnRawCommandText()
    {
        const string command = "curl https://api.example.test --header Authorization: Bearer fake-token";

        var description = LogSanitizer.DescribeCommand(command);

        Assert.DoesNotContain("curl", description);
        Assert.DoesNotContain("fake-token", description);
        Assert.Contains("length=", description);
        Assert.Contains("sha256=", description);
    }

    [Fact]
    public void DescribeCommandProducesStableFingerprintForSameInput()
    {
        const string command = "echo hello";

        var first = LogSanitizer.DescribeCommand(command);
        var second = LogSanitizer.DescribeCommand(command);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DescribeUrlRedactsQueryValuesButPreservesHostAndPath()
    {
        const string url = "https://api.example.test/v1/upload?token=secret-value&model=scribe-v1";

        var description = LogSanitizer.DescribeUrl(url);

        Assert.Equal("https://api.example.test/v1/upload?keys=token,model", description);
        Assert.DoesNotContain("secret-value", description);
        Assert.DoesNotContain("scribe-v1", description);
    }

    [Fact]
    public void DescribeUrlReturnsMetadataForInvalidUrl()
    {
        const string url = "not a valid url ?token=secret";

        var description = LogSanitizer.DescribeUrl(url);

        Assert.Equal($"invalid-url length={url.Length}", description);
        Assert.DoesNotContain("secret", description);
    }
}
