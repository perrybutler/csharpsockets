using DeltaSockets;
using System;
using static DeltaSockets.SocketGlobals;

namespace consoleserver
{
    internal class Program
    {
        private static SocketServer server = new SocketServer(true);

        private static void Main(string[] args)
        {
            server.MessageReceived += new SocketServer.MessageReceivedEventHandler(server_MessageReceived);
            server.StartServer();
            Console.Read();
        }

        private static void server_MessageReceived(string argMessage, SocketContainer argContainer)
        {
            Console.WriteLine("Received message of {0} bytes length.", argMessage.Length);
        }
    }
}