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

        internal Host(IPAddress ipAddress, int port, Action<Segment> send, Receiving receive, Receiving signal, Receiving acknowledge)
        {
            address = new Address(ipAddress, port);
            connection = new Connection(send, receive, signal, acknowledge);

            m_stopwatch = new Stopwatch();
            m_stopwatch.Start();
        }

        public bool Connect() => connection.Signal(Time, false, Connect);
        public bool Disconnect() => connection.Signal(Time, false, Disconnect);
        public void Input(ref Reader reader) => connection.Input(Time, ref reader);
        public void Output(Channel channel, Write callback) => connection.Output(Time, channel, callback);
        public bool Update() => connection.Update(Time, Ping);

        private static void Ping(ref Writer writer)
        {
            writer.Write((byte)Sync.Ping);
        }

        private static void Connect(ref Writer writer)
        {
            writer.Write((byte)Sync.Connect);
        }

        private static void Disconnect(ref Writer writer)
        {
            writer.Write((byte)Sync.Disconnect);
        }
    }
}