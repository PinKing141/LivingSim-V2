using System;
using System.Collections.Generic;
using System.Linq;
using LivingSim.Core;
using LivingSim.World;
using LivingSim.Animals; // <--- ADDED THIS

namespace LivingSim.Visualisation
{
    public class ConsoleVisualizer
    {
        private readonly Dictionary<Guid, ConsoleColor> _groupColors = new Dictionary<Guid, ConsoleColor>();

        public void Draw(Grid grid, IReadOnlyList<Animal> animals, SimulationClock clock, List<Dictionary<Species, int>> history, bool showStats)
        {
            Console.SetCursorPosition(0, 0);

            if (showStats)
            {
                DrawStatistics(history, grid.Width * 2, grid.Height);
            }
            else
            {
                var displayGrid = new (char character, ConsoleColor fg, ConsoleColor bg)[grid.Width, grid.Height];

                // 1. Draw Terrain and Resources
                for (int y = 0; y < grid.Height; y++)
                {
                    for (int x = 0; x < grid.Width; x++)
                    {
                        var cell = grid.GetCell(x, y);
                        displayGrid[x, y] = GetTerrainDisplay(cell, clock.CurrentTick);
                    }
                }

                // 2. Draw dead animals (carcasses)
                foreach (var animal in animals.Where(a => !a.IsAlive && a.CarcassFoodValue > 0))
                {
                    if (animal.X >= 0 && animal.X < grid.Width && animal.Y >= 0 && animal.Y < grid.Height)
                    {
                        var existingBg = displayGrid[animal.X, animal.Y].bg;
                        displayGrid[animal.X, animal.Y] = ('x', ConsoleColor.DarkGray, existingBg);
                    }
                }

                // 3. Draw living Animals on top
                foreach (var animal in animals.Where(a => a.IsAlive))
                {
                    if (animal.X >= 0 && animal.X < grid.Width && animal.Y >= 0 && animal.Y < grid.Height)
                    {
                        var (character, speciesColor) = GetAnimalDisplay(animal);
                        var existingBg = displayGrid[animal.X, animal.Y].bg;
                        displayGrid[animal.X, animal.Y] = (character, speciesColor, existingBg);
                    }
                }

                // 4. Render to console
                for (int y = 0; y < grid.Height; y++)
                {
                    for (int x = 0; x < grid.Width; x++)
                    {
                        var cell = displayGrid[x, y];
                        var fg = cell.fg;
                        var bg = cell.bg;

                        if (fg == bg) fg = ConsoleColor.White;

                        Console.ForegroundColor = fg;
                        Console.BackgroundColor = bg;
                        Console.Write(cell.character + " ");
                    }
                    Console.BackgroundColor = ConsoleColor.Black; 
                    Console.WriteLine();
                }
                Console.ResetColor();
            }

            // 5. Draw simulation stats
            var aliveAnimals = animals.Where(a => a.IsAlive).ToList();
            int totalAnimals = aliveAnimals.Count;

            var speciesSummary = aliveAnimals
                .GroupBy(a => a.Species)
                .OrderBy(g => g.Key.ToString());

            string tickInfo = $"Tick: {clock.CurrentTick,-5} | Day: {clock.CurrentDay,-3} | Season: {clock.CurrentSeason,-6}".PadRight(Console.WindowWidth > 0 ? Console.WindowWidth - 1 : 80);
            string popInfo = $"Population: {totalAnimals,-4} | " + string.Join(" | ", speciesSummary.Select(g => $"{g.Key}: {g.Count()}"));
            
            Console.WriteLine(tickInfo);
            Console.WriteLine(popInfo.PadRight(Console.WindowWidth > 0 ? Console.WindowWidth - 1 : 80));
        }

        private void DrawStatistics(List<Dictionary<Species, int>> history, int width, int height)
        {
            int maxPop = 0;
            if (history.Count > 0)
            {
                maxPop = history.Max(d => d.Values.Count > 0 ? d.Values.Max() : 0);
            }
            if (maxPop == 0) maxPop = 1;

            int count = history.Count;
            int startIndex = Math.Max(0, count - width);
            
            var buffer = new (char c, ConsoleColor color)[width, height];
            for(int y=0; y<height; y++)
                for(int x=0; x<width; x++)
                    buffer[x,y] = (' ', ConsoleColor.White);

            for (int i = startIndex; i < count; i++)
            {
                int x = i - startIndex;
                if (x >= width) break;

                var tickStats = history[i];
                foreach (var kvp in tickStats)
                {
                    Species species = kvp.Key;
                    int pop = kvp.Value;
                    
                    int scaledY = (int)(((float)pop / maxPop) * (height - 1));
                    int y = height - 1 - scaledY;
                    
                    if (y >= 0 && y < height)
                    {
                        var (c, _) = GetSpeciesDisplay(species);
                        buffer[x, y] = (c, GetSpeciesGraphColor(species));
                    }
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Console.ForegroundColor = buffer[x, y].color;
                    Console.Write(buffer[x, y].c);
                }
                Console.WriteLine();
            }
            Console.ResetColor();
            Console.WriteLine($"Graph Max Y: {maxPop}".PadRight(width));
        }

