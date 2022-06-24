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
        }

        /*public void Close()
        {
            m_socket.Close();
        }*/

        protected bool SendTo(Segment segment, EndPoint remote)
        {
            try
            {
                m_socket.SendTo(segment.ToArray(), remote);
            }
            catch (SocketException)
            {
                /*Log.Error($"{e.GetType()} {e.Message}");*/
                /*Log.Debug(e.StackTrace);*/
                return false;
            }
            return true;
        }

        protected bool ReceiveFrom(ref EndPoint remote, Action<Segment> callback)
        {
            while (m_socket.Poll(0, SelectMode.SelectRead))
            {
                try
                {
                    byte[] buffer = new byte[Buffer.MaxLength];
                    int size = m_socket.ReceiveFrom(buffer, ref remote);
                    callback?.Invoke(new Segment(buffer, 0, size));
                }
                catch (SocketException)
                {
                    /*Log.Error($"{e.GetType()} {e.Message}");*/
                    /*Log.Debug(e.StackTrace);*/
                    return false;
                }
            }
            return true;
        }

        internal static IPEndPoint Any(int port, Mode mode)
        {
            switch (mode)
            {
                case Mode.Dual:
                case Mode.IPV6:
                    return new IPEndPoint(IPAddress.IPv6Any, port);
                case Mode.IPV4:
                default:
                    return new IPEndPoint(IPAddress.Any, port);
            }
        }
    }
}
