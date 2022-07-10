﻿using System;
using System.Collections.Concurrent;

namespace Lattice.Delivery.Transmission.Carrier
{
    using Bolt;

    // Sliding Window Protocol
    // Header: 19 bytes (including 1 byte: channel from connection)
    public class Lancet : Module
    {
        const int SIZE = 16;
        const int RESEND = 255;

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

        private byte m_next/*queue*/;
        private ulong m_sequence = 0/*packet order*/;

        private Frame[] m_sending;
        private ConcurrentQueue<Segment>[] m_waiting;

        private ulong m_marker = 0/*the packing we are waiting for*/;
        private ConcurrentDictionary<ulong, Prompt> m_received;

        internal Lancet(Action<Segment> send, Receiving receive, Responding response) : base(send, receive, response)
        {
            m_sending = new Frame[SIZE];
            m_waiting = new ConcurrentQueue<Segment>[SIZE];
            m_received = new ConcurrentDictionary<ulong, Prompt>();
            for (int i = 0; i < SIZE; i++)
            {
                m_sending[i] = new Frame();
                m_waiting[i] = new ConcurrentQueue<Segment>();
            }
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

                        // need to make sure it wasn't any previous or duplicate frame at the position
                        if (seq > m_sending[header.serial].Push.Count)
                        {
                            if (header.time > m_sending[header.serial].Push.Time)
                            {
                                ulong order = reader.ReadULong();
                                m_received.TryAdd(order, new Prompt(header.time, reader));

                               /* Log.Print($"Frame {header.serial} aquired {seq}");*/
                                m_sending[header.serial].Push.Count = seq;
                                m_sending[header.serial].Push.Time = header.time;
                            }
                        }
                    }
                    break;
                case Command.Ack:
                    {
                        // need to make sure it wasn't for any previous or duplicate frame
                        if (seq > m_sending[header.serial].Ack.Count)
                        {
                            if (header.time > m_sending[header.serial].Ack.Time)
                            {
                                Log.Lost?.Invoke(m_sending[header.serial].Loss);
                                m_sending[header.serial].Reset();
                                /*Log.Debug($"Frame {header.serial} acknowledged");*/

                                m_sending[header.serial].Ack.Count = seq;
                                m_sending[header.serial].Ack.Time = header.time;
                            }
                        }
                    }
                    break;
            }

            // release
            while (m_received.ContainsKey(m_marker))
            {
                if (m_received.TryRemove(m_marker, out Prompt prompt))
                {
                    /*Log.Print($"Releasing Frame {m_marker}");*/
                    Reader other = prompt.reader;
                    receive(prompt.time, ref other);
                    m_marker++;
                }
            }
        }

        public override void Output(uint time, ref Writer writer, Write callback)
        {
            m_sending[m_next].Seq++;

            writer.WriteHeader(Command.Push, m_next, time);
            writer.Write(m_sending[m_next].Seq);
            writer.Write(m_sequence);

            callback?.Invoke(ref writer);

            m_waiting[m_next].Enqueue(writer.ToSegment());

            /*Log.Debug($"Queued for Frame {m_next}, Waiting {m_waiting[m_next].Count}");*/
            m_next = Increment(m_next);
            m_sequence++;
        }

        public override void Update(uint time)
        {
            for (byte i = 0; i < SIZE; i++)
            {
                if (m_sending[i].Data.HasValue)
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
                else if (m_waiting[i].Count > 0)
                {
                    if (m_waiting[i].TryDequeue(out Segment segment))
                    {
                        m_sending[i].Reset();
                        m_sending[i].Data = segment;
                        /*Log.Print($"Sending Frame {i}");*/
                    }
                }
            }
        }

        internal static byte Increment(int current) => (byte)(current < SIZE - 1 ? current + 1 : 0);
    }
}
