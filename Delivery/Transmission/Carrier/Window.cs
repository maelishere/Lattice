using System;
using System.Collections.Generic;


namespace Lattice.Delivery.Transmission.Carrier
{
    using Bolt;

    public class Window : Module
    {
        const int SIZE = 32;
        const int RESEND = 400;

        private Frame[] m_frames;
        private Queue<Segment>[] m_waiting;

        internal Window(Action<Segment> send, Receiving receive, Responding response) : base(send, receive, response)
        {
            m_frames = new Frame[SIZE];
            m_waiting = new Queue<Segment>[SIZE];
            for (int i = 0; i < SIZE; i++)
            {
                m_waiting[i] = new Queue<Segment>();
            }
        }

        private int Level()
        {
            int max = 0;
            for (byte i = 0; i < SIZE; i++)
            {
                int count = m_waiting[i].Count;
                if (count > max)
                    max = count;
            }
            return max < 1 ? 1 : max;
        }

        public override void Input(uint time, ref Reader reader)
        {
            Header header = reader.ReadHeader();
            switch (header.command)
            {
                case Command.Push:

                    send(response(
                        (ref Writer writer) =>
                        {
                            writer.WriteHeader(Command.Ack, header.serial, header.time);
                        }));

                    // ? need to make sure it wasn't any previous or duplicate frame at the position
                    receive(header.time, ref reader);
                    break;
                case Command.Ack:
                    if (m_frames[header.serial].Data.HasValue)
                    {
                        // ? need to make sure it wasn't for any previous or duplicate frame
                        m_frames[header.serial].Reset();
                    }
                    break;
            }
        }

        public override void Output(uint time, ref Writer writer, Write callback)
        {
            int level = Level();
            for (byte i = 0; i < SIZE; i++)
            {
                if (m_waiting[i].Count < level || i == SIZE - 1)
                {
                    writer.WriteHeader(Command.Push, i, time);
                    callback?.Invoke(ref writer);
                    Segment segment = writer.ToSegment();
                    if (m_waiting[i].Count == 0 && !m_frames[i].Data.HasValue)
                    {
                        m_frames[i].Reset();
                        m_frames[i].Data = segment;
                        /*Log.Debug($"Sending Frame {i}");*/
                    }
                    else
                    {
                        m_waiting[i].Enqueue(segment);
                        /*Log.Debug($"Level {level} | Queue {i} -> Wating {m_waiting[i].Count}");*/
                    }
                    break;
                }
            }
        }

        public override void Update(uint time)
        {
            for (byte i = 0; i < SIZE; i++)
            {
                int count = m_waiting[i].Count;

                if (m_frames[i].Data.HasValue)
                {
                    if (m_frames[i].Send < time)
                    {
                        send(m_frames[i].Data.Value);
                        m_frames[i].Post(time + RESEND);
                    }
                }
                else if (count > 0) // we need to make sure remote has released the frame
                {
                    m_frames[i].Reset();
                    m_frames[i].Data = m_waiting[i].Dequeue();
                    /*Log.Debug($"Sending Frame {i}");*/
                }
            }
        }
    }
}