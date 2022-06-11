﻿using System;
using System.Net;
using System.Collections.Generic;

namespace Lattice.Delivery
{
    public class Server : Transport
    {
        private readonly Queue<int> m_outgoing = new Queue<int>();
        private readonly Dictionary<int, Host> m_hosts = new Dictionary<int, Host>();

        public Server(int port, Mode mode) : base(mode)
        {
            m_socket.Bind(Any(port, mode));
            Log.Debug($"Server({m_socket.LocalEndPoint}): Listening");
        }

        public bool Disconnect(int connection)
        {
            if (m_hosts.TryGetValue(connection, out Host host))
            {
                host.connection.Signal(false, Host.Disconnect);
                return true;
            }
            return false;
        }

        public bool Send(int connection, Transmission.Channel channel, Write callback)
        {
            if (m_hosts.TryGetValue(connection, out Host host))
            {
                host.connection.Output(channel, callback);
                return true;
            }
            return false;
        }

        public void Update(uint time, Action<int, Segment> receive, Action<int, Sync, uint> sync, Action<int, Error> error)
        {
            EndPoint listen = m_socket.LocalEndPoint;
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
                if (!endpoint.Value.connection.Update(time, Host.Ping))
                {
                    Log.Warning($"Server({m_socket.LocalEndPoint}) timed out from Client({endpoint.Key}|{endpoint.Value.address})");
                    error?.Invoke(endpoint.Key, Error.Timeout);
                    m_outgoing.Enqueue(endpoint.Key);
                }
            }

            while(m_outgoing.Count > 0)
            {
                m_hosts.Remove(m_outgoing.Dequeue());
            }
        }

        private void Handle(EndPoint remote, Segment segment, Action<int, Segment> receive, Action<int, Sync, uint> sync, Action<int, Error> error)
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
                    (Segment other) =>
                    {
                        receive?.Invoke(id, other);
                    },
                    (Segment other) =>
                    {
                        switch ((Sync)other[0])
                        {
                        case Sync.Disconnect:
                                m_outgoing.Enqueue(id);
                                Log.Debug($"Server({m_socket.LocalEndPoint}) received diconnect request from Client({id}|{remote})");
                                error?.Invoke(id, Error.Disconnected);
                                break;
                        }
                    },
                    (Segment other, uint delay) =>
                    {
                        switch ((Sync)other[0])
                        {
                            case Sync.Disconnect:
                                m_outgoing.Enqueue(id);
                                Log.Debug($"Server({m_socket.LocalEndPoint}) disconnecting from Client({id}|{remote})");
                                break;
                        }
                        sync?.Invoke(id, (Sync)other[0], delay);
                    }
                    );
                m_hosts.Add(id, host);
                Log.Debug($"Server({m_socket.LocalEndPoint}) connected to Client({id}|{remote})");
            }
            m_hosts[id].connection.Input(segment);
        }
    }
}