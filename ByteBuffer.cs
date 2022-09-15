using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SerialTee
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ByteBuffer
    {
        private byte _x0;
        private byte _x1;
        private byte _x2;
        private byte _x3;
        private byte _x4;
        private byte _x5;
        private byte _x6;
        private byte _x7;
        private byte _x8;
        private byte _x9;
        private byte _x10;
        private byte _x11;
        private byte _x12;
        private byte _x13;
        private byte _x14;
        private byte _x15;

        public int Length;
        public int MaxLenght => 16;
        
        public byte this[int index]
        {
            get => AsSpan()[index];
            set => AsSpan()[index] = value;
        }

        public Span<byte> AsSpan() => MemoryMarshal.CreateSpan(ref _x0, Length);
        
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(64);
            for (int i = 0; i < Length; i++)
                sb.AppendFormat("{0:X2} ", this[i]);

            return sb.ToString();
        }
    }
}