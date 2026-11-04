namespace HalfGripSlapPack
{
    // Hand-authored native-resolution pixel art. Index zero is transparent; seven opaque colors
    // provide outlines, broad stepped shading, rubber ribs and small stamped arrow markings.
    internal static class SignalStalkPixels
    {
        internal static readonly uint[] Palette = {
            0x00000000, 0x141619ff, 0x25282cff, 0x353940ff,
            0x4a4f56ff, 0x626871ff, 0x858a90ff, 0xb3b4a8ff
        };

        internal static byte[] Build()
        {
            var pixels = new byte[SignalStalkLayout.TextureWidth * SignalStalkLayout.TextureHeight];
            // Grip silhouette: stepped corners, inset face and a broad upper bevel.
            Rect(pixels, 6, 4, 21, 15, 1);
            Rect(pixels, 4, 5, 23, 14, 1);
            Rect(pixels, 2, 6, 25, 13, 1);
            Rect(pixels, 4, 6, 23, 13, 3);
            Rect(pixels, 5, 13, 22, 13, 5);
            Rect(pixels, 6, 14, 20, 14, 4);
            Pixel(pixels, 5, 13, 6);
            Pixel(pixels, 6, 14, 6);
            Rect(pixels, 4, 7, 4, 12, 5);
            Rect(pixels, 3, 8, 3, 11, 4);
            Rect(pixels, 5, 6, 23, 6, 2);
            Rect(pixels, 6, 7, 18, 7, 2);
            Rect(pixels, 6, 8, 18, 12, 2);
            Rect(pixels, 7, 8, 18, 11, 3);
            // Two molded ribs at the inner end; deliberate one-pixel highlights.
            Rect(pixels, 20, 7, 20, 12, 1);
            Rect(pixels, 21, 8, 21, 12, 5);
            Rect(pixels, 23, 7, 23, 12, 1);
            Rect(pixels, 24, 8, 24, 11, 4);
            // Small left/right turn markings, stamped into the face.
            Pixel(pixels, 8, 12, 7); Pixel(pixels, 7, 11, 7); Pixel(pixels, 6, 10, 7);
            Pixel(pixels, 7, 9, 7); Pixel(pixels, 8, 8, 7); Rect(pixels, 7, 10, 10, 10, 7);
            Pixel(pixels, 15, 12, 7); Pixel(pixels, 16, 11, 7); Pixel(pixels, 17, 10, 7);
            Pixel(pixels, 16, 9, 7); Pixel(pixels, 15, 8, 7); Rect(pixels, 13, 10, 16, 10, 7);
            // Slender metal tube: dark lower edge, broad midtone, single upper highlight.
            Rect(pixels, 26, 8, 70, 11, 1);
            Rect(pixels, 26, 9, 70, 10, 4);
            Rect(pixels, 27, 10, 70, 10, 5);
            Rect(pixels, 71, 9, 114, 11, 1);
            Rect(pixels, 71, 10, 114, 10, 4);
            Rect(pixels, 28, 9, 69, 9, 3);
            Rect(pixels, 26, 8, 27, 11, 2);
            // Small collar, then three increasingly tall corrugations and the mounting flange.
            Rect(pixels, 111, 7, 114, 13, 1);
            Rect(pixels, 112, 8, 113, 12, 5);
            Rect(pixels, 113, 8, 113, 11, 3);
            Rib(pixels, 115, 117, 6, 13);
            Rib(pixels, 118, 120, 4, 15);
            Rib(pixels, 121, 123, 3, 16);
            Rect(pixels, 124, 2, 125, 17, 1);
            Rect(pixels, 124, 3, 124, 16, 3);
            Pixel(pixels, 124, 16, 5);
            return pixels;
        }

        private static void Rib(byte[] pixels, int x0, int x1, int y0, int y1)
        {
            Rect(pixels, x0, y0, x1, y1, 1);
            Rect(pixels, x0, y0 + 1, x1 - 1, y1 - 1, 2);
            Rect(pixels, x0, y0 + 2, x0, y1 - 1, 4);
        }

        private static void Pixel(byte[] pixels, int x, int y, byte color) =>
            pixels[y * SignalStalkLayout.TextureWidth + x] = color;

        private static void Rect(byte[] pixels, int x0, int y0, int x1, int y1, byte color)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) Pixel(pixels, x, y, color);
        }
    }
}
