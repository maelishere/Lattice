namespace Lattice.Delivery.Transmission.Carrier
{
    public struct Header
    {
        public Header(Command command, byte serial, uint time)
        {
            this.command = command;
            this.serial = serial;
            this.time = time;
        }

        public Command command { get; }
        public byte serial { get; }
        public uint time { get; }
    }
}