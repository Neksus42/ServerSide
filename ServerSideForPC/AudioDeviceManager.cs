using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
namespace ServerSideForPC
{
    internal class AudioDeviceManager
    {
        /// <summary>
        /// Получает список доступных аудиовыходов.
        /// Обратите внимание: для дальнейшей установки по умолчанию понадобится не только FriendlyName, но и ID устройства.
        /// </summary>
        public static List<MMDevice> GetAudioOutputDevices()
        {
            List<MMDevice> devices = new List<MMDevice>();
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            var deviceCollection = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            foreach (var device in deviceCollection)
            {
                devices.Add(device);
            }

            return devices;
        }

        /// <summary>
        /// Устанавливает аудиоустройство с заданным идентификатором в качестве устройства по умолчанию для мультимедиа.
        /// </summary>
        /// <param name="deviceId">Идентификатор устройства (свойство ID из MMDevice)</param>
        public static void SetDefaultAudioPlaybackDevice(string deviceId)
        {
            try
            {
                IPolicyConfig policyConfig = new PolicyConfig() as IPolicyConfig;
                int hr = policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
                if (hr != 0)
                {
                    throw new Exception("Ошибка при установке аудиоустройства. HRESULT: " + hr);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка установки дефолтного аудиоустройства: " + ex.Message);
            }
        }
        /// <summary>Дождаться появления устройства с заданной меткой и сделать его default.</summary>
        /// <param name="friendlyPart">Часть FriendlyName, например "Samsung" или "HDMI".</param>
        /// <param name="timeout">Максимальное время ожидания.</param>
        public static async Task WaitAndSetDefaultAsync(string friendlyPart,
                                                        TimeSpan timeout,
                                                        CancellationToken ct = default)
        {
            using var watcher = new DeviceAppearWatcher(friendlyPart, ct);
            string id = await watcher.WaitAsync(timeout, ct).ConfigureAwait(false);

            SetDefaultAudioPlaybackDevice(id);
        }

        // ---------- внутренний класс‑наблюдатель ----------
        private sealed class DeviceAppearWatcher : IMMNotificationClient, IDisposable
        {
            private readonly TaskCompletionSource<string> _tcs =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly string _needle;
            private readonly MMDeviceEnumerator _enum = new();
            private readonly CancellationTokenRegistration _ctr;

            public DeviceAppearWatcher(string needle, CancellationToken ct)
            {
                _needle = needle;
                _enum.RegisterEndpointNotificationCallback(this);
                _ctr = ct.Register(() => _tcs.TrySetCanceled(ct));
            }

            public Task<string> WaitAsync(TimeSpan timeout, CancellationToken ct)
                => _tcs.Task.WaitAsync(timeout, ct);

            // ---- IMMNotificationClient ----
            public void OnDeviceAdded(string pwstrDeviceId)
                => Check(pwstrDeviceId);

            public void OnDeviceStateChanged(string pwstrDeviceId, DeviceState newState)
            {
                if (newState == DeviceState.Active)
                    Check(pwstrDeviceId);
            }

            private void Check(string id)
            {
                using var dev = _enum.GetDevice(id);
                if (dev.FriendlyName.Contains(_needle, StringComparison.OrdinalIgnoreCase))
                    _tcs.TrySetResult(id);
            }

            // остальные методы интерфейса (ничего не делаем)
            public void OnDeviceRemoved(string id) { }
            public void OnDefaultDeviceChanged(DataFlow flow, Role role, string id) { }
            public void OnPropertyValueChanged(string id, PropertyKey key) { }

            public void Dispose()
            {
                _enum.UnregisterEndpointNotificationCallback(this);
                _ctr.Dispose();
                _enum.Dispose();
            }

            public void OnPropertyValueChanged(string pwstrDeviceId, NAudio.CoreAudioApi.PropertyKey key)
            {
                throw new NotImplementedException();
            }
        }
    }

    // Необходимые определения для COM-интеропа:

    /// <summary>
    /// Роли аудиоустройств.
    /// </summary>
    public enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2,
    }

    /// <summary>
    /// Неофициальный COM-интерфейс для управления политикой аудиоустройств.
    /// </summary>
    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPolicyConfig
    {
        // Некоторые методы опущены, т.к. нам нужен только SetDefaultEndpoint
        [PreserveSig]
        int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr ppFormat);
        [PreserveSig]
        int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out IntPtr ppFormat);
        [PreserveSig]
        int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
        [PreserveSig]
        int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr pMixFormat);
        [PreserveSig]
        int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out long pmftDefaultPeriod, out long pmftMinimumPeriod);
        [PreserveSig]
        int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, long pmftPeriod);
        [PreserveSig]
        int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr pMode);
        [PreserveSig]
        int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
        [PreserveSig]
        int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PropertyKey key, out object pv);
        [PreserveSig]
        int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PropertyKey key, object pv);
        [PreserveSig]
        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ERole eRole);
      
    }

    /// <summary>
    /// Структура для ключей свойств (используется в IPolicyConfig), здесь не используется, но объявлена для полноты.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        public Guid fmtid;
        public int pid;
    }

    /// <summary>
    /// Класс для создания COM-объекта PolicyConfig.
    /// </summary>
    [ComImport]
    [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    public class PolicyConfig
    {
    }

}
