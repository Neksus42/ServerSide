using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSideForPC
{
    internal class DisplayControl
    {
        public static void SetDisplayMode(string mode)
        {
          
            string arg = mode.ToLower() switch
            {
                "internal" => "/internal",
                "external" => "/external",
                "clone" => "/clone",
                "extend" => "/extend",
                _ => throw new ArgumentException("Неверный режим отображения.")
            };

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("DisplaySwitch.exe", arg)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                
                Console.WriteLine("Ошибка при переключении режима дисплея: " + ex.Message);
            }
        }
    }
}
