using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;


namespace ServerSideForPC
{
    internal class CodeHandler
    {
        private static MMDeviceEnumerator _enum;
        private static MMDevice _device;
        private static bool _volumeSubscribed;
        private static TcpClient _lastClient; 


        private static DefaultAudioDeviceWatcher _watcher;
        private static bool _started;

        private static void StartAudioBindingIfNeeded()
        {
            if (_started) return;

            try
            {
                _enum = new MMDeviceEnumerator();
                BindToCurrentDefaultDevice();

                lock (_lock)
                {
                    if (!_volumeSubscribed && _device?.AudioEndpointVolume != null)
                    {
                        _device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotify;
                        _volumeSubscribed = true;
                    }
                }

                _watcher = new DefaultAudioDeviceWatcher(_enum, OnDefaultRenderDeviceChanged);
                _watcher.Start();

                _started = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при запуске аудио-сервиса: " + ex);
            }
        }

        private static void BindToCurrentDefaultDevice()
        {
            try
            {
                var newDev = _enum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                lock (_lock)
                {
                    if (_device != null && _device.ID == newDev.ID)
                        return;

                    if (_device != null && _volumeSubscribed)
                    {
                        _device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotify;
                    }

                    _device = newDev;

                    if (!_volumeSubscribed && _device?.AudioEndpointVolume != null)
                    {
                        _device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotify;
                        _volumeSubscribed = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при привязке к аудио-устройству: " + ex);
            }
        }

        private static readonly object _lock = new object();

        private static void OnVolumeNotify(AudioVolumeNotificationData data)
        {
            try
            {
                lock (_lock)
                {
                    if (_lastClient != null)
                    {
                        SendVolumeSync(data.MasterVolume);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при уведомлении о громкости: " + ex);
            }
        }


        private static void OnDefaultRenderDeviceChanged(MMDevice newDefault)
        {
            try
            {
                lock (_lock)
                {
                    if (_device != null)
                        _device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotify;

                    _device = newDefault;
                    if (_device?.AudioEndpointVolume != null)
                        _device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotify;

                    if (_lastClient != null)
                        SendVolumeSync(_device.AudioEndpointVolume.MasterVolumeLevelScalar);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при смене аудио-устройства: " + ex);
            }
        }

        private static void SendVolumeSync(float volume)
        {
            try
            {
                lock (_lock)
                {
                    if (_lastClient != null)
                    {
                        TcpServer.SendMessage("VolumeSync:" + volume.ToString("0.###", CultureInfo.InvariantCulture), _lastClient);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при отправке VolumeSync: " + ex);
            }
        }

      
        public static async void CodeHandlerFunc(string jsonstring, TcpClient tcpClient)
        {
            lock (_lock)
            {
                _lastClient = tcpClient;
            }

            StartAudioBindingIfNeeded();

            string[] subarr = jsonstring.Split(':', 2);
            string code = subarr[0].Trim();

            Console.WriteLine($"Код задачи: {code}");

            try
            {
                switch (code)
                {
                    case "ShutdownPC":
                        ShutdownPC();
                        break;

                    case "GetAudioDevices":
                        {
                            List<MMDevice> devices = AudioDeviceManager.GetAudioOutputDevices();
                            List<string> deviceNames = devices.Select((d, i) => $"{i}: {d.FriendlyName}").ToList();
                            string response = JsonSerializer.Serialize(deviceNames);
                            TcpServer.SendMessage(response, tcpClient);
                            break;
                        }

                    case "SwitchAudioDevice":
                        {
                            List<MMDevice> devices = AudioDeviceManager.GetAudioOutputDevices();
                            int idx = Convert.ToInt32(subarr[1]);
                            AudioDeviceManager.SetDefaultAudioPlaybackDevice(devices[idx].ID);
                            Console.WriteLine("Устройство успешно установлено по умолчанию.");
                            break;
                        }

                    case "SwapDisplayPC":
                        DisplayControl.SetDisplayMode("internal");
                        break;

                    case "SwapDisplayTV":
                        { 
                        DisplayControl.SetDisplayMode("external");

                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                        try
                        {
                            await AudioDeviceManager.WaitAndSetDefaultAsync("TV", TimeSpan.FromSeconds(10), cts.Token);

                            Console.WriteLine("Аудио успешно переключено на TV.");
                        }
                        catch (TaskCanceledException)
                        {
                            Console.WriteLine("TV-устройство не найдено в течение 10 секунд.");
                        }
                        break;
                        }

                    case "GetVolume":
                        lock (_lock)
                        {
                            if (_device?.AudioEndpointVolume != null)
                                SendVolumeSync(_device.AudioEndpointVolume.MasterVolumeLevelScalar);
                        }
                        break;

                    case "SetVolume":
                        lock (_lock)
                        {
                            if (subarr.Length > 1 &&
                                float.TryParse(subarr[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                            {
                                if (v > 1f) v /= 100f;
                                v = Math.Clamp(v, 0f, 1f);

                                if (_device?.AudioEndpointVolume != null)
                                    _device.AudioEndpointVolume.MasterVolumeLevelScalar = v;
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обработке кода {code}: {ex}");
            }
        }
        public static void ShutdownPC()
        {
          
            ProcessStartInfo psi = new ProcessStartInfo("shutdown", "/s /t 0")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process.Start(psi);
        }


    }



}


