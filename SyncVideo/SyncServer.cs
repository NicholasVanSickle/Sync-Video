using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;

namespace SyncVideo
{
    public class SyncExecutionContext
    {
        public Action<int> SetPlayState;
        public Action<double> SetPlayPosition;
        public Func<SyncClientMessage> GetSyncMessage;
        public Action<string> Log;
        public Action<string> AttemptPlayFile;
    }

    [Serializable]
    public abstract class SyncClientMessage
    {
        public abstract void Execute(SyncExecutionContext context, SyncClient client);
    }

    [Serializable]
    public class SyncStateMessage : SyncClientMessage
    {
        public int PlayState { get; private set; }
        public double PlayPosition { get; private set; }

        public SyncStateMessage(int playState, double playPosition)
        {
            PlayState = playState;
            PlayPosition = playPosition;
        }

        public override void Execute(SyncExecutionContext context, SyncClient client)
        {
            context.SetPlayState(PlayState);
            context.SetPlayPosition(PlayPosition);
            context.Log("Synced player state");
        }
    }

    [Serializable]
    public class SyncPlayFileMessage : SyncClientMessage
    {
        public string FileName { get; private set; }

        public SyncPlayFileMessage(string fileName)
        {
            FileName = fileName;
        }

        public override void Execute(SyncExecutionContext context, SyncClient client)
        {
            context.Log("Attempting to sync play file: " + FileName);
            context.AttemptPlayFile(FileName);
        }
    }

    [Serializable]
    public class PingMessage : SyncClientMessage
    {
        public override void Execute(SyncExecutionContext context, SyncClient client)
        {
            if (client == null)
                return;
            client.SendMessage(new SyncPongMessage());
        }
    }
    
    [Serializable]
    public abstract class SyncServerMessage
    {        
        public abstract void Execute(SyncExecutionContext context, TcpClient client, SyncServer server);
    }

    [Serializable]
    public class SyncPongMessage : SyncServerMessage
    {
        public override void Execute(SyncExecutionContext context, TcpClient client, SyncServer server)
        {
            server.Pong(client);
        }
    }

    [Serializable]
    public class SyncPropogateMessage : SyncServerMessage
    {
        public SyncClientMessage PropogatedMessage { get; private set; }

        public override void Execute(SyncExecutionContext context, TcpClient client, SyncServer server)
        {
            server.PropogateMessage(PropogatedMessage);
        }

        public SyncPropogateMessage(SyncClientMessage message)
        {
            PropogatedMessage = message;
        }
    }

    public abstract class SyncConnection
    {
        protected Thread ConnectionThread { get; set; }
        protected static BinaryFormatter Formatter = new BinaryFormatter();
        protected SyncExecutionContext Context { get; set; }

        private bool _running;
        public bool Running
        {
            get { return _running; }
            protected set
            {
                if (!_running && value)
                    ConnectionThread.Start();
                _running = value;
            }
        }

        public virtual void Stop()
        {
            Running = false;
            if(ConnectionThread != null)
                ConnectionThread.Abort();
        }

        public virtual void Start()
        {
            Running = true;
        }

        protected SyncConnection(SyncExecutionContext context)
        {
            Context = context;
        }

        public void Log(string s)
        {
            Context.Log(s);
        }

        public void Log(string format, params object[] args)
        {
            Log(string.Format(format,args));
        }

        public void Countdown(int seconds)
        {
            while (seconds > 0)
            {
                Log("{0}", seconds--);
                Thread.Sleep(1000);
            }
        }

        public void Countdown(int seconds, Action result)
        {
            Countdown(seconds);
            result();
        }
    }

    public class SyncServer : SyncConnection
    {
        private TcpListener _listener;
        private Thread _pingThread;
        private Dictionary<TcpClient, int> _pings; 

        public SyncServer(SyncExecutionContext context, int port = 4885) : base(context)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            ConnectionThread = new Thread(Listen);
            _pingThread = new Thread(Ping);
        }

        public void Pong(TcpClient client)
        {
            _pings[client] = -1;
        }

        private void Ping()
        {
            const int timeout = 50000;
            const int check = 1000;

            _pings = new Dictionary<TcpClient, int>();
            while(Running)
            {
                List<TcpClient> badClients = new List<TcpClient>();
                lock (_pings)
                {
                    foreach (var client in _clients)
                    {
                        if (!_pings.ContainsKey(client))
                        {
                            _pings[client] = -1;                            
                        }

                        if (_pings[client] == -1)
                        {
                            Formatter.Serialize(client.GetStream(), new PingMessage());
                            _pings[client] = 0;
                            //Log("Client {0}, pong", client.Client.RemoteEndPoint.ToString());
                        }
                        else
                        {
                            _pings[client] += check;
                            if(_pings[client] >= timeout)
                            {
                                Log("Client {0}, ping timeout", client.Client.RemoteEndPoint.ToString());
                                badClients.Add(client);
                            }
                        }
                    }
                }

                lock(_clients)
                {
                    foreach (var client in badClients)
                        _clients.Remove(client);
                }

                Thread.Sleep(check);
            }
        }

