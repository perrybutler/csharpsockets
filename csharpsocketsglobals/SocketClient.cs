using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using static DeltaSockets.SocketGlobals;

namespace DeltaSockets
{
    /// <summary>
    /// Class SocketClientSocket.
    /// </summary>
    public class SocketClient : IDisposable
    {
        #region "Fields"

        public bool IsConnected;

        // ManualResetEvent instances signal completion.
        public SocketClientConsole myLogger = new SocketClientConsole(null);

        /// <summary>
        /// The client socket
        /// </summary>
        //public Socket ClientSocket;
        public SocketContainer my;

        /// <summary>
        /// The ip
        /// </summary>
        public IPAddress IP;

        /// <summary>
        /// The port
        /// </summary>
        public ushort Port;

        public ulong Id;

        [Obsolete("Use IPEnd instead.")]
        private IPEndPoint _endpoint;

        //private StateObject stateObject = new StateObject();

        public Action<object, SocketContainer> ReceivedServerMessageCallback;
        public Action OnConnectedCallback;

        private MessageQueue cSendQueue = new MessageQueue();

        #endregion "Fields"

        #region "Properties"

        private SocketState _state;

        public SocketState myState
        {
            get
            {
                return _state;
            }
        }

#pragma warning disable 0618

        internal IPEndPoint IPEnd
        {
            get
            {
                if (IP != null)
                {
                    if (_endpoint == null)
                        _endpoint = new IPEndPoint(IP, Port);
                    return _endpoint;
                }
                else return null;
            }
        }

#pragma warning restore 0618

        public ulong maxReqId
        {
            get
            {
                return Id + ushort.MaxValue;
            }
        }

        #endregion "Properties"

        #region "Constructors"

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketClient"/> class.
        /// </summary>
        /// <param name="doConnection">if set to <c>true</c> [do connection].</param>
        public SocketClient(bool doConnection = false) :
            this(IPAddress.Loopback, SocketServer.DefPort, SocketType.Stream, ProtocolType.Tcp, null, doConnection)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketClient"/> class.
        /// </summary>
        /// <param name="everyFunc">The every function.</param>
        /// <param name="doConnection">if set to <c>true</c> [do connection].</param>
        public SocketClient(Action<object, SocketContainer> everyFunc, bool doConnection = false) :
            this(IPAddress.Loopback, SocketServer.DefPort, SocketType.Stream, ProtocolType.Tcp, everyFunc, doConnection)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketClient"/> class.
        /// </summary>
        /// <param name="ip">The ip.</param>
        /// <param name="port">The port.</param>
        /// <param name="doConnection">if set to <c>true</c> [do connection].</param>
        public SocketClient(string ip, ushort port, bool doConnection = false) :
            this(ip, port, null, doConnection)
        { }

        public SocketClient(IPAddress ip, ushort port, bool doConnection = false) :
            this(ip, port, SocketType.Stream, ProtocolType.Tcp, null, doConnection)
        { }

        public SocketClient(IPAddress ip, ushort port, Action<object, SocketContainer> everyFunc, bool doConnection = false) :
            this(ip, port, SocketType.Stream, ProtocolType.Tcp, everyFunc, doConnection)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketClient"/> class.
        /// </summary>
        /// <param name="ip">The ip.</param>
        /// <param name="port">The port.</param>
        /// <param name="readEvery">The read every.</param>
        /// <param name="everyFunc">The every function.</param>
        /// <param name="doConnection">if set to <c>true</c> [do connection].</param>
        public SocketClient(string ip, ushort port, Action<object, SocketContainer> everyFunc, bool doConnection = false) :
            this(IPAddress.Parse(ip), port, SocketType.Stream, ProtocolType.Tcp, everyFunc, doConnection)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketClient"/> class.
        /// </summary>
        /// <param name="ipAddr">The ip addr.</param>
        /// <param name="port">The port.</param>
        /// <param name="sType">Type of the s.</param>
        /// <param name="pType">Type of the p.</param>
        /// <param name="readEvery">The read every.</param>
        /// <param name="everyFunc">The every function.</param>
        /// <param name="doConnection">if set to <c>true</c> [do connection].</param>
        public SocketClient(IPAddress ipAddr, ushort port, SocketType sType, ProtocolType pType, Action<object, SocketContainer> everyFunc, bool doConnection = false)
        {
            //period = readEvery;

            cSendQueue.MessageQueued += cSendQueue_MessageQueued;

            ReceivedServerMessageCallback = everyFunc;
            //TimerCallback timerDelegate = new TimerCallback(Timering);

            //if (everyFunc != null)
            //    task = new Timer(timerDelegate, null, 5, readEvery);

            IP = ipAddr;
            Port = port;

            //0 as an ID is not allowed, this is waiting for a ID
            my = new SocketContainer(0, new Socket(ipAddr.AddressFamily, sType, pType)
            {
                NoDelay = false
            });

            if (doConnection)
                DoConnection();
        }

