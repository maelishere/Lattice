using System;

namespace Lattice.Delivery.Transmission
{
    using Bolt;
    using Carrier;

    public sealed class Connection
    {
        const int SMTU = 512;
        const int LMTU = 1024;
        const int INTERVAL = 1000; // 1 sec
        const int TIMEOUT = 10000; // 10 secs

        private Bit m_status;
        private Module m_direct;
        private Window m_irregular;
        private Lancet m_orderded;

        private uint m_shaked = 0;
        private uint m_recieved = 0;

        internal Connection(Action<Segment> send, Receiving receive, Receiving signal, Receiving acknowledge)
        {
            m_status = new Bit(send, signal, acknowledge, 
                (Write callback)=>
                {
                    Writer buffer = new Writer(SMTU);
                    buffer.Write(Channel.None);
                    callback(ref buffer);
                    return buffer.ToSegment();
                });

            m_direct = new Module(send, receive, null);

            m_irregular = new Window(send, receive,
                (Write callback) =>
                {
                    Writer buffer = new Writer(SMTU);
                    buffer.Write(Channel.Irregular);
                    callback(ref buffer);
                    return buffer.ToSegment();
                });

            m_orderded = new Lancet(send, receive,
                (Write callback) =>
                {
                    Writer buffer = new Writer(SMTU);
                    buffer.Write(Channel.Ordered);
                    callback(ref buffer);
                    return buffer.ToSegment();
                });
        }

        public bool Signal(uint time, bool wait, Write request)
        {
            if (!wait || !m_status.Sending)
            {
                Writer buffer = new Writer(SMTU);
                buffer.Write(Channel.None);
                m_status.Output(time, ref buffer, request);
                return true;
            }
            return false;
        }

        public void Input(uint time, ref Reader reader)
        {
            m_recieved = time;
            switch (reader.ReadChannel())
            {
                case Channel.None:
                    m_status.Input(time, ref reader);
                    break;

                case Channel.Direct:
                    m_direct.Input(time, ref reader);
                    break;

                case Channel.Irregular:
                    m_irregular.Input(time, ref reader);
                    break;

                case Channel.Ordered:
                    m_orderded.Input(time, ref reader);
                    break;

                default:
                    Log.Error("Connection input from channel that does not exist");
                    break;
            }
        }

        public void Output(uint time, Channel channel, Write callback)
        {
            Writer buffer = new Writer(LMTU);
            buffer.Write(channel);
            switch (channel)
            {
                case Channel.Direct:
                    m_direct.Output(time, ref buffer, callback);
                    break;

                case Channel.Irregular:
                    m_irregular.Output(time, ref buffer, callback);
                    break;

                case Channel.Ordered:
                    m_orderded.Output(time, ref buffer, callback);
                    break;

                default:
                    throw new ArgumentException($"{channel} channel is invalid");
            }
        }

        public bool Update(bool recurrent, uint time, Write ping)
        {
            // sends a ping every interval given it has received the last ping
            if (recurrent && time > m_shaked + INTERVAL)
            {
                // Send Ping
                if (Signal(time, true, ping))
                {
                    m_shaked = time;
                }
            }

            m_status.Update(time);
            m_orderded.Update(time);
            m_irregular.Update(time);
            m_direct.Update(time);

            // if receive hasn't been called in a while it will timeout
            if (time > m_recieved + TIMEOUT)
            {
                // lost connection | Connection Timeout
                return false;
            }
            return true;
        }
    }
}
