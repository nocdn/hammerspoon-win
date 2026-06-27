using System.Text.Json;

namespace HsWin.Core.Commands;

public static class HsWinCommandProtocol
{
    public const string PipeName = "HsWin.Command.5E65F5D3-AC46-43D2-B31C-B98F8757C640";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string SerializeRequest(HsWinCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return JsonSerializer.Serialize(request, SerializerOptions);
    }

    public static HsWinCommandRequest DeserializeRequest(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        return JsonSerializer.Deserialize<HsWinCommandRequest>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Command request payload was empty.");
    }

    public static string SerializeResponse(HsWinCommandResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return JsonSerializer.Serialize(response, SerializerOptions);
    }

    public static HsWinCommandResponse DeserializeResponse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        return JsonSerializer.Deserialize<HsWinCommandResponse>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Command response payload was empty.");
    }
}
