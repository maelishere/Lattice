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

        private Action<Error> error { get; }

        public Client(Mode mode, IPEndPoint remote, Receiving receiving, Action<uint, Request> request, Action<Request, uint> acknowledge, Action<Error> error) : base(mode)
        {
            this.error = error;

            Remote = remote.Port;
            m_socket.Connect(remote);
            Local = m_socket.LocalEndPoint.Serialize().GetHashCode();

            m_host = new Host(remote.Address, remote.Port, 
                (Segment segment) =>
                {
                    try
                    {
                        /// connect was already called
                        m_socket.Send(segment.Array, segment.Offset, segment.Count, SocketFlags.None);
                    }
                    catch (SocketException e)
                    {
                        // for mac os
                        Log.Error($"[{e.SocketErrorCode}] {e.Message}");
                        Log.Debug($"Client({Local}): send exception");
                        error?.Invoke(Error.Send);
                    }
                }, 
                receiving,
                (uint timestamp, ref Reader reader) =>
                {
                    Request type = (Request)reader.Read();
                    switch (type)
                    {
                        case Request.Connect:
                            Log.Debug($"Client({Local}): Server({Remote}) connected");
                            break;
                        case Request.Disconnect:
                            Log.Debug($"Client({Local}): Server({Remote}) disconnected");
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
                            /*m_socket.Shutdown(SocketShutdown.Both);
                            m_socket.Disconnect(false);*/
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
        public void Receive()
        {
            EndPoint remote = m_host.address;
            ReceiveFrom(ref remote,
                (Segment segment) =>
                {
                    Reader reader = new Reader(segment);
                    m_host.Input(ref reader);
                },
                () =>
                {
                    // otherside closed unexpectedly
                    Log.Warning($"Client({Local}): receive exception");
                    error?.Invoke(Error.Recieve);
                });
        }

        /// update connection and/or send packets
        public void Update()
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