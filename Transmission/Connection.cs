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

        private uint m_time = 0;
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

        public bool Signal(bool wait, Write callback)
        {
            if (!m_status.Sending && wait)
            {
                m_buffer.Reset();
                m_buffer.Write((byte)Channel.None);
                m_buffer.Write((byte)Prompt.Shake);
                m_status.Output(m_time, ref m_buffer, callback);
                return true;
            }
            return false;
        }

        public void Input(Segment segment)
        {
            m_recieved = m_time;
            Packet packet = new Packet(segment);
            switch (packet.Channel)
            {
                case Channel.None:
                    m_status.Input(m_time, packet);
                    break;
                case Channel.Ordered:
                    m_orderded.Input(m_time, packet);
                    break;
                case Channel.Irregular:
                    m_irregular.Input(m_time, packet);
                    break;
                case Channel.Direct:
                    m_direct.Input(m_time, packet);
                    break;
                default:
                    Log.Error("Connection input from channel that does not exist");
                    break;
            }
        }

        public void Output(Channel channel, Write callback)
        {
            m_buffer.Reset();
            m_buffer.Write((byte)channel);
            m_buffer.Write((byte)Prompt.Accept);
            switch (channel)
            {
                case Channel.Ordered:
                    m_orderded.Output(m_time, ref m_buffer, callback);
                    break;
                case Channel.Irregular:
                    m_irregular.Output(m_time, ref m_buffer, callback);
                    break;
                case Channel.Direct:
                    m_direct.Output(m_time, ref m_buffer, callback);
                    break;
                default:
                    throw new ArgumentException("Connection channel does not exist or not allowed");
            }
        }

        public bool Update(uint time, Write handshake)
        {
            m_status.Update(m_time);
            /*m_orderded.Update(m_time);
            m_irregular.Update(m_time);*/

            // if receive hasn't been called in a while it will timeout
            if (m_time > m_recieved + TIMEOUT)
            {
                // lost connection | Connection Timeout
                return false;
            }

            // sends a ping every interval given it has received the last ping
            if (m_time > m_shaked + INTERVAL)
            {
                // Send Ping
                if (Signal(true, handshake))
                {
                    m_shaked = m_time;
                }
            }

            m_time = time;
            return true;
        }
    }
}
