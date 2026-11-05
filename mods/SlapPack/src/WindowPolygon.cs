using System;
using System.Collections.Generic;
using System.Linq;

namespace HalfGripSlapPack
{
    internal struct PanePoint
    {
        internal float X, Y;
        internal PanePoint(float x, float y) { X = x; Y = y; }
    }

    internal static class WindowPolygon
    {
        private static float Cross(PanePoint a, PanePoint b, PanePoint p) =>
            (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

        internal static List<PanePoint> Hull(IEnumerable<PanePoint> source)
        {
            var points = source.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            var hull = new List<PanePoint>();
            foreach (PanePoint point in points)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            int lower = hull.Count;
            for (int i = points.Count - 2; i >= 0; i--)
            {
                while (hull.Count > lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(points[i]);
            }
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        internal static List<PanePoint> Lower(IReadOnlyList<PanePoint> opening, float fraction)
        {
            if (opening.Count < 3 || fraction >= 1) return new List<PanePoint>();
            float height = opening.Max(p => p.Y) - opening.Min(p => p.Y);
            var result = opening.Select(p => new PanePoint(p.X, p.Y - Math.Max(0, fraction) * height)).ToList();
            return Clip(result, opening);
        }

        /// <summary>Clips a polygon against a convex, counterclockwise window aperture.</summary>
        internal static List<PanePoint> Clip(List<PanePoint> result, IReadOnlyList<PanePoint> opening)
        {
            // Keep each original glass triangle within the fixed door frame.
            for (int i = 0; i < opening.Count && result.Count > 0; i++)
            {
                PanePoint a = opening[i], b = opening[(i + 1) % opening.Count];
                var clipped = new List<PanePoint>();
                PanePoint previous = result[result.Count - 1];
                float previousSide = Cross(a, b, previous);
                foreach (PanePoint current in result)
                {
                    float side = Cross(a, b, current);
                    if ((side >= 0) != (previousSide >= 0))
                    {
                        float t = previousSide / (previousSide - side);
                        clipped.Add(new PanePoint(previous.X + (current.X - previous.X) * t, previous.Y + (current.Y - previous.Y) * t));
                    }
                    if (side >= 0) clipped.Add(current);
                    previous = current;
                    previousSide = side;
                }
                result = clipped;
            }
            return result;
        }
    }
}
