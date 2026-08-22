using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using ServerSideForPC.Models;
using NAudioPropertyKey = NAudio.CoreAudioApi.PropertyKey;

namespace ServerSideForPC.Services;

public sealed class AudioService : IDisposable
{
    private readonly Lock _gate = new();
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly MMDeviceNotificationClient _notificationClient;
    private MMDevice? _defaultDevice;
    private bool _disposed;

    public event Action? StateChanged;

    public AudioService()
    {
        // NAudio 3.x no longer exposes IMMNotificationClient or manual
        // RegisterEndpointNotificationCallback/UnregisterEndpointNotificationCallback.
        // CreateNotificationClient is the supported public API.
        _notificationClient = _enumerator.CreateNotificationClient(useSynchronizationContext: false);
        _notificationClient.DefaultDeviceChanged += OnDefaultDeviceChanged;
        RebindDefaultDevice();
    }

    public IReadOnlyList<AudioDeviceInfo> GetDevices()
    {
        ThrowIfDisposed();

        string? defaultId;
        lock (_gate)
        {
            defaultId = _defaultDevice?.ID;
        }

        var result = new List<AudioDeviceInfo>();
        using var enumerator = new MMDeviceEnumerator();
        using var collection = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        foreach (var device in collection)
        {
            using (device)
            {
                result.Add(new AudioDeviceInfo(
                    device.ID,
                    device.FriendlyName,
                    string.Equals(device.ID, defaultId, StringComparison.Ordinal)));
            }
        }

        return result;
    }

    public (float Volume, bool Muted, string? DefaultDeviceId) GetCurrentState()
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            if (_defaultDevice is null)
            {
                return (0f, false, null);
            }

            return (
                _defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar,
                _defaultDevice.AudioEndpointVolume.Mute,
                _defaultDevice.ID);
        }
    }

    public void SetVolume(float value)
    {
        ThrowIfDisposed();
        value = Math.Clamp(value, 0f, 1f);

        lock (_gate)
        {
            EnsureDefaultDevice();
            _defaultDevice!.AudioEndpointVolume.MasterVolumeLevelScalar = value;
        }
    }

    public void SetMute(bool muted)
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            EnsureDefaultDevice();
            _defaultDevice!.AudioEndpointVolume.Mute = muted;
        }
    }

    public void SetDefaultDevice(string deviceId)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Audio device id is required.", nameof(deviceId));
        }

        using var enumerator = new MMDeviceEnumerator();
        using var probe = enumerator.GetDevice(deviceId);
        if (probe.State != DeviceState.Active)
        {
            throw new InvalidOperationException("Audio device is not active.");
        }

        var policyConfig = (IPolicyConfig)new PolicyConfig();
        try
        {
            SetDefaultRole(policyConfig, deviceId, ERole.Console);
            SetDefaultRole(policyConfig, deviceId, ERole.Multimedia);
            SetDefaultRole(policyConfig, deviceId, ERole.Communications);
        }
        finally
        {
            if (Marshal.IsComObject(policyConfig))
            {
                Marshal.ReleaseComObject(policyConfig);
            }
        }

        RebindDefaultDevice();
    }

    public async Task<bool> WaitAndSetDefaultByFriendlyNameAsync(
        string friendlyNamePart,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(friendlyNamePart))
        {
            return false;
        }

        var deadline = DateTimeOffset.UtcNow + timeout;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = GetDevices().FirstOrDefault(device =>
                device.Name.Contains(friendlyNamePart, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                SetDefaultDevice(match.Id);
                return true;
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return false;
    }

    private static void SetDefaultRole(IPolicyConfig policyConfig, string deviceId, ERole role)
    {
        var hr = policyConfig.SetDefaultEndpoint(deviceId, role);
        if (hr != 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }

    private void EnsureDefaultDevice()
    {
        if (_defaultDevice is null)
        {
            RebindDefaultDevice();
        }

        if (_defaultDevice is null)
        {
            throw new InvalidOperationException("No active default audio output device is available.");
        }
    }

    private void RebindDefaultDevice()
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_defaultDevice is not null)
            {
                _defaultDevice.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
                _defaultDevice.Dispose();
                _defaultDevice = null;
            }

            try
            {
                if (_enumerator.TryGetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var device))
                {
                    _defaultDevice = device;
                    _defaultDevice.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification;
                }
            }
            catch
            {
                _defaultDevice = null;
            }
        }

        StateChanged?.Invoke();
    }

    private void OnDefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
    {
        if (e.Flow != DataFlow.Render || e.Role != Role.Multimedia)
        {
            return;
        }

        // With useSynchronizationContext:false NAudio raises this on the Windows audio worker thread.
        // Do not call CoreAudio back from that callback. Queue the rebind on the thread pool instead.
        _ = Task.Run(RebindDefaultDevice);
    }

    private void OnVolumeNotification(AudioVolumeNotificationData _)
    {
        StateChanged?.Invoke();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _notificationClient.DefaultDeviceChanged -= OnDefaultDeviceChanged;
        _notificationClient.Dispose();

        lock (_gate)
        {
            if (_defaultDevice is not null)
            {
                _defaultDevice.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
                _defaultDevice.Dispose();
                _defaultDevice = null;
            }
        }

        _enumerator.Dispose();
    }
}

internal enum ERole
{
    Console = 0,
    Multimedia = 1,
    Communications = 2
}

[ComImport]
[Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr ppFormat);
    [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out IntPtr ppFormat);
    [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
    [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr pMixFormat);
    [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out long pmftDefaultPeriod, out long pmftMinimumPeriod);
    [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, long pmftPeriod);
    [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr pMode);
    [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
    [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref NAudioPropertyKey key, out object pv);
    [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref NAudioPropertyKey key, object pv);
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ERole eRole);
}

[ComImport]
[Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
internal class PolicyConfig
{
}
