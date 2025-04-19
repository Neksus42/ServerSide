using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ServerSideForPC
{
    internal class CodeHandler
    {
        static public async void CodeHandlerFunc(string jsonstring, TcpClient tcpClient)
        {


            string[] subarr = jsonstring.Split(':', 2);
            string Code = subarr[0].Trim();
        

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


