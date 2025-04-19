using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ServerSideForPC
{
    internal class TcpServer
    {
        private static TcpListener listener;
     

        public static void StartServer(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine("Сервер запущен на порту " + port);
        }

        public static async Task WaitConnectionAsync()
        {
            while (true)
            {
                TcpClient tcpClient = await listener.AcceptTcpClientAsync();
                Console.WriteLine("Подключен: " + tcpClient.Client.RemoteEndPoint);
                
                
              
                _ = Task.Run(() => RecieveConnectionAsync(tcpClient));
            }
        }

        public static async Task RecieveConnectionAsync(TcpClient tcpClient)
        {
            try
            {
                NetworkStream stream = tcpClient.GetStream();
                byte[] buffer = new byte[1024];

                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("Клиент отключился.");
                        break;
                    }

                    string receivedJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine("Полученное сообщение: " + receivedJson);

                    try
                    {
                        CodeHandler.CodeHandlerFunc(receivedJson, tcpClient);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Ошибка в обработчике: " + ex.Message);
                        //break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка подключения: " + ex);
            }
            finally
            {
                tcpClient.Close();
                Console.WriteLine("Клиент отключен.");
            }
        }

        public static void SendMessage(string message, TcpClient tcpClient)
        {
            Stream currentStream = tcpClient.GetStream();
            if (currentStream == null)
            {
                Console.WriteLine("Нет активного подключения для отправки сообщения.");
                return;
            }

            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            try
            {
                currentStream.Write(data, 0, data.Length);
                Console.WriteLine("Сообщение отправлено: " + message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка отправки сообщения: " + ex.Message);
            }
        }
    }
}
