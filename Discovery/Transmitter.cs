using System;using System.Net;namespace Lattice.Discovery{    using Bolt;    public sealed class Transmitter : Transport    {        public Transmitter(int port, Mode mode) : base(mode)        {            IPEndPoint remote = new IPEndPoint(IPAddress.Broadcast, port);            m_socket.Connect(remote);            Log.Print($"Brodcasting to {m_socket.LocalEndPoint}");        }        public void Broadcast(Segment segment, Action exception)        {
            if (!Send(segment))
            {
                exception?.Invoke();
            }        }    }}