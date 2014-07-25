using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace csharpsocketsclient
{

    using System;
    using System.Windows.Forms;
    using System.Messaging;
    using System.Net;
    using System.Net.Sockets;

    public class SocketClient
    {

        int cServerPort = 9898;
        string cServerAddress = "localhost";

        System.Net.Sockets.Socket cClientSocket;
        SocketGlobals.MessageQueue cSendQueue = new SocketGlobals.MessageQueue();

        public event MessageSentToServerEventHandler MessageSentToServer;
        public delegate void MessageSentToServerEventHandler(string argCommandString);

        public void ConnectToServer()
	    {
		    // create the TcpListener which will listen for and accept new client connections asynchronously
		    cClientSocket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		    // convert the server address and port into an ipendpoint
		    IPAddress[] mHostAddresses = Dns.GetHostAddresses(cServerAddress);
		    IPEndPoint mEndPoint = null;
            foreach (IPAddress mHostAddress in mHostAddresses)
            {
			    if (mHostAddress.AddressFamily == AddressFamily.InterNetwork) {
				    mEndPoint = new IPEndPoint(mHostAddress, cServerPort);
			    }
		    }
            
		    // connect to server async
		    try {
			    cClientSocket.BeginConnect(mEndPoint, new AsyncCallback(ConnectToServerCompleted), new SocketGlobals.AsyncSendState(cClientSocket));
		    } catch (Exception ex) {
			    MessageBox.Show("ConnectToServer error: " + ex.Message);
		    }
	    }

        public void DisconnectFromServer()
        {
            cClientSocket.Disconnect(false);
        }

        /// <summary>
        /// Fires right when a client is connected to the server.
        /// </summary>
        /// <param name="ar"></param>
        /// <remarks></remarks>
        public void ConnectToServerCompleted(IAsyncResult ar)
        {
            // get the async state object which was returned by the async beginconnect method
            SocketGlobals.AsyncSendState mState = (SocketGlobals.AsyncSendState) ar.AsyncState;

            // end the async connection request so we can check if we are connected to the server
            try
            {
                // call the EndConnect method which will succeed or throw an error depending on the result of the connection
                mState.Socket.EndConnect(ar);
                // at this point, the EndConnect succeeded and we are connected to the server!
                // send a welcome message
                SendMessageToServer("/say What? My name is...");
                // start waiting for messages from the server
                SocketGlobals.AsyncReceiveState mReceiveState = new SocketGlobals.AsyncReceiveState();
                mReceiveState.Socket = mState.Socket;
                mReceiveState.Socket.BeginReceive(mReceiveState.Buffer, 0, SocketGlobals.gBufferSize, SocketFlags.None, new AsyncCallback(ServerMessageReceived), mReceiveState);
            }
            catch (Exception ex)
            {
                // at this point, the EndConnect failed and we are NOT connected to the server!
                MessageBox.Show("Connect error: " + ex.Message);
            }
        }

        public void ServerMessageReceived(IAsyncResult ar)
        {
            // get the async state object from the async BeginReceive method
            SocketGlobals.AsyncReceiveState mState = (SocketGlobals.AsyncReceiveState) ar.AsyncState;
            // call EndReceive which will give us the number of bytes received
            int numBytesReceived = 0;
            numBytesReceived = mState.Socket.EndReceive(ar);
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
                mState.Socket.BeginReceive(mState.Buffer, 0, SocketGlobals.gBufferSize, SocketFlags.None, new AsyncCallback(ServerMessageReceived), mState);
            }
            else
            {
                // ## FINAL DATA RECEIVED, PARSE AND PROCESS THE PACKET ##
                // the TotalBytesReceived is equal to the ReceiveSize, so we are done receiving this Packet...parse it!
                System.Runtime.Serialization.Formatters.Binary.BinaryFormatter mSerializer = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                // rewind the PacketBufferStream so we can de-serialize it
                mState.PacketBufferStream.Position = 0;
                // de-serialize the PacketBufferStream which will give us an actual Packet object
                mState.Packet = (string) mSerializer.Deserialize(mState.PacketBufferStream);
                // parse the complete message that was received from the server
                ParseReceivedServerMessage(mState.Packet, mState.Socket);
                // call BeginReceive again, so we can start receiving another packet from this client socket
                SocketGlobals.AsyncReceiveState mNextState = new SocketGlobals.AsyncReceiveState();
                mNextState.Socket = mState.Socket;
                mNextState.Socket.BeginReceive(mNextState.Buffer, 0, SocketGlobals.gBufferSize, SocketFlags.None, new AsyncCallback(ServerMessageReceived), mNextState);
            }
        }

        public void ParseReceivedServerMessage(string argCommandString, System.Net.Sockets.Socket argClient)
        {
            Console.WriteLine(argCommandString);
            //Select Case argDat
            //    Case "hi"
            //        Send("hi", argClient)
            //End Select
            //RaiseEvent MessageReceived(argMsg & " | " & argDat)
        }

        public void SendMessageToServer(string argCommandString)
        {
            // create a Packet object from the passed data; this packet can be any object type because we use serialization!
            //Dim mPacket As New Dictionary(Of String, String)
            //mPacket.Add("CMD", argCommandString)
            //mPacket.Add("MSG", argMessageString)
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
            SocketGlobals.AsyncSendState mState = new SocketGlobals.AsyncSendState(cClientSocket);

            // resize the BytesToSend array to fit both the mSizeBytes and the mPacketBytes
            // ERROR: Not supported in C#: ReDimStatement
            Array.Resize(ref mState.BytesToSend, mPacketBytes.Length + mSizeBytes.Length);

            // copy the mSizeBytes and mPacketBytes to the BytesToSend array
            System.Buffer.BlockCopy(mSizeBytes, 0, mState.BytesToSend, 0, mSizeBytes.Length);
            System.Buffer.BlockCopy(mPacketBytes, 0, mState.BytesToSend, mSizeBytes.Length, mPacketBytes.Length);

            cClientSocket.BeginSend(mState.BytesToSend, mState.NextOffset(), mState.NextLength(), SocketFlags.None, new AsyncCallback(MessagePartSent), mState);

        }

        ///' <summary>
        ///' QueueMessage prepares a Message object containing our data to send and queues this Message object in the OutboundMessageQueue.
        ///' </summary>
        ///' <param name="argCommandMessage"></param>
        ///' <param name="argCommandData"></param>
        ///' <remarks></remarks>
        //Sub QueueMessage(ByVal argCommandMessage As String, ByVal argCommandData As Object)

        //End Sub

        private void cSendQueue_MessageQueued()
        {
            // when a message is queued, we need to check whether or not we are currently processing the queue before allowing the top item in the queue to start sending
            if (cSendQueue.Processing == false)
            {
                // process the top message in the queue, which in turn will process all other messages until the queue is empty
                SocketGlobals.AsyncSendState mState = (SocketGlobals.AsyncSendState) cSendQueue.Messages.Dequeue();
                // we must send the correct number of bytes, which must not be greater than the remaining bytes
                cClientSocket.BeginSend(mState.BytesToSend, mState.NextOffset(), mState.NextLength(), SocketFlags.None, new AsyncCallback(MessagePartSent), mState);
            }
        }

        public void MessagePartSent(IAsyncResult ar)
        {
            // get the async state object which was returned by the async beginsend method
            SocketGlobals.AsyncSendState mState = (SocketGlobals.AsyncSendState) ar.AsyncState;
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
                // TODO: use the queue to determine what message was sent and show it in the local chat buffer
                //RaiseEvent MessageSentToServer()
            }
            catch (Exception ex)
            {
                MessageBox.Show("DataSent error: " + ex.Message);
            }
        }

    }
}
