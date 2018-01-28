using ProjectSockets;
using System;

namespace consoleclient
{
    internal class Program
    {
        private static SocketClient client = new SocketClient();

        private static void Main(string[] args)
        {
            client.ConnectToServer();
            string line;
            do
            {
                if (client.IsConnected)
                    client.SendMessageToServer(Properties.Resources.lorem_ipsum);
            } while ((line = Console.ReadLine()) != "exit");
        }
    }
}