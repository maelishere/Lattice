using System;
using System.Net;
using System.Diagnostics;

namespace Lattice.Delivery
{
    using Bolt;
    using Transmission;

    public sealed class Host
    {
        public Address address { get; }
        internal Connection connection { get; }

        private readonly Stopwatch m_stopwatch = new Stopwatch();
        public uint Time => (uint)m_stopwatch.ElapsedMilliseconds;

        internal Host(IPAddress ipAddress, int port, Action<Segment> send, Action<Segment> receive, Action<Segment> signal, Action<Segment, uint> acknowledge)
        {
            address = new Address(ipAddress, port);
            connection = new Connection(send, receive, signal, acknowledge);

            m_stopwatch = new Stopwatch();
            m_stopwatch.Start();
        }

        public bool Signal(bool wait, Write callback) => connection.Signal(Time, wait, callback);
        public void Input(Segment segment) => connection.Input(Time, segment);
        public void Output(Channel channel, Write callback) => connection.Output(Time, channel, callback);
        public bool Update(Write callback) => connection.Update(Time, callback);


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