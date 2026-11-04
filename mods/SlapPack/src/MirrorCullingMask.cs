namespace HalfGripSlapPack
{
    // Own only the bits this feature adds, preserving unrelated camera-mask changes on restore.
    internal sealed class MirrorCullingMask
    {
        private readonly int addedLayers;

        internal MirrorCullingMask(int originalMask, int interiorLayers)
        {
            addedLayers = interiorLayers & ~originalMask;
        }

        internal int Apply(int currentMask, bool includeInterior) =>
            includeInterior ? currentMask | addedLayers : currentMask & ~addedLayers;
    }
}
