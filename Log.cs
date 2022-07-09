﻿using System;namespace Lattice{    public static class Log    {        // Debug Messages        public static Action<object> POut { get; set; }        public static Action<object> EOut { get; set; }        public static Action<object> WOut { get; set; }        // called when a packet is sent through a socket, with the number of bytes sent        public static Action<int> Sent { get; set; }        // called when a packet is recieved through a socket, with the number of bytes recieved        public static Action<int> Received { get; set; }        // called every retransmission of the same packet        public static Action Loss { get; set; }        // called when a packet ackwonledge is recived; with the number of retransmissions        public static Action<int> Lost { get; set; }        internal static void Print(object value) => POut?.Invoke(value);        internal static void Error(object value) => EOut?.Invoke(value);        internal static void Warning(object value) => WOut?.Invoke(value);    }}