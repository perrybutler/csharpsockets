using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using static DeltaSockets.SocketGlobals;

namespace DeltaSockets
{
    /// <summary>
    /// Class SocketServer.
    /// </summary>
    public class SocketServer : IDisposable
    {
        #region "Fields"

        public SocketServerConsole myLogger = new SocketServerConsole(null);

        /// <summary>
        /// The lerped port
        /// </summary>
        public const int DefPort = 7776;

        /// <summary>
        /// The server socket
        /// </summary>
        public Socket ServerSocket;

        /// <summary>
        /// The permision
        /// </summary>
        public SocketPermission Permision;

        /// <summary>
        /// The ip
        /// </summary>
        public IPAddress IP;

        /// <summary>
        /// The port
        /// </summary>
        public int Port;

        [Obsolete("Use IPEnd instead.")]
        private IPEndPoint _endpoint;

        /// <summary>
        /// All done
        /// </summary>
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        /// <summary>
        /// The routing table
        /// </summary>
        public static Dictionary<ulong, SocketContainer> routingTable = new Dictionary<ulong, SocketContainer>(); //With this we can assume that ulong.MaxValue clients can connect to the Socket (2^64 - 1)

        //The ulong is redundant ^^^

        public Action<object, SocketContainer> ReceivedClientMessageCallback;

        private static bool dispose, debug;

        //Check this vvv

        public event ClientConnectedEventHandler ClientConnected;

        public event MessageReceivedEventHandler MessageReceived;

        public event ClientDisconnectedEventHandler ClientDisconnected;

        public delegate void ClientConnectedEventHandler(SocketContainer argContainer);

        public delegate void MessageReceivedEventHandler(string argMessage, SocketContainer argContainer);

        public delegate void ClientDisconnectedEventHandler(SocketContainer argContainer);

        #endregion "Fields"

        #region "Propierties"

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

        #endregion "Propierties"

        #region "Constructors"

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketServer"/> class.
        /// </summary>
        /// <param name="debug">if set to <c>true</c> [debug].</param>
        /// <param name="doConnection">if set to <c>true</c> [do connection].</param>
        public SocketServer(bool debug, bool doConnection = false) :
            this(new SocketPermission(NetworkAccess.Accept, TransportType.Tcp, "", SocketPermission.AllPorts), IPAddress.Loopback, DefPort, SocketType.Stream, ProtocolType.Tcp, debug, doConnection)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketServer"/> class.
        /// </summary>
        /// <param name="ip">The ip.</param>
        /// <param name="port">The port.</param>
        /// <param name="debug">if set to <c>true</c> [debug].</param>
        /// <param name="doConnection">if set to <c>true</c> [do connection].</param>
        public SocketServer(string ip, int port, bool debug, bool doConnection = false) :
            this(new SocketPermission(NetworkAccess.Accept, TransportType.Tcp, "", SocketPermission.AllPorts), IPAddress.Parse(ip), port, SocketType.Stream, ProtocolType.Tcp, debug, doConnection)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketServer"/> class.
        /// </summary>
        /// <param name="permission">The permission.</param>
        /// <param name="ipAddr">The ip addr.</param>
        /// <param name="port">The port.</param>
        /// <param name="sType">Type of the s.</param>
        /// <param name="pType">Type of the p.</param>
        /// <param name="curDebug">if set to <c>true</c> [current debug].</param>
        /// <param name="doConnection">if set to <c>true</c> [do connection].</param>
        public SocketServer(SocketPermission permission, IPAddress ipAddr, int port, SocketType sType, ProtocolType pType, bool curDebug, bool doConnection = false)
        {
            permission.Demand();

            IP = ipAddr;
            Port = port;

            debug = curDebug;

            ServerSocket = new Socket(ipAddr.AddressFamily, sType, pType);

            if (doConnection)
                StartServer(); // ??? --> ComeAlive
        }

        #endregion "Constructors"

        #region "Socket Methods"

        public void InitializeServer()
        {
        }

