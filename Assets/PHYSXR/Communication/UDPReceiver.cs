using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace PHYSXR.Communication
{
    // Plain C# UDP listener - deliberately not a MonoBehaviour, since it has
    // no per-frame Unity lifecycle needs and must be testable without a
    // scene or a live ESP32 connection.
    //
    // Contains no semantic/decision logic: it only ever hands raw bytes
    // onward via PacketReceived. Validation and parsing happen elsewhere.
    public class UDPReceiver
    {
        public event Action<byte[]> PacketReceived;

        private readonly int port;
        private UdpClient client;
        private Thread listenThread;
        private volatile bool running;

        public UDPReceiver(int port)
        {
            this.port = port;
        }

        public bool IsRunning => running;

        public void Start()
        {
            if (running)
                return;

            client = new UdpClient(port);
            running = true;

            listenThread = new Thread(ListenLoop) { IsBackground = true };
            listenThread.Start();
        }

        public void Stop()
        {
            running = false;
            client?.Close();
            client = null;
        }

        // Exposed so callers (and tests) can push bytes through the exact
        // same path the socket loop uses, without opening a real socket -
        // this is what makes the receiver testable without an ESP32.
        public void Dispatch(byte[] data)
        {
            PacketReceived?.Invoke(data);
        }

        private void ListenLoop()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (running)
            {
                byte[] data;
                try
                {
                    data = client.Receive(ref remoteEndPoint);
                }
                catch (SocketException)
                {
                    break; // socket closed via Stop()
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                Dispatch(data);
            }
        }
    }
}
