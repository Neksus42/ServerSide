namespace ServerSideForPC
{
    internal class Program
    {
        
        static async Task Main(string[] args)
        {
       
            TcpServer.StartServer(8888);
           
            _ = TcpServer.WaitConnectionAsync();

        
            await Task.Delay(Timeout.Infinite);
        }
    }
}
