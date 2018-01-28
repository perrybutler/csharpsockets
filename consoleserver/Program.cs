using ProjectSockets;
using System;
using System.Net.Sockets;

namespace consoleserver
{
    internal class Program
    {
        private static SocketServer server = new SocketServer();

        private static void Main(string[] args)
        {
            server.MessageReceived += new SocketServer.MessageReceivedEventHandler(server_MessageReceived);
            server.StartServer();
            Console.Read();
        }

        private static void server_MessageReceived(string argMessage, Socket argClient)
        {
            Console.WriteLine("Received message of {0} bytes length.", argMessage.Length);
        }
    }
}