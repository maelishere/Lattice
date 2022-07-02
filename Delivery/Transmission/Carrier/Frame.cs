﻿namespace Lattice.Delivery.Transmission.Carrier
{
    using Bolt;

    internal struct Frame
    {
        public int Loss;
        public uint Sent;
        public uint Send;
        public uint Count;

        public Segment? Data;

        public Memo Ack;
        public Memo Push;
        public uint Seq;

        public void Reset()
        {
            Loss = -1;
            Sent = 0;
            Send = 0;
            Count = 0;
            Data = null;
        }

        public bool Post(uint time)
        {
            Sent = Send;
            Send = time;
            Loss++;
            Count++;
            return Loss < 1;
        }
    }
}
