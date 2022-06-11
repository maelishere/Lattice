using System;
using System.Collections.Generic;

namespace Lattice.Transmission.Carrier
{
    public class Lancet : Module
    {
        const int SIZE = 16;
        const int RESEND = 350;

        private Frame[] m_sending;
        private Packet?[] m_received;
        private byte m_current, m_next;
        private Mask m_local, m_remote;
        private Queue<Packet> m_waiting;

        internal Lancet(Action<Segment> send, Action<Segment> receive) : base(send, receive)
        {
            m_sending = new Frame[SIZE];
            m_received = new Packet?[SIZE];
            m_waiting = new Queue<Packet>();
        }

        public override void Input(uint time, Packet packet)
        {
            switch (packet.Command)
            {
                case Command.Push:
                    if (!m_remote[packet.Serial])
                    {
                        Writer writer = new Writer(12);
                        writer.Write((byte)packet.Channel);
                        writer.Write((byte)packet.Prompt);
                        writer.Write((byte)Command.Ack);
                        writer.Write(packet.Time);
                        writer.Write(packet.Serial);
                        writer.Write(m_local.Value);

                        send?.Invoke(writer.ToSegment());
                    }

                    // ? need to make sure it wasn't the previous frame at the position
                    m_local[packet.Serial] = true;
                    m_received[packet.Serial] = packet;
                    break;
                case Command.Ack:
                    // ? need to make sure it wasn't from the previous frame
                    m_remote.Value = BitConverter.ToUInt16(packet.Segment.Array, 8);
                    break;
            }
        }

        public override void Output(uint time, ref Writer writer, Write callback)
        {
            writer.Write((byte)Command.Push);
            writer.Write(time);
            writer.Write(m_next);
            callback?.Invoke(ref writer);
            m_waiting.Enqueue(new Packet(writer.ToSegment()));
            m_next = Mask.IncLoop(m_next);
        }

        public override void Update(uint time)
        {
            for (int i = 0; i < SIZE; i++)
            {
                if (m_sending[i].Data.HasValue && !m_remote[i])
                {
                    if (m_sending[i].Send < time)
                    {
                        send?.Invoke(m_sending[i].Data.Value.Segment);
                        m_sending[i].Post(time + RESEND);
                    }
                }
                else if ((i < m_current || i == 0) && m_waiting.Count > 0)
                {
                    if (i == m_waiting.Peek().Serial)
                    {
                        Packet packet = m_waiting.Dequeue();
                        m_sending[i].Reset();
                        m_sending[i].Send = time;
                        m_sending[i].Data = packet;
                        send?.Invoke(m_sending[i].Data.Value.Segment);
                        m_sending[i].Post(time + RESEND);
                    }
                }

                if (i == m_current && m_received[i].HasValue)
                {
                    receive?.Invoke(m_received[i].Value.Segment);
                    m_received[i] = null;
                    m_local[i] = false;

                    m_current = Mask.IncLoop(m_current);
                }
            }
        }
    }
}