        #endregion "Constructors"

        #region "Socket Methods"

        #region "Timering Methods"

        /// <summary>
        /// Starts the receiving.
        /// </summary>
        /*[Obsolete]
        protected void StartReceiving()
        {
            _Receiving(period);
        }

        /// <summary>
        /// Stops the receiving.
        /// </summary>
        [Obsolete]
        protected void StopReceiving()
        {
            _Receiving();
        }

        [Obsolete]
        private void _Receiving(int p = 0)
        {
            if (task != null)
                task.Change(5, p);
        }

        [Obsolete]
        private void Timering(object stateInfo)
        {
            //Receive();
            //ClientCallback(null);
            //if (deserialize) deserialize = false;
        }*/

        #endregion "Timering Methods"

        public void DoConnection()
        {
            // connect to server async
            try
            {
                my.Socket.BeginConnect(IPEnd, new AsyncCallback(ConnectToServerCompleted), my);
            }
            catch (Exception ex)
            {
                myLogger.Log("ConnectToServer error: " + ex.Message);
            }
        }

        public void DisconnectFromServer()
        {
            my.Socket.Disconnect(false);
        }

        /// <summary>
        /// Fires right when a client is connected to the server.
        /// </summary>
        /// <param name="ar"></param>
        /// <remarks></remarks>
        public void ConnectToServerCompleted(IAsyncResult ar)
        {
            // get the async state object which was returned by the async beginconnect method
            SocketContainer co = ar.AsyncState.CastType<SocketContainer>();

            // end the async connection request so we can check if we are connected to the server
            try
            {
                // call the EndConnect method which will succeed or throw an error depending on the result of the connection
                co.Socket.EndConnect(ar); //Send

                // at this point, the EndConnect succeeded and we are connected to the server!
                // send a welcome message

                IsConnected = true;
                OnConnectedCallback?.Invoke();
                //Send(SocketManager.ManagedConn(), co); // ??? --> el problema estába en que estaba llamado a Socket.Send directamente y estamos dentro de un Socket async xD

                // start waiting for messages from the server
                //AsyncReceiveState mReceiveState = new AsyncReceiveState();
                //mReceiveState.Socket = mState.Socket;

                Console.WriteLine("Client ConnectedToServer => CompletedSynchronously: {0}; IsCompleted: {1}", ar.CompletedSynchronously, ar.IsCompleted);

                co.Socket.BeginReceive(co.rState.Buffer, 0, gBufferSize, SocketFlags.None, new AsyncCallback(ServerMessageReceived), co);
            }
            catch (Exception ex)
            {
                // at this point, the EndConnect failed and we are NOT connected to the server!
                myLogger.Log("Connect error: " + ex.Message);
            }
        }

        public void ServerMessageReceived(IAsyncResult ar)
        {
            // get the async state object from the async BeginReceive method
            //AsyncReceiveState mState = (AsyncReceiveState)ar.AsyncState;
            SocketContainer co = ar.AsyncState.CastType<SocketContainer>();

            // call EndReceive which will give us the number of bytes received
            int numBytesReceived = 0;
            numBytesReceived = co.Socket.EndReceive(ar);

            // determine if this is the first data received
            if (co.rState.ReceiveSize == 0)
            {
                // this is the first data recieved, so parse the receive size which is encoded in the first four bytes of the buffer
                co.rState.ReceiveSize = BitConverter.ToInt32(co.rState.Buffer, 0);
                // write the received bytes thus far to the packet data stream
                co.rState.PacketBufferStream.Write(co.rState.Buffer, 4, numBytesReceived - 4);
            }
            else
            {
                // write the received bytes thus far to the packet data stream
                co.rState.PacketBufferStream.Write(co.rState.Buffer, 0, numBytesReceived);
            }

            // increment the total bytes received so far on the state object
            co.rState.TotalBytesReceived += numBytesReceived;
            // check for the end of the packet
            // bytesReceived = Carcassonne.Library.PacketBufferSize Then
            if (co.rState.TotalBytesReceived < co.rState.ReceiveSize)
            {
                // ## STILL MORE DATA FOR THIS PACKET, CONTINUE RECEIVING ##
                // the TotalBytesReceived is less than the ReceiveSize so we need to continue receiving more data for this packet
                co.Socket.BeginReceive(co.rState.Buffer, 0, gBufferSize, SocketFlags.None, new AsyncCallback(ServerMessageReceived), co);
            }
            else
            {
                // ## FINAL DATA RECEIVED, PARSE AND PROCESS THE PACKET ##
                // the TotalBytesReceived is equal to the ReceiveSize, so we are done receiving this Packet...parse it!
                BinaryFormatter mSerializer = new BinaryFormatter();
                // rewind the PacketBufferStream so we can de-serialize it
                co.rState.PacketBufferStream.Position = 0;
                // de-serialize the PacketBufferStream which will give us an actual Packet object
                co.rState.Packet = mSerializer.Deserialize(co.rState.PacketBufferStream);
                // parse the complete message that was received from the server
                ParseReceivedServerMessage(co.rState.Packet, co);

                // call BeginReceive again, so we can start receiving another packet from this client socket
                co.Socket.BeginReceive(co.rState.Buffer, 0, gBufferSize, SocketFlags.None, new AsyncCallback(ServerMessageReceived), co);

                //Dispose of everything we dont need after receive message from server
                Array.Clear(co.rState.Buffer, 0, co.rState.Buffer.Length);
                co.rState.ReceiveSize = 0;
                co.rState.Packet = null;
                co.rState.TotalBytesReceived = 0;
                co.rState.PacketBufferStream.Close();
                co.rState.PacketBufferStream.Dispose();
                co.rState.PacketBufferStream = new MemoryStream();
            }
        }

