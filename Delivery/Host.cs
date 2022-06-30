using System;
using System.Net;
using System.Diagnostics;

namespace Lattice.Delivery
{
    using Bolt;
    using Transmission;

    internal sealed class Host
    {
        public Address address { get; }
        internal Connection connection { get; }

        private bool m_active;
        private readonly Stopwatch m_stopwatch;

        public uint Time => (uint)m_stopwatch.ElapsedMilliseconds;

        internal Host(IPAddress ipAddress, int port, Action<Segment> send, Receiving receive, Receiving signal, Receiving acknowledge)
        {
            address = new Address(ipAddress, port);
            connection = new Connection(send, receive, signal, acknowledge);

            m_stopwatch = new Stopwatch();
            m_stopwatch.Start();
        }

        public bool Connect() => m_active = connection.Signal(Time, false, Connect);
        public bool Disconnect() => !(m_active = !connection.Signal(Time, false, Disconnect));
        public void Input(ref Reader reader) => connection.Input(Time, ref reader);
        public void Output(Channel channel, Write callback) => connection.Output(Time, channel, callback);
        public bool Update() => connection.Update(m_active, Time, Ping);

        private static void Ping(ref Writer writer)
        {
            writer.Write((byte)Request.Ping);
            writer.Write(new byte[32]);
        }

        private static void Connect(ref Writer writer)
        {
            writer.Write((byte)Request.Connect);
            writer.Write(new byte[32]);
        }

        private static void Disconnect(ref Writer writer)
        {
            writer.Write((byte)Request.Disconnect);
            writer.Write(new byte[32]);
        }
    }
}