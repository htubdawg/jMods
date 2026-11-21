using System;

namespace HalfGripSlapPack
{
    internal sealed class WiperKnobRotation
    {
        private float travel;
        internal float Position => (int)Setting;
        internal WiperSpeed Setting { get; private set; }
        internal void Begin(WiperSpeed setting) { Setting = setting; travel = (int)setting; }
        internal void Move(float upwardMovement)
        {
            if (float.IsNaN(upwardMovement) || float.IsInfinity(upwardMovement)) return;
            // 0.4 cockpit units per detent. Consume movement at both hard stops so
            // reversal is immediate, and use hysteresis to prevent detent chatter.
            travel = Math.Max(0, Math.Min(3, travel + upwardMovement * 2.5f));
            int setting = (int)Setting;
            while (setting < 3 && travel >= setting + 0.6f) setting++;
            while (setting > 0 && travel <= setting - 0.6f) setting--;
            Setting = (WiperSpeed)setting;
        }
        internal void Release() { travel = (int)Setting; }
    }

	/// <summary>
	/// Mouse-wheel speed selection while the hand hovers the stalk: one detent per wheel notch
	/// (Unity reports about 1 per notch). Fractional trackpad deltas accumulate, reversing
	/// direction discards the partial notch, and scrolling past a hard stop is not banked.
	/// </summary>
	internal sealed class WiperScrollDetents
	{
		private float accumulated;

		/// <summary>Returns the setting after this frame's wheel movement (positive is up, faster).</summary>
		internal WiperSpeed Scroll(WiperSpeed current, float delta)
		{
			if (float.IsNaN(delta) || float.IsInfinity(delta) || delta == 0) return current;
			if (accumulated != 0 && Math.Sign(delta) != Math.Sign(accumulated)) accumulated = 0;
			accumulated += delta;
			int setting = (int)current;
			while (accumulated >= 1) { accumulated -= 1; setting = Math.Min(3, setting + 1); }
			while (accumulated <= -1) { accumulated += 1; setting = Math.Max(0, setting - 1); }
			if ((setting == 3 && accumulated > 0) || (setting == 0 && accumulated < 0)) accumulated = 0;
			return (WiperSpeed)setting;
		}

		/// <summary>Forgets a partial notch when the hand leaves the stalk.</summary>
		internal void Reset() { accumulated = 0; }
	}

    internal static class WasherSpray
    {
        internal static PanePoint Point(PanePoint pivot, float radius, float width, bool driver, int drop, float phase)
        {
            float inward = driver ? width * (0.11f + 0.04f * phase) : 0;
            return new PanePoint(pivot.X + inward + (drop % 3 - 1) * width * 0.06f * phase,
                pivot.Y + radius * (1.8f * phase - phase * phase));
        }
    }
}
