using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LivingSim.World;

namespace LivingSim.Generation
{
    /// <summary>
    /// Generates the world grid using multiple noise maps to create distinct biomes.
    /// </summary>
    public class WorldGenerator
    {
        private readonly Random _random;
        private Dictionary<Biome, HashSet<Biome>> _adjacencyRules;

        public WorldGenerator(Random random)
        {
            _random = random;
            InitializeRules();
        }

        /// <summary>
        /// Fills the grid with terrain and resources based on biome generation.
        /// </summary>
        public void Generate(Grid grid, Action<Grid>? onStep = null, int delayMs = 0)
        {
            int width = grid.Width;
            int height = grid.Height;
            var allBiomes = Enum.GetValues(typeof(Biome)).Cast<Biome>().Where(b => b != Biome.River).ToList();

            // 1. Initialize Superposition: All biomes are possible for every cell
            var possibilities = new List<Biome>[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    possibilities[x, y] = new List<Biome>(allBiomes);
                }
            }

            // 2. Wave Function Collapse Loop
            while (true)
            {
                // Find cell with lowest entropy (fewest possibilities > 1)
                int minEntropy = int.MaxValue;
                List<(int x, int y)> candidates = new List<(int x, int y)>();

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int count = possibilities[x, y].Count;
                        if (count > 1)
                        {
                            if (count < minEntropy)
                            {
                                minEntropy = count;
                                candidates.Clear();
                                candidates.Add((x, y));
                            }
                            else if (count == minEntropy)
                            {
                                candidates.Add((x, y));
                            }
                        }
                    }
                }

                if (candidates.Count == 0) break; // All cells collapsed

                // Collapse a random candidate
                var (cx, cy) = candidates[_random.Next(candidates.Count)];
                var chosenBiome = possibilities[cx, cy][_random.Next(possibilities[cx, cy].Count)];
                possibilities[cx, cy].Clear();
                possibilities[cx, cy].Add(chosenBiome);

                // Propagate constraints
                Stack<(int x, int y)> stack = new Stack<(int x, int y)>();
                stack.Push((cx, cy));

                while (stack.Count > 0)
                {
                    var (currentX, currentY) = stack.Pop();
                    var currentPossible = possibilities[currentX, currentY];

                    // Determine allowed neighbors based on current possibilities
                    HashSet<Biome> allowedNeighbors = new HashSet<Biome>();
                    foreach (var p in currentPossible)
                    {
                        if (_adjacencyRules.TryGetValue(p, out var neighbors))
                        {
                            foreach (var n in neighbors) allowedNeighbors.Add(n);
                        }
                    }

                    // Check all 4 neighbors
                    int[] dx = { 0, 0, 1, -1 };
                    int[] dy = { 1, -1, 0, 0 };

                    for (int i = 0; i < 4; i++)
                    {
                        int nx = currentX + dx[i];
                        int ny = currentY + dy[i];

                        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                        {
                            var neighborPossible = possibilities[nx, ny];
                            int originalCount = neighborPossible.Count;

                            // Remove impossible biomes
                            for (int j = neighborPossible.Count - 1; j >= 0; j--)
                            {
                                if (!allowedNeighbors.Contains(neighborPossible[j]))
                                {
                                    neighborPossible.RemoveAt(j);
                                }
                            }

                            if (neighborPossible.Count == 0)
                            {
                                // Contradiction occurred (no valid biomes left). 
                                // Fallback to Plains to prevent crash.
                                neighborPossible.Add(Biome.Plains);
                            }

                            if (neighborPossible.Count != originalCount)
                            {
                                stack.Push((nx, ny));
                            }
                        }
                    }
                }