        public void ParseReceivedServerMessage(object obj, SocketContainer argContainer)
        {
            Console.WriteLine("Received object of type: {0}", obj.GetType().Name);

            if (obj is string)
                myLogger.Log((string)obj);
            else if (obj is SocketMessage)
                HandleAction((SocketMessage)obj, argContainer);
        }

        public void Send(object obj, SocketContainer co)
        {
            // serialize the Packet into a stream of bytes which is suitable for sending with the Socket
            BinaryFormatter mSerializer = new BinaryFormatter();
            using (MemoryStream mSerializerStream = new MemoryStream())
            {
                mSerializer.Serialize(mSerializerStream, obj);

                // get the serialized Packet bytes
                byte[] mPacketBytes = mSerializerStream.GetBuffer();

                // convert the size into a byte array
                byte[] mSizeBytes = BitConverter.GetBytes(mPacketBytes.Length + 4);

                // create the async state object which we can pass between async methods
                //AsyncSendState mState = new AsyncSendState(ClientSocket);

                // resize the BytesToSend array to fit both the mSizeBytes and the mPacketBytes
                // ERROR: Not supported in C#: ReDimStatement

                if (co.sState.BytesToSend != null)
                {
                    if (co.sState.BytesToSend.Length != mPacketBytes.Length + mSizeBytes.Length)
                        Array.Resize(ref co.sState.BytesToSend, mPacketBytes.Length + mSizeBytes.Length);
                }
                else
                    co.sState.BytesToSend = new byte[mPacketBytes.Length + mSizeBytes.Length];

                // copy the mSizeBytes and mPacketBytes to the BytesToSend array
                Buffer.BlockCopy(mSizeBytes, 0, co.sState.BytesToSend, 0, mSizeBytes.Length);
                Buffer.BlockCopy(mPacketBytes, 0, co.sState.BytesToSend, mSizeBytes.Length, mPacketBytes.Length);

                Array.Clear(mSizeBytes, 0, mSizeBytes.Length);
                Array.Clear(mPacketBytes, 0, mPacketBytes.Length);

                Console.WriteLine("Ready to send a object of {0} bytes length", co.sState.BytesToSend.Length);

                co.Socket.BeginSend(co.sState.BytesToSend, co.sState.NextOffset(), co.sState.NextLength(), SocketFlags.None, new AsyncCallback(MessagePartSent), co);
            }
        }

        public void SendMessageToServer(string argCommandString, SocketContainer container)
        {
            Send(argCommandString, container);
        }

