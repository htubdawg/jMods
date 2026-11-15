namespace HalfGripSlapPack
{
	/// <summary>
	/// Winds flat, generated cockpit meshes so their normals point along local −Z, the way the game's cockpit sprites face the
	/// cockpit camera. The fallback material is URP Lit, which does not flip normals on back faces: a quad facing +Z draws
	/// (culling is off) but receives no direct light, so it goes black under a cabin lamp in a dark map.
	/// </summary>
	internal static class CockpitFacing
	{
		/// <summary>Z of Unity's face normal, cross(b − a, c − a), for a triangle in local XY.</summary>
		internal static float NormalZ(float ax, float ay, float bx, float by, float cx, float cy) =>
			(bx - ax) * (cy - ay) - (by - ay) * (cx - ax);

		/// <summary>Swaps the last two indices of every triangle whose normal points toward +Z.</summary>
		internal static void Orient(int[] triangles, float[] xs, float[] ys)
		{
			for (int t = 0; t + 2 < triangles.Length; t += 3)
			{
				int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
				if (NormalZ(xs[a], ys[a], xs[b], ys[b], xs[c], ys[c]) > 0)
				{
					triangles[t + 1] = c;
					triangles[t + 2] = b;
				}
			}
		}

		/// <summary>Fan triangles over a convex outline, wound toward −Z.</summary>
		internal static int[] Fan(float[] xs, float[] ys)
		{
			int count = xs.Length;
			var triangles = new int[count < 3 ? 0 : (count - 2) * 3];
			for (int i = 1; i + 1 < count; i++)
			{
				triangles[(i - 1) * 3] = 0;
				triangles[(i - 1) * 3 + 1] = i;
				triangles[(i - 1) * 3 + 2] = i + 1;
			}
			Orient(triangles, xs, ys);
			return triangles;
		}
	}
}
