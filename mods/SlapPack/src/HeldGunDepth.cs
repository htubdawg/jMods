namespace HalfGripSlapPack
{
	/// <summary>Compresses view-model depth about the near plane while preserving clip X/Y/W and near clipping.</summary>
	internal static class HeldGunDepth
	{
		internal const float Scale = 0.001f;

		/// <summary>Applies to clip Z directly, or to each coefficient of the projection's Z/W rows.</summary>
		internal static float Compress(float z, float w, float nearDepth) => z * Scale + w * nearDepth * (1 - Scale);
	}
}