        public void MessagePartSent(IAsyncResult ar)
        {
            // get the async state object which was returned by the async beginsend method
            //AsyncSendState mState = (AsyncSendState)ar.AsyncState;
            SocketContainer co = ar.AsyncState.CastType<SocketContainer>();

            try
            {
                int numBytesSent = 0;
                // call the EndSend method which will succeed or throw an error depending on if we are still connected
                numBytesSent = co.Socket.EndSend(ar);

                // increment the total amount of bytes processed so far
                co.sState.Progress += numBytesSent;

                // determine if we havent' sent all the data for this Packet yet
                if (co.sState.NextLength() > 0)
                {
                    // we need to send more data
                    co.Socket.BeginSend(co.sState.BytesToSend, co.sState.NextOffset(), co.sState.NextLength(), SocketFlags.None, new AsyncCallback(MessagePartSent), co);
                }
                else
                {
                    Console.WriteLine("Client MessagePartSent completed. Clearing stuff...");

                    //Reset for the next time
                    Array.Clear(co.sState.BytesToSend, 0, co.sState.BytesToSend.Length);

                    Array.Resize(ref co.sState.BytesToSend, gBufferSize);
                }
                // at this point, the EndSend succeeded and we are ready to send something else!
                // TODO: use the queue to determine what message was sent and show it in the local chat buffer
                //RaiseEvent MessageSentToServer()
            }
            catch (Exception ex)
            {
                myLogger.Log("DataSent error: " + ex.Message);
            }
        }

        private void cSendQueue_MessageQueued()
        {
            // when a message is queued, we need to check whether or not we are currently processing the queue before allowing the top item in the queue to start sending
            if (cSendQueue.Processing == false)
            {
                // process the top message in the queue, which in turn will process all other messages until the queue is empty
                //AsyncSendState mState = (AsyncSendState)cSendQueue.Messages.Dequeue();
                SocketContainer co = cSendQueue.Messages.Dequeue().CastType<SocketContainer>();

                // we must send the correct number of bytes, which must not be greater than the remaining bytes
                co.Socket.BeginSend(co.sState.BytesToSend, co.sState.NextOffset(), co.sState.NextLength(), SocketFlags.None, new AsyncCallback(MessagePartSent), co);
            }
        }

        #endregion "Socket Methods"

        #region "Class Methods"

        private void HandleAction(SocketMessage sm, SocketContainer argContainer)
        {
            //Before we connect we request an id to the master server...
            if (sm.msg is SocketCommand)
            {
                SocketCommand cmd = sm.msg.CastType<SocketCommand>();
                if (cmd != null)
                {
                    switch (cmd.Command)
                    {
                        /*case SocketCommands.CreateConnId:
                            myLogger.Log("Starting new CLIENT connection with ID: {0}", sm.id);
                            Id = sm.id;

                            Send(SocketManager.ConfirmConnId(Id), argContainer); //???
                            OnConnectedCallback?.Invoke();
                            break;*/

                        case SocketCommands.ReturnClientIDAfterAccept:
                            myLogger.Log("Starting new CLIENT connection with ID: {0}", sm.id);
                            Id = sm.id;
                            break;

                        case SocketCommands.CloseInstance:
                            myLogger.Log("Client is closing connection...");
                            Stop(false);
                            break;

                        default:
                            myLogger.Log("Unknown ClientCallbackion to take! Case: {0}", cmd.Command);
                            break;
                    }
                }
                else
                {
                    myLogger.Log("Empty string received by client!");
                }
            }
            else
                ReceivedServerMessageCallback?.Invoke(sm.msg, argContainer);
        }

        #region "Error & Close & Stop & Dispose"

        private void CloseConnection(SocketShutdown soShutdown)
        {
            if (soShutdown == SocketShutdown.Receive)
            {
                myLogger.Log("Remember that you're in a Client, you can't only close Both connections or only your connection.");
                return;
            }
            if (my.Socket.Connected)
            {
                IsConnected = false;
                my.Socket.Disconnect(false);
                if (my.Socket.Connected)
                    my.Socket.Shutdown(soShutdown);
            }
        }

        private bool disposed;

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                Send(SocketManager.ClientClosed(Id), my);
                // Free any other managed objects here.
                //
            }

            // Free any unmanaged objects here.
            //
            disposed = true;
        }

        /// <summary>
        /// Ends this instance.
        /// </summary>
        public void Stop(bool dis)
        {
            if (_state == SocketState.ClientStarted)
            {
                try
                {
                    myLogger.Log("Closing client (#{0})", Id);

                    _state = SocketState.ClientStopped;
                    CloseConnection(SocketShutdown.Both); //No hace falta comprobar si estamos connected

                    if (dis)
                    {
                        my.Socket.Close();
                        my.Socket.Dispose();
                    }
                    else Send(SocketManager.ClientClosed(Id), my); //If not disposed then send this
                }
                catch (Exception ex)
                {
                    myLogger.Log("Exception ocurred while trying to stop client: " + ex);
                }
            }
            else
                myLogger.Log("Client cannot be stopped because it hasn't been started!");
        }

        #endregion "Error & Close & Stop & Dispose"

        #endregion "Class Methods"
    }
}