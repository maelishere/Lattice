﻿using System;using System.Net;namespace Lattice.Discovery{    using Bolt;    public class Receiver : Transport    {        private readonly EndPoint m_remote;        public Receiver(int port, Mode mode) : base(mode)        {            m_socket.Bind(m_remote = Any(port, mode));            Log.Debug($"Receiving Broadcast at {m_socket.LocalEndPoint}");        }        public void Update(Action<Segment, EndPoint> receive)        {            EndPoint remote = m_remote.Create(m_remote.Serialize());            ReceiveFrom(ref remote, (segment) =>            {                receive?.Invoke(segment, remote);            });        }    }}