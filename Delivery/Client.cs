using System;
using System.Net;

namespace Lattice.Delivery
{
    public class Client : Transport
    {
        private readonly Host m_host;

        public Client(Mode mode, IPEndPoint remote, Action<Segment> receive, Action<Sync, uint> sync, Action<Error> error) : base(mode)
        {
            Log.Debug($"Client({m_socket.LocalEndPoint}) connecting to Server({remote})");

            m_socket.Connect(remote);
            m_host = new Host(remote.Address, remote.Port, 
                (Segment segment) =>
                {
                    EndPoint casted = m_host.address;
                    SendTo(segment, casted);
                }, 
                receive,
                (Segment segment) =>
                {
                    switch ((Sync)segment[0])
                    {
                        case Sync.Disconnect:
                            Log.Warning($"Client({m_socket.LocalEndPoint}) received diconnect request from Server({remote})");
                            error?.Invoke(Error.Disconnected);
                            break;
                    }
                },
                (Segment segment, uint delay) =>
                {
                    switch ((Sync)segment[0])
                    {
                        case Sync.Disconnect:
                            Log.Warning($"Client({m_socket.LocalEndPoint}) diconnecting from Server({remote})");
                            break;
                    }
                    sync?.Invoke((Sync)segment[0], delay);
                });

            m_host.Signal(false, Host.Connect);
        }

        public void Disconnect()
        {
            m_host.Signal(false, Host.Disconnect);
        }

        public void Send(Transmission.Channel channel, Write callback)
        {
            m_host.Output(channel, callback);
        }

        public void Update(Action<Error> error)
        {
            EndPoint remote = m_host.address;
            if (!ReceiveFrom(ref remote,
                (Segment segment) =>
                {
                    m_host.Input(segment);
                }))
            {
                // hasn't received anything in while so timed out
                Log.Warning($"Client({m_socket.LocalEndPoint}) exception");
                error?.Invoke(Error.Exception);
            }
            if (!m_host.Update(Host.Ping))
            {
                // hasn't received anything in while so timed out
                Log.Error($"Client({m_socket.LocalEndPoint}) timeout");
                error?.Invoke(Error.Timeout);
            }
        }
    }
}