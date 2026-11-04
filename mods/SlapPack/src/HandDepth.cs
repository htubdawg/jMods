using System;

namespace HalfGripSlapPack
{
    internal static class HandDepth
    {
        internal const float Clearance = 0.05f;

        internal static float InFrontOfMirror(float pivotDepth, float farthestHandDepth, float nearestMirrorDepth, float nearClip)
        {
            float overlap = Math.Max(0, farthestHandDepth - nearestMirrorDepth + Clearance);
            // Never push a hand backwards or pull it through the camera's near clipping plane.
            return Math.Min(pivotDepth, Math.Max(nearClip + Clearance, pivotDepth - overlap));
        }
    }
}
