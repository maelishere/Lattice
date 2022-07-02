using System;
using System.Net;
using System.Collections.Concurrent;

namespace Lattice.Delivery
{
    using Bolt;

    public class Server : Transport
    {
        private readonly Address m_listen;
        private readonly ConcurrentQueue<int> m_disconnecting = new ConcurrentQueue<int>();
        private readonly ConcurrentDictionary<int, Host> m_hosts = new ConcurrentDictionary<int, Host>();

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

            Listen = port;
            m_listen = new Address(Any(mode), port);
            m_socket.Bind(m_listen);

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // gets rid of Connection Reset Exception 
                /// url: https://stackoverflow.com/questions/7201862/an-existing-connection-was-forcibly-closed-by-the-remote-host
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                m_socket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
            }

            Log.Print($"Server({Listen}): Listening");
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
        public void Connect(IPEndPoint remote)
        {
            Incoming(remote.Serialize().GetHashCode(), remote, true);
        }

        // receive data from listen endpoint
        public void Receive()
        {
            EndPoint remote = new IPEndPoint(m_listen.Address, m_listen.Port);
            ReceiveFrom(ref remote,
                (Segment segment) =>
                {
                    Handle(remote, segment);
                },
                () =>
                {
                    Log.Error($"Server({Listen}): receive exception");
                    error?.Invoke(Listen, Error.Recieve);
                    /*m_disconnecting.Enqueue(m_remote.Serialize().GetHashCode());*/
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
                    // keep trying to add incase another thread is adding (Tick() -> Handle())
                    while (m_hosts.ContainsKey(connection))
                    {
                        m_hosts.TryRemove(connection, out Host _);
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

                // keep trying to add incase another thread is removing (Update())
                while (!m_hosts.ContainsKey(connection))
                {
                    m_hosts.TryAdd(connection, host);
                }

                Log.Print($"Server({Listen}): connecting to Client({connection})");
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