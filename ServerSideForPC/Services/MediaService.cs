using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ServerSideForPC.Services;

/// <summary>
/// Sends global Windows media commands through WM_APPCOMMAND.
/// This follows the same Windows command path used by hardware media keys
/// after they are translated by DefWindowProc / the shell.
/// </summary>
public sealed class MediaService
{
    public void PlayPause() => SendMediaCommand(AppCommandMediaPlayPause);
    public void Next() => SendMediaCommand(AppCommandMediaNextTrack);
    public void Previous() => SendMediaCommand(AppCommandMediaPreviousTrack);
    public void Stop() => SendMediaCommand(AppCommandMediaStop);

    private static void SendMediaCommand(int command)
    {
        // Hardware media keys are delivered to the active window first. If that
        // window does not handle WM_APPCOMMAND, DefWindowProc bubbles it to the
        // top-level window and then to the Windows shell hook. Sending to the
        // foreground window therefore mirrors the normal hardware-key path much
        // more closely than synthesising VK_MEDIA_* keyboard input.
        var target = GetForegroundWindow();
        if (target == IntPtr.Zero)
        {
            target = GetShellWindow();
        }

        if (target == IntPtr.Zero)
        {
            throw new InvalidOperationException("Windows did not provide a foreground or shell window for the media command.");
        }

        // WM_APPCOMMAND stores APPCOMMAND_* in the high word of lParam.
        // FAPPCOMMAND_KEY is zero, so no additional source flag is required.
        var lParam = new IntPtr(command << 16);
        var sent = SendMessageTimeout(
            target,
            WmAppCommand,
            target,
            lParam,
            SmtoAbortIfHung,
            1000,
            out _);

        if (sent == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                throw new Win32Exception(error, "WM_APPCOMMAND media command could not be delivered.");
            }

            throw new InvalidOperationException("WM_APPCOMMAND media command timed out or could not be delivered.");
        }
    }

    private const uint WmAppCommand = 0x0319;
    private const uint SmtoAbortIfHung = 0x0002;

    private const int AppCommandMediaNextTrack = 11;
    private const int AppCommandMediaPreviousTrack = 12;
    private const int AppCommandMediaStop = 13;
    private const int AppCommandMediaPlayPause = 14;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result);
}
