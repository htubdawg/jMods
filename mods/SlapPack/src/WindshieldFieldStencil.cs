using System;

namespace HalfGripSlapPack
{
	/// <summary>Fixed water-grid addresses for a refraction vertex, calculated once for each body.</summary>
	internal readonly struct WindshieldFieldStencil
	{
		internal readonly int Center, Left, Right, Down, Up;

		/// <summary>Preserves the original coordinate arithmetic and clamped neighbor rules.</summary>
		internal WindshieldFieldStencil(PanePoint point, WindshieldWeatherState field)
		{
			const int width = WindshieldWeatherState.Width, height = WindshieldWeatherState.Height;
			int x = Math.Max(0, Math.Min(width - 1, (int)((point.X - field.MinX) / field.SpanX * width)));
			int y = Math.Max(0, Math.Min(height - 1, (int)((point.Y - field.MinY) / field.SpanY * height)));
			Center = y * width + x;
			Left = y * width + Math.Max(0, x - 1); Right = y * width + Math.Min(width - 1, x + 1);
			Down = Math.Max(0, y - 1) * width + x; Up = Math.Min(height - 1, y + 1) * width + x;
		}
	}
}
