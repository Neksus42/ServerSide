using ServerSideForPC.Models;

namespace ServerSideForPC.Services;

public sealed class PcStateService : IDisposable
{
    private readonly AudioService _audio;
    private readonly DisplayService _display;

    public event Action? StateChanged;

    public PcStateService(AudioService audio, DisplayService display)
    {
        _audio = audio;
        _display = display;
        _audio.StateChanged += OnStateChanged;
        _display.StateChanged += OnStateChanged;
    }

    public PcStateSnapshot GetSnapshot()
    {
        var audioState = _audio.GetCurrentState();
        return new PcStateSnapshot(
            Environment.MachineName,
            audioState.Volume,
            audioState.Muted,
            audioState.DefaultDeviceId,
            _audio.GetDevices(),
            _display.CurrentMode);
    }

    private void OnStateChanged() => StateChanged?.Invoke();

    public void Dispose()
    {
        _audio.StateChanged -= OnStateChanged;
        _display.StateChanged -= OnStateChanged;
    }
}
