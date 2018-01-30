using DeltaSockets;
using System;

namespace consoleclient
{
    internal class Program
    {
        private static SocketClient client = new SocketClient();

        private static void Main(string[] args)
        {
            client.DoConnection();
            string line;
            do
            {
                if (client.IsConnected)
                    client.SendMessageToServer(Properties.Resources.lorem_ipsum, client.my);
            } while ((line = Console.ReadLine()) != "exit");
        }
    }
}