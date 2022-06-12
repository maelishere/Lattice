using System;


namespace Lattice.Delivery.Transmission.Carrier
{
    using Bolt;

    public partial class Module
    {
        internal Module(Action<Segment> send, Action<Segment> receive)
        {
            this.send = send;
            this.receive = receive;
        }

        protected Action<Segment> send { get; }
        protected Action<Segment> receive { get; }

        public virtual void Input(uint time, Packet packet)
        {
            receive?.Invoke(packet.Slice);
        }

        public virtual void Output(uint time, ref Writer writer, Write callback)
        {
            writer.Write((byte)Command.Push);
            writer.Write(time);
            writer.Write((byte)0);
            callback?.Invoke(ref writer);
            send?.Invoke(writer.ToSegment());
        }

        public virtual void Update(uint time)
        {
        }
    }
}