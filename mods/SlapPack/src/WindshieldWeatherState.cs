using System;
using System.Collections.Generic;
using System.Linq;

namespace HalfGripSlapPack
{
	// No Unity state or random globals: the same field is used by the renderer and tests.
	// Water and slush persist between impacts; only the rubber's radial interval clears.
	/// <summary>Simulates persistent deposits and rubber-blade clearing without Unity state.</summary>
	internal sealed class WindshieldWeatherState
	{
		internal const int Width = 160, Height = 80;
		internal readonly float[] Water = new float[Width * Height], Snow = new float[Width * Height];
		internal readonly float[] Impact = new float[Width * Height];
		/// <summary>Persistent droplet relief, independent of the clamped water quantity.</summary>
		internal readonly float[] Relief = new float[Width * Height];
		internal readonly bool[] Inside = new bool[Width * Height];
		internal readonly float MinX, MinY, SpanX, SpanY;
		private readonly float[][] angles = { new float[Width * Height], new float[Width * Height] };
		private readonly float[][] margins = { new float[Width * Height], new float[Width * Height] };
		private readonly float[] waterNext = new float[Width * Height], snowNext = new float[Width * Height], reliefNext = new float[Width * Height];
		private readonly float[] deposition = new float[Width * Height];
		private readonly WiperGeometry[] blades;
		private readonly Random random = new Random(4093);
		private double rainDrops, snowDrops, washerDrops;
		/// <summary>Invalidates cached optics only after a simulation change.</summary>
		internal uint Revision { get; private set; }

		internal WindshieldWeatherState(IReadOnlyList<PanePoint> opening)
		{
			MinX = opening.Min(p => p.X); MinY = opening.Min(p => p.Y);
			SpanX = opening.Max(p => p.X) - MinX; SpanY = opening.Max(p => p.Y) - MinY;
			blades = new[] { new WiperGeometry(opening, false), new WiperGeometry(opening, true) };
			for (int y = 0; y < Height; y++)
				for (int x = 0; x < Width; x++)
				{
					int k = y * Width + x; PanePoint p = Point(x, y);
					deposition[k] = 1 + 0.3f * (float)(Math.Sin(x * 0.19) * Math.Cos(y * 0.23));
					Inside[k] = true;
					for (int i = 0; i < opening.Count; i++)
					{
						PanePoint a = opening[i], b = opening[(i + 1) % opening.Count];
						if ((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X) < 0) Inside[k] = false;
					}
					for (int i = 0; i < 2; i++)
					{
						float dx = p.X - blades[i].Pivot.X, dy = p.Y - blades[i].Pivot.Y;
						double radius = Math.Sqrt(dx * dx + dy * dy);
						angles[i][k] = radius >= blades[i].Radius * 0.35 && radius <= blades[i].Radius
							? (float)((Math.Atan2(dy, dx) * 180 / Math.PI - WiperGeometry.ParkDegrees) / WiperGeometry.TravelDegrees)
							: -10;
						margins[i][k] = (float)(Math.Atan2(SpanX * 0.01125, Math.Max(radius, 0.001)) * 180 / Math.PI / WiperGeometry.TravelDegrees);
					}
				}
		}

		internal PanePoint Point(int x, int y) => new PanePoint(MinX + (x + 0.5f) / Width * SpanX, MinY + (y + 0.5f) / Height * SpanY);
		internal static float Unit(float x) => float.IsNaN(x) || float.IsInfinity(x) ? 0 : Math.Max(0, Math.Min(1, x));

