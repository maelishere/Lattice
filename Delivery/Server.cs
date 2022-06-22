using System;
using System.Net;
using System.Collections.Generic;

namespace Lattice.Delivery
{
    using Bolt;

    public class Server : Transport
    {
        private readonly EndPoint m_listen;
        private readonly Queue<int> m_outgoing = new Queue<int>();
        private readonly Dictionary<int, Host> m_hosts = new Dictionary<int, Host>();

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
            if (m_hosts.TryGetValue(connection, out Host host) && !m_outgoing.Contains(connection))
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
        public void Tick(ReceivingFrom receive, Action<int, uint, Request> request, Action<int, Request, uint> acknowledge, Action<int, Error> error)
        {
            EndPoint listen = m_listen.Create(m_listen.Serialize());
            if (!ReceiveFrom(ref listen,
                (Segment segment) =>
                {
                    Handle(listen, segment, receive, request, acknowledge, error);
                }))
            {
                Log.Warning($"Server({Listen}) exception with {listen.Serialize().GetHashCode()}");
                error?.Invoke(0, Error.Recieve);
            }
        }

        /// update connections and/or send packets
        public void Update(Action<int, Error> error)
        {
            foreach (var endpoint in m_hosts)
            {
                if (!endpoint.Value.Update())
                {
                    Log.Warning($"Server({Listen}) timed out from Client({endpoint.Key})");
                    error?.Invoke(endpoint.Key, Error.Timeout);
                    m_outgoing.Enqueue(endpoint.Key);
                }
            }

            while (m_outgoing.Count > 0)
            {
                m_hosts.Remove(m_outgoing.Dequeue());
            }
        }

        private void Handle(EndPoint remote, Segment segment, ReceivingFrom receive, Action<int, uint, Request> request, Action<int, Request, uint> acknowledge, Action<int, Error> error)
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
                            error?.Invoke(id, Error.Send);
                        }
                    },
                    (uint timestamp, ref Reader reader) =>
                    {
                        receive?.Invoke(id, timestamp, ref reader);
                    },
                    (uint timestamp, ref Reader reader) =>
                    {
                        Request type = (Request)reader.Read();
                        switch (type)
                        {
                            case Request.Connect:
                                Log.Debug($"Server({Listen}) received connect request from Client({id})");
                                break;
                            case Request.Disconnect:
                                m_outgoing.Enqueue(id);
                                Log.Debug($"Server({Listen}) received diconnect request from Client({id})");
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
                                Log.Debug($"Server({Listen}) connecting to Client({id})");
                                break;
                            case Request.Disconnect:
                                m_outgoing.Enqueue(id);
                                Log.Debug($"Server({Listen}) disconnecting from Client({id})");
                                break;
                        }
                        acknowledge?.Invoke(id, type, delay);
                    }
                    );
                host.Connect();
                m_hosts.Add(id, host);
                Log.Debug($"Server({Listen}) connected to Client({id})");
            }
            Reader reading = new Reader(segment);
            m_hosts[id].Input(ref reading);
        }
    }
}