                // Visualization hook
                if (onStep != null)
                {
                    for (int x = 0; x < width; x++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            if (possibilities[x, y].Count == 1)
                            {
                                var b = possibilities[x, y][0];
                                var c = grid.GetCell(x, y);
                                c.Biome = b;
                                c.Terrain = GetTerrainType(b);
                            }
                        }
                    }
                    onStep(grid);
                    if (delayMs > 0) Thread.Sleep(delayMs);
                }
            }

            // 3. Apply results to grid
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var cell = grid.GetCell(x, y);
                    var biome = possibilities[x, y].FirstOrDefault();
                    ApplyBiomeToCell(cell, biome);
                }
            }

            // 4. Generate Rivers (Post-Processing)
            GenerateRivers(grid);
        }

        private void InitializeRules()
        {
            _adjacencyRules = new Dictionary<Biome, HashSet<Biome>>();
            var allBiomes = Enum.GetValues(typeof(Biome)).Cast<Biome>().ToList();
            foreach (var b in allBiomes) _adjacencyRules[b] = new HashSet<Biome>();

            void AddRule(Biome a, Biome b)
            {
                _adjacencyRules[a].Add(b);
                _adjacencyRules[b].Add(a);
            }

            // Allow self-adjacency for grouping
            foreach (var b in allBiomes) AddRule(b, b);

            // Define transitions
            AddRule(Biome.Water, Biome.Wetlands);
            // Removed Water <-> Plains to force a Wetlands buffer zone

            AddRule(Biome.Wetlands, Biome.Plains);
            AddRule(Biome.Wetlands, Biome.Forest);

            AddRule(Biome.Plains, Biome.Forest);
            AddRule(Biome.Plains, Biome.Desert);
            AddRule(Biome.Plains, Biome.Tundra);

            AddRule(Biome.Forest, Biome.Tundra);
            AddRule(Biome.Forest, Biome.Mountain);

            AddRule(Biome.Desert, Biome.Mountain);

            AddRule(Biome.Tundra, Biome.Mountain);
        }

        private void GenerateRivers(Grid grid)
        {
            int riverCount = Math.Max(1, grid.Width / 5); 
            var mountains = new List<(int x, int y)>();
            
            // Find potential sources
            for(int x=0; x<grid.Width; x++)
                for(int y=0; y<grid.Height; y++)
                    if(grid.GetCell(x,y).Biome == Biome.Mountain) mountains.Add((x,y));

            if (mountains.Count == 0) return;

            for(int i=0; i<riverCount; i++)
            {
                var source = mountains[_random.Next(mountains.Count)];
                int cx = source.x;
                int cy = source.y;
                
                // Flow downhill towards water
                int maxLength = grid.Width + grid.Height;
                for(int step=0; step<maxLength; step++)
                {
                    // Find best neighbor (lowest "elevation" rank)
                    var neighbors = new List<(int x, int y, int rank)>();
                    int[] dx = {0,0,1,-1};
                    int[] dy = {1,-1,0,0};
                    
                    for(int j=0; j<4; j++)
                    {
                        int nx = cx + dx[j];
                        int ny = cy + dy[j];
                        if(nx>=0 && nx<grid.Width && ny>=0 && ny<grid.Height)
                        {
                            var cell = grid.GetCell(nx, ny);
                            if (cell.Biome == Biome.Mountain && step > 0) continue; // Don't go back up to mountain
                            
                            int rank = GetBiomeRank(cell.Biome);
                            neighbors.Add((nx, ny, rank));
                        }
                    }
                    
                    // Sort by rank (lower is better/wetter), then random to meander
                    var best = neighbors.OrderBy(n => n.rank).ThenBy(n => _random.Next()).FirstOrDefault();
                    
                    if (best.rank == 0 && neighbors.Count == 0) break; // Stuck
                    
                    cx = best.x;
                    cy = best.y;
                    
                    var targetCell = grid.GetCell(cx, cy);
                    
                    if (targetCell.Biome == Biome.Water) break; // Reached ocean
                    if (targetCell.Biome == Biome.River) break; // Joined another river
                    
                    // Carve river
                    targetCell.Biome = Biome.River;
                    targetCell.Terrain = TerrainType.River;
                    targetCell.AddWater(5.0f);
                    targetCell.AddFood(1.0f); // Some food near rivers
                }
            }
        }

        private int GetBiomeRank(Biome b)
        {
            // Simulated elevation/wetness rank for river flow
            return b switch {
                Biome.Water => 0,
                Biome.Wetlands => 1,
                Biome.River => 0,
                Biome.Plains => 2,
                Biome.Forest => 3,
                Biome.Tundra => 4,
                Biome.Desert => 4,
                Biome.Mountain => 5,
                _ => 10
            };
        }

        /// <summary>
        /// Sets a cell's terrain and initial resources based on its biome.
        /// </summary>
        private void ApplyBiomeToCell(WorldCell cell, Biome biome)
        {
            cell.Biome = biome;
            switch (biome)
            {
                case Biome.Forest:
                    cell.Terrain = TerrainType.Forest;
                    cell.AddFood(5.0f + (float)_random.NextDouble() * 2); // Forests are rich in food
                    cell.AddWater(2.0f + (float)_random.NextDouble());
                    cell.AddTimber(8.0f + (float)_random.NextDouble() * 2);
                    break;

                case Biome.Plains:
                    cell.Terrain = TerrainType.Plains;
                    cell.AddFood(2.0f + (float)_random.NextDouble()); // Plains have some food
                    cell.AddWater(1.0f + (float)_random.NextDouble());
                    break;

                case Biome.Mountain:
                    cell.Terrain = TerrainType.Mountain;
                    // Mountains are barren
                    break;

                case Biome.Water:
                    cell.Terrain = TerrainType.Water;
                    cell.AddWater(10.0f); // Water cells are full of water
                    break;

                case Biome.Desert:
                    cell.Terrain = TerrainType.Desert;
                    cell.AddFood((float)_random.NextDouble()); // Very little food
                    cell.AddWater(0); // No initial water
                    break;

                case Biome.Tundra:
                    cell.Terrain = TerrainType.Tundra;
                    cell.AddFood(1.0f + (float)_random.NextDouble()); // Scarce food
                    cell.AddWater(1.0f + (float)_random.NextDouble()); // Some water
                    break;

                case Biome.Wetlands:
                    cell.Terrain = TerrainType.Wetlands;
                    cell.AddFood(3.0f + (float)_random.NextDouble()); // Moderate food
                    cell.AddWater(8.0f + (float)_random.NextDouble()); // Abundant water
                    break;
                
                case Biome.River:
                    cell.Terrain = TerrainType.River;
                    cell.AddFood(1.0f);
                    cell.AddWater(5.0f);
                    break;
            }
        }

        private TerrainType GetTerrainType(Biome biome)
        {
            return biome switch
            {
                Biome.Forest => TerrainType.Forest,
                Biome.Plains => TerrainType.Plains,
                Biome.Mountain => TerrainType.Mountain,
                Biome.Water => TerrainType.Water,
                Biome.Desert => TerrainType.Desert,
                Biome.Tundra => TerrainType.Tundra,
                Biome.Wetlands => TerrainType.Wetlands,
                Biome.River => TerrainType.River,
                _ => TerrainType.Plains
            };
        }
    }
}