        /// <summary>
        /// StartServer starts the server by listening for new client connections with a TcpListener.
        /// </summary>
        /// <remarks></remarks>
        public void StartServer()
        {
            // create the TcpListener which will listen for and accept new client connections asynchronously

            // bind to the server's ipendpoint
            ServerSocket.Bind(IPEnd);

            // configure the listener to allow 1 incoming connection at a time
            ServerSocket.Listen(1000);

            // accept client connection async
            ServerSocket.BeginAccept(new AsyncCallback(ClientAccepted), ServerSocket);
        }

        public void StopServer()
        {
            //cServerSocket.Disconnect(True)
            ServerSocket.Close();
            //cStopRequested = True
        }

        /// <summary>
        /// ClientConnected is a callback that gets called when the server accepts a client connection from the async BeginAccept method.
        /// </summary>
        /// <param name="ar"></param>
        /// <remarks></remarks>
        public void ClientAccepted(IAsyncResult ar)
        {
            // get the async state object from the async BeginAccept method, which contains the server's listening socket
            Socket mServerSocket = ar.AsyncState.CastType<Socket>();
            // call EndAccept which will connect the client and give us the the client socket
            Socket mClientSocket = null;
            try
            {
                mClientSocket = mServerSocket.EndAccept(ar);
            }
            catch //(ObjectDisposedException ex)
            {
                // if we get an ObjectDisposedException it that means the server socket terminated while this async method was still active
                return;
            }
            // instruct the client to begin receiving data
            //AsyncReceiveState mState = new AsyncReceiveState();
            //mState.Socket = mClientSocket;

            //??? hay que quitar todo lo de los enums para obtener una ID, directamente dar aquí
            //Tenemos que generar una id asociada a este socket para hacer el socket container
            //Esta en la linea 455 (case SocketCommands.Conn: ...) esto me lo tengo q traer
            //En el endreceive es donde tengo q decirle al cliente su id

            ulong genID = 1;

            //Give id in a range...
            bool b = routingTable.Keys.FindFirstMissingNumberFromSequence(out genID, new MinMax<ulong>(1, (ulong)routingTable.Count));
            myLogger.Log("Adding #{0} client to routing table!", genID); //Esto ni parece funcionar bien

            SocketContainer co = new SocketContainer(genID, mClientSocket);
            if (!routingTable.ContainsKey(genID))
                routingTable.Add(genID, co);
            else
                Console.WriteLine("Overlapping id error!");

            //We don't have a way to know if there was a problem in the transmission ???
            SendToClient(SocketManager.ReturnClientIDAfterAccept(genID), co);
            ClientConnected?.Invoke(co);

            co.Socket.BeginReceive(co.rState.Buffer, 0, gBufferSize, SocketFlags.None, new AsyncCallback(ClientMessageReceived), co); //ClientMessageReceived
            // begin accepting another client connection
            mServerSocket.BeginAccept(new AsyncCallback(ClientAccepted), mServerSocket);

            //mServerSocket.Dispose(); // x?x?
            //Console.WriteLine("Server ClientAccepted => CompletedSynchronously: {0}; IsCompleted: {1}", ar.CompletedSynchronously, ar.IsCompleted);
        }

