using System;
using System.Net;
using System.Collections.Concurrent;

namespace Lattice.Delivery
{
    using Bolt;

    public class Server : Transport
    {
        private readonly EndPoint m_listen;
        private readonly ConcurrentQueue<int> m_disconnecting = new ConcurrentQueue<int>();
        private readonly ConcurrentDictionary<int, Host> m_hosts = new ConcurrentDictionary<int, Host>();

        public int Listen { get; }

        public Server(int port, Mode mode) : base(mode)
        {
            Listen = port;
            m_listen = Any(port, mode);
            m_socket.Bind(m_listen);
            // Listen = m_listen.Serialize().GetHashCode();
            Log.Debug($"Server({Listen}): Listening");
        }

        public bool Disconnect(int connection)
        {
            if (m_hosts.TryGetValue(connection, out Host host))
            {
                host.Disconnect();
                return true;
            }
            return false;
        }

        public void Send(Channel channel, Write callback)
        {
            foreach (var endpoint in m_hosts)
            {
                endpoint.Value.Output(channel, callback);
            }
        }

        public bool Send(int connection, Channel channel, Write callback)
        {
            if (m_hosts.TryGetValue(connection, out Host host))
            {
                host.Output(channel, callback);
                return true;
            }
            return false;
        }

        public void Send(Func<int, bool> predicate, Channel channel, Write callback)
        {
            foreach (var endpoint in m_hosts)
            {
                if (predicate(endpoint.Key))
                {
                    endpoint.Value.Output(channel, callback);
                }
            }
        }

        // receive data from listen endpoint
        public void Receive(ReceivingFrom received, Action<int, uint, Request> request, Action<int, Request, uint> acknowledge, Action<int, Error> error)
        {
            EndPoint listen = m_listen.Create(m_listen.Serialize());
            if (!ReceiveFrom(ref listen,
                (Segment segment) =>
                {
                    Handle(listen, segment, received, request, acknowledge, error);
                }))
            {
                int id = listen.Serialize().GetHashCode();
                /*Log.Warning($"Server({Listen}): receive exception with Endpoint({id})");*/
                error?.Invoke(id, Error.Recieve);
            }
        }

        /// update connections and/or send packets
        public void Update(Action<int, Error> error)
        {
            foreach (var endpoint in m_hosts)
            {
                if (!endpoint.Value.Update())
                {
                    Log.Error($"Server({Listen}): timed out from Client({endpoint.Key})");
                    error?.Invoke(endpoint.Key, Error.Timeout);
                    m_disconnecting.Enqueue(endpoint.Key);
                }
            }

            while (m_disconnecting.Count > 0)
            {
                if (m_disconnecting.TryDequeue(out int connection))
                {
                    // keep trying to add incase another thread is adding (Tick() -> Handle())
                    while (m_hosts.ContainsKey(connection))
                    {
                        m_hosts.TryRemove(connection, out Host _);
                    }
                }
            }
        }

        private void Handle(EndPoint remote, Segment segment, ReceivingFrom received, Action<int, uint, Request> request, Action<int, Request, uint> acknowledge, Action<int, Error> error)
        {
            int id = remote.Serialize().GetHashCode();
            if (!m_hosts.ContainsKey(id))
            {
                IPEndPoint point = (IPEndPoint)remote;
                Host host = new Host(point.Address, point.Port,
                    (Segment other) =>
                    {
                        EndPoint casted = m_hosts[id].address;
                        if (!SendTo(other, casted))
                        {
                            /*Log.Warning($"Server({Listen}): send exception with Client({id})");*/
                            error?.Invoke(id, Error.Send);
                        }
                    },
                    (uint timestamp, ref Reader reader) =>
                    {
                        received?.Invoke(id, timestamp, ref reader);
                    },
                    (uint timestamp, ref Reader reader) =>
                    {
                        Request type = (Request)reader.Read();
                        switch (type)
                        {
                            case Request.Connect:
                                Log.Debug($"Server({Listen}): Client({id}) testing connection");
                                break;
                            case Request.Disconnect:
                                Log.Debug($"Server({Listen}): Client({id}) wants to disconnect");
                                Disconnect(id);
                                break;
                        }
                        request?.Invoke(id, timestamp, type);
                    },
                    (uint delay, ref Reader reader) =>
                    {
                        Request type = (Request)reader.Read();
                        switch (type)
                        {
                            case Request.Connect:
                                Log.Debug($"Server({Listen}): connected to Client({id})");
                                break;
                            case Request.Disconnect:
                                Log.Debug($"Server({Listen}): disconnected from Client({id})");
                                m_disconnecting.Enqueue(id);
                                break;
                        }
                        acknowledge?.Invoke(id, type, delay);
                    }
                    );
                host.Connect();
                // keep trying to add incase another thread is removing (Update())
                while (!m_hosts.ContainsKey(id))
                {
                    m_hosts.TryAdd(id, host);
                }
                Log.Debug($"Server({Listen}): connecting to Client({id})");
            }
            Reader reading = new Reader(segment);
            m_hosts[id].Input(ref reading);
        }
    }
}