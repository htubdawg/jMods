namespace HalfGripSlapPack
{
	/// <summary>Selects an overlay layer visible in both existing world views without changing either camera.</summary>
	internal static class WindowWeatherLayer
	{
		internal static int Select(int originalLayer, int cockpitMask, int mirrorMask)
		{
			int common = cockpitMask & mirrorMask;
			if ((common & (1 << originalLayer)) != 0) return originalLayer;
			for (int layer = 0; layer < 32; layer++)
				if ((common & (1 << layer)) != 0) return layer;
			return -1;
		}
	}
}
