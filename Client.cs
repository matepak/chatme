using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace chatmee_clientserver
{
    public class Client
    {
        private TcpClient _client;
        private int _port;
        private string _serverAddres;
        private string _nickname;
        private volatile bool isDisconnected = true;
        public delegate void ClientChangeStateHandler(string msg);
        public event ClientChangeStateHandler UpdateUI;
        

        public Client(string serverAddres, int port, string nickname)
        {
            _serverAddres = serverAddres;
            _port = port;
            _nickname = nickname;
        }

        public bool isConnected()
        {
            if(!isDisconnected)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public void Connect()

        {
            try
            {
                _client = new TcpClient(_serverAddres, _port);
                isDisconnected  = false;
                SendData(_nickname);
                ReadStreamAsync(_client.GetStream());
            }

            catch (System.Net.Sockets.SocketException ex)
            {
                UpdateUI(ex.Message + "\n");
            }
        }

        public void Disconnect()
        {
            if (!isDisconnected)
            {
                SendData("/disconnect");
                _nickname = string.Empty;
                isDisconnected = true;
            }
        }

        public async void SendData(string msg)
        {
            byte[] message = System.Text.Encoding.UTF8.GetBytes(msg);
           
            var clientStream = _client.GetStream();
            try
            {
                await clientStream.WriteAsync(message, 0, message.Length);
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }


        }

        public async void ReadStreamAsync(NetworkStream stream)
        {
            byte[] result;
            result = new byte[1024];
            while (!isDisconnected)
            {
                try
                {
                    int bytesRead = await stream.ReadAsync(result, 0, result.Length);
                    string response = System.Text.Encoding.UTF8.GetString(result, 0, bytesRead);
                    UpdateUI(response);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                }

                catch (System.IO.IOException)
                {
                    break;
                }

            }
            stream.Close();
        }

    }
}