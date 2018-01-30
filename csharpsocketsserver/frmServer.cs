using System;
using System.Windows.Forms;

namespace csharpsocketsserver
{
    using DeltaSockets;
    using System.Net.Sockets;
    using static DeltaSockets.SocketGlobals;

    internal delegate void MessageReceivedDelegate(string argMessage, SocketContainer argContainer);

    internal delegate void ClientConnectedDelegate(SocketContainer argContainer);

    internal delegate void ClientDisconnectedDelegate(SocketContainer argContainer);

    public partial class frmServer : Form
    {
        private SocketServer server = new SocketServer(true);

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

        private void server_ClientConnected(SocketContainer argContainer)
        {
            Invoke(new ClientConnectedDelegate(ClientConnected), argContainer);
        }

        private void ClientConnected(SocketContainer co)
        {
            listBox1.Items.Add(co.Socket.RemoteEndPoint);
        }

        private void server_ClientDisconnected(SocketContainer argContainer)
        {
            Invoke(new ClientDisconnectedDelegate(ClientDisconnected), argContainer);
        }

        private void ClientDisconnected(SocketContainer co)
        {
            listBox1.Items.Remove(co.Socket.RemoteEndPoint);
        }

        private void server_MessageReceived(string argMessage, SocketContainer argContainer)
        {
            Invoke(new MessageReceivedDelegate(MessageReceived), new object[] { argMessage, argContainer });
        }

        private void MessageReceived(string argMessage, SocketContainer co)
        {
            if (txtReceiveLog.Text != "")
            {
                txtReceiveLog.Text = Environment.NewLine + txtReceiveLog.Text;
            }
            txtReceiveLog.Text = co.Socket.RemoteEndPoint.ToString() + ": " + argMessage + txtReceiveLog.Text;
        }
    }
}