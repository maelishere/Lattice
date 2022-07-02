using System;
using System.Collections.Concurrent;

namespace Lattice.Delivery.Transmission.Carrier
{
    using Bolt;

    public class Lancet : Module
    {
        const int SIZE = 16;
        const int RESEND = 350;

        public struct Prompt
        {
            public uint time { get; }
            public Reader reader { get; }

            public Prompt(uint time, Reader reader) : this()
            {
                this.time = time;
                this.reader = reader;
            }
        }

        private Frame[] m_sending;
        private Prompt?[] m_received;
        private Mask m_local, m_remote;
        private ConcurrentQueue<Segment>[] m_waiting;

        private byte m_here /*local marker*/, m_there /*remote marker*/, m_next/*queue order sequencer*/;

        internal Lancet(Action<Segment> send, Receiving receive, Responding response) : base(send, receive, response)
        {
            m_sending = new Frame[SIZE];
            m_received = new Prompt?[SIZE];
            m_waiting = new ConcurrentQueue<Segment>[SIZE];
            for (int i = 0; i < SIZE; i++)
            {
                m_waiting[i] = new ConcurrentQueue<Segment>();
            }
        }

        private void Release()
        {
            while (m_received[m_here].HasValue)
            {
                /*Log.Debug($"Releasing Frame {i}");*/
                Reader reader = m_received[m_here].Value.reader;
                receive(m_received[m_here].Value.time, ref reader);

                m_received[m_here] = null;
                m_local[m_here] = false;
                m_here = Increment(m_here);
            }
        }

        public override void Input(uint time, ref Reader reader)
        {
            Header header = reader.ReadHeader();
            uint seq = reader.ReadUInt();
            /*Log.Debug($"Input Frame {header.command}");*/
            switch (header.command)
            {
                case Command.Push:
                    {
                        if (!m_remote[header.serial])
                        {
                            send(response(
                                (ref Writer writer) =>
                                {
                                    writer.WriteHeader(Command.Ack, header.serial, header.time);
                                    writer.Write(seq);
                                    writer.Write(m_local.Value);
                                    writer.Write(m_here);
                                }));
                        }

                        // ? need to make sure it wasn't any previous or duplicate frame at the position
                        if (seq >= m_sending[header.serial].Push.Count)
                        {
                            if (header.time > m_sending[header.serial].Push.Time)
                            {
                                m_local[header.serial] = true;
                                m_received[header.serial] = new Prompt(header.time, reader);
                                /*Log.Debug($"Frame {header.serial} aquired");*/

                                m_sending[header.serial].Push.Count = seq;
                                m_sending[header.serial].Push.Time = header.time;
                            }
                        }
                    }
                    break;
                case Command.Ack:
                    {
                        // ? need to make sure it wasn't for any previous or duplicate frame
                        if (seq >= m_sending[header.serial].Ack.Count)
                        {
                            if (header.time > m_sending[header.serial].Ack.Time)
                            {
                                m_sending[header.serial].Reset();
                                m_remote.Value = reader.ReadUShort();
                                m_there = reader.Read();
                                /*Log.Debug($"Frame {header.serial} acknowledged | There: Remote {m_remote} Marker {m_there}");*/

                                m_sending[header.serial].Ack.Count = seq;
                                m_sending[header.serial].Ack.Time = header.time;
                            }
                        }
                    }
                    break;
            }
            Release();
        }

        public override void Output(uint time, ref Writer writer, Write callback)
        {
            writer.WriteHeader(Command.Push, m_next, time);
            writer.Write(m_sending[m_next].Seq);
            callback?.Invoke(ref writer);
            m_waiting[m_next].Enqueue(writer.ToSegment());
            /*Log.Debug($"Queued for Frame {m_next}, Waiting {m_waiting[m_next].Count}");*/
            m_sending[m_next].Seq++;
            m_next = Increment(m_next);
        }

        public override void Update(uint time)
        {
            Release();

            for (byte i = 0; i < SIZE; i++)
            {
                if (m_sending[i].Data.HasValue)
                {
                    if (!m_remote[i])
                    {
                        if (m_sending[i].Send < time)
                        {
                            send(m_sending[i].Data.Value);

                            if (!m_sending[i].Post(time + RESEND))
                            {
                                Log.Loss?.Invoke();
                            }
                        }
                    }
                }
                else if (Within(i, m_there, 8) && m_waiting[i].Count > 0)
                {
                    do
                    {
                        if (m_waiting[i].TryDequeue(out Segment segment))
                        {
                            m_sending[i].Reset();
                            m_sending[i].Data = segment;
                            /*Log.Debug($"Sending Frame {i}");*/
                        }
                    }
                    while (!m_sending[i].Data.HasValue);
                }
            }
        }

        internal static bool Within(int current, int marker, int allowance)
        {
            int mark = marker + allowance;
            if (mark > SIZE) mark = ((mark % SIZE) * 2);
            return (mark < marker ? current <= mark : false) || current >= marker;
        }

        internal static byte Increment(int current) => (byte)(current < SIZE - 1 ? current + 1 : 0);
    }
}
