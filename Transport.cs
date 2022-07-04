using System;
using System.Net;
using System.Net.Sockets;

namespace Lattice
{
    using Bolt;

    public abstract class Transport
    {
        protected readonly Socket m_socket;

        internal Transport(Mode mode)
        {
            switch (mode)
            {
                case Mode.Dual:
                    m_socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
                    break;

                case Mode.IPV6:
                    m_socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                    break;

                case Mode.IPV4:
                default:
                    m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    break;
            }

            m_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        public void Close()
        {
            m_socket.Close();
        }

        internal void Configure(int receive, int send)
        {
            m_socket.ReceiveBufferSize = receive;
            m_socket.SendBufferSize = send;
        }

        internal void Configure(int buffer) => Configure(buffer, buffer);

        protected bool SendTo(Segment segment, EndPoint remote)
        {
            try
            {
                m_socket.SendTo(segment.Array, segment.Offset, segment.Count, SocketFlags.None, remote);
            }
            catch (SocketException e)
            {
                // for mac os
                Log.Warning($"[{e.SocketErrorCode}] {e.Message}");
                return false;
            }
            return true;
        }

        protected void ReceiveFrom(ref EndPoint remote, Action<Segment> callback, Action exception)
        {
            while (m_socket.Poll(0, SelectMode.SelectRead))
            {
                try
                {
                    byte[] buffer = new byte[Buffer.MaxLength];
                    int size = m_socket.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref remote);
                    callback?.Invoke(new Segment(buffer, 0, size));
                }
                catch (SocketException e)
                {
                    Log.Warning($"[{e.SocketErrorCode}] {e.Message}");
                    exception?.Invoke();
                }
            }
        }

        internal static IPAddress Any(Mode mode)
        {
            switch (mode)
            {
                case Mode.Dual:
                case Mode.IPV6:
                    return IPAddress.IPv6Any;
                case Mode.IPV4:
                default:
                    return IPAddress.Any;
            }
        }

        internal static IPEndPoint Any(Mode mode, int port)
        {
            return new IPEndPoint(Any(mode), port);
        }
    }
}
