using System;

namespace Lattice.Transmission.Carrier
{
    public class Bit : Module
    {
        const int RESEND = 300;

        private Frame m_frame;
        private byte m_serial;

        public bool Sending => m_frame.Data.HasValue;
        private Action<Segment, uint> acknowledge { get; }

        internal Bit(Action<Segment> send, Action<Segment> receive, Action<Segment, uint> acknowledge) : base(send, receive)
        {
            this.acknowledge = acknowledge;
            m_frame = new Frame();
            m_frame.Reset();
        }

        public override void Input(uint time, Packet packet)
        {
            switch (packet.Command)
            {
                case Command.Push:
                    Segment ack = packet.Segment.Slice(0, 9);
                    ack[2] = (byte)Command.Ack;
                    send?.Invoke(ack);

                    receive?.Invoke(packet.Slice);
                    break;
                case Command.Ack:
                    if (packet.Serial == m_serial)
                    {
                        // packet.Time : time it was proccessed / time the first push was sent
                        // time : time the acknowledge was recived relative to last update
                        acknowledge?.Invoke(packet.Slice, time - packet.Time);
                        m_frame.Reset();
                    }
                    break;
            }
        }

        public override void Output(uint time, ref Writer writer, Write callback)
        {
            m_serial = (byte)(m_serial < 255 ? m_serial + 1 : 0);
            writer.Write((byte)Command.Push);
            writer.Write(time);
            writer.Write(m_serial);
            callback?.Invoke(ref writer);

            m_frame.Reset();
            /*m_frame.Send = time;*/
            m_frame.Data = new Packet(writer.ToSegment());
           /* send?.Invoke(m_frame.Data.Value.Segment);
            m_frame.Post(time + RESEND);*/
        }

        public override void Update(uint time)
        {
            if (m_frame.Data.HasValue && m_frame.Send < time)
            {
                send?.Invoke(m_frame.Data.Value.Segment);
                m_frame.Post(time + RESEND);
            }
        }
    }
}
