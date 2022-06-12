using System;

namespace Lattice.Delivery.Transmission.Carrier
{
    public struct Mask
    {
        public const ushort Length = sizeof(ushort) * 8;

        public ushort Value;

        public Mask(ushort value)
        {
            Value = value;
        }

        public bool this[int index]
        {
            get
            {
                if (index > Length - 1)
                    throw new ArgumentOutOfRangeException($"Mask only contains {Length} values: {index} is over");
                else if (index < 0)
                    throw new ArgumentOutOfRangeException($"Negative index");

                return (Value & (1 << index)) != 0;
            }

            set
            {
                if (index > Length - 1)
                    throw new ArgumentOutOfRangeException($"Mask only contains {Length} values: {index} is over");
                else if (index < 0)
                    throw new ArgumentOutOfRangeException($"Negative index");

                if (value)
                    Value = (ushort)(Value | (1 << index));
                else
                    Value = (ushort)(Value & (1 << index));
            }
        }

        public override string ToString()
        {
            return $"{{{Convert.ToString(Value, 2).PadLeft(Length, '0')}}}";
        }

        internal static byte IncLoop(byte current) => (byte)(current < Length - 1 ? current++ : 0);
    }
}