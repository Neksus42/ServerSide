using System.Diagnostics;

namespace ServerSideForPC.Services;

public sealed class PowerService
{
    public void Shutdown()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            Arguments = "/s /t 1",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }
}
