using System;
using System.Collections.Concurrent;


namespace Lattice.Delivery.Transmission.Carrier
{
    using Bolt;

    public class Window : Module
    {
        const int SIZE = 32;
        const int RESEND = 400;

        private Frame[] m_frames;
        private ConcurrentQueue<Segment>[] m_waiting;

        internal Window(Action<Segment> send, Receiving receive, Responding response) : base(send, receive, response)
        {
            m_frames = new Frame[SIZE];
            m_waiting = new ConcurrentQueue<Segment>[SIZE];
            for (int i = 0; i < SIZE; i++)
            {
                m_waiting[i] = new ConcurrentQueue<Segment>();
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
            uint seq = reader.ReadUInt();
            switch (header.command)
            {
                case Command.Push:
                    {
                        send(response(
                            (ref Writer writer) =>
                            {
                                writer.WriteHeader(Command.Ack, header.serial, header.time);
                                writer.Write(seq);
                            }));

                        // ? need to make sure it wasn't any previous or duplicate frame at the position
                        if (seq >= m_frames[header.serial].Push.Count)
                        {
                            if (header.time > m_frames[header.serial].Push.Time)
                            {
                                receive(header.time, ref reader);
                                m_frames[header.serial].Push.Count = seq;
                                m_frames[header.serial].Push.Time = header.time;
                            }
                        }
                    }
                    break;
                case Command.Ack:
                    {
                        // ? need to make sure it wasn't for any previous or duplicate frame
                        if (seq >= m_frames[header.serial].Ack.Count)
                        {
                            if (header.time > m_frames[header.serial].Ack.Time)
                            {
                                Log.Lost?.Invoke(m_frames[header.serial].Loss);
                                m_frames[header.serial].Reset();
                                m_frames[header.serial].Ack.Count = seq;
                                m_frames[header.serial].Ack.Time = header.time;
                            }
                        }
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
                    writer.Write(m_frames[i].Seq);
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
                    m_frames[i].Seq++;
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

                        if (!m_frames[i].Post(time + RESEND))
                        {
                            Log.Loss?.Invoke();
                        }
                    }
                }
                else if (count > 0) // we need to make sure remote has released the frame
                {
                    do
                    {
                        if (m_waiting[i].TryDequeue(out Segment segment))
                        {
                            m_frames[i].Reset();
                            m_frames[i].Data = segment;
                            /*Log.Debug($"Sending Frame {i}");*/
                        }
                    }
                    while (!m_frames[i].Data.HasValue);
                }
            }
        }
    }
}