using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ServerSideForPC
{
    public static class TcpServer
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
                using NetworkStream stream = tcpClient.GetStream();
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
            if (tcpClient == null)
            {
                Console.WriteLine("Нет активного клиента для отправки сообщения.");
                return;
            }

            lock (tcpClient) 
            {
                try
                {
                    if (tcpClient.Connected)
                    {
                        NetworkStream stream = tcpClient.GetStream();
                        if (stream.CanWrite)
                        {
                            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
                            stream.Write(data, 0, data.Length);
                            stream.Flush();
                            Console.WriteLine("Сообщение отправлено: " + message);
                        }
                        else
                        {
                            Console.WriteLine("Сеть недоступна для записи у клиента: " + tcpClient.Client.RemoteEndPoint);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Клиент не подключен: " + tcpClient.Client.RemoteEndPoint);
                    }
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine("Попытка отправки на уже закрытое соединение.");
                }
                catch (IOException ioEx)
                {
                    Console.WriteLine("Сетевая ошибка при отправке: " + ioEx.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Неожиданная ошибка при отправке сообщения: " + ex);
                }
            }
        }
    }

}
