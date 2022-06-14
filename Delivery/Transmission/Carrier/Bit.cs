using System;

namespace Lattice.Delivery.Transmission.Carrier
{
    using Bolt;

    public class Bit : Module
    {
        const int RESEND = 300;

        private Frame m_frame;
        private byte m_serial, m_last;

        public bool Sending => m_frame.Data.HasValue;
        private Receiving acknowledge { get; }

        internal Bit(Action<Segment> send, Receiving receive, Receiving acknowledge, Responding response) : base(send, receive, response)
        {
            this.acknowledge = acknowledge; 
            m_last = byte.MaxValue;
            m_frame = new Frame();
            m_frame.Reset();
        }

        public override void Input(uint time, ref Reader reader)
        {
            Header header = reader.ReadHeader();
            switch (header.command)
            {
                case Command.Push:
                    {
                        Segment segment = reader.Cut(0, reader.Length - reader.Current);
                        send(response(
                            (ref Writer writer) =>
                            {
                                writer.WriteHeader(Command.Ack, header.serial, header.time);
                                writer.Write(segment);
                            }));

                        if (header.serial != m_last)
                        {
                            receive?.Invoke(header.time, ref reader);
                            m_last = header.serial;
                        }
                    }
                    break;
                case Command.Ack:
                    {
                        if (header.serial == m_serial)
                        {
                            // packet.Time : time it was proccessed / time the first push was sent
                            // time : time the acknowledge was recived relative to last update
                            acknowledge(time - header.time, ref reader);
                            m_frame.Reset();
                        }
                    }
                    break;
            }
        }

        public override void Output(uint time, ref Writer writer, Write callback)
        {
            m_serial = (byte)(m_serial < 255 ? m_serial + 1 : 0);
            writer.WriteHeader(Command.Push, m_serial, time);
            callback(ref writer);

            m_frame.Reset();
            m_frame.Data = writer.ToSegment();
        }

        public override void Update(uint time)
        {
            if (m_frame.Data.HasValue && m_frame.Send < time)
            {
                send(m_frame.Data.Value);
                m_frame.Post(time + RESEND);
            }
        }
    }
}