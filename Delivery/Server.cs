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
            m_listen = Any(port, mode);
            m_socket.Bind(m_listen);
            Listen = m_listen.Serialize().GetHashCode();
            Log.Debug($"Server({m_socket.LocalEndPoint}): Listening");
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

        public bool Send(int connection, Channel channel, Write callback)
        {
            if (m_hosts.TryGetValue(connection, out Host host))
            {
                host.Output(channel, callback);
                return true;
            }
            return false;
        }

        public void Update(ReceivingFrom receive, Action<int, uint, Request> request, Action<int, Request, uint> acknowledge, Action<int, Error> error)
        {
            EndPoint listen = m_listen.Create(m_listen.Serialize());
            if (!ReceiveFrom(ref listen, 
                (Segment segment) =>
                {
                    Handle(listen, segment, receive, request, acknowledge);
                }))
            {
                Log.Warning($"Server({m_socket.LocalEndPoint}) exception with {listen}");
                error?.Invoke(0, Error.Exception);
            }

            foreach (var endpoint in m_hosts)
            {
                if (!endpoint.Value.Update())
                {
                    Log.Warning($"Server({m_socket.LocalEndPoint}) timed out from Client({endpoint.Key}|{endpoint.Value.address})");
                    error?.Invoke(endpoint.Key, Error.Timeout);
                    m_outgoing.Enqueue(endpoint.Key);
                }
            }

            while (m_outgoing.Count > 0)
            {
                m_hosts.Remove(m_outgoing.Dequeue());
            }
        }

        private void Handle(EndPoint remote, Segment segment, ReceivingFrom receive, Action<int, uint, Request> request, Action<int, Request, uint> acknowledge)
        {
            int id = remote.Serialize().GetHashCode();
            if (!m_hosts.ContainsKey(id))
            {
                IPEndPoint point = (IPEndPoint)remote;
                Host host = new Host(point.Address, point.Port,
                    (Segment other) =>
                    {
                        EndPoint casted = m_hosts[id].address;
                        SendTo(other, casted);
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
                                Log.Debug($"Server({m_socket.LocalEndPoint}) received connect request from Client({id}|{remote})");
                                break;
                            case Request.Disconnect:
                                m_outgoing.Enqueue(id);
                                Log.Debug($"Server({m_socket.LocalEndPoint}) received diconnect request from Client({id}|{remote})");
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
                                Log.Debug($"Server({m_socket.LocalEndPoint}) connecting to Client({id}|{remote})");
                                break;
                            case Request.Disconnect:
                                m_outgoing.Enqueue(id);
                                Log.Debug($"Server({m_socket.LocalEndPoint}) disconnecting from Client({id}|{remote})");
                                break;
                        }
                        acknowledge?.Invoke(id, type, delay);
                    }
                    );
                host.Connect();
                m_hosts.Add(id, host);
                Log.Debug($"Server({m_socket.LocalEndPoint}) connected to Client({id}|{remote})");
            }
            Reader reading = new Reader(segment);
            m_hosts[id].Input(ref reading);
        }
    }
}