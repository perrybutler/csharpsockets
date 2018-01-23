using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace csharpsocketsserver
{
    using System.Net;
    using System.Net.Sockets;

    delegate void MessageReceivedDelegate(string argMessage, Socket argClientSocket);
    delegate void ClientConnectedDelegate(Socket argClientSocket);
    delegate void ClientDisconnectedDelegate(Socket argClientSocket);

    public partial class frmServer : Form
    {
        SocketServer server = new SocketServer();
        
        public frmServer()
        {
            server.ClientConnected += new SocketServer.ClientConnectedEventHandler(server_ClientConnected);
            server.ClientDisconnected += new SocketServer.ClientDisconnectedEventHandler(server_ClientDisconnected);
            server.MessageReceived += new SocketServer.MessageReceivedEventHandler(server_MessageReceived);
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            server.StartServer();
            btnStartServer.Text = "Stop Server";
        }

        private void btnStartServer_Click(object sender, EventArgs e)
        {
            if (btnStartServer.Text == "Start Server")
            {
                server.StartServer();
                btnStartServer.Text = "Stop Server";
            }
            else
            {
                server.StopServer();
                btnStartServer.Text = "Start Server";
            }
        }

        private void server_ClientConnected(Socket argClientSocket)
        {
            Invoke(new ClientConnectedDelegate(ClientConnected), argClientSocket);
        }

        private void ClientConnected(Socket argClientSocket)
        {
            listBox1.Items.Add(argClientSocket.RemoteEndPoint);
        }

        private void server_ClientDisconnected(Socket argClientSocket)
        {
            Invoke(new ClientDisconnectedDelegate(ClientDisconnected), argClientSocket);
        }

        private void ClientDisconnected(Socket argClientSocket)
        {
            listBox1.Items.Remove(argClientSocket.RemoteEndPoint);
        }

        private void server_MessageReceived(string argMessage, Socket argClient)
        {
            Invoke(new MessageReceivedDelegate(MessageReceived), new object[] { argMessage, argClient });
        }

        private void MessageReceived(string argMessage, Socket argClient)
        {
            if (txtReceiveLog.Text != "")
            {
                txtReceiveLog.Text = Environment.NewLine + txtReceiveLog.Text;
            }
            txtReceiveLog.Text = argClient.RemoteEndPoint.ToString() + ": " + argMessage + txtReceiveLog.Text;
        }
    }
}
