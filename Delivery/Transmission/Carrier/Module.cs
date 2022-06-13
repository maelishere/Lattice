using System;

namespace Lattice.Delivery.Transmission.Carrier
{
    using Bolt;

    public class Module
    {
        internal Module(Action<Segment> send, Receiving receive, Responding response)
        {
            this.send = send;
            this.receive = receive;
            this.response = response;
        }

        protected Action<Segment> send { get; }
        protected Receiving receive { get; }
        protected Responding response { get; }

        public virtual void Input(uint time, ref Reader reader)
        {
            receive?.Invoke(reader.ReadUInt(), ref reader);
        }

        public virtual void Output(uint time, ref Writer writer, Write callback)
        {
            writer.Write(time);
            callback?.Invoke(ref writer);
            send?.Invoke(writer.ToSegment());
        }

        public virtual void Update(uint time)
        {
        }
    }
}