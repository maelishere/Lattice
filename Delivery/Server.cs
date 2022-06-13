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

        public Server(int port, Mode mode) : base(mode)
        {
            m_socket.SendBufferSize = Buffer.MaxLength;
            m_socket.ReceiveBufferSize = Buffer.MaxLength;
            m_socket.Bind(m_listen = Any(port, mode));
            Log.Debug($"Server({m_socket.LocalEndPoint}): Listening");
        }

        public bool Disconnect(int connection)
        {
            if (m_hosts.TryGetValue(connection, out Host host))
            {
                host.Signal(false, Host.Disconnect);
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

        public void Update(ReceivingFrom receive, Action<int, Sync, uint> sync, Action<int, Error> error)
        {
            EndPoint listen = m_listen.Create(m_listen.Serialize());
            if (!ReceiveFrom(ref listen, 
                (Segment segment) =>
                {
                    Handle(listen, segment, receive, sync, error);
                }))
            {
                Log.Warning($"Server({m_socket.LocalEndPoint}) exception with {listen}");
                error?.Invoke(0, Error.Exception);
            }

            foreach (var endpoint in m_hosts)
            {
                if (!endpoint.Value.Update(Host.Ping))
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

        private void Handle(EndPoint remote, Segment segment, ReceivingFrom receive, Action<int, Sync, uint> sync, Action<int, Error> error)
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
                        Sync type = (Sync)reader.Read();
                        switch (type)
                        {
                        case Sync.Disconnect:
                                m_outgoing.Enqueue(id);
                                Log.Debug($"Server({m_socket.LocalEndPoint}) received diconnect request from Client({id}|{remote})");
                                error?.Invoke(id, Error.Disconnected);
                                break;
                        }
                    },
                    (uint delay, ref Reader reader) =>
                    {
                        Sync type = (Sync)reader.Read();
                        switch (type)
                        {
                            case Sync.Disconnect:
                                m_outgoing.Enqueue(id);
                                Log.Debug($"Server({m_socket.LocalEndPoint}) disconnecting from Client({id}|{remote})");
                                break;
                        }
                        sync?.Invoke(id, type, delay);
                    }
                    );
                m_hosts.Add(id, host);
                Log.Debug($"Server({m_socket.LocalEndPoint}) connected to Client({id}|{remote})");
            }
            Reader reading = new Reader(segment);
            m_hosts[id].Input(ref reading);
        }
    }
}
