using System;
using System.Net;
using System.Net.Sockets;

namespace Lattice.Delivery
{
    // where-allocation library (IPEndPointNonAlloc)
    // url: https://github.com/vis2k/where-allocation/tree/master/where-allocation
    public sealed class Address : IPEndPoint
    {
        private SocketAddress m_socket;

        internal Address(IPAddress address, int port) : base(address, port)
        {
            m_socket = base.Serialize();
        }

        public override SocketAddress Serialize() => m_socket;

        public override EndPoint Create(SocketAddress socketAddress)
        {
            if (socketAddress.Family != AddressFamily)
                throw new ArgumentException($"Unsupported socketAddress.AddressFamily: {socketAddress.Family}. Expected: {AddressFamily}");
            if (socketAddress.Size < 8)
                throw new ArgumentException($"Unsupported socketAddress.Size: {socketAddress.Size}. Expected: <8");

            if (socketAddress != m_socket)
            {
                m_socket = socketAddress;

                unchecked
                {
                    m_socket[0] += 1;
                    m_socket[0] -= 1;
                }

                if (m_socket.GetHashCode() == 0)
                    throw new Exception($"SocketAddress GetHashCode() is 0 after ReceiveFrom. Does the m_changed trick not work anymore?");

            }

            return this;
        }

        public override int GetHashCode() => m_socket.GetHashCode();

        public IPEndPoint DeepCopyIPEndPoint()
        {
            IPAddress ipAddress;
            if (m_socket.Family == AddressFamily.InterNetworkV6)
                ipAddress = IPAddress.IPv6Any;
            else if (m_socket.Family == AddressFamily.InterNetwork)
                ipAddress = IPAddress.Any;
            else
                throw new Exception($"Unexpected SocketAddress family: {m_socket.Family}");

            IPEndPoint placeholder = new IPEndPoint(ipAddress, 0);
            return (IPEndPoint)placeholder.Create(m_socket);
        }
    }
}
