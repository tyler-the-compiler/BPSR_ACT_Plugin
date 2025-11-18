using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BPSR_ACT_Plugin
{
    public static class Extensions
    {
        public static T[] SubArray<T>(this T[] data, long index, long length)
        {
            var internalLen = length;
            T[] result = new T[internalLen];
            Array.Copy(data, index, result, 0, internalLen);
            return result;
        }

        public static T[] SubArray<T>(this T[] data, long index)
        {
            var internalLen = (data.Length - index);
            T[] result = new T[internalLen];
            Array.Copy(data, index, result, 0, internalLen);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32BigEndianEx(this byte[] buf)
        {
            if (buf.Length < 4)
            {
                throw new ArgumentException("integer must have 4 bytes");
            }

            return (buf[0] << 24) | (buf[1] << 16) | (buf[2] << 8) | buf[3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32BigEndian(this byte[] buf, int def = 0)
        {
            try
            {
                return ReadInt32BigEndianEx(buf);
            }
            catch { return def; }
        }
    }
}
