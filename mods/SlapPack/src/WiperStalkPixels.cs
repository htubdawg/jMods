namespace HalfGripSlapPack
{
    internal static class WiperStalkPixels
    {
        internal static byte[] Build(int speed)
        {
            byte[] pixels = SignalStalkPixels.Build();
            // Keep the molded grip, but replace turn arrows with a tiny windscreen/wiper
            // symbol and 0 / I / II / III speed marks on the rotating end sleeve.
            for (int y = 8; y <= 12; y++)
                for (int x = 6; x <= 18; x++) Set(pixels, x, y, 3);
            for (int x = 7; x <= 11; x++) Set(pixels, x, 12, 7);
            Set(pixels, 6, 11, 7); Set(pixels, 6, 10, 7);
            Set(pixels, 12, 11, 7); Set(pixels, 12, 10, 7);
            for (int x = 7; x <= 11; x++) Set(pixels, x, 9, 7);
            Set(pixels, 9, 10, 5); Set(pixels, 10, 11, 5);
            if (speed == 0)
            {
                for (int y = 9; y <= 11; y++) { Set(pixels, 15, y, 7); Set(pixels, 17, y, 7); }
                Set(pixels, 16, 9, 7); Set(pixels, 16, 11, 7);
            }
            else
                for (int bar = 0; bar < speed; bar++)
                    for (int y = 9; y <= 11; y++) Set(pixels, 14 + bar * 2, y, 7);
            // Circumferential witness mark advances with each dial detent.
            for (int y = 7; y <= 12; y++) Set(pixels, 3, y, 2);
            Set(pixels, 3, 12 - speed, 7);
            return pixels;
        }

        private static void Set(byte[] pixels, int x, int y, byte color) => pixels[y * SignalStalkLayout.TextureWidth + x] = color;

    }
}
