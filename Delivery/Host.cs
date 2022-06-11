using System;
using System.Net;

namespace Lattice.Delivery
{
    using Lattice.Transmission;

    public sealed class Host
    {
        public Address address { get; }
        internal Connection connection { get; }

        internal Host(IPAddress ipAddress, int port, Action<Segment> send, Action<Segment> receive, Action<Segment> signal, Action<Segment, uint> acknowledge)
        {
            address = new Address(ipAddress, port);
            connection = new Connection(send, receive, signal, acknowledge);
        }

        private static void Body(ref Writer writer)
        {
            writer.Write(new byte[32 - 9]);
        }

        internal static void Ping(ref Writer writer)
        {
            writer.Write((byte)Sync.Ping);
            Body(ref writer);
        }

        internal static void Connect(ref Writer writer)
        {
            writer.Write((byte)Sync.Connect);
            Body(ref writer);
        }

        internal static void Disconnect(ref Writer writer)
        {
            writer.Write((byte)Sync.Disconnect);
            Body(ref writer);
        }
    }
}