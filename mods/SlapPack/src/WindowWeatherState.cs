using System;
using System.Collections.Generic;
using System.Linq;

namespace HalfGripSlapPack
{
	/// <summary>Sparse, self-clearing deposits for glass without a wiper or defroster.</summary>
	internal sealed class WindowWeatherState
	{
		internal const int Width = 64, Height = 32;
		internal readonly float[] Water = new float[Width * Height], Snow = new float[Width * Height];
		internal readonly float[] Trail = new float[Width * Height];
		internal readonly bool[] Inside = new bool[Width * Height];
		internal readonly float MinX, MinY, SpanX, SpanY;
		internal readonly bool Rear;
		private readonly float[] waterNext = new float[Width * Height], snowNext = new float[Width * Height], edge = new float[Width * Height];
		private readonly float[] deposition = new float[Width * Height];
		private readonly IReadOnlyList<PanePoint> opening;
		private readonly Random random;
		private double rainDrops, snowDrops;
		private readonly Rivulet[] rivulets;
		private int nextRivulet;

		/// <summary>A bounded moving water head leaves a short-lived path fixed to the glass.</summary>
		private struct Rivulet
		{
			internal float X, Y, Life, Mobility;
		}

		internal WindowWeatherState(IReadOnlyList<PanePoint> opening, bool rear, int seed)
		{
			this.opening = opening; Rear = rear; random = new Random(seed);
			rivulets = new Rivulet[14];
			MinX = opening.Min(p => p.X); MinY = opening.Min(p => p.Y);
			SpanX = opening.Max(p => p.X) - MinX; SpanY = opening.Max(p => p.Y) - MinY;
			for (int y = 0; y < Height; y++)
				for (int x = 0; x < Width; x++)
				{
					int k = y * Width + x; PanePoint p = Point(x, y);
					Inside[k] = Contains(p);
					deposition[k] = 0.65f + 0.35f * (float)(Math.Sin(x * 0.23f) * Math.Cos(y * 0.31f));
					float distance = float.MaxValue;
					for (int i = 0; i < opening.Count; i++)
					{
						PanePoint a = opening[i], b = opening[(i + 1) % opening.Count];
						float dx = b.X - a.X, dy = b.Y - a.Y;
						distance = Math.Min(distance, Math.Abs(dx * (p.Y - a.Y) - dy * (p.X - a.X)) / (float)Math.Sqrt(dx * dx + dy * dy));
					}
					edge[k] = Math.Max(0, 1 - distance / (SpanY * 0.16f));
				}
		}

		internal PanePoint Point(int x, int y) => new PanePoint(MinX + (x + 0.5f) / Width * SpanX, MinY + (y + 0.5f) / Height * SpanY);

		/// <summary>Tests the actual aperture, including sloping edges when glass is lowered.</summary>
		internal bool Contains(PanePoint p)
		{
			for (int i = 0; i < opening.Count; i++)
			{
				PanePoint a = opening[i], b = opening[(i + 1) % opening.Count];
				if ((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X) < -0.000001f) return false;
			}
			return true;
		}

		/// <summary>Advects adhered water, sheds snow and deposits isolated impacts only on exposed glass.</summary>
		internal void Step(float dt, float rain, float snow, float incidence, float flowX, float flowY, float openFraction)
		{
			if (!(dt > 0) || float.IsInfinity(dt)) return;
			dt = Math.Min(dt, 0.1f);
			rain = WindshieldWeatherState.Unit(rain); snow = WindshieldWeatherState.Unit(snow);
			float sum = rain + snow; if (sum > 1) { rain /= sum; snow /= sum; }
			openFraction = WindshieldWeatherState.Unit(openFraction);
			flowX = Finite(flowX); flowY = Finite(flowY);
			AdvanceRivulets(dt, flowX, flowY, openFraction);
			float speed = (float)Math.Sqrt((double)flowX * flowX + (double)flowY * flowY);
			float limit = Rear ? 0.018f : 0.055f;
			if (speed > limit) { flowX *= limit / speed; flowY *= limit / speed; }
			float dx = flowX * dt * Width / SpanX, dy = flowY * dt * Height / SpanY;
			float waterKeep = (float)Math.Exp(-dt * (Rear ? 0.35f : 0.4f));
			float snowKeep = (float)Math.Exp(-dt * 0.10f);
			float exposure = (Rear ? 0.80f : 0.85f) + Math.Min(1.2f, Math.Max(0, Finite(incidence)) / 35);
			for (int y = 0; y < Height; y++)
				for (int x = 0; x < Width; x++)
				{
					int k = y * Width + x;
					// A shallow, irregular wet layer ties the beads together like the
					// windshield's water field. Drying bounds it; hidden glass stays dry.
					float film = Exposed(x, y, openFraction) ? rain * exposure * dt * 0.035f * deposition[k] : 0;
					waterNext[k] = Inside[k] ? Math.Min(0.85f, Sample(Water, x - dx, y - dy) * waterKeep + film) : 0;
					snowNext[k] = Inside[k] ? Sample(Snow, x - dx * 0.04f, y - dy * 0.04f) * snowKeep : 0;
				}
			Array.Copy(waterNext, Water, Water.Length); Array.Copy(snowNext, Snow, Snow.Length);
			// Rear glass is sheltered from forward travel. Reverse travel/crosswind
			// still increases normal exposure, without making a permanent opaque film.
			rainDrops += rain * exposure * (Rear ? 20 : 24) * dt;
			snowDrops += snow * exposure * 7 * dt;
			while (rainDrops >= 1) { rainDrops--; Deposit(false, openFraction); }
			while (snowDrops >= 1) { snowDrops--; Deposit(true, openFraction); }
		}