        private void Listen()
        {
            try
            {
                bool error;
                do
                {
                    error = false;
                    try
                    {
                        _listener.Start();
                    }
                    catch (SocketException e)
                    {
                        error = true;
                        Log("Error starting server, SocketException: {0}", e.Message);
                        Log("Retrying in:");
                        Countdown(5);
                    }
                } while (error);

                Log("Server started, listening for clients...");

                while (Running)
                {
                    try
                    {
                        if (!_listener.Pending())
                        {
                            Thread.Sleep(250);
                            continue;
                        }
                        var client = _listener.AcceptTcpClient();
                        var clientThread = new Thread(HandleClient);
                        clientThread.Start(client);
                    }
                    catch (SocketException e)
                    {
                        Log("Error accepting client, socket exception: {0}", e.Message);
                    }
                }
            }
            finally
            {
                _listener.Stop();
            }
        }

        private List<TcpClient> _clients = new List<TcpClient>();

        public override void Start()
        {
            base.Start();

            _pingThread.Start();
        }

        public override void Stop()
        {
            base.Stop();

            _pingThread.Abort();

            if(_client != null)
                _client.Close();

            foreach(var client in _clients)
                client.Close();
            _clients.Clear();
        }

        private TcpClient _client;

        private void HandleClient(object arg)
        {
            TcpClient client = (TcpClient) arg;
            _client = client;
            _clients.Add(client);
            var stream = client.GetStream();

            var clientName = client.Client.RemoteEndPoint.ToString();
            Log("Client {0} connected.", clientName);

            using(client)
            while(Running && _clients.Contains(client))
            {
                try
                {
                    SyncServerMessage message = (SyncServerMessage)Formatter.Deserialize(stream);
                    message.Execute(Context, client, this);
                }
                catch (EndOfStreamException e)
                {
                    Log("Client connection {0} failed, EndOfStreamException: {1}", clientName, e.Message);
                    break;
                }                
                catch (SerializationException e)
                {                    
                    Log("Client connection {0} failed, SerializationException: {1}", clientName, e.Message);
                    break;
                }
                catch (IOException e)
                {
                    Log("Client connection {0} failed, IOException: {1}", clientName, e.Message);
                    break;
                }
            }
            _clients.Remove(client);
        }

        public void PropogateMessage(SyncClientMessage message)
        {
            if (message == null)
                message = Context.GetSyncMessage();
            else
                message.Execute(Context, null);            
            var thread = new Thread(() =>
                                        {
                                            foreach(TcpClient c in _clients)
                                            {
                                                try
                                                {
                                                    Formatter.Serialize(c.GetStream(), message);
                                                }
                                                catch (SerializationException)
                                                {
                                                }
                                                catch (IOException e)
                                                {
                                                    var clientName = c.Client.RemoteEndPoint.ToString();
                                                    Log("Client connection {0} propogation failed, IOException: {1}", clientName, e.Message);
                                                }
                                            }
                                        });
            thread.Start();
        }
    }

    public class SyncClient : SyncConnection
    {
        private string _ip;
        private int _port;
        private TcpClient _client;

        public SyncClient(SyncExecutionContext context, string ip, int port = 4885) : base (context)
        {
            ConnectionThread = new Thread(Listen);
            _ip = ip;
            _port = port;
        }

        public override void Stop()
        {
            base.Stop();
            if(_client != null)
                _client.Close();
        }

        private void Listen()
        {
            using(_client)
            {
                for (;;)
                {
                    if (!Running)
                    {
                        break;
                    }

                    bool error = false;
                    do
                    {
                        error = false;
                        Log("Connecting to {0}:{1}", _ip, _port);
                        try
                        {
                            if (_client == null)
                            {
                                _client = new TcpClient(_ip, _port);
                            }
                            else
                            {
                                _client.Close();
                                _client = new TcpClient(_ip, _port);
                            }
                        }
                        catch (SocketException e)
                        {
                            error = true;
                            Log("Connection failed, retrying: {0}", e.Message);
                            Countdown(5);
                        }
                    } while (error);

                    Log("Connected succesfully to: {0}:{1}", _ip, _port);

                    var stream = _client.GetStream();
                    while (Running && !error)
                    {
                        try
                        {
                            var message = (SyncClientMessage) Formatter.Deserialize(stream);
                            message.Execute(Context, this);
                        }
                        catch (EndOfStreamException e)
                        {
                            Log("EndOfStreamException: ", e.Message);
                        }
                        catch (IOException e)
                        {
                            Log("IOException: ", e.Message);
                            error = true;
                        }
                        catch (SerializationException)
                        {
                        }
                    }
                }
            }
        }

        public void SendMessage(SyncServerMessage message)
        {
            if (!Running)
                return;
            try
            {
                Formatter.Serialize(_client.GetStream(), message);
            }
            catch (SerializationException e)
            {
                Log("Sending to server failed, SerializationException: ", e.Message);
            }
            catch (IOException e)
            {
                Log("Sending to server failed, IOException: ", e.Message);
            }
        }

        public void PropogateToServer(SyncClientMessage message)
        {
            SendMessage(new SyncPropogateMessage(message));
        }
    }
}
