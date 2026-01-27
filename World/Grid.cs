using System.Collections.Generic;

namespace LivingSim.World
{
    /// <summary>
    /// The world grid stores all cells in a 2D layout.
    /// </summary>
    public class Grid
    {
        private readonly WorldCell[,] _cells;

        public int Width { get; }
        public int Height { get; }

        public Grid(int width, int height)
        {
            Width = width;
            Height = height;
            _cells = new WorldCell[Width, Height];

            // Initialize all cells
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    _cells[x, y] = new WorldCell();
                }
            }
        }

        // Access a cell safely
        public WorldCell? GetCell(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return null;
            return _cells[x, y];
        }

        // Enumerate all cells
        public IEnumerable<WorldCell> GetAllCells()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    yield return _cells[x, y];
        }
    }
}
