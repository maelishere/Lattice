using System;

namespace Lattice.Delivery.Transmission.Carrier
{
    using Bolt;

    // Mix of Bit with Stop and Wait Protocol
    // Header: 7 bytes (including 1 byte: channel from connection)
    public class Bit : Module
    {
        const int RESEND = 300;

        private uint m_sent;
        private Frame m_frame;
        private byte m_serial, m_last;

        public bool Sending => m_frame.Data.HasValue;
        private Receiving acknowledge { get; }

        internal Bit(Action<Segment> send, Receiving receive, Receiving acknowledge, Responding response) : base(send, receive, response)
        {
            this.acknowledge = acknowledge; 
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
                        Segment segment = reader.Peek(reader.Length - reader.Current);
                        send(response(
                            (ref Writer writer) =>
                            {
                                writer.WriteHeader(Command.Ack, header.serial, header.time);
                                writer.Write(segment);
                            }));

                        // need to make sure it wasn't any previous or duplicate frame at the position
                        if (header.serial != m_last)
                        {
                            if (header.time > m_frame.Push.Time)
                            {
                                receive?.Invoke(header.time - 1, ref reader);

                                m_last = header.serial;
                                /*m_frame.Push.Count = header.serial;*/
                                m_frame.Push.Time = header.time;
                            }
                        }
                    }
                    break;
                case Command.Ack:
                    {
                        // need to make sure it wasn't for any previous or duplicate frame
                        if (header.serial == m_serial)
                        {
                            if (header.time > m_frame.Ack.Time)
                            {
                                // m_sent : time the last push was sent
                                acknowledge(time - m_sent, ref reader);
                                Log.Lost?.Invoke(m_frame.Loss);
                                m_frame.Reset();

                                /*m_frame.Ack.Count = header.serial;*/
                                m_frame.Ack.Time = header.time;
                            }
                        }
                    }
                    break;
            }
        }

        public override void Output(uint time, ref Writer writer, Write callback)
        {
            m_serial = (byte)(m_serial < 255 ? m_serial + 1 : 0);
            writer.WriteHeader(Command.Push, m_serial, time + 1); /*connect shouldn't be sent at time 0*/
            callback(ref writer);

            m_frame.Reset();
            m_frame.Data = writer.ToSegment();
        }

        public override void Update(uint time)
        {
            if (m_frame.Data.HasValue)
            {
                if (m_frame.Send < time)
                {
                    send(m_frame.Data.Value);
                    m_sent = time;

                    if (!m_frame.Post(time + RESEND))
                    {
                        Log.Loss?.Invoke();
                    }
                }
            }
        }
    }
}