        /// <summary>
        /// BeginReceiveCallback is an async callback method that gets called when the server receives some data from a client socket after calling the async BeginReceive method.
        /// </summary>
        /// <param name="ar"></param>
        /// <remarks></remarks>
        public void ClientMessageReceived(IAsyncResult ar)
        {
            // get the async state object from the async BeginReceive method
            //AsyncReceiveState mState = (AsyncReceiveState)ar.AsyncState;
            SocketContainer co = ar.AsyncState.CastType<SocketContainer>();

            // call EndReceive which will give us the number of bytes received
            int numBytesReceived = 0;
            try
            {
                numBytesReceived = co.Socket.EndReceive(ar);
            }
            catch (SocketException ex)
            {
                // if we get a ConnectionReset exception, it could indicate that the client has disconnected
                if (ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    ClientDisconnected?.Invoke(co);
                    return;
                }
            }

            // if we get numBytesReceived equal to zero, it could indicate that the client has disconnected
            if (numBytesReceived == 0)
            {
                ClientDisconnected?.Invoke(co);
                return;
            }

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
                co.Socket.BeginReceive(co.rState.Buffer, 0, gBufferSize, SocketFlags.None, new AsyncCallback(ClientMessageReceived), co);
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
                Console.WriteLine("Succesfully deserialized object of type: {0}", co.rState.Packet.GetType().Name);
                // handle the message
                ParseReceivedClientMessage(co.rState.Packet, co);
                // call BeginReceive again, so we can start receiving another packet from this client socket
                //AsyncReceiveState mNextState = new AsyncReceiveState();
                //mNextState.Socket = mState.Socket;

                // ???
                //mState.PacketBufferStream.Close();
                //mState.PacketBufferStream.Dispose();
                //mState.PacketBufferStream = null;
                Array.Clear(co.rState.Buffer, 0, co.rState.Buffer.Length);

                co.rState.ReceiveSize = 0;

                Console.WriteLine("Server ClientMessageReceived => CompletedSynchronously: {0}; IsCompleted: {1}", ar.CompletedSynchronously, ar.IsCompleted);

                co.Socket.BeginReceive(co.rState.Buffer, 0, gBufferSize, SocketFlags.None, new AsyncCallback(ClientMessageReceived), co);

                //mState.Socket.Dispose(); // x?x?
                //mState = null;
            }
        }

        public void ParseReceivedClientMessage(object obj, SocketContainer argContainer)
        {
            //Aquí ocurre la magia
            if (obj is string)
            {
                string argCommandString = (string)obj;

                myLogger.Log("");
                //myLogger.Log("ParseReceivedClientMessage: " + argCommandString);

                // parse the command string
                string argCommand = null;
                string argText = null;

                if (argCommandString.StartsWith("/"))
                {
                    argCommand = argCommandString.Substring(0, argCommandString.IndexOf(" "));
                    argText = argCommandString.Remove(0, argCommand.Length + 1);
                }
                else
                    argText = argCommandString;

                switch (argText)
                {
                    case "hi server":
                        SendMessageToClient("/say Server replied.", argContainer);
                        break;
                }

                MessageReceived?.Invoke(argCommandString, argContainer);
            }
            else if (obj is SocketMessage)
                HandleAction((SocketMessage)obj, argContainer);

            //This is a server-only method, that is called for "Debugging purpouses" by the moment
            ReceivedClientMessageCallback?.Invoke(obj, argContainer);
            //Check there in case of ((SocketMessage)obj).DestsId[0] == 0??
        }

        /// <summary>
        /// QueueMessage prepares a Message object containing our data to send and queues this Message object in the OutboundMessageQueue.
        /// </summary>
        /// <remarks></remarks>
        public void SendToClient(object obj, SocketContainer co)
        {
            // serialize the Packet into a stream of bytes which is suitable for sending with the Socket
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter mSerializer = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            using (System.IO.MemoryStream mSerializerStream = new System.IO.MemoryStream())
            {
                mSerializer.Serialize(mSerializerStream, obj);

                // get the serialized Packet bytes
                byte[] mPacketBytes = mSerializerStream.GetBuffer();

                // convert the size into a byte array
                byte[] mSizeBytes = BitConverter.GetBytes(mPacketBytes.Length + 4);

                // create the async state object which we can pass between async methods
                //AsyncSendState mState = new AsyncSendState(argClient);

                // resize the BytesToSend array to fit both the mSizeBytes and the mPacketBytes
                // TODO: ReDim mState.BytesToSend(mPacketBytes.Length + mSizeBytes.Length - 1)
                Array.Resize(ref co.sState.BytesToSend, mPacketBytes.Length + mSizeBytes.Length);

                // copy the mSizeBytes and mPacketBytes to the BytesToSend array
                Buffer.BlockCopy(mSizeBytes, 0, co.sState.BytesToSend, 0, mSizeBytes.Length);
                Buffer.BlockCopy(mPacketBytes, 0, co.sState.BytesToSend, mSizeBytes.Length, mPacketBytes.Length);

                Array.Clear(mSizeBytes, 0, mSizeBytes.Length);
                Array.Clear(mPacketBytes, 0, mPacketBytes.Length);

                Console.Write("");

                // queue the Message
                Console.WriteLine("Server (SendToClient): NextOffset: {0}; NextLength: {1}", co.sState.NextOffset(), co.sState.NextLength());
                co.Socket.BeginSend(co.sState.BytesToSend, co.sState.NextOffset(), co.sState.NextLength(), SocketFlags.None, new AsyncCallback(MessagePartSent), co);
            }
        }

