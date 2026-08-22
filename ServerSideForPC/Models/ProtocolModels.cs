using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServerSideForPC.Models;

public sealed record ProtocolRequest(
    [property: JsonPropertyName("v")] int Version,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("payload")] JsonElement Payload);

public sealed record AudioDeviceInfo(
    string Id,
    string Name,
    bool IsDefault);

public sealed record PcStateSnapshot(
    string PcName,
    float Volume,
    bool Muted,
    string? DefaultAudioDeviceId,
    IReadOnlyList<AudioDeviceInfo> AudioDevices,
    string DisplayMode);

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
}
