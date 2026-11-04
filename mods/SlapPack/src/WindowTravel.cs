using System;

namespace HalfGripSlapPack
{
    // Each door owns its own travel. Positive crank movement lowers the window; zero is fully up.
    internal sealed class WindowTravel
    {
        internal float OpenFraction { get; private set; }

        internal void Turn(float degrees, float turnsForFullTravel)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees) ||
                float.IsNaN(turnsForFullTravel) || float.IsInfinity(turnsForFullTravel) || turnsForFullTravel <= 0) return;
            OpenFraction = Math.Max(0, Math.Min(1, OpenFraction + degrees / 360f / turnsForFullTravel));
        }

        // Unwrap the hand's angular movement at the -180/180 seam, not the full multi-turn travel.
        internal static float AngularDelta(float previousDegrees, float currentDegrees)
        {
            float difference = (currentDegrees - previousDegrees) % 360f;
            if (difference > 180) difference -= 360;
            if (difference < -180) difference += 360;
            return difference;
        }
    }
}
