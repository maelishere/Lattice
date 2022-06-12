using System;
using System.Collections.Generic;


namespace Lattice.Delivery.Transmission.Carrier
{
    using Bolt;

    public class Window : Module
    {
        const int SIZE = 32;
        const int RESEND = 400;

        private int m_level;
        private Frame[] m_frames;
        /*private uint[] m_recieved;*/
        private Queue<Packet>[] m_waiting;

        internal Window(Action<Segment> send, Action<Segment> receive) : base(send, receive)
        {
            m_frames = new Frame[SIZE];
            /*m_recieved = new uint[SIZE];*/
            m_waiting = new Queue<Packet>[SIZE];
            for (int i = 0; i < SIZE; i++)
            {
                m_waiting[i] = new Queue<Packet>();
            }
        }

        public override void Input(uint time, Packet packet)
        {
            switch (packet.Command)
            {
                case Command.Push:
                    Segment ack = packet.Segment.Slice(0, 9);
                    ack[2] = (byte)Command.Ack;
                    send?.Invoke(ack);

                    // ? need to make sure it wasn't from the previous frame at the position
                    receive?.Invoke(packet.Slice);
                    break;
                case Command.Ack:
                    if (m_frames[packet.Serial].Data.HasValue)
                    {
                        // make sure it wasn't from the previous frame
                        if (m_frames[packet.Serial].Data.Value.Time == packet.Time)
                        {
                            m_frames[packet.Serial].Reset();
                        }
                    }
                    break;
            }
        }

        public override void Output(uint time, ref Writer writer, Write callback)
        {
            for (int i = 0; i < SIZE; i++)
            {
                if (m_waiting[i].Count < m_level || i == m_waiting.Length - 1)
                {
                    writer.Write((byte)Command.Push);
                    writer.Write(time);
                    writer.Write((byte)i);
                    callback?.Invoke(ref writer);
                    Packet packet = new Packet(writer.ToSegment());

                    if (m_waiting[i].Count == 0 && !m_frames[i].Data.HasValue)
                    {
                        m_frames[i].Reset();
                        /*m_frames[i].Send = time;*/
                        m_frames[i].Data = packet;
                        /*send?.Invoke(m_frames[i].Data.Value.Segment);
                        m_frames[i].Post(time + RESEND);*/
                    }
                    else
                    {
                        m_waiting[i].Enqueue(packet);
                    }

                    break;
                }
            }
        }

        public override void Update(uint time)
        {
            int max = 0;
            for (byte i = 0; i < SIZE; i++)
            {
                int count = m_waiting[i].Count;

                if (m_frames[i].Data.HasValue)
                {
                    if (m_frames[i].Send < time)
                    {
                        send?.Invoke(m_frames[i].Data.Value.Segment);
                        m_frames[i].Post(time + RESEND);
                    }
                }
                else if (count > 0)
                {
                    m_frames[i].Reset();
                    m_frames[i].Send = time;
                    m_frames[i].Data = m_waiting[i].Dequeue();
                    send?.Invoke(m_frames[i].Data.Value.Segment);
                    m_frames[i].Post(time + RESEND);
                }

                if (count > max)
                    max = count;
            }
            m_level = max + 1;
        }
    }
}