		private static float Finite(float x) => float.IsNaN(x) || float.IsInfinity(x) ? 0 : x;

		/// <summary>Preserves narrow runoff paths instead of diffusing moving beads into a faint film.</summary>
		private void AdvanceRivulets(float dt, float flowX, float flowY, float openFraction)
		{
			float keep = (float)Math.Exp(-dt * (Rear ? 0.95f : 0.85f));
			for (int k = 0; k < Trail.Length; k++) Trail[k] *= keep;
			double speed = Math.Sqrt((double)flowX * flowX + (double)flowY * flowY);
			float limit = Rear ? 0.09f : 0.20f;
			if (speed > limit) { float scale = (float)(limit / speed); flowX *= scale; flowY *= scale; }
			for (int i = 0; i < rivulets.Length; i++)
			{
				Rivulet drop = rivulets[i];
				if (drop.Life <= 0) continue;
				drop.Life -= dt;
				float dx = flowX * dt * Width / SpanX * drop.Mobility;
				float dy = flowY * dt * Height / SpanY * drop.Mobility;
				// Rasterize the whole traveled segment even during hitches; trails
				// follow signed gravity/airflow, with no looping texture animation.
				int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) * 2));
				for (int step = 0; step <= steps; step++)
					StampTrail(drop.X + dx * step / steps, drop.Y + dy * step / steps, openFraction);
				drop.X += dx; drop.Y += dy;
				if (!Exposed((int)Math.Floor(drop.X + 0.5f), (int)Math.Floor(drop.Y + 0.5f), openFraction)) drop.Life = 0;
				rivulets[i] = drop;
			}
		}

		/// <summary>Draws a narrow, feathered rivulet while keeping the surrounding aperture clear.</summary>
		private void StampTrail(float cx, float cy, float openFraction)
		{
			int ix = (int)Math.Floor(cx + 0.5f), iy = (int)Math.Floor(cy + 0.5f);
			for (int y = iy - 1; y <= iy + 1; y++)
				for (int x = ix - 1; x <= ix + 1; x++)
				{
					if (!Exposed(x, y, openFraction)) continue;
					float dx = x - cx, dy = y - cy;
					float value = Math.Max(0, 1 - (float)Math.Sqrt(dx * dx + dy * dy) / (Rear ? 1.8f : 1.45f));
					int k = y * Width + x;
					Trail[k] = Math.Max(Trail[k], value);
				}
		}
		private float At(float[] values, int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? 0 : values[y * Width + x];
		private float Sample(float[] values, float x, float y)
		{
			int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
			float fx = x - ix, fy = y - iy;
			return At(values, ix, iy) * (1 - fx) * (1 - fy) + At(values, ix + 1, iy) * fx * (1 - fy)
				+ At(values, ix, iy + 1) * (1 - fx) * fy + At(values, ix + 1, iy + 1) * fx * fy;
		}

		private void Deposit(bool snow, float openFraction)
		{
			int cx = random.Next(Width), cy = random.Next(Height), center = cy * Width + cx;
			if (!Exposed(cx, cy, openFraction)) return;
			if (snow && random.NextDouble() > 0.12f + edge[center] * 0.88f) return;
			for (int y = cy - 1; y <= cy + 1; y++)
				for (int x = cx - 1; x <= cx + 1; x++)
				{
					if (!Exposed(x, y, openFraction)) continue;
					int k = y * Width + x;
					float weight = x == cx && y == cy ? 1 : 0.18f;
					if (snow) Snow[k] = Math.Min(0.4f, Snow[k] + weight * (0.08f + edge[k] * 0.25f));
					else Water[k] = Math.Min(0.85f, Water[k] + weight * 0.70f);
				}
			if (!snow && random.NextDouble() < (Rear ? 0.70 : 0.75))
			{
				rivulets[nextRivulet] = new Rivulet { X = cx, Y = cy, Life = 2 + (float)random.NextDouble() * 2,
					Mobility = 0.75f + (float)random.NextDouble() * 0.5f };
				nextRivulet = (nextRivulet + 1) % rivulets.Length;
			}
		}

		private bool Exposed(int x, int y, float fraction)
		{
			if (x < 0 || y < 0 || x >= Width || y >= Height || !Inside[y * Width + x] || fraction >= 1) return false;
			PanePoint p = Point(x, y); p.Y -= fraction * SpanY;
			return Contains(p);
		}

		/// <summary>Hard local opacity ceiling remains safe even after indefinitely long storms.</summary>
		internal float Opacity(int k) => Math.Min(Rear ? 0.36f : 0.40f, Water[k] * 0.40f + Trail[k] * 0.46f + Snow[k] * 0.42f);

		/// <summary>Bright and dark water rims remain legible against both dark cabins and bright scenery.</summary>
		internal float Shine(int k)
		{
			int x = k % Width;
			float left = x > 0 ? Water[k - 1] + Trail[k - 1] : 0;
			float right = x + 1 < Width ? Water[k + 1] + Trail[k + 1] : 0;
			return Math.Max(0.08f, Math.Min(1, 0.80f + (left - right) * 1.8f));
		}
	}
}
