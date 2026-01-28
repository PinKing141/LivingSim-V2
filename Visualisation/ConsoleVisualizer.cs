using System;
using System.Collections.Generic;
using System.Linq;
using LivingSim.Core;
using LivingSim.World;
using LivingSim.Animals;

namespace LivingSim.Visualisation
{
    public class ConsoleVisualizer
    {
        private readonly Dictionary<Guid, ConsoleColor> _groupColors = new Dictionary<Guid, ConsoleColor>();
        private (char chara, ConsoleColor fg, ConsoleColor bg)[,] _displayBuffer;

        // UPDATED: Now accepts Camera coordinates and Viewport size
        public void Draw(Grid grid, IReadOnlyList<Animal> animals, SimulationClock clock, List<Dictionary<Species, int>> history, bool showStats, bool showTerritories, int camX, int camY, int viewWidth, int viewHeight)
        {
            Console.SetCursorPosition(0, 0);

            // Resize buffer if the *Viewport* size changes (not the grid size)
            if (_displayBuffer == null || _displayBuffer.GetLength(0) != viewWidth || _displayBuffer.GetLength(1) != viewHeight)
            {
                _displayBuffer = new (char, ConsoleColor, ConsoleColor)[viewWidth, viewHeight];
            }

            if (showStats)
            {
                DrawStatistics(history, viewWidth, viewHeight);
            }
            else
            {
                // 1. Draw Terrain relative to Camera
                for (int y = 0; y < viewHeight; y++)
                {
                    for (int x = 0; x < viewWidth; x++)
                    {
                        // Calculate actual world coordinates
                        int worldX = camX + x;
                        int worldY = camY + y;

                        if (worldX >= 0 && worldX < grid.Width && worldY >= 0 && worldY < grid.Height)
                        {
                            var cell = grid.GetCell(worldX, worldY);
                            _displayBuffer[x, y] = GetCellDisplay(cell, showTerritories);
                        }
                        else
                        {
                            // Draw "Void" if off the map
                            _displayBuffer[x, y] = (' ', ConsoleColor.Black, ConsoleColor.Black);
                        }
                    }
                }

                // 2. Draw Dead Animals relative to Camera
                foreach (var animal in animals.Where(a => !a.IsAlive && a.CarcassFoodValue > 0))
                {
                    if (IsOnScreen(animal.X, animal.Y, camX, camY, viewWidth, viewHeight))
                    {
                        int screenX = animal.X - camX;
                        int screenY = animal.Y - camY;
                        var bg = _displayBuffer[screenX, screenY].bg;
                        _displayBuffer[screenX, screenY] = ('x', ConsoleColor.DarkGray, bg);
                    }
                }

                // 3. Draw Living Animals relative to Camera
                foreach (var animal in animals.Where(a => a.IsAlive))
                {
                    if (IsOnScreen(animal.X, animal.Y, camX, camY, viewWidth, viewHeight))
                    {
                        int screenX = animal.X - camX;
                        int screenY = animal.Y - camY;
                        
                        var (ch, color) = GetSpeciesDisplay(animal.Species);
                        var bg = _displayBuffer[screenX, screenY].bg;
                        
                        if (color == bg) color = (bg == ConsoleColor.White) ? ConsoleColor.Black : ConsoleColor.White;

                        _displayBuffer[screenX, screenY] = (ch, color, bg);
                    }
                }

                RenderBuffer(viewWidth, viewHeight);
            }

            string mode = showTerritories ? "[TERRITORY]" : "[NORMAL]";
            // Show Camera Coordinates in HUD
            DrawHUD(clock, animals, $"{mode} Cam:({camX},{camY})");
        }

        private bool IsOnScreen(int objX, int objY, int camX, int camY, int w, int h)
        {
            return objX >= camX && objX < camX + w && 
                   objY >= camY && objY < camY + h;
        }

