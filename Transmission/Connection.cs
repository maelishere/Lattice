using System;

namespace Lattice.Transmission
{
    using Lattice.Transmission.Carrier;

    public sealed class Connection
    {
        const int MTU = 1024;
        const int INTERVAL = 1000; // 1 sec
        const int TIMEOUT = 10000; // 10 secs

        private Writer m_buffer;

        private Bit m_status;
        private Module m_direct;
        private Window m_irregular;
        private Lancet m_orderded;

        private uint m_shaked = 0;
        private uint m_recieved = 0;

        internal Connection(Action<Segment> send, Action<Segment> receive, Action<Segment> signal, Action<Segment, uint> acknowledge)
        {
            m_buffer = new Writer(MTU);
            m_status = new Bit(send, signal, acknowledge);
            m_direct = new Module(send, receive);
            m_irregular = new Window(send, receive);
            m_orderded = new Lancet(send, receive);
        }

        public bool Signal(uint time, bool wait, Write callback)
        {
            if (!m_status.Sending && wait)
            {
                m_buffer.Reset();
                m_buffer.Write((byte)Channel.None);
                m_buffer.Write((byte)Prompt.Shake);
                m_status.Output(time, ref m_buffer, callback);
                return true;
            }
            return false;
        }

        public void Input(uint time, Segment segment)
        {
            m_recieved = time;
            Packet packet = new Packet(segment);
            switch (packet.Channel)
            {
                case Channel.None:
                    m_status.Input(time, packet);
                    break;
                case Channel.Ordered:
                    m_orderded.Input(time, packet);
                    break;
                case Channel.Irregular:
                    m_irregular.Input(time, packet);
                    break;
                case Channel.Direct:
                    m_direct.Input(time, packet);
                    break;
                default:
                    Log.Error("Connection input from channel that does not exist");
                    break;
            }
        }

        public void Output(uint time, Channel channel, Write callback)
        {
            m_buffer.Reset();
            m_buffer.Write((byte)channel);
            m_buffer.Write((byte)Prompt.Accept);
            switch (channel)
            {
                case Channel.Ordered:
                    m_orderded.Output(time, ref m_buffer, callback);
                    break;
                case Channel.Irregular:
                    m_irregular.Output(time, ref m_buffer, callback);
                    break;
                case Channel.Direct:
                    m_direct.Output(time, ref m_buffer, callback);
                    break;
                default:
                    throw new ArgumentException("Connection channel does not exist or not allowed");
            }
        }

        public bool Update(uint time, Write callback)
        {
            m_status.Update(time);
            /*m_orderded.Update(m_time);
            m_irregular.Update(m_time);*/

            // if receive hasn't been called in a while it will timeout
            if (time > m_recieved + TIMEOUT)
            {
                // lost connection | Connection Timeout
                return false;
            }

            // sends a ping every interval given it has received the last ping
            if (time > m_shaked + INTERVAL)
            {
                // Send Ping
                if (Signal(time, true, callback))
                {
                    m_shaked = time;
                }
            }
            return true;
        }
    }
}
