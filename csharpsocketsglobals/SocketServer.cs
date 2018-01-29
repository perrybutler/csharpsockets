namespace ProjectSockets
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    public class SocketServer
    {
        private int cServerPort = 9898;

        private string cServerAddress = "localhost";
        private Socket cServerSocket;
        private bool cStopRequested;

        private System.Collections.ArrayList cClients = new System.Collections.ArrayList();

        public event MessageReceivedEventHandler MessageReceived;

        public delegate void MessageReceivedEventHandler(string argMessage, Socket argClientSocket);

        public event ClientConnectedEventHandler ClientConnected;

        public delegate void ClientConnectedEventHandler(Socket argClientSocket);

        public event ClientDisconnectedEventHandler ClientDisconnected;

        public delegate void ClientDisconnectedEventHandler(Socket argClientSocket);

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
            cServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // convert the server address and port into an ipendpoint
            IPAddress[] mHostAddresses = Dns.GetHostAddresses(cServerAddress);
            IPEndPoint mEndPoint = null;
            foreach (IPAddress mHostAddress in mHostAddresses)
            {
                if (mHostAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    mEndPoint = new IPEndPoint(mHostAddress, cServerPort);
                }
            }

            // bind to the server's ipendpoint
            cServerSocket.Bind(mEndPoint);

            // configure the listener to allow 1 incoming connection at a time
            cServerSocket.Listen(1);

            // accept client connection async
            cServerSocket.BeginAccept(new AsyncCallback(ClientAccepted), cServerSocket);
        }

        public void StopServer()
        {
            //cServerSocket.Disconnect(True)
            cServerSocket.Close();
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
            Socket mServerSocket = (Socket)ar.AsyncState;
            // call EndAccept which will connect the client and give us the the client socket
            Socket mClientSocket = null;
            try
            {
                mClientSocket = mServerSocket.EndAccept(ar);
            }
            catch (ObjectDisposedException ex)
            {
                // if we get an ObjectDisposedException it that means the server socket terminated while this async method was still active
                return;
            }
            // instruct the client to begin receiving data
            SocketGlobals.AsyncReceiveState mState = new SocketGlobals.AsyncReceiveState();
            mState.Socket = mClientSocket;
            if (ClientConnected != null)
            {
                ClientConnected(mState.Socket);
            }
            mState.Socket.BeginReceive(mState.Buffer, 0, SocketGlobals.gBufferSize, SocketFlags.None, new AsyncCallback(ClientMessageReceived), mState);
            // begin accepting another client connection
            mServerSocket.BeginAccept(new AsyncCallback(ClientAccepted), mServerSocket);
        }

        /// <summary>
        /// BeginReceiveCallback is an async callback method that gets called when the server receives some data from a client socket after calling the async BeginReceive method.
        /// </summary>
        /// <param name="ar"></param>
        /// <remarks></remarks>
        public void ClientMessageReceived(IAsyncResult ar)
        {
            // get the async state object from the async BeginReceive method
            SocketGlobals.AsyncReceiveState mState = (SocketGlobals.AsyncReceiveState)ar.AsyncState;
            // call EndReceive which will give us the number of bytes received
            int numBytesReceived = 0;
            try
            {
                numBytesReceived = mState.Socket.EndReceive(ar);
            }
            catch (SocketException ex)
            {
                // if we get a ConnectionReset exception, it could indicate that the client has disconnected
                if (ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    if (ClientDisconnected != null)
                    {
                        ClientDisconnected(mState.Socket);
                    }
                    return;
                }
            }
            // if we get numBytesReceived equal to zero, it could indicate that the client has disconnected
            if (numBytesReceived == 0)
            {
                if (ClientDisconnected != null)
                {
                    ClientDisconnected(mState.Socket);
                }
                return;
            }
            // determine if this is the first data received
            if (mState.ReceiveSize == 0)
            {
                // this is the first data recieved, so parse the receive size which is encoded in the first four bytes of the buffer
                mState.ReceiveSize = BitConverter.ToInt32(mState.Buffer, 0);
                // write the received bytes thus far to the packet data stream
                mState.PacketBufferStream.Write(mState.Buffer, 4, numBytesReceived - 4);
            }
            else
            {
                // write the received bytes thus far to the packet data stream
                mState.PacketBufferStream.Write(mState.Buffer, 0, numBytesReceived);
            }
            // increment the total bytes received so far on the state object
            mState.TotalBytesReceived += numBytesReceived;
            // check for the end of the packet
            // bytesReceived = Carcassonne.Library.PacketBufferSize Then
            if (mState.TotalBytesReceived < mState.ReceiveSize)
            {
                // ## STILL MORE DATA FOR THIS PACKET, CONTINUE RECEIVING ##
                // the TotalBytesReceived is less than the ReceiveSize so we need to continue receiving more data for this packet
                mState.Socket.BeginReceive(mState.Buffer, 0, SocketGlobals.gBufferSize, SocketFlags.None, new AsyncCallback(ClientMessageReceived), mState);
            }
            else
            {
                // ## FINAL DATA RECEIVED, PARSE AND PROCESS THE PACKET ##
                // the TotalBytesReceived is equal to the ReceiveSize, so we are done receiving this Packet...parse it!
                System.Runtime.Serialization.Formatters.Binary.BinaryFormatter mSerializer = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                // rewind the PacketBufferStream so we can de-serialize it
                mState.PacketBufferStream.Position = 0;
                // de-serialize the PacketBufferStream which will give us an actual Packet object
                mState.Packet = (string)mSerializer.Deserialize(mState.PacketBufferStream);
                // handle the message
                ParseReceivedClientMessage(mState.Packet, mState.Socket);

                mState.PacketBufferStream.Close();
                mState.PacketBufferStream.Dispose();

                // call BeginReceive again, so we can start receiving another packet from this client socket
                SocketGlobals.AsyncReceiveState mNextState = new SocketGlobals.AsyncReceiveState();
                mNextState.Socket = mState.Socket;
                mNextState.Socket.BeginReceive(mNextState.Buffer, 0, SocketGlobals.gBufferSize, SocketFlags.None, new AsyncCallback(ClientMessageReceived), mNextState);
            }
        }

        public void ParseReceivedClientMessage(string argCommandString, Socket argClient)
        {
            //Console.WriteLine("ParseReceivedClientMessage: " + argCommandString);

            // parse the command string
            string argCommand = null;
            string argText = null;
            argCommand = argCommandString.Substring(0, argCommandString.IndexOf(" "));
            argText = argCommandString.Remove(0, argCommand.Length + 1);

            switch (argText)
            {
                case "hi server":
                    SendMessageToClient("/say Server replied.", argClient);
                    break;
            }

            if (MessageReceived != null)
            {
                MessageReceived(argCommandString, argClient);
            }

            //' respond back to the client on certain messages
            //Select Case argMessageString
            //    Case "hi"
            //        SendMessageToClient("\say", "hi received", argClient)
            //End Select
            //RaiseEvent MessageReceived(argCommandString & " | " & argMessageString)
        }

        /// <summary>
        /// QueueMessage prepares a Message object containing our data to send and queues this Message object in the OutboundMessageQueue.
        /// </summary>
        /// <remarks></remarks>
        public void SendMessageToClient(string argCommandString, Socket argClient)
        {
            // parse the command string
            string argCommand = null;
            string argText = null;
            argCommand = argCommandString.Substring(0, argCommandString.IndexOf(" "));
            argText = argCommandString.Remove(0, argCommand.Length);

            //' create a Packet object from the passed data
            //Dim mPacket As New Dictionary(Of String, String)
            //mPacket.Add("CMD", argCommandMessage)
            //mPacket.Add("MSG", argCommandData)

            string mPacket = argCommandString;

            // serialize the Packet into a stream of bytes which is suitable for sending with the Socket
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter mSerializer = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            System.IO.MemoryStream mSerializerStream = new System.IO.MemoryStream();
            mSerializer.Serialize(mSerializerStream, mPacket);

            // get the serialized Packet bytes
            byte[] mPacketBytes = mSerializerStream.GetBuffer();

            // convert the size into a byte array
            byte[] mSizeBytes = BitConverter.GetBytes(mPacketBytes.Length + 4);

            // create the async state object which we can pass between async methods
            SocketGlobals.AsyncSendState mState = new SocketGlobals.AsyncSendState(argClient);

            // resize the BytesToSend array to fit both the mSizeBytes and the mPacketBytes
            // TODO: ReDim mState.BytesToSend(mPacketBytes.Length + mSizeBytes.Length - 1)

            // copy the mSizeBytes and mPacketBytes to the BytesToSend array
            System.Buffer.BlockCopy(mSizeBytes, 0, mState.BytesToSend, 0, mSizeBytes.Length);
            System.Buffer.BlockCopy(mPacketBytes, 0, mState.BytesToSend, mSizeBytes.Length, mPacketBytes.Length);

            // queue the Message
            argClient.BeginSend(mState.BytesToSend, mState.NextOffset(), mState.NextLength(), SocketFlags.None, new AsyncCallback(MessagePartSent), mState);
        }

        public void MessagePartSent(IAsyncResult ar)
        {
            // get the async state object which was returned by the async beginsend method
            SocketGlobals.AsyncSendState mState = (SocketGlobals.AsyncSendState)ar.AsyncState;

            try
            {
                int numBytesSent = 0;

                // call the EndSend method which will succeed or throw an error depending on if we are still connected
                numBytesSent = mState.Socket.EndSend(ar);

                // increment the total amount of bytes processed so far
                mState.Progress += numBytesSent;

                // determine if we havent' sent all the data for this Packet yet
                if (mState.NextLength() > 0)
                {
                    // we need to send more data
                    mState.Socket.BeginSend(mState.BytesToSend, mState.NextOffset(), mState.NextLength(), SocketFlags.None, new AsyncCallback(MessagePartSent), mState);
                }

                // at this point, the EndSend succeeded and we are ready to send something else!
            }
            catch (Exception ex)
            {
                Console.WriteLine("DataSent error: " + ex.Message);
            }
        }
    }
}