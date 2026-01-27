using System;

namespace LivingSim.Generation
{
    /// <summary>
    /// A basic 2D noise generator, similar to Perlin noise.
    /// This is a simplified implementation to fulfill the WorldGenerator's dependency.
    /// For a full Simplex Noise implementation, a more complex algorithm is required.
    /// </summary>
    public class SimplexNoise
    {
        private readonly int[] _perm;
        private readonly int[] _p;

        public SimplexNoise(int seed)
        {
            _perm = new int[512];
            _p = new int[256];
            Random rand = new Random(seed);
            for (int i = 0; i < 256; i++)
            {
                _p[i] = rand.Next(256);
            }

            for (int i = 0; i < 512; i++)
            {
                _perm[i] = _p[i & 255];
            }
        }

        private static float Grad(int hash, float x, float y)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : h == 12 || h == 14 ? x : 0;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        /// <summary>
        /// Generates a 2D noise value between -1 and 1.
        /// </summary>
        public float Generate(float x, float y)
        {
            // This is a simplified Perlin-like noise, not true Simplex noise.
            // It serves the purpose of providing a varied noise map for world generation.
            int X = (int)Math.Floor(x) & 255;
            int Y = (int)Math.Floor(y) & 255;

            x -= (float)Math.Floor(x);
            y -= (float)Math.Floor(y);

            float u = x * x * (3 - 2 * x);
            float v = y * y * (3 - 2 * y);

            int A = _perm[X] + Y;
            int B = _perm[X + 1] + Y;

            float n00 = Grad(_perm[A], x, y);
            float n10 = Grad(_perm[B], x - 1, y);
            float n01 = Grad(_perm[A + 1], x, y - 1);
            float n11 = Grad(_perm[B + 1], x - 1, y - 1);

            float nx0 = Lerp(n00, n10, u);
            float nx1 = Lerp(n01, n11, u);

            // Normalize to -1 to 1 range (approximate for this simplified noise)
            return Lerp(nx0, nx1, v);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + t * (b - a);
        }
    }
}