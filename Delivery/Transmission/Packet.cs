using System;

namespace Lattice.Delivery.Transmission
{
    using Bolt;

    public struct Packet
    {
        public Packet(Segment segment)
        {
            Segment = segment;
        }

        public Segment Segment { get; }
        public Channel Channel => (Channel)Segment[0];
        public Prompt Prompt => (Prompt)Segment[1];
        public Command Command => (Command)Segment[2];
        public uint Time => BitConverter.ToUInt32(Segment.Array, Segment.GetRelativePosition(3));
        public byte Serial => Segment[7];
        public Segment Slice => Segment.Slice(8);
    }
}
