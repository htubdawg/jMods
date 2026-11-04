using System;

namespace HalfGripSlapPack
{
    internal enum WiperSpeed { Off, Slow, Medium, Fast }

    // Phase always advances through a complete out-and-back stroke. Stopping never
    // reverses or teleports a moving blade; the current cycle finishes at the park stop.
    internal sealed class WiperState
    {
        private readonly double[] periods;
        private readonly double intermittentPause;
        private double pauseRemaining;
        private double phase;
        private bool cycling;
        private int washCyclesRemaining;
        internal WiperSpeed Speed { get; private set; }
        internal float Pull { get; private set; }
        internal bool Washing => Pull >= 0.3f;
        internal bool Parked => !cycling;
        internal float Sweep => (float)((1 - Math.Cos(phase * Math.PI * 2)) * 0.5);

        internal WiperState(float intermittentPause = 2.5f, float medium = 1.4f, float fast = 0.8f)
        {
            if (!Positive(intermittentPause) || !Positive(medium) || !Positive(fast) || medium <= fast)
                throw new ArgumentOutOfRangeException(nameof(intermittentPause), "The intermittent pause and wipe periods must be finite and positive; fast must be faster than normal.");
            this.intermittentPause = intermittentPause;
            periods = new[] { 0.0, medium, medium, fast };
        }

        internal void SetSpeed(WiperSpeed setting)
        {
            if ((int)setting < 0 || (int)setting > 3) throw new ArgumentOutOfRangeException(nameof(setting));
            if (setting != WiperSpeed.Slow) pauseRemaining = 0;
            Speed = setting;
        }
        internal void SetPull(float fraction)
        {
            if (float.IsNaN(fraction) || float.IsInfinity(fraction)) return;
            bool wasWashing = Washing;
            Pull = Math.Max(0, Math.Min(1, fraction));
            // Count full out-and-back cycles, not individual strokes. An already moving
            // blade finishes its partial cycle before the three guaranteed full cycles.
            if (!wasWashing && Washing)
            {
                washCyclesRemaining = phase > 0 ? 4 : 3;
                pauseRemaining = 0;
            }
            if (wasWashing && !Washing && washCyclesRemaining == 0 && phase == 0 && Speed == WiperSpeed.Slow)
            {
                pauseRemaining = intermittentPause; cycling = false;
            }
        }
        internal void Release() { SetPull(0); }

        internal void Tick(float seconds)
        {
            if (!Positive(seconds)) return;
            double elapsed = seconds;
            if (!Washing && washCyclesRemaining > 0)
            {
                double untilBurstEnds = (washCyclesRemaining - phase) * periods[Math.Max(2, (int)Speed)];
                if (elapsed + 0.000001 >= untilBurstEnds)
                {
                    elapsed = Math.Max(0, elapsed - untilBurstEnds);
                    phase = 0; washCyclesRemaining = 0; cycling = Speed != WiperSpeed.Off;
                    if (Speed == WiperSpeed.Slow)
                    {
                        // The washer has just finished a wipe. Resume intermittent mode
                        // with its parked pause rather than adding an immediate extra wipe.
                        pauseRemaining = intermittentPause; cycling = false;
                        TickIntermittent(elapsed); return;
                    }
                    if (elapsed == 0 || Speed == WiperSpeed.Off) return;
                }
            }
            if (Speed == WiperSpeed.Slow && !Washing && washCyclesRemaining == 0)
            {
                TickIntermittent(elapsed); return;
            }
            int effective = Washing || washCyclesRemaining > 0 ? Math.Max(2, (int)Speed) : (int)Speed;
            if (effective == 0 && phase == 0) { cycling = false; return; }
            if (effective == 0 && !cycling) return;
            if (effective > 0) cycling = true;
            // Use the last operating period while completing an off-commanded cycle.
            if (effective > 0) lastPeriod = periods[effective];
            phase += elapsed / lastPeriod;
            if (phase < 1) return;
            double completed = Math.Floor(phase);
            washCyclesRemaining -= (int)Math.Min(washCyclesRemaining, completed);
            if (effective == 0) { phase = 0; cycling = false; }
            else phase %= 1;
        }

        private void TickIntermittent(double elapsed)
        {
            if (pauseRemaining > 0)
            {
                if (elapsed < pauseRemaining) { pauseRemaining -= elapsed; return; }
                elapsed -= pauseRemaining; pauseRemaining = 0;
            }
            double period = periods[(int)WiperSpeed.Medium];
            lastPeriod = period;
            double untilPark = (1 - phase) * period;
            if (elapsed + 0.000001 < untilPark)
            {
                cycling = true; phase += elapsed / period; return;
            }
            elapsed = Math.Max(0, elapsed - untilPark);
            phase = 0; cycling = false;
            // Each following block is a parked pause then a normal-speed wipe. Reduce
            // whole blocks at once so large frame gaps never require an unbounded loop.
            elapsed %= intermittentPause + period;
            if (elapsed <= intermittentPause) { pauseRemaining = intermittentPause - elapsed; return; }
            phase = (elapsed - intermittentPause) / period;
            cycling = true;
        }

        private double lastPeriod = 1.4;
        private static bool Positive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
