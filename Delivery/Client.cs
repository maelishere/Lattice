using System;
using System.Net;

namespace Lattice.Delivery
{
    using Bolt;

    public class Client : Transport
    {
        private readonly Host m_host;

        public int Local { get; }
        public int Remote { get; }

        public Client(Mode mode, IPEndPoint remote, Receiving receive, Action<uint, Request> request, Action<Request, uint> acknowledge) : base(mode)
        {
            m_socket.Connect(remote);
            m_host = new Host(remote.Address, remote.Port, 
                (Segment segment) =>
                {
                    EndPoint casted = m_host.address;
                    SendTo(segment, casted);
                }, 
                receive,
                (uint timestamp, ref Reader reader) =>
                {
                    Request type = (Request)reader.Read();
                    switch (type)
                    {
                        case Request.Connect:
                            Log.Warning($"Client({m_socket.LocalEndPoint}) received connect request from Server({remote})");
                            break;
                        case Request.Disconnect:
                            Log.Warning($"Client({m_socket.LocalEndPoint}) received diconnect request from Server({remote})");
                            break;
                    }
                    request?.Invoke(timestamp, type);
                },
                (uint delay, ref Reader reader) =>
                {
                    Request type = (Request)reader.Read();
                    switch (type)
                    {
                        case Request.Connect:
                            Log.Warning($"Client({m_socket.LocalEndPoint}) connected to Server({remote})");
                            break;
                        case Request.Disconnect:
                            Log.Warning($"Client({m_socket.LocalEndPoint}) diconnected from Server({remote})");
                            break;
                    }
                    acknowledge?.Invoke(type, delay);
                });

            m_host.Connect();
            Remote = remote.Serialize().GetHashCode();
            Local = m_socket.LocalEndPoint.Serialize().GetHashCode();
            Log.Debug($"Client({m_socket.LocalEndPoint}) connecting to Server({remote})");
        }

        public void Disconnect()
        {
            m_host.Disconnect();
        }

        public void Send(Channel channel, Write callback)
        {
            m_host.Output(channel, callback);
        }

        public void Update(Action<Error> error)
        {
            EndPoint remote = m_host.address;
            if (!ReceiveFrom(ref remote,
                (Segment segment) =>
                {
                    Reader reader = new Reader(segment);
                    m_host.Input(ref reader);
                }))
            {
                // hasn't received anything in while so timed out
                Log.Warning($"Client({m_socket.LocalEndPoint}) exception");
                error?.Invoke(Error.Exception);
            }
            if (!m_host.Update())
            {
                // hasn't received anything in while so timed out
                Log.Error($"Client({m_socket.LocalEndPoint}) timeout");
                error?.Invoke(Error.Timeout);
            }
        }
    }
}