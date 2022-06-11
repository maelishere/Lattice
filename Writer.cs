﻿using System;
using System.Text;
using System.Runtime.InteropServices;

namespace Lattice
{
    // creates a byte array, but he frist four byes are for header information
    public class Writer : Buffer
    {
        public Writer()
        {
            this.m_segement = new Segment(new byte[MaxLength]);
            this.m_position = 0;
        }

        public Writer(int length)
        {
            this.m_segement = new Segment(new byte[length]);
            this.m_position = 0;
        }

        public override byte[] ToArray()
        {
            byte[] data = new byte[m_segement.Count];
            m_segement.CopyTo(data);
            return data;
        }

        public override Segment ToSegment()
        {
            return m_segement;
        }

        public void Write(byte value)
        {
            m_segement[m_position] = value;
            m_position++;
        }

        public void Write(byte[] values)
        {
            if (values.Length <= 0) return;

            Array.Copy(values, 0, m_segement.Array, m_segement.GetRelativePosition(m_position), values.Length);
            m_position += values.Length;
        }

        public void Write(Segment values)
        {
            if (values.Count <= 0) return;

            values.CopyTo(m_segement, m_position);
            m_position += values.Count;
        }

        /*public void Write(sbyte value)
        {
            Write(Convert.ToByte(value));
        }*/

        public void Write(bool value)
        {
            Write(value ? 1 : 0);
        }

        public void Write(char value)
        {
            Write(BitConverter.GetBytes(value));
        }

        public void Write(short value)
        {
            Write(BitConverter.GetBytes(value));
        }

        public void Write(ushort value)
        {
            Write(BitConverter.GetBytes(value));
        }

        public void Write(int value)
        {
            Write(BitConverter.GetBytes(value));
        }

        public void Write(uint value)
        {
            Write(BitConverter.GetBytes(value));
        }

        public void Write(long value)
        {
            Write(BitConverter.GetBytes(value));
        }

        public void Write(ulong value)
        {
            Write(BitConverter.GetBytes(value));
        }

        public void Write(float value)
        {
            Write(BitConverter.GetBytes(value));
        }

        public void Write(double value)
        {
            Write(BitConverter.GetBytes(value));
        }

        /*public void Write(decimal value)
        {

        }*/

        public void Write(string value)
        {
            int length = value.Length;
            if (length > ushort.MaxValue)
                throw new InvalidOperationException($"String [{value}] is more than {ushort.MaxValue} characters");

            Write((ushort)length);
            for (int c = 0; c < length; c++)
            {
                Write(value[c]);
            }
        }
    }
}
