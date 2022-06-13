namespace Lattice.Delivery
{
    using Bolt;

    public delegate Segment Responding(Write callback);
    public delegate void Receiving(uint timestamp, ref Reader reader);
    public delegate void ReceivingFrom(int connection, uint timestamp, ref Reader reader);
}