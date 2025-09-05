using System;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using PropertyKeyNAudio = NAudio.CoreAudioApi.PropertyKey;

public sealed class DefaultAudioDeviceWatcher : IMMNotificationClient, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly Action<MMDevice> _onDefaultRenderChanged;
    private bool _registered;

    public DefaultAudioDeviceWatcher(MMDeviceEnumerator enumerator, Action<MMDevice> onDefaultRenderChanged)
    {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _onDefaultRenderChanged = onDefaultRenderChanged ?? throw new ArgumentNullException(nameof(onDefaultRenderChanged));
    }

    public void Start() { if (_registered) return; _enumerator.RegisterEndpointNotificationCallback(this); _registered = true; }
    public void Stop() { if (!_registered) return; _enumerator.UnregisterEndpointNotificationCallback(this); _registered = false; }
    public void Dispose() => Stop();

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow == DataFlow.Render && role == Role.Multimedia)
        {
            try { var dev = _enumerator.GetDevice(defaultDeviceId); _onDefaultRenderChanged(dev); } catch { }
        }
    }

    public void OnDeviceAdded(string pwstrDeviceId) { }
    public void OnDeviceRemoved(string deviceId) { }
    public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKeyNAudio key) { }
}
