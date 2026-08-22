namespace ServerSideForPC.Configuration;

public sealed class PcControlOptions
{
    public int Port { get; init; } = 8888;
    public string ApiKey { get; init; } = "home-pc-control";
    public string TvAudioName { get; init; } = "TV";
    public float? TvVolume { get; init; } = 0.25f;
    public string DesktopAudioName { get; init; } = string.Empty;
    public float? DesktopVolume { get; init; }
    public int TvAudioWaitSeconds { get; init; } = 12;
}
