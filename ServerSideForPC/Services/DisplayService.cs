using System.Diagnostics;

namespace ServerSideForPC.Services;

public sealed class DisplayService
{
    private readonly Lock _gate = new();
    private string _currentMode = "unknown";

    public event Action? StateChanged;

    public string CurrentMode
    {
        get
        {
            lock (_gate)
            {
                return _currentMode;
            }
        }
    }

    public Task ActivateTvAsync(CancellationToken cancellationToken = default) =>
        SetModeAsync("external", "/external", cancellationToken);

    public Task ActivateExtendAsync(CancellationToken cancellationToken = default) =>
        SetModeAsync("extend", "/extend", cancellationToken);

    private async Task SetModeAsync(
        string stateName,
        string displaySwitchArgument,
        CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "DisplaySwitch.exe",
            Arguments = displaySwitchArgument,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Failed to start DisplaySwitch.exe.");

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        lock (_gate)
        {
            _currentMode = stateName;
        }

        StateChanged?.Invoke();
    }
}