        // --- Standard Render Logic (Unchanged) ---
        private void RenderBuffer(int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = _displayBuffer[x, y];
                    Console.ForegroundColor = cell.fg;
                    Console.BackgroundColor = cell.bg;
                    Console.Write(cell.chara + " "); 
                }
                Console.BackgroundColor = ConsoleColor.Black; 
                Console.WriteLine();
            }
            Console.ResetColor();
        }

        private (char, ConsoleColor, ConsoleColor) GetCellDisplay(WorldCell cell, bool showTerritories)
        {
            if (showTerritories && cell.TerritoryOwnerId.HasValue)
            {
                ConsoleColor groupColor = GetGroupColor(cell.TerritoryOwnerId.Value);
                return ('▒', groupColor, ToDarkColor(groupColor)); 
            }
            
            ConsoleColor bg = cell.Biome switch
            {
                Biome.Water => ConsoleColor.DarkBlue,
                Biome.Mountain => ConsoleColor.DarkGray,
                Biome.Desert => ConsoleColor.DarkYellow,
                Biome.Tundra => ConsoleColor.DarkCyan,
                Biome.Forest => ConsoleColor.DarkGreen,
                Biome.Wetlands => ConsoleColor.DarkMagenta,
                _ => ConsoleColor.Black
            };
            if (cell.Terrain == TerrainType.River) bg = ConsoleColor.Blue;

            char ch = ' ';
            ConsoleColor fg = ConsoleColor.White;

            if (cell.Resource != ResourceType.None)
            {
                switch (cell.Resource)
                {
                    case ResourceType.BerryBush:   ch = 'o'; fg = ConsoleColor.Red; break;
                    case ResourceType.IronOre:     ch = '*'; fg = ConsoleColor.Gray; break;
                    case ResourceType.GoldDeposit: ch = '$'; fg = ConsoleColor.Yellow; break;
                    case ResourceType.Boulder:     ch = 'O'; fg = ConsoleColor.DarkGray; break;
                    case ResourceType.AncientRuins:ch = 'Ω'; fg = ConsoleColor.Cyan; break;
                }
            }
            else
            {
                switch (cell.Terrain)
                {
                    case TerrainType.Mountain: ch = '^'; fg = ConsoleColor.White; break;
                    case TerrainType.Forest:   ch = 'T'; fg = ConsoleColor.Green; break;
                    case TerrainType.Water:    ch = '~'; fg = ConsoleColor.Cyan; break;
                    case TerrainType.River:    ch = '≈'; fg = ConsoleColor.White; break;
                    case TerrainType.Desert:   ch = '.'; fg = ConsoleColor.Yellow; break;
                    case TerrainType.Plains:   ch = ','; fg = ConsoleColor.DarkGreen; break;
                    default: ch = ' '; break;
                }
            }
            return (ch, fg, bg);
        }

        private void DrawHUD(SimulationClock clock, IReadOnlyList<Animal> animals, string extraInfo)
        {
            var alive = animals.Where(a => a.IsAlive).ToList();
            string line1 = $"Tick: {clock.CurrentTick} | Season: {clock.CurrentSeason} | {extraInfo}";
            string line2 = $"Pop: {alive.Count}";
            Console.WriteLine(line1.PadRight(Console.WindowWidth - 1));
            Console.WriteLine(line2.PadRight(Console.WindowWidth - 1));
        }

        // --- Helpers (Colors/Stats) ---
        private (char, ConsoleColor) GetSpeciesDisplay(Species species) => species switch {
            Species.Rabbit => ('r', ConsoleColor.White),
            Species.Deer => ('D', ConsoleColor.White),
            Species.Boar => ('b', ConsoleColor.DarkYellow),
            Species.Bear => ('B', ConsoleColor.Yellow),
            Species.Fox => ('f', ConsoleColor.Red),
            Species.Wolf => ('W', ConsoleColor.DarkRed),
            _ => ('?', ConsoleColor.Magenta),
        };

        private ConsoleColor GetGroupColor(Guid groupId)
        {
            if (!_groupColors.TryGetValue(groupId, out ConsoleColor color))
            {
                ConsoleColor[] palette = { ConsoleColor.Cyan, ConsoleColor.Yellow, ConsoleColor.Red, ConsoleColor.Green, ConsoleColor.Magenta, ConsoleColor.White };
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
        
        private ConsoleColor GetSpeciesGraphColor(Species species) => species switch {
            Species.Rabbit => ConsoleColor.Cyan,
            Species.Deer => ConsoleColor.Green,
            Species.Boar => ConsoleColor.DarkYellow,
            Species.Bear => ConsoleColor.Yellow,
            Species.Fox => ConsoleColor.Red,
            Species.Wolf => ConsoleColor.DarkRed,
            _ => ConsoleColor.Magenta,
        };

        private void DrawStatistics(List<Dictionary<Species, int>> history, int width, int height)
        {
            int maxPop = 0;
            if (history.Count > 0) maxPop = history.Max(d => d.Values.Count > 0 ? d.Values.Max() : 0);
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
                    int scaledY = (int)(((float)kvp.Value / maxPop) * (height - 1));
                    int y = height - 1 - scaledY;
                    if (y >= 0 && y < height) 
                        buffer[x, y] = ('*', GetSpeciesGraphColor(kvp.Key));
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
    }
}