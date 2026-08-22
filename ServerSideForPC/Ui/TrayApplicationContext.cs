using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using ServerSideForPC.Configuration;

namespace ServerSideForPC.Ui;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly IHost _host;
    private readonly NotifyIcon _notifyIcon;
    private readonly string _settingsPath;

    public TrayApplicationContext(IHost host, PcControlOptions options)
    {
        _host = host;
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        var menu = new ContextMenuStrip();
        menu.Items.Add($"PC Control :{options.Port}").Enabled = false;
        menu.Items.Add("Open settings", null, (_, _) => OpenSettings());
        menu.Items.Add("Exit", null, async (_, _) => await ExitAsync());

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = $"PC Control - port {options.Port}",
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => OpenSettings();
        _notifyIcon.ShowBalloonTip(1500, "PC Control", $"Server started on port {options.Port}", ToolTipIcon.Info);
    }

    private void OpenSettings()
    {
        if (!File.Exists(_settingsPath))
        {
            MessageBox.Show($"Settings file not found:\n{_settingsPath}", "PC Control");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _settingsPath,
            UseShellExecute = true
        });
    }

    private async Task ExitAsync()
    {
        _notifyIcon.Visible = false;
        await _host.StopAsync(TimeSpan.FromSeconds(3));
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
