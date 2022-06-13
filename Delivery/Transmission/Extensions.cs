namespace Lattice.Delivery.Transmission
{
    using Bolt;
    using Carrier;

    internal static class Extensions
    {
        public static void Write(this Writer writer, Channel channel)
        {
            writer.Write((byte)channel);
        }

        public static Channel ReadChannel(this Reader reader)
        {
            return (Channel)reader.Read();
        }

        public static void Write(this Writer writer, Header header)
        {
            WriteHeader(writer, header.command, header.serial, header.time);
        }

        public static void WriteHeader(this Writer writer, Command command, byte serial, uint time)
        {
            writer.Write((byte)command);
            writer.Write(serial);
            writer.Write(time);
        }

        public static Header ReadHeader(this Reader reader)
        {
            return new Header((Command)reader.Read(), reader.Read(), reader.ReadUInt());
        }
    }
}