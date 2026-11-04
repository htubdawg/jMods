using System;

namespace HalfGripSlapPack
{
    internal sealed class TurnSignalState
    {
        internal int Direction { get; private set; } // -1 left, +1 right
        internal float StalkPosition { get; private set; }
        internal bool Latched { get; private set; }
        internal bool Held { get; private set; }
        internal bool FlashOn => Direction != 0 && illuminated;
        private bool illuminated, turnArmed;
        private float phase, heading;
        private int completed;
        private readonly int shortFlashes;
        private readonly float halfPeriod, armSteering, centeredSteering, minimumHeading;

        internal TurnSignalState(int shortFlashes = 3, float halfPeriod = 0.4f,
            float armSteering = 12, float centeredSteering = 4, float minimumHeading = 15)
        {
            if (shortFlashes < 1 || !Finite(halfPeriod) || halfPeriod <= 0 ||
                !Finite(armSteering) || !Finite(centeredSteering) || !Finite(minimumHeading) ||
                centeredSteering < 0 || armSteering <= centeredSteering || minimumHeading <= 0)
                throw new ArgumentOutOfRangeException("Invalid turn-signal timing or cancellation thresholds.");
            this.shortFlashes = shortFlashes;
            this.halfPeriod = halfPeriod;
            this.armSteering = armSteering;
            this.centeredSteering = centeredSteering;
            this.minimumHeading = minimumHeading;
        }

        internal void BeginHold() { Held = true; }

        internal void Move(float position)
        {
            if (!Finite(position)) return;
            Held = true;
            position = Math.Max(-1, Math.Min(1, position));
            if (Math.Abs(position) < 0.25f) { Cancel(); return; }
            int direction = position < 0 ? -1 : 1;
            if (direction != Direction)
            {
                Cancel();
                Direction = direction;
                illuminated = true;
            }
            StalkPosition = position;
            if (Math.Abs(position) >= 0.85f) { Latched = true; StalkPosition = Direction; }
        }

        internal void Release()
        {
            Held = false;
            StalkPosition = Latched ? Direction : 0;
            if (!Latched && completed >= shortFlashes) Cancel();
        }

        internal void Tick(float dt)
        {
            if (Direction == 0 || !Finite(dt) || dt <= 0) return;
            phase += Math.Min(dt, 10);
            while (Direction != 0 && phase >= halfPeriod)
            {
                phase -= halfPeriod;
                illuminated = !illuminated;
                if (!illuminated) completed++;
                if (!Held && !Latched && completed >= shortFlashes) Cancel();
            }
        }

        internal void ObserveTurn(float steeringDegrees, float headingDeltaDegrees, bool movingForward)
        {
            if (!Latched || !movingForward || !Finite(steeringDegrees) || !Finite(headingDeltaDegrees)) return;
            if (steeringDegrees * Direction >= armSteering) turnArmed = true;
            heading = Math.Max(0, heading + headingDeltaDegrees * Direction);
            if (!Held && turnArmed && heading >= minimumHeading && Math.Abs(steeringDegrees) <= centeredSteering) Cancel();
        }

        internal void Cancel()
        {
            Direction = 0; StalkPosition = 0; Latched = false; illuminated = false;
            turnArmed = false; phase = 0; heading = 0; completed = 0;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
