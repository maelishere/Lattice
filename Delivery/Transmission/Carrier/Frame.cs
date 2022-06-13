namespace Lattice.Delivery.Transmission.Carrier
{
    using Bolt;

    internal struct Frame
    {
        public uint Sent;
        public uint Send;
        public uint Count;
        public Segment? Data;

        /*public Memo Ack;
        public Memo Push;*/

        public void Reset()
        {
            Sent = 0;
            Send = 0;
            Count = 0;
            Data = null;
        }

        public void Post(uint time)
        {
            Sent = Send;
            Send = time;
            Count++;
        }
    }
}
