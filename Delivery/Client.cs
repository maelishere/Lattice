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
            Remote = remote.Port;
            // Remote = remote.Serialize().GetHashCode();
            Local = m_socket.LocalEndPoint.Serialize().GetHashCode();

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
                            Log.Warning($"Client({Local}) received connect request from Server({Remote})");
                            break;
                        case Request.Disconnect:
                            Log.Warning($"Client({Local}) received diconnect request from Server({Remote})");
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
                            Log.Warning($"Client({Local}) connected to Server({Remote})");
                            break;
                        case Request.Disconnect:
                            Log.Warning($"Client({Local}) diconnected from Server({Remote})");
                            break;
                    }
                    acknowledge?.Invoke(type, delay);
                });

            m_host.Connect();
            Log.Debug($"Client({Local}) connecting to Server({Remote})");
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
                Log.Warning($"Client({Local}) exception");
                error?.Invoke(Error.Exception);
            }
            if (!m_host.Update())
            {
                // hasn't received anything in while so timed out
                Log.Error($"Client({Local}) timeout");
                error?.Invoke(Error.Timeout);
            }
        }
    }
}