		// Relative air, in glass-plane units/sec, includes gravity along the tilted glass.
		// Slush is heavier and moves much more slowly than water. Off-glass flow drains.
		internal void Deposit(float dt, float rain, float snow, float impactSpeed, float flowX, float flowY, bool washing, float washerFraction = 1)
		{
			if (!(dt > 0) || float.IsInfinity(dt)) return;
			rain = Unit(rain); snow = Unit(snow);
			if (float.IsNaN(impactSpeed) || float.IsInfinity(impactSpeed)) impactSpeed = 0;
			if (float.IsNaN(flowX) || float.IsInfinity(flowX)) flowX = 0;
			if (float.IsNaN(flowY) || float.IsInfinity(flowY)) flowY = 0;
			// This is adhered water, not airborne rain. Unbounded air-to-surface
			// coupling can drain the glass faster than rain replenishes it on a
			// favorable heading. Bound runoff without changing its direction.
			double flowSpeed = Math.Sqrt((double)flowX * flowX + (double)flowY * flowY);
			if (flowSpeed > 0.025)
			{
				float scale = (float)(0.025 / flowSpeed);
				flowX *= scale; flowY *= scale;
			}
			float total = rain + snow;
			if (total > 1) { rain /= total; snow /= total; }
			float exposure = 1 + Math.Min(2, Math.Max(0, impactSpeed) / 35);
			float dx = flowX * dt / SpanX * Width, dy = flowY * dt / SpanY * Height;
			// Impact exposure adds incoming water. It must not also act as a
			// speed-dependent dryer; runoff already accounts for air movement.
			float waterKeep = (float)Math.Exp(-dt * 0.012);
			float snowKeep = (float)Math.Exp(-dt * 0.003);
			float reliefKeep = (float)Math.Exp(-dt * 0.12);
			var waterDrift = new Drift(-dx, -dy);
			var snowDrift = new Drift(-dx * 0.07f, -dy * 0.07f);
			float rainFilm = rain * exposure * dt * 0.08f;
			float snowFilm = snow * exposure * dt * 0.12f;
			for (int y = 0; y < Height; y++)
				for (int x = 0; x < Width; x++)
				{
					int k = y * Width + x;
					if (!Inside[k]) continue;
					waterNext[k] = Math.Min(1, Sample(Water, x, y, waterDrift) * waterKeep + rainFilm * deposition[k]);
					snowNext[k] = Math.Min(1, Sample(Snow, x, y, snowDrift) * snowKeep + snowFilm * deposition[k]);
					reliefNext[k] = waterNext[k] > 0.001f ? Sample(Relief, x, y, waterDrift) * reliefKeep : 0;
					Impact[k] = Math.Max(0, Impact[k] - dt * 4);
				}
			Array.Copy(waterNext, Water, Water.Length); Array.Copy(snowNext, Snow, Snow.Length);
			Array.Copy(reliefNext, Relief, Relief.Length);
			rainDrops += rain * exposure * 85 * dt;
			snowDrops += snow * exposure * 55 * dt;
			while (rainDrops >= 1) { rainDrops--; Splat(random.Next(Width), random.Next(Height), false); }
			while (snowDrops >= 1) { snowDrops--; Splat(random.Next(Width), random.Next(Height), true); }
			if (washing)
			{
				washerDrops += 100 * dt * Unit(washerFraction);
				while (washerDrops >= 1)
				{
					washerDrops--;
					int blade = random.Next(2), drop = random.Next(6);
					PanePoint p = WasherSpray.Point(blades[blade].Pivot, blades[blade].Radius, SpanX, blade == 0, drop, 1);
					Splat((int)((p.X - MinX) / SpanX * Width), (int)((p.Y - MinY) / SpanY * Height), false);
				}
			}
			unchecked { Revision++; }
		}

		private readonly struct Drift
		{
			internal readonly int X, Y;
			internal readonly float A, B, C, D;
			internal Drift(float x, float y)
			{
				X = (int)Math.Floor(x); Y = (int)Math.Floor(y);
				float fx = x - X, fy = y - Y;
				A = (1 - fx) * (1 - fy); B = fx * (1 - fy); C = (1 - fx) * fy; D = fx * fy;
			}
		}
		private float Sample(float[] field, int x, int y, Drift drift)
		{
			int ix = x + drift.X, iy = y + drift.Y;
			if (ix >= 0 && iy >= 0 && ix + 1 < Width && iy + 1 < Height)
			{
				int k = iy * Width + ix;
				return field[k] * drift.A + field[k + 1] * drift.B + field[k + Width] * drift.C + field[k + Width + 1] * drift.D;
			}
			return At(field, ix, iy) * drift.A + At(field, ix + 1, iy) * drift.B
				+ At(field, ix, iy + 1) * drift.C + At(field, ix + 1, iy + 1) * drift.D;
		}
		private float At(float[] field, int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? 0 : field[y * Width + x];

		private void Splat(int cx, int cy, bool snow)
		{
			float radius = 2.5f;
			int extent = (int)Math.Ceiling(radius);
			for (int y = cy - extent; y <= cy + extent; y++)
				for (int x = cx - extent; x <= cx + extent; x++)
				{
					if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
					int k = y * Width + x; if (!Inside[k]) continue;
					float r = ((x - cx) * (x - cx) + (y - cy) * (y - cy)) / (radius * radius);
					if (r >= 1) continue;
					float[] field = snow ? Snow : Water;
					field[k] = Math.Min(1, field[k] + (1 - r) * (snow ? 0.5f : 0.7f));
					if (!snow) Relief[k] = Math.Min(2, Relief[k] + (1 - r) * 0.8f);
					Impact[k] = 1;
				}
		}

		internal void Wipe(float previous, float current)
		{
			if (Math.Abs(current - previous) < 0.000001f) return; // Parked blades never clear remotely.
			float lo = Math.Min(previous, current), hi = Math.Max(previous, current);
			for (int k = 0; k < Water.Length; k++)
			{
				if (!Inside[k]) continue;
				for (int i = 0; i < 2; i++)
					if (angles[i][k] >= lo - margins[i][k] && angles[i][k] <= hi + margins[i][k])
					{
						Water[k] = 0; Snow[k] = 0; Impact[k] = 0; Relief[k] = 0; break;
					}
			}
			unchecked { Revision++; }
		}
	}
}
