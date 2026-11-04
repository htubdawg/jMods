using System;

namespace HalfGripSlapPack
{
    internal static class MeshBufferData
    {
        internal static float HalfToSingle(ushort bits)
        {
            int sign = (bits & 0x8000) == 0 ? 1 : -1;
            int exponent = (bits >> 10) & 31;
            int fraction = bits & 1023;
            if (exponent == 31)
                return fraction == 0 ? sign * float.PositiveInfinity : float.NaN;
            if (exponent == 0) return sign * fraction / 16777216f;
            return (float)(sign * (1 + fraction / 1024.0) * Math.Pow(2, exponent - 15));
        }
    }
}
