using System;
using System.Linq;

namespace LivingSim.Generation
{
    public class SimplexNoise
    {
        private readonly int[] _perm;

        public SimplexNoise(int seed)
        {
            _perm = new int[512];
            var random = new Random(seed);

            // 1. Create an ordered array 0..255
            var p = Enumerable.Range(0, 256).ToArray();

            // 2. Fisher-Yates Shuffle (FIXED: This creates natural randomness)
            for (int i = 0; i < 256; i++)
            {
                int swapIndex = random.Next(i, 256);
                (p[i], p[swapIndex]) = (p[swapIndex], p[i]);
            }

            // 3. Duplicate for overflow handling
            for (int i = 0; i < 512; i++)
            {
                _perm[i] = p[i & 255];
            }
        }

        private static float Grad(int hash, float x, float y)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : h == 12 || h == 14 ? x : 0;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        public float Generate(float x, float y)
        {
            int X = (int)Math.Floor(x) & 255;
            int Y = (int)Math.Floor(y) & 255;

            x -= (float)Math.Floor(x);
            y -= (float)Math.Floor(y);

            float u = Fade(x);
            float v = Fade(y);

            int A = _perm[X] + Y;
            int B = _perm[X + 1] + Y;

            float n00 = Grad(_perm[A], x, y);
            float n10 = Grad(_perm[B], x - 1, y);
            float n01 = Grad(_perm[A + 1], x, y - 1);
            float n11 = Grad(_perm[B + 1], x - 1, y - 1);

            float nx0 = Lerp(n00, n10, u);
            float nx1 = Lerp(n01, n11, u);

            return Lerp(nx0, nx1, v);
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Lerp(float a, float b, float t) => a + t * (b - a);
    }
}
