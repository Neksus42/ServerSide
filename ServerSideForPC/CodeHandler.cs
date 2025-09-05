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
        // === НОВОЕ: сервис громкости + ссылка на последнего клиента ===
        private static MMDeviceEnumerator _enum;
        private static MMDevice _device;
        private static bool _volumeSubscribed;
        private static TcpClient _lastClient; // единственный телефон


        private static DefaultAudioDeviceWatcher _watcher;
        private static bool _started;

        private static void StartAudioBindingIfNeeded()
        {
            if (_started) return;

            _enum = new MMDeviceEnumerator();
            BindToCurrentDefaultDevice(); // первичная привязка к дефолтному устройству

            // слушаем изменения громкости у текущего устройства
            _device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotify;

            // слушаем смену дефолтного устройства (Render/Multimedia)
            _watcher = new DefaultAudioDeviceWatcher(_enum, OnDefaultRenderDeviceChanged);
            _watcher.Start();

            _started = true;
        }

        private static void BindToCurrentDefaultDevice()
        {
            var newDev = _enum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (_device != null && _device.ID == newDev.ID)
                return;

            if (_device != null)
                _device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotify;

            _device = newDev;
            _device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotify;
        }

        private static void OnVolumeNotify(AudioVolumeNotificationData data)
        {
            try
            {
                if (_lastClient != null)
                {
                    var msg = "VolumeSync:" + data.MasterVolume.ToString("0.###", CultureInfo.InvariantCulture);
                    TcpServer.SendMessage(msg, _lastClient);
                }
            }
            catch { /* игнор */ }
        }

        private static void OnDefaultRenderDeviceChanged(MMDevice newDefault)
        {
            // перепривязка к новому дефолтному устройству
            if (_device != null)
                _device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotify;

            _device = newDefault;
            _device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotify;

            // сразу высылаем актуальный уровень нового устройства
            try
            {
                if (_lastClient != null)
                {
                    float cur = _device.AudioEndpointVolume.MasterVolumeLevelScalar;
                    var msg = "VolumeSync:" + cur.ToString("0.###", CultureInfo.InvariantCulture);
                    TcpServer.SendMessage(msg, _lastClient);
                }
            }
            catch { }
        }

        private static void EnsureVolumeServiceStarted()
        {
            if (_volumeSubscribed) return;

            _enum = new MMDeviceEnumerator();
            _device = _enum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            // Любое системное изменение громкости → пушим в телефон
            _device.AudioEndpointVolume.OnVolumeNotification += data =>
            {
                TcpServer.SendMessage(
                    "VolumeSync:" + data.MasterVolume.ToString("0.###", CultureInfo.InvariantCulture),
                    _lastClient
                );
            };

            _volumeSubscribed = true;
        }
        static public async void CodeHandlerFunc(string jsonstring, TcpClient tcpClient)
        {
            _lastClient = tcpClient;

            StartAudioBindingIfNeeded();

            string[] subarr = jsonstring.Split(':', 2);
            string Code = subarr[0].Trim();
            EnsureVolumeServiceStarted();

            Console.WriteLine($"Код задачи: {Code}");
           

            switch (Code)
            {
                case "ShutdownPC":
                    {
                        
                        ShutdownPC();
                        break;
                    }
                case "GetAudioDevices": 
                    {
                        List<MMDevice> devices = AudioDeviceManager.GetAudioOutputDevices();
                       List<string> devicesNames = new List<string>();

                        
                        Console.WriteLine("Доступные аудиовыходы:");
                        for (int i = 0; i < devices.Count; i++)
                        {
                            devicesNames.Add($"{i}: {devices[i].FriendlyName}");
                            //Console.WriteLine($"{i}: {devices[i].FriendlyName}");
                        }

                        string response = JsonSerializer.Serialize<List<string>>(devicesNames);
                        TcpServer.SendMessage(response,tcpClient);
                        break;
                    }
                case "SwitchAudioDevice":
                    {
                     
                        List<MMDevice> devices = AudioDeviceManager.GetAudioOutputDevices();



                        string deviceId = devices[Convert.ToInt32(subarr[1])].ID;

                        AudioDeviceManager.SetDefaultAudioPlaybackDevice(deviceId);


                        Console.WriteLine("Устройство успешно установлено по умолчанию.");
                        break;
                    }
                case "SwapDisplayPC":
                    {
                        DisplayControl.SetDisplayMode("internal");

                        break;
                    }
                case "SwapDisplayTV":
                    {
                        DisplayControl.SetDisplayMode("external");
                        await AudioDeviceManager.WaitAndSetDefaultAsync("TV", TimeSpan.FromSeconds(10));
                        break;
                    }
                case "GetVolume":
                    {
                        BindToCurrentDefaultDevice();
                        float cur = _device.AudioEndpointVolume.MasterVolumeLevelScalar;
                        TcpServer.SendMessage(
                            "VolumeSync:" + cur.ToString("0.###", CultureInfo.InvariantCulture),
                            tcpClient
                        );
                        break;

                    }

                case "SetVolume":
                    {
                        BindToCurrentDefaultDevice();

                        if (subarr.Length > 1 &&
                            float.TryParse(subarr[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                        {
                            if (v > 1f) v /= 100f;            // поддержка процентов
                            v = Math.Clamp(v, 0f, 1f);

                            _device.AudioEndpointVolume.MasterVolumeLevelScalar = v;
                            float cur = _device.AudioEndpointVolume.MasterVolumeLevelScalar;
                            TcpServer.SendMessage(
                                "VolumeSync:" + cur.ToString("0.###", CultureInfo.InvariantCulture),
                                tcpClient
                            );
                            break;
                        }
                        break;
                    }

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


