using System;
using System.Collections.Generic;
using System.Linq;

namespace HalfGripSlapPack
{
	/// <summary>
	/// Tandem wiper layout: both blades park pointing toward the passenger side and sweep up together.
	/// The driver blade is long enough to reach past the passenger pivot, so the two swept areas
	/// overlap in the middle of the glass instead of forming two separate fans.
	/// Each wiper is articulated like a real one: a straight arm runs from the pivot to a joint
	/// near the middle of the blade, and the blade sits at a fixed angle to the arm (tilted back
	/// toward the parked side), so it is not a single radial stick.
	/// </summary>
	internal sealed class WiperGeometry
	{
		internal readonly PanePoint Pivot;
		/// <summary>Scale of the whole wiper; the blade's outer end is about this far from the pivot.</summary>
		internal readonly float Radius;
		/// <summary>Arm angle when parked and its total travel. The blade itself trails/leads by a few degrees.</summary>
		internal const float ParkDegrees = 8, TravelDegrees = 88;
		internal const float DriverPivot = 0.18f, PassengerPivot = 0.48f;
		/// <summary>Arm length and blade extents past/short of the joint, in units of <see cref="Radius"/>.</summary>
		internal const float ArmLength = 0.58f, BladeOuter = 0.42f, BladeInner = 0.36f;
		/// <summary>Blade angle relative to the arm; negative tilts it back toward the parked side.</summary>
		internal const float BladeTiltDegrees = -11;
		internal static readonly float BladeCos = (float)Math.Cos(BladeTiltDegrees * Math.PI / 180);
		internal static readonly float BladeSin = (float)Math.Sin(BladeTiltDegrees * Math.PI / 180);

		/// <summary>Distances of the blade's inner and outer ends from the pivot.</summary>
		internal float InnerDistance => Radius * Length(ArmLength - BladeInner * BladeCos, -BladeInner * BladeSin);
		internal float OuterDistance => Radius * Length(ArmLength + BladeOuter * BladeCos, BladeOuter * BladeSin);

		internal WiperGeometry(IReadOnlyList<PanePoint> opening, bool passenger)
		{
			if (opening.Count < 3) throw new ArgumentException("Windshield opening is empty.");
			float minX = opening.Min(p => p.X), maxX = opening.Max(p => p.X);
			float minY = opening.Min(p => p.Y), maxY = opening.Max(p => p.Y);
			float width = maxX - minX, height = maxY - minY;
			if (width < 0.5f || height < 0.3f) throw new ArgumentException("Windshield opening is too small.");
			float x = minX + width * (passenger ? PassengerPivot : DriverPivot);
			float bottom = float.PositiveInfinity;
			for (int i = 0; i < opening.Count; i++)
			{
				PanePoint a = opening[i], b = opening[(i + 1) % opening.Count];
				if (x < Math.Min(a.X, b.X) || x > Math.Max(a.X, b.X) || Math.Abs(b.X - a.X) < 0.00001f) continue;
				bottom = Math.Min(bottom, a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X));
			}
			Pivot = new PanePoint(x, bottom + height * 0.035f);
			float radius = Math.Min(width * 0.55f, height * 0.9f);
			// Constrain the whole sweep of the joint and both blade ends, not just the parked
			// blade. Every point scales with the radius, so each gives a ray limit. The margin
			// also clears the rubber blade's width and the pillars of trapezoidal windshields.
			var features = new[]
			{
				(x: ArmLength, y: 0f),
				(x: ArmLength + BladeOuter * BladeCos, y: BladeOuter * BladeSin),
				(x: ArmLength - BladeInner * BladeCos, y: -BladeInner * BladeSin),
			};
			for (int degree = 0; degree <= (int)TravelDegrees; degree++)
				foreach (var feature in features)
				{
					float scale = Length(feature.x, feature.y);
					double angle = (ParkDegrees + degree) * Math.PI / 180 + Math.Atan2(feature.y, feature.x);
					float dx = (float)Math.Cos(angle), dy = (float)Math.Sin(angle);
					for (int i = 0; i < opening.Count; i++)
					{
						PanePoint a = opening[i], b = opening[(i + 1) % opening.Count];
						float ex = b.X - a.X, ey = b.Y - a.Y;
						float atPivot = ex * (Pivot.Y - a.Y) - ey * (Pivot.X - a.X);
						float alongRay = ex * dy - ey * dx;
						if (alongRay < -0.00001f) radius = Math.Min(radius, -atPivot / alongRay * 0.94f / scale);
					}
				}
			if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < width * 0.12f)
				throw new ArgumentException("Cannot fit a safe wiper sweep in this opening.");
			Radius = radius;
		}

		private static float Length(float x, float y) => (float)Math.Sqrt(x * x + y * y);

		internal PanePoint Point(float radial, float sideways, float sweep)
		{
			SweepAxes(sweep, out float c, out float s);
			return Point(radial, sideways, c, s);
		}

		/// <summary>Calculates the shared rotation once, rather than for every rubber/arm corner.</summary>
		internal static void SweepAxes(float sweep, out float c, out float s)
		{
			double angle = (ParkDegrees + TravelDegrees * Math.Max(0, Math.Min(1, sweep))) * Math.PI / 180;
			c = (float)Math.Cos(angle); s = (float)Math.Sin(angle);
		}

		/// <summary>Uses the same float operations with already calculated axes. Radial runs along the arm.</summary>
		internal PanePoint Point(float radial, float sideways, float c, float s)
		{
			return new PanePoint(Pivot.X + radial * c - sideways * s, Pivot.Y + radial * s + sideways * c);
		}

		/// <summary>
		/// A point in the blade's own frame: <paramref name="along"/> runs from the arm joint toward
		/// the blade's outer end and <paramref name="across"/> is perpendicular to it (both in pane units).
		/// </summary>
		internal PanePoint BladePoint(float along, float across, float c, float s)
		{
			return Point(Radius * ArmLength + along * BladeCos - across * BladeSin, along * BladeSin + across * BladeCos, c, s);
		}

		/// <summary>
		/// The sweep (0 parked, 1 fully up; may fall outside that range) at which the blade passes over
		/// <paramref name="p"/>, or false when <paramref name="p"/> is nearer or farther than the blade reaches.
		/// </summary>
		internal bool SweepAt(PanePoint p, out float sweep)
		{
			sweep = 0;
			double dx = p.X - Pivot.X, dy = p.Y - Pivot.Y, distance = Math.Sqrt(dx * dx + dy * dy);
			if (distance < InnerDistance || distance > OuterDistance) return false;
			// Blade points are joint + t * direction; the perpendicular foot lies behind the inner end,
			// so distance grows along the whole blade and each distance has exactly one blade point.
			double a = Radius * ArmLength, r = distance;
			double t = -a * BladeCos + Math.Sqrt(Math.Max(0, r * r - a * a * BladeSin * BladeSin));
			double lead = Math.Atan2(t * BladeSin, a + t * BladeCos);
			sweep = (float)(((Math.Atan2(dy, dx) - lead) * 180 / Math.PI - ParkDegrees) / TravelDegrees);
			return true;
		}
	}
}
