using System;

namespace HalfGripSlapPack
{
    // Independent of Unity so the adjustment rules can be tested outside the game.
    internal sealed class MirrorAdjustment
    {
        internal float Yaw { get; private set; }
        internal float Pitch { get; private set; }

        internal MirrorAdjustment(float yaw, float pitch)
        {
            Yaw = Finite(yaw) ? yaw : 0;
            Pitch = Finite(pitch) ? pitch : 0;
        }

        internal void Move(float dx, float dy, float sensitivity, float yawLimit, float pitchLimit)
        {
            if (!Finite(dx) || !Finite(dy) || !Finite(sensitivity) ||
                !Finite(yawLimit) || !Finite(pitchLimit)) return;
            Yaw = Clamp(Yaw + dx * sensitivity, yawLimit);
            Pitch = Clamp(Pitch + dy * sensitivity, pitchLimit);
        }

        internal void Reset() { Yaw = 0; Pitch = 0; }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp(float value, float limit) => Math.Max(-Math.Abs(limit), Math.Min(Math.Abs(limit), value));
    }
}