        private (char, ConsoleColor, ConsoleColor) GetTerrainDisplay(WorldCell cell, long currentTick)
        {
            var fg = cell.Terrain switch {
                TerrainType.Water => ConsoleColor.Blue,
                TerrainType.Mountain => ConsoleColor.Gray,
                TerrainType.Forest => cell.Food > 5 ? ConsoleColor.Green : ConsoleColor.DarkGreen,
                TerrainType.Plains => cell.Food > 3 ? ConsoleColor.Green : ConsoleColor.DarkYellow,
                TerrainType.Desert => ConsoleColor.Yellow,
                TerrainType.Tundra => ConsoleColor.Cyan,
                TerrainType.Wetlands => ConsoleColor.DarkCyan,
                TerrainType.River => ConsoleColor.Blue,
                _ => ConsoleColor.White,
            };

            var bg = ConsoleColor.Black;
            if (cell.TerritoryOwnerId.HasValue)
            {
                ConsoleColor groupColor = GetGroupColor(cell.TerritoryOwnerId.Value);
                bg = ToDarkColor(groupColor);
            }

            var ch = cell.Terrain switch {
                TerrainType.Water => '~',
                TerrainType.Mountain => '^',
                TerrainType.Forest => 'T',
                TerrainType.Plains => '.',
                TerrainType.Desert => '.',
                TerrainType.Tundra => '-',
                TerrainType.Wetlands => '"',
                TerrainType.River => '≈',
                _ => '?',
            };

            return (ch, fg, bg);
        }

        private (char, ConsoleColor) GetAnimalDisplay(Animal animal) => GetSpeciesDisplay(animal.Species);

        private (char, ConsoleColor) GetSpeciesDisplay(Species species) => species switch {
            Species.Rabbit => ('r', ConsoleColor.White),
            Species.Deer => ('D', ConsoleColor.White),
            Species.Boar => ('b', ConsoleColor.DarkYellow),
            Species.Bear => ('B', ConsoleColor.Yellow),
            Species.Fox => ('f', ConsoleColor.Red),
            Species.Wolf => ('W', ConsoleColor.DarkRed),
            _ => ('?', ConsoleColor.Magenta),
        };

        private ConsoleColor GetSpeciesGraphColor(Species species) => species switch {
            Species.Rabbit => ConsoleColor.Cyan,
            Species.Deer => ConsoleColor.Green,
            Species.Boar => ConsoleColor.DarkYellow,
            Species.Bear => ConsoleColor.Yellow,
            Species.Fox => ConsoleColor.Red,
            Species.Wolf => ConsoleColor.DarkRed,
            _ => ConsoleColor.Magenta,
        };

        private ConsoleColor GetGroupColor(Guid groupId)
        {
            if (!_groupColors.TryGetValue(groupId, out ConsoleColor color))
            {
                ConsoleColor[] palette = {
                    ConsoleColor.Cyan, ConsoleColor.Yellow, ConsoleColor.Red,
                    ConsoleColor.Green, ConsoleColor.Magenta, ConsoleColor.White
                };
                color = palette[_groupColors.Count % palette.Length];
                _groupColors[groupId] = color;
            }
            return color;
        }

        private ConsoleColor ToDarkColor(ConsoleColor color) => color switch
        {
            ConsoleColor.White => ConsoleColor.DarkGray,
            ConsoleColor.Cyan => ConsoleColor.DarkCyan,
            ConsoleColor.Yellow => ConsoleColor.DarkYellow,
            ConsoleColor.Red => ConsoleColor.DarkRed,
            ConsoleColor.Green => ConsoleColor.DarkGreen,
            ConsoleColor.Magenta => ConsoleColor.DarkMagenta,
            ConsoleColor.Blue => ConsoleColor.DarkBlue,
            _ => color,
        };
    }
}