        /// <summary>
        /// QueueMessage prepares a Message object containing our data to send and queues this Message object in the OutboundMessageQueue.
        /// </summary>
        /// <remarks></remarks>
        public void SendMessageToClient(string argCommandString, SocketContainer argContainer)
        {
            SendToClient(argCommandString, argContainer);
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
                    Console.WriteLine("Server MessagePartSent completed. Clearing stuff...");

                    Array.Clear(co.sState.BytesToSend, 0, co.sState.BytesToSend.Length);

                    //mState.Socket.Dispose(); // x?x? n
                    co.sState = null;
                }

                // at this point, the EndSend succeeded and we are ready to send something else!
            }
            catch (Exception ex)
            {
                myLogger.Log("DataSent error: " + ex.Message);
            }
        }

        #endregion "Socket Methods"

        #region "Class Methods"

        private void HandleAction(SocketMessage sm, SocketContainer argContainer)
        {
            //string val = sm.StringValue;
            if (sm.msg is SocketCommand)
            {
                SocketCommand cmd = sm.msg.CastType<SocketCommand>();
                if (cmd != null)
                {
                    switch (cmd.Command)
                    {
                        /*case SocketCommands.Conn:
                            //Para que algo se añade aqui debe ocurrir algo...
                            //Give an id for a client before we add it to the routing table
                            //and create a request id for the next action that needs it

                            //First, we have to assure that there are free id on the current KeyValuePair to optimize the process...
                            ulong genID = 1;

                            //Give id in a range...
                            bool b = routingTable.Keys.FindFirstMissingNumberFromSequence(out genID, new MinMax<ulong>(1, (ulong)routingTable.Count));
                            myLogger.Log("Adding #{0} client to routing table!", genID); //Esto ni parece funcionar bien

                            SendToClient(SocketManager.SendConnId(genID), handler);
                            break;*/

                        /*case SocketCommands.ConfirmConnId:
                            routingTable.Add(sm.id, argContainer);
                            break;*/

                        case SocketCommands.CloseClients:
                            CloseAllClients(sm.id);
                            break;

                        case SocketCommands.ClosedClient:
                            //closedClients.Add(sm.id);
                            SocketManager.PoliteClose(sm.id); //Tell to the client that it has been disconnected from it
                            routingTable.Remove(sm.id);
                            CloseServerAfterClientsClose(dispose);
                            break;

                        case SocketCommands.Stop:
                            CloseAllClients(sm.id);
                            break;

                        case SocketCommands.UnpoliteStop:
                            object d = cmd.Metadata["Dispose"];
                            Stop(d != null && ((bool)d));
                            break;

                        default:
                            DoServerError(string.Format("Cannot de-encrypt the message! Unrecognized 'enum' case: {0}", cmd.Command), sm.id);
                            break;
                    }
                }
            }
            else
                //If not is a command, then send the object to other clients...
                SendToClient(sm, sm.DestsId);
        }

        #region "Send Methods"

        private void SendToAllClients(SocketMessage sm, object obj = null, int bytesRead = 0)
        {
            myLogger.Log("---------------------------");
            if (bytesRead > 0) myLogger.Log("Client with ID {0} sent {1} bytes (JSON).", sm.id, bytesRead);
            myLogger.Log("Sending to the other clients.");
            myLogger.Log("---------------------------");
            myLogger.Log("");

            //Send to the other clients
            foreach (KeyValuePair<ulong, SocketContainer> soc in routingTable)
                if (soc.Key != sm.id)
                    SendToClient(obj == null ? sm : obj, soc.Value); // ??? <-- byteData ??
        }

        private void SendToClient(SocketMessage sm, object obj = null, params ulong[] dests)
        {
            SendToClient(sm, dests.AsEnumerable(), obj);
            dests = null;
        }

        private void SendToClient(SocketMessage sm, IEnumerable<ulong> dests, object obj = null)
        {
            if (dests == null)
            {
                Console.WriteLine("Can't send null list of Destinations!");
                return;
            }
            if (dests.Count() == 1)
            {
                if (dests.First() == ulong.MaxValue)
                { //Send to all users
                    foreach (KeyValuePair<ulong, SocketContainer> soc in routingTable)
                        if (soc.Key != sm.id)
                            SendToClient(obj == null ? sm : obj, soc.Value);
                }
            }
            else if (dests.Count() > 1)
            { //Select dictionary keys that contains dests
                foreach (KeyValuePair<ulong, SocketContainer> soc in routingTable.Where(x => dests.Contains(x.Key)))
                    if (soc.Key != sm.id)
                        SendToClient(obj == null ? sm : obj, soc.Value);
            }
            else
            {
                //Error
                Console.WriteLine("Destinations var isn't null, but it's length is 0.");
            }
        }

        #endregion "Send Methods"

        #region "Error & Close & Stop & Dispose"

        private void DoServerError(string msg, ulong id = 0, bool dis = false)
        {
            PoliteStop(dis, id);
            myLogger.Log("{0} CLOSING SERVER due to: " + msg,
                id == 0 ? "" : string.Format("(FirstClient: #{0})", id));
        }

        private void CloseAllClients(ulong id = 0)
        {
            if (id > 0) SendToClient(SocketManager.PoliteClose(id), routingTable[id]); //First, close the client that has send make the request...
            myLogger.Log("Closing all {0} clients connected!", routingTable.Count);
            foreach (KeyValuePair<ulong, SocketContainer> soc in routingTable)
            {
                if (soc.Key != id) //Then, close the others one
                {
                    myLogger.Log("Sending to CLIENT #{0} order to CLOSE", soc.Key);
                    SendToClient(SocketManager.PoliteClose(soc.Key), soc.Value);
                }
            }
        }

        private void CloseServerAfterClientsClose(bool dis)
        {
            if (routingTable.Count == routingTable.Count)
                Stop(dis); //Close the server, when all the clients has been closed.
        }

        public void PoliteStop(bool dis = false, ulong id = 0)
        {
            dispose = dis;
            CloseAllClients(id); //And then, the server will autoclose itself...
        }

        /// <summary>
        /// Closes the server.
        /// </summary>
        private void Stop(bool dis = true)
        {
            if (_state == SocketState.ServerStarted)
            {
                try
                {
                    myLogger.Log("Closing server");

                    _state = SocketState.ServerStopped;
                    if (ServerSocket.Connected) //Aqui lo que tengo que hacer es que se desconecten los clientes...
                        ServerSocket.Shutdown(SocketShutdown.Both);

                    ServerSocket.Close();

                    if (dis)
                    { //Dispose
                        ServerSocket.Dispose();
                        ServerSocket = null;
                    }
                }
                catch (Exception ex)
                {
                    myLogger.Log("Exception ocurred while trying to stop server: " + ex);
                }
            }
            else
                myLogger.Log("Server cannot be stopped because it hasn't been started!");
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
                myLogger.Log("Disposing server");
                PoliteStop(true);
                // Free any other managed objects here.
                //
            }

            // Free any unmanaged objects here.
            //
            disposed = true;
        }

        #endregion "Error & Close & Stop & Dispose"

        #endregion "Class Methods"
    }
}