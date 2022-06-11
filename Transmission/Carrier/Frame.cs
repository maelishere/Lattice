namespace Lattice.Transmission.Carrier
{
    internal struct Frame
    {
        public uint Sent;
        public uint Send;
        public uint Count;
        public Packet? Data;

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
