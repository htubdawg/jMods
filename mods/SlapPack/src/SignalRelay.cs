using System;

namespace HalfGripSlapPack
{
    internal enum RelayCue { None, Tick, Tock }

    internal sealed class SignalRelayTracker
    {
        private bool wasLit;
        private int previousDirection;

        internal RelayCue Step(int direction, bool lit, bool audible)
        {
            lit = direction != 0 && lit;
            RelayCue cue = lit ? (!wasLit || previousDirection != direction ? RelayCue.Tick : RelayCue.None)
                : wasLit ? RelayCue.Tock : RelayCue.None;
            wasLit = lit; previousDirection = direction;
            // Track silent/paused/exterior phases too, so no stale click is replayed on return.
            return audible ? cue : RelayCue.None;
        }
    }

    internal static class SignalRelaySamples
    {
        internal const int SampleRate = 22050;

        internal static float[] Generate(bool tick)
        {
            int count = (int)(SampleRate * (tick ? 0.045 : 0.055));
            var samples = new float[count];
            uint seed = tick ? 0x81f271u : 0x44ba03u;
            double previous = 0;
            for (int i = 0; i < count; i++)
            {
                seed = unchecked(seed * 1664525u + 1013904223u);
                double noise = (seed >> 8) / 16777215.0 * 2 - 1;
                double high = noise - previous * 0.7;
                previous = noise;
                double t = (double)i / SampleRate;
                double impact = high * 0.38 + Math.Sin(2 * Math.PI * (tick ? 1850 : 1350) * t) * 0.24
                    + Math.Sin(2 * Math.PI * (tick ? 3100 : 2400) * t) * 0.09;
                double envelope = Math.Min(1, t / 0.0005) * Math.Exp(-t * (tick ? 170 : 145));
                double fade = Math.Min(1, (double)(count - 1 - i) / (SampleRate * 0.003));
                samples[i] = (float)(impact * envelope * fade * 0.8);
            }
            return samples;
        }
    }
}
