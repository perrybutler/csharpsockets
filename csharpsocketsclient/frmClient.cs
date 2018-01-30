using DeltaSockets;
using System;
using System.Windows.Forms;

namespace csharpsocketsclient
{
    public partial class frmClient : Form
    {
        private SocketClient client = new SocketClient();

        public frmClient()
        {
            InitializeComponent();
            txtSend.KeyDown += new KeyEventHandler(txtSend_KeyDown);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            client.DoConnection();
            btnConnect.Text = "Disconnect";
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (btnConnect.Text == "Connect")
            {
                client.DoConnection();
                btnConnect.Text = "Disconnect";
            }
            else
            {
                client.DisconnectFromServer();
                btnConnect.Text = "Connect";
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            sendMessage();
        }

        private void sendMessage()
        {
            client.SendMessageToServer("/say " + Properties.Resources.lorem_ipsum);
        }

        private void txtSend_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                sendMessage();
            }
        }
    }
}