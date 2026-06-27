using HsWin.Core.Commands;
using HsWin.Core.Logging;

namespace HsWin.Core.Tests;

public sealed class HsWinCommandProtocolTests
{
    [Fact]
    public void RequestRoundTripsThroughProtocol()
    {
        var request = new HsWinCommandRequest(HsWinCommandNames.ConfigReload);

        var roundTripped = HsWinCommandProtocol.DeserializeRequest(HsWinCommandProtocol.SerializeRequest(request));

        Assert.Equal(request, roundTripped);
    }

    [Fact]
    public void ResponseRoundTripsThroughProtocol()
    {
        var response = HsWinCommandResponse.Ok("Config reload requested.");

        var roundTripped = HsWinCommandProtocol.DeserializeResponse(HsWinCommandProtocol.SerializeResponse(response));

        Assert.Equal(response, roundTripped);
    }

    [Fact]
    public void ClientReceivesServerResponse()
    {
        var pipeName = $"HsWin.Core.Tests.{Guid.NewGuid():N}";
        using var server = new HsWinCommandServer(
            pipeName,
            request => HsWinCommandResponse.Ok($"handled {request.Command}"),
            NullRuntimeLogger.Instance);
        server.Start();

        var response = new HsWinCommandClient(pipeName).Send(
            new HsWinCommandRequest(HsWinCommandNames.ConfigReload),
            timeoutMs: 5000);

        Assert.True(response.Success);
        Assert.Equal("handled config.reload", response.Message);
    }
}
