using System;

public static class SocketGlobals
{
    public static int gBufferSize = 1024;

    [Serializable]
    public class AsyncReceiveState
    {
        public System.Net.Sockets.Socket Socket;
        public byte[] Buffer = new byte[gBufferSize];

        // a buffer for appending received data to build the packet
        public System.IO.MemoryStream PacketBufferStream = new System.IO.MemoryStream();

        public string Packet;

        // the size (in bytes) of the Packet
        public int ReceiveSize;

        // the total bytes received for the Packet so far
        public int TotalBytesReceived;

        [NonSerialized]
        private bool disposed;

        ~AsyncReceiveState()
        {
            Console.WriteLine("Calling Destructor");
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            //GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                PacketBufferStream.Close();
                PacketBufferStream.Dispose();
            }

            // Free any unmanaged objects here.
            //
            disposed = true;
        }
    }

    public class AsyncSendState
    {
        public System.Net.Sockets.Socket Socket;

        //Public Buffer(Carcassonne.Library.PacketBufferSize - 1) As Byte ' a buffer to store the currently received chunk of bytes
        public byte[] BytesToSend;

        public int Progress;

        public AsyncSendState(System.Net.Sockets.Socket argSocket)
        {
            this.Socket = argSocket;
        }

        public int NextOffset()
        {
            return Progress;
        }

        public int NextLength()
        {
            if (BytesToSend.Length - Progress > gBufferSize)
            {
                return gBufferSize;
            }
            else
            {
                return BytesToSend.Length - Progress;
            }
        }
    }

    public class MessageQueue
    {
        public System.Collections.Queue Messages = new System.Collections.Queue();
        public bool Processing;

        public event MessageQueuedEventHandler MessageQueued;

        public delegate void MessageQueuedEventHandler();

        public void Add(AsyncSendState argState)
        {
            this.Messages.Enqueue(argState);
            if (MessageQueued != null)
            {
                MessageQueued();
            }
        }
    }
}