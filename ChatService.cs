using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace chatmee_clientserver
{

    public class ChatService
    {
        private List<User> clientsTaskList = new List<User>();
        private List<ChatRoom> chatRoomList = new List<ChatRoom>();
        private IPAddress iPAddress;
        private int port;
        private TcpListener listener;
        private TcpClient client;
        private volatile bool serverIsClosed = true;
        private volatile bool clientIsDisconnected = true;
        private DateTime timer = new DateTime();

        private string nickname;
        private User usr;

        public delegate void ServerChangeStateHandler(string stopMsg);
        public event ServerChangeStateHandler UpdateUI;

        public ChatService(int port)
        {
            this.port = port;
            iPAddress = IPAddress.Any;
            if (iPAddress == null)
            {
                throw new Exception("No ip address for sever");
            }
        }

        public ChatService(int port, string iPAddress)
        {
            this.port = port;
            this.iPAddress = IPAddress.Parse(iPAddress);
        }

        public ChatService(string serverAddres, int port, string nickname)
        {

            this.iPAddress = IPAddress.Parse(serverAddres);
            this.nickname = nickname;
            this.port = port;
        }

        public bool isConnected()
        {
            if (!clientIsDisconnected)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public string GetServerIpAddress()
        {
            if (iPAddress != null)
            {
                return iPAddress.ToString();
            }
            else
            {
                return "none";
            }
        }

        public bool IsStarted()
        {
            if (!serverIsClosed)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void StopServer()
        {
            listener.Stop();
            serverIsClosed = true;
            UpdateUI(TimeStamp() + " listener has stopped\n");

        }

        public void StopClient()
        {
            if (!clientIsDisconnected)
            {
                MessageFrame mf = new MessageFrame();
                mf.Data = string.Format(nickname + " disconected");
                SendObjStream(mf);
                usr.clientStream.Close();
                clientsTaskList.Remove(usr);
                nickname = string.Empty;
                clientIsDisconnected = true;
            }

        }


        public void StartServer()
        {
            try
            {
                listener = new TcpListener(this.iPAddress, this.port);
                listener.Start();
                string msg = string.Format(TimeStamp() + " listening on: {0}:{1}\n", iPAddress.ToString(), port.ToString());
                UpdateUI(msg);
                serverIsClosed = false;
                ListenForClients(); //oczekuje na połączenie z klientem
            }
            catch (Exception ex)
            {
                UpdateUI(ex.Message);
            }

        }

        public void StartClient()
        {
            if (serverIsClosed)
            {
                try
                {

                    client = new TcpClient(iPAddress.ToString(), port);
                    usr = new User(client.GetStream(), nickname);
                    clientIsDisconnected = false;
                    MessageFrame mf = new MessageFrame();
                    mf.Data = usr.username;
                    SendObjStream(mf);
                    ClientReadStreamAsync(usr.clientStream);
                }

                catch (System.Net.Sockets.SocketException ex)
                {
                    UpdateUI(ex.Message + "\n");
                }

            }
            else
            {

            }
        }

        //oczekuje na połączenia dodaje klientów do listy
        private async void ListenForClients()
        {
            while (!serverIsClosed)
            {
                try
                {
                    MessageFrame mf = new MessageFrame();
                    client = await listener.AcceptTcpClientAsync();
                    string username = await ReadStreamAsync(client.GetStream());
                    clientsTaskList.Add(new User(client.GetStream(), username, this));
                    string time = timer.TimeOfDay.ToString();
                    string msg = string.Format(TimeStamp() + " " + "{0}, joined {1}\n", username, GetServerIpAddress());
                    UpdateUI(msg);
                }
                catch (ObjectDisposedException) when (serverIsClosed)
                {
                    break;
                }

                catch (SerializationException) when (serverIsClosed)
                {
                    break;
                }
            }

            //jeśli sa podłączeni klienci rozłącz i wyczysć liste klientów
            if (clientsTaskList != null)
            {
                foreach (var client in clientsTaskList)
                {
                    client.Close();
                }
                clientsTaskList.Clear();
            }

        }

        //odczytuje strumień jako string
        private async Task<string> ReadStreamAsync(NetworkStream stream)
        {
            byte[] result;
            result = new byte[1024];
            int bytesRead = await stream.ReadAsync(result, 0, result.Length);
            MessageFrame messageFrame = new MessageFrame();
            messageFrame = (MessageFrame)Deserialize(result);

            //string response = System.Text.Encoding.UTF8.GetString(result, 0, bytesRead);
            return messageFrame.Data;
        }
        public async void ClientReadStreamAsync(NetworkStream stream)
        {
            while (!clientIsDisconnected)
            {
                try
                { 
                    MessageFrame mf = new MessageFrame();
                    object obj = await ReadObjAsync(stream);
                    mf = (MessageFrame)obj;
                    if (mf.Data != null)
                    {
                        //string response = await ReadStreamAsync(stream);
                        UpdateUI(FormatMsg(mf.Sender, mf.Data));
                        if (mf.Data == string.Empty)
                        {
                            break;
                        }
                    }
                    if (mf.ConnectedUsers != null)
                    {
                        foreach (var user in mf.ConnectedUsers)
                        {
                            UpdateUI(FormatMsg(usr.username, user));
                        }

                    }
                }

                catch (System.IO.IOException)
                {
                    break;
                }

                catch (ObjectDisposedException) when (serverIsClosed) //przerwij kiedy serwer rozłączony
                {
                    break;
                }

                catch (SerializationException) when (serverIsClosed)
                {
                    break;
                }

            }
            stream.Close();
        }
    
        public async void SendObjStream(object obj)
        {
            try
            {
                NetworkStream stream = usr.clientStream;
                byte[] data = Serialize(obj);
                await stream.WriteAsync(data, 0, data.Length);
            }

            catch (ObjectDisposedException) when (serverIsClosed)
            {
            }
            catch (Exception ex)
            {
                UpdateUI(ex.Message);
            }
        }

        public async void SendObjStream(object obj, NetworkStream stream)
        {
            byte[] data = Serialize(obj);
            await stream.WriteAsync(data, 0, data.Length);
        }

        private async Task<object> ReadObjAsync(NetworkStream stream)
        {
            byte[] result;
            result = new byte[1024];
            int bytesRead = await stream.ReadAsync(result, 0, result.Length);
            object obj = Deserialize(result);
            return obj;

        }


        private void BroadCast(object obj)
        {
            MessageFrame mf = (MessageFrame)obj;

            foreach (var client in clientsTaskList.Where(u => u.username != mf.Sender))
            {
                try
                {
                    SendObjStream(obj, client.clientStream);
                }
                catch (Exception ex)
                {
                    UpdateUI(ex.Message);
                }
            }
        }

        private void BroadCast(object obj, string userName)
        {
            foreach (var client in clientsTaskList.Where(u => u.username == userName))
            {
                try
                {
                    SendObjStream(obj, client.clientStream);
                }
                catch (Exception ex)
                {
                    UpdateUI(ex.Message);
                }
            }
        }

        private string TimeStamp() //zwraca stringa z aktualnym czasem w formacie (HH:mm:ss)
        {
            timer = DateTime.Now;
            return string.Format(timer.ToString("HH:mm"));
        }

        public string FormatMsg(string username, string msg)
        {
            string formatedMessage = string.Format("{0} <{1}> {2}\n", TimeStamp(), username, msg);
            return formatedMessage;
        }

        private byte[] Serialize (object obj)
        {
            IFormatter formatter = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            formatter.Serialize(ms, obj);
            return ms.ToArray();
        }

        private object Deserialize (byte[] data)
        {
            MemoryStream ms = new MemoryStream(data);
            IFormatter formatter = new BinaryFormatter();
            object obj = (object)formatter.Deserialize(ms);
            return obj;
        }
        private List<string> GetUsersList()
        {
            List<string> userList = new List<string>();
            foreach (var users in clientsTaskList)
            {
                userList.Add(users.username);
            }
            return userList;
        }



        //klasa opakowująca uzytkownika
        private class User
        {
            public string username { get; }
            public NetworkStream clientStream { get; }
            private ChatService service;
            private volatile bool isDisconected = true;

            public User(NetworkStream stream, string username)
            {
                this.username = username;
                clientStream = stream;
                isDisconected = false;
            }


            public User(NetworkStream stream, string username, ChatService service)
            {
                this.username = username;
                this.service = service;
                clientStream = stream;
                isDisconected = false;
                ReadStreamAsync();
            }
            public void Close()
            {
                isDisconected = true;
                clientStream.Close();
            }

            private async void ReadStreamAsync() //odczytuje strumień i przekazuje do innych podłączonych klientów
            {
                while (!isDisconected)
                {
                    try
                    {
                      
                        object obj = await service.ReadObjAsync(clientStream);
                        MessageFrame mf = new MessageFrame();
                        mf = (MessageFrame)obj;
                        if (mf.Destination == null && mf.Command == null)
                        {
                            service.BroadCast(obj);
                            service.UpdateUI(service.FormatMsg(mf.Sender, mf.Data));
                        }
                        if (mf.Destination != null)
                        {
                            service.BroadCast(obj, mf.Destination);
                        }
                        if (mf.Command == "list")
                        {
                            MessageFrame listmf = new MessageFrame();
                            listmf.ConnectedUsers = service.GetUsersList();
                            service.BroadCast(listmf, mf.Sender);

                        }
                        if (mf.Command == "join")
                        {
                            ChatRoom romm = new ChatRoom(mf.Param);
                            romm.AddClient(clientStream);
                        }

                    }

                    catch (System.IO.IOException) // przerwij kiedy połączenie z klientem zerwane
                    {
                        service.clientsTaskList.Remove(this);
                        break;
                    }

                    catch (ObjectDisposedException) when (service.serverIsClosed) //przerwij kiedy serwer rozłączony
                    {
                        break;
                    }
                    catch (SerializationException) when (service.serverIsClosed)
                    {
                        break;
                    }

                }
                service.clientsTaskList.Remove(this);
                clientStream.Close();
            }
        }



        public class ChatRoom
        {
            List<NetworkStream> clientsList = new List<NetworkStream>();

            public string ChatRoomName { get; set; }

            public ChatRoom(string chatRoomName)
            {
                this.ChatRoomName = chatRoomName;
            }

            public void AddClient(NetworkStream networkStream)
            {
                clientsList.Add(networkStream);
            }

            public void RemoveClient()
            {

            }

            public void ListClients()
            {

            }

            public void CloseChatroom()
            {

            }

        }
    }
}

