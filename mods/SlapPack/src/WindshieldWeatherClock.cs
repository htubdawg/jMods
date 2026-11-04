using System;

namespace HalfGripSlapPack
{
	// Motor samples are cheap. Accumulate their actual traversed range without
	// touching the field, then perform one field pass/upload at most 20 times/sec.
	/// <summary>Batches deposition and sampled blade paths into at most twenty field updates per second.</summary>
	internal sealed class WindshieldWeatherClock
	{
		internal const double Interval = 1.0 / 20;
		private double clock, elapsed, washing;
		private float low = 1, high;

		internal void Record(float seconds, float previous, float current, bool washer)
		{
			if (!(seconds > 0) || float.IsInfinity(seconds)) return;
			clock += seconds; elapsed += seconds;
			if (washer) washing += seconds;
			if (Math.Abs(current - previous) < 0.000001f) return;
			low = Math.Min(low, Math.Min(previous, current));
			high = Math.Max(high, Math.Max(previous, current));
		}

		internal bool TryStep(out float seconds, out float washerFraction, out float from, out float to)
		{
			seconds = washerFraction = from = to = 0;
			if (clock < Interval) return false;
			clock = Math.Max(0, clock % Interval);
			// No catch-up loop after a hitch. Bound weather work and discard excess
			// weather time, while the motor still advances through the complete frame.
			seconds = (float)Math.Min(elapsed, 0.25);
			washerFraction = (float)(washing / elapsed);
			if (high > low) { from = low; to = high; }
			elapsed = washing = 0; low = 1; high = 0;
			return true;
		}
	}

	internal static class WindshieldWeatherOptics
	{
		/// <summary>Keeps at least half of water obscuration while retaining full snow opacity.</summary>
		internal static float RefractionFloor(float water, float snow, float strength = 1) =>
			1 - (1 - Opacity(water, 0, strength) * 0.5f) * (1 - WindshieldWeatherState.Unit(snow) * 0.95f);

		internal static float Opacity(float water, float snow, float strength = 1)
		{
			float film = WindshieldWeatherState.Unit(water) * 5 * strength;
			float waterOpacity = film / (1 + film);
			return 1 - (1 - waterOpacity) * (1 - WindshieldWeatherState.Unit(snow) * 0.95f);
		}
	}
}
