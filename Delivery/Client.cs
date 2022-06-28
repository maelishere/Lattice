using System;
using System.Net;
using System.Net.Sockets;

namespace Lattice.Delivery
{
    using Bolt;

    public class Client : Transport
    {
        private readonly Host m_host;

        public int Local { get; }
        public int Remote { get; }

        public Client(Mode mode, IPEndPoint remote, Receiving receive, Action<uint, Request> request, Action<Request, uint> acknowledge, Action<Error> error) : base(mode)
        {
            m_socket.Connect(remote);
            Remote = remote.Port;
            // Remote = remote.Serialize().GetHashCode();
            Local = m_socket.LocalEndPoint.Serialize().GetHashCode();

            m_host = new Host(remote.Address, remote.Port, 
                (Segment segment) =>
                {
                    EndPoint casted = m_host.address;
                    if (!SendTo(segment, casted))
                    {
                        /*Log.Debug($"Client({Local}): send exception");*/
                        error?.Invoke(Error.Send);
                    }
                }, 
                receive,
                (uint timestamp, ref Reader reader) =>
                {
                    Request type = (Request)reader.Read();
                    switch (type)
                    {
                        case Request.Connect:
                            Log.Debug($" Client({Local}): Server({Remote}) testing connection");
                            break;
                        case Request.Disconnect:
                            Log.Debug($"Client({Local}): Server({Remote}) wants to disconnect");
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
                            Log.Debug($"Client({Local}): connected to Server({Remote})");
                            break;
                        case Request.Disconnect:
                            Log.Debug($"Client({Local}): diconnected from Server({Remote})");
                            m_socket.Shutdown(SocketShutdown.Both);
                            m_socket.Disconnect(false);
                            break;
                    }
                    acknowledge?.Invoke(type, delay);
                });

            m_host.Connect();
            Log.Debug($"Client({Local}): connecting to Server({Remote})");
        }

        // send a push to server, wait for ack or timeout
        public void Disconnect()
        {
            m_host.Disconnect();
        }

        // sends data to server
        public void Send(Channel channel, Write callback)
        {
            m_host.Output(channel, callback);
        }

        /// receive from socket
        public void Receive(Action<Error> error)
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
                /*Log.Warning($"Client({Local}): receive exception");*/
                error?.Invoke(Error.Recieve);
            }
        }

        /// update connection and/or send packets
        public void Update(Action<Error> error)
        {
            if (!m_host.Update())
            {
                // hasn't received anything in while so timed out
                Log.Error($"Client({Local}): timeout");
                error?.Invoke(Error.Timeout);
            }
        }
    }
}