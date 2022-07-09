using System;
using System.Net;
using System.Collections.Concurrent;

namespace Lattice.Delivery
{
    using Bolt;

    public class Server : Transport
    {
        private EndPoint m_remote;

        private readonly Address m_listen;
        private readonly ConcurrentQueue<int> m_disconnecting = new ConcurrentQueue<int>();
        private readonly ConcurrentDictionary<int, Host> m_hosts = new ConcurrentDictionary<int, Host>();
        private readonly ConcurrentDictionary<int, Address> m_disconnected = new ConcurrentDictionary<int, Address>();

        public int Listen { get; }

        private ReceivingFrom receiving { get; }
        private Action<int, uint, Request> request { get; }
        private Action<int, Request, uint> acknowledge { get; }
        private Action<int, Error> error { get; }

        public Server(Mode mode, int port, ReceivingFrom receiving, Action<int, uint, Request> request, Action<int, Request, uint> acknowledge, Action<int, Error> error) : base(mode)
        {
            this.receiving = receiving;
            this.request = request;
            this.acknowledge = acknowledge;
            this.error = error;

            m_listen = new Address(Any(mode), port);
            m_remote = new IPEndPoint(m_listen.Address, 0);

            m_socket.Bind(m_listen);

            Listen = m_listen.Serialize().GetHashCode();

            Log.Print($"Server({Listen}): Listening");
        }

        public bool Find(int connection, out IPEndPoint point)
        {
            if (m_hosts.TryGetValue(connection, out Host host))
            {
                point = host.address;
                return true;
            }
            point = null;
            return false;
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

        // for reconnecting, when approriate
        //      only works for if the client actually disconnected (for any reason)
        public bool Reconnect(int connection)
        {
            if (m_disconnected.TryRemove(connection, out Address address))
            {
                Incoming(connection, address, true);
                return true;
            }
            return false;
        }

        // receive data from listen endpoint
        public void Receive()
        {
            ReceiveFrom(ref m_remote,
                (Segment segment) =>
                {
                    Handle(m_remote, segment);
                },
                () =>
                {
                    Log.Error($"Server({Listen}): receive exception");
                    error?.Invoke(Listen, Error.Recieve);
                });
        }

        /// update connections and/or send packets
        public void Update()
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
                    if (m_hosts.TryRemove(connection, out Host host))
                    {
                        // add it to the list of previously connected clients
                        m_disconnected.TryAdd(connection, host.address);
                    }
                }
            }
        }

        private void Incoming(int connection, IPEndPoint point, bool signal)
        {
            if (!m_hosts.ContainsKey(connection))
            {
                Host host = new Host(point.Address, point.Port,
                    (Segment other) =>
                    {
                        EndPoint casted = m_hosts[connection].address;
                        if (!SendTo(other, casted))
                        {
                            Log.Error($"Server({Listen}): send exception with Client({connection})");
                            error?.Invoke(connection, Error.Send);
                            m_disconnecting.Enqueue(connection);
                        }
                    },
                    (uint timestamp, ref Reader reader) =>
                    {
                        receiving?.Invoke(connection, timestamp, ref reader);
                    },
                    (uint timestamp, ref Reader reader) =>
                    {
                        Request type = (Request)reader.Read();
                        switch (type)
                        {
                            case Request.Connect:
                                Log.Print($"Server({Listen}): Client({connection}) connected");
                                break;
                            case Request.Disconnect:
                                Log.Print($"Server({Listen}): Client({connection}) disconnected");
                                m_disconnecting.Enqueue(connection);
                                break;
                        }
                        request?.Invoke(connection, timestamp, type);
                    },
                    (uint delay, ref Reader reader) =>
                    {
                        Request type = (Request)reader.Read();
                        switch (type)
                        {
                            case Request.Connect:
                                Log.Print($"Server({Listen}): connected to Client({connection})");
                                break;
                            case Request.Disconnect:
                                Log.Print($"Server({Listen}): disconnected from Client({connection})");
                                m_disconnecting.Enqueue(connection);
                                break;
                        }
                        acknowledge?.Invoke(connection, type, delay);
                    }
                    );

                if (signal)
                {
                    host.Connect();
                }
                else
                {
                    host.n_active = true;
                }

                if (m_hosts.TryAdd(connection, host))
                {
                    Log.Print($"Server({Listen}): connecting to Client({connection})");
                }
            }
        }

        private void Handle(EndPoint remote, Segment segment)
        {
            int id = remote.Serialize().GetHashCode();
            Incoming(id, (IPEndPoint)remote, false);
            Reader reading = new Reader(segment);
            m_hosts[id].Input(ref reading);
        }
    }
}