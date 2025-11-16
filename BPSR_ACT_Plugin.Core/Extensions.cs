using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
