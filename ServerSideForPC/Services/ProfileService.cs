using ServerSideForPC.Configuration;

namespace ServerSideForPC.Services;

public sealed class ProfileService
{
    private readonly AudioService _audio;
    private readonly DisplayService _display;
    private readonly PcControlOptions _options;
    private string? _rememberedDesktopDeviceId;
    private float? _rememberedDesktopVolume;

    public ProfileService(AudioService audio, DisplayService display, PcControlOptions options)
    {
        _audio = audio;
        _display = display;
        _options = options;
    }

    public async Task ActivateTvAsync(CancellationToken cancellationToken)
    {
        if (!string.Equals(_display.CurrentMode, "external", StringComparison.OrdinalIgnoreCase))
        {
            var current = _audio.GetCurrentState();
            _rememberedDesktopDeviceId = current.DefaultDeviceId;
            _rememberedDesktopVolume = current.Volume;
        }

        await _display.ActivateTvAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_options.TvAudioName))
        {
            var found = await _audio.WaitAndSetDefaultByFriendlyNameAsync(
                _options.TvAudioName,
                TimeSpan.FromSeconds(_options.TvAudioWaitSeconds),
                cancellationToken).ConfigureAwait(false);

            if (!found)
            {
                throw new InvalidOperationException(
                    $"TV audio device containing '{_options.TvAudioName}' was not found.");
            }
        }

        
    }

    public async Task ActivateExtendAsync(CancellationToken cancellationToken)
    {
        await _display.ActivateExtendAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_options.DesktopAudioName))
        {
            var found = await _audio.WaitAndSetDefaultByFriendlyNameAsync(
                _options.DesktopAudioName,
                TimeSpan.FromSeconds(3),
                cancellationToken).ConfigureAwait(false);

            if (!found)
            {
                throw new InvalidOperationException(
                    $"Desktop audio device containing '{_options.DesktopAudioName}' was not found.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(_rememberedDesktopDeviceId))
        {
            try
            {
                _audio.SetDefaultDevice(_rememberedDesktopDeviceId);
            }
            catch
            {
                // The previous desktop endpoint may have disappeared. Keeping the current endpoint is safer.
            }
        }

        if (_options.DesktopVolume is { } desktopVolume)
        {
            _audio.SetVolume(desktopVolume);
        }
        else if (_rememberedDesktopVolume is { } rememberedVolume)
        {
            _audio.SetVolume(rememberedVolume);
        }

        _rememberedDesktopDeviceId = null;
        _rememberedDesktopVolume = null;
    }
}
