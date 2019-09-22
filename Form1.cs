using System;
using System.Windows.Forms;
namespace chatmee_clientserver
{
    public partial class Chatmee_form : Form
    {
        bool serverIsStarted = false;
        bool clientIsStarted = false;
        ChatService server;
        ChatService client;
        string serverIp = "127.0.0.1";
        string userName = "default";
        int port = 1100;

        public Chatmee_form()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "Ready";
            inputBox.Enabled = false;
            sndMsgBtn.Enabled = false;
        }

     
        private void serverStart_Click(object sender, EventArgs e)
        {
            if (!serverIsStarted)
            {
                try
                {
                    server = new ChatService(port, serverIp);
                    server.UpdateUI += new ChatService.ServerChangeStateHandler(Update);
                    server.StartServer();
                    if (server.IsStarted())
                    {
                        serverIsStarted = true;
                        serverStart.Text = "Close Server";
                        toolStripStatusLabel1.Text = "Server started";
                        clientStart.Enabled = false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            else
            {
                server.StopServer();
                serverIsStarted = false;
                serverStart.Text = "Start Server";
                toolStripStatusLabel1.Text = "Server stoped";
                clientStart.Enabled = true;
            }

        }

        private void clientStart_Click(object sender, EventArgs e)
        {
            if (!clientIsStarted)
            {
                try
                {
                    client = new ChatService(serverIp, port, userName);
                    client.UpdateUI +=  new ChatService.ServerChangeStateHandler(Update);

                    client.StartClient();
                    if (client.isConnected())
                    {
                        serverStart.Enabled = false;
                        clientStart.Text = "Close Client";
                        clientIsStarted = true;
                        inputBox.Enabled = true;
                        sndMsgBtn.Enabled = true;
                        toolStripStatusLabel1.Text = "Connected to: " + serverIp;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            else
            {
                serverStart.Enabled = true;
                clientStart.Text = "Start Client";
                clientIsStarted = false;
                client.StopClient();
                inputBox.Enabled = false;
                sndMsgBtn.Enabled = false;
            }


        }

        private void sndMsgBtn_Click(object sender, EventArgs e)
        {
            string msg = inputBox.Text;
            Update(client.FormatMsg(userName, msg));
            MessageFrame mf = MsgParser(msg);
            client.SendObjStream(mf);
        }


        public void Update(string msg)
        {
            if (InvokeRequired)
            {
                statusBox.Invoke(new Action(() => statusBox.AppendText(msg)));
            }
            else
            {
                statusBox.AppendText(msg);
            }
        }


        private void inputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                string msg = inputBox.Text;
                Update(client.FormatMsg(userName, msg));
                MessageFrame mf = MsgParser(msg);
                client.SendObjStream(mf);
                inputBox.Text = "";
                e.SuppressKeyPress = true;
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            About about = new About();
            about.Show();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About aboutWindow = new About();
            aboutWindow.Show();
        }

        private void configurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (Configuration configurationWindow = new Configuration())
            {
                configurationWindow.ShowDialog();
                serverIp = configurationWindow.serverIp;
                port = configurationWindow.port;
                userName = configurationWindow.userName;
            }
        }

        private MessageFrame MsgParser(string msg)
        {
            MessageFrame mf = new MessageFrame();
            string[] param = msg.Split(new char[] { ' ' }, 3);
            mf.Sender = userName;

            switch (param[0])
            {
                case ":list":
                    mf.Command = "list";
                    break;

                case ":quit":

                    Close();
                    break;

                case ":join":

                    mf.Command = "join";
                    mf.Param = param[1];
                    break;

                case ":pv":
                    mf.Data = msg;
                    mf.Destination = param[1];


                    break;
                default:
                    mf.Data = msg;
                    break;

            }
            return mf;
        }

    }
}
