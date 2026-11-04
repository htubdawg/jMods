using System;

namespace HalfGripSlapPack
{
	/// <summary>Pure field-driven optics, projection visibility and texture-orientation rules.</summary>
	internal static class WindshieldRefractionMath
	{
		/// <summary>Uses water and impact gradients, without an independently animated wave.</summary>
		internal static PanePoint Offset(float water, float gradientX, float gradientY, float impactX, float impactY, float strength)
		{
			float wet = WindshieldWeatherState.Unit(water);
			float nx = gradientX * 1.5f + impactX * 0.4f, ny = gradientY * 1.5f + impactY * 0.4f;
			float x = wet * strength * nx / (0.15f + Math.Abs(nx));
			float y = wet * strength * ny / (0.15f + Math.Abs(ny));
			return new PanePoint(Math.Max(-0.02f, Math.Min(0.02f, x)), Math.Max(-0.02f, Math.Min(0.02f, y)));
		}

		/// <summary>Makes meaningful shifts survive nearest-neighbor sampling without amplifying numerical noise.</summary>
		internal static float PointOffset(float offset, int pixels)
		{
			if (pixels < 1) return 0;
			float texels = offset * pixels;
			if (Math.Abs(texels) < 0.125f) return 0;
			float whole = Math.Min((float)Math.Floor(pixels * 0.02f), Math.Max(1, (float)Math.Round(Math.Abs(texels))));
			return Math.Sign(texels) * whole / Math.Max(1, pixels);
		}

		/// <summary>Tests visible wet samples instead of mean wetness across the whole glass.</summary>
		internal static bool Visible(float water, float clipX, float clipY, float clipW, float near) =>
			water > 0.01f && clipW > near && Math.Abs(clipX) <= clipW && Math.Abs(clipY) <= clipW;

		/// <summary>Preserves viewport aspect when downsampling the same-frame scene image.</summary>
		internal static int CopyHeight(int width, float aspect) => Math.Max(64, (int)Math.Round(width / Math.Max(0.25f, aspect)));

		/// <summary>Accounts for render-graph texture origins in the built-in blitter.</summary>
		internal static void BlitScale(float scaleX, float scaleY, bool differentOrigins, out float x, out float y, out float offsetY)
		{
			x = scaleX; y = differentOrigins ? -scaleY : scaleY; offsetY = differentOrigins ? scaleY : 0;
		}
	}
}
