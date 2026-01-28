using System;
using System.Collections.Generic;
using System.Linq;
using LivingSim.World;

namespace LivingSim.Generation
{
    public class WorldGenerator
    {
        private readonly Random _random;
        private readonly SimplexNoise _noise; 

        public WorldGenerator(Random random)
        {
            _random = random;
            _noise = new SimplexNoise(random.Next());
        }

        public void Generate(Grid grid, Action<Grid>? onStep = null, int delayMs = 0)
        {
            int width = grid.Width;
            int height = grid.Height;

            // --- PASS 1: Height Map ---
            // Small scale = zoomed in. 0.02 is good for larger continents, smoother terrain.
            float heightScale = 0.02f;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // Generate noise -1 to 1, normalize to 0 to 1
                    float n = _noise.Generate(x * heightScale, y * heightScale);
                    grid.GetCell(x, y)!.Height = (n + 1) / 2.0f; 
                }
            }

            // --- PASS 2: Temperature & Moisture ---
            float tempScale = 0.04f;
            float moistureScale = 0.04f;
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var cell = grid.GetCell(x, y);

                    // 1. Base Temperature from Noise
                    float rawTemp = (_noise.Generate(x * tempScale + 100, y * tempScale + 100) + 1) / 2.0f;
                    
                    // 2. Latitude Adjustment (Equator is hot, poles are cold)
                    // Assuming y=0 is North Pole, y=Height is South Pole, Middle is Equator
                    float normalizedY = (float)y / height; 
                    float latitudeHeat = 1.0f - Math.Abs(normalizedY - 0.5f) * 2; 

                    // 3. Elevation Cooling (Higher = Colder)
                    float heightCooling = cell.Height * 0.4f; 

                    cell.Temperature = Math.Clamp((rawTemp * 0.3f) + (latitudeHeat * 0.7f) - heightCooling, 0, 1);
                    
                    // 4. Base Moisture
                    cell.Moisture = (_noise.Generate(x * moistureScale + 500, y * moistureScale + 500) + 1) / 2.0f;
                }
            }

            // --- PASS 2.5: Rain Shadows (Wind Simulation) ---
            // Simulates wind blowing Left to Right. Mountains block rain.
            for (int y = 0; y < height; y++)
            {
                float rainCloud = 0.8f; // Start with moisture coming from "ocean"
                for (int x = 0; x < width; x++)
                {
                    var cell = grid.GetCell(x, y);
                    
                    if (cell.Height > 0.75f) 
                    {
                        // Mountain blocks the rain
                        rainCloud -= 0.3f;
                        cell.Moisture += 0.2f; // Mountain gets wet (Snow)
                    }
                    else
                    {
                        // Drop rain
                        cell.Moisture = (cell.Moisture * 0.4f) + (rainCloud * 0.6f);
                        
                        // Recharge cloud over water
                        if (cell.Height < 0.3f) rainCloud += 0.15f;
                    }
                    
                    rainCloud = Math.Clamp(rainCloud, 0, 1);
                    cell.Moisture = Math.Clamp(cell.Moisture, 0, 1);
                }
            }

            // --- PASS 3: Biomes ---
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var cell = grid.GetCell(x, y);
                    AssignBiome(cell);
                    InitializeCellStats(cell);
                }
            }

            // --- PASS 4: Rivers ---
            GenerateRivers(grid);

            // --- PASS 5: Decorators ---
            GenerateDecorators(grid);

            // --- PASS 6: Gameplay Layers (Soil & Movement) ---
            CalculateGameplayLayers(grid);

            if (onStep != null) onStep(grid); 
        }

        private void AssignBiome(WorldCell cell)
        {
            float h = cell.Height;
            float t = cell.Temperature;
            float m = cell.Moisture;

            // 1. Ocean / Deep Water
            if (h < 0.35f)
            {
                cell.Biome = Biome.Water;
                cell.Terrain = TerrainType.Water;
                return;
            }

            // 2. High Altitude
            if (h > 0.85f)
            {
                cell.Biome = Biome.Mountain;
                cell.Terrain = TerrainType.Mountain;
                return;
            }

            // 3. Land Biomes (Whittaker Diagram simplified)
            if (t < 0.25f) // Cold
            {
                cell.Biome = Biome.Tundra;
                cell.Terrain = TerrainType.Tundra;
            }
            else if (t > 0.7f) // Hot
            {
                if (m < 0.3f) 
                {
                    cell.Biome = Biome.Desert; // Hot + Dry
                    cell.Terrain = TerrainType.Desert;
                }
                else if (m > 0.6f)
                {
                    cell.Biome = Biome.Wetlands; // Hot + Wet
                    cell.Terrain = TerrainType.Wetlands;
                }
                else
                {
                    cell.Biome = Biome.Plains; // Savanna
                    cell.Terrain = TerrainType.Plains;
                }
            }
            else // Temperate
            {
                if (m < 0.3f)
                {
                    cell.Biome = Biome.Plains; 
                    cell.Terrain = TerrainType.Plains;
                }
                else if (m > 0.6f)
                {
                    cell.Biome = Biome.Forest; 
                    cell.Terrain = TerrainType.Forest;
                }
                else
                {
                    // Transition zone
                    cell.Biome = _random.NextDouble() > 0.5 ? Biome.Forest : Biome.Plains;
                    cell.Terrain = cell.Biome == Biome.Forest ? TerrainType.Forest : TerrainType.Plains;
                }
            }
        }

        private void GenerateRivers(Grid grid)
        {
            int riverCount = (grid.Width * grid.Height) / 50; // 1 river per 50 tiles roughly
            
            for (int i = 0; i < riverCount; i++)
            {
                // Start a river at a random high, wet location
                int x = _random.Next(grid.Width);
                int y = _random.Next(grid.Height);
                var startCell = grid.GetCell(x, y);

                if (startCell.Height > 0.6f && startCell.Moisture > 0.5f)
                {
                    FlowRiver(grid, x, y);
                }
            }
        }

        private void FlowRiver(Grid grid, int startX, int startY)
        {
            int cx = startX;
            int cy = startY;

            // Prevent infinite loops
            for (int length = 0; length < 100; length++)
            {
                var current = grid.GetCell(cx, cy);
                
                // Don't overwrite ocean, but stop when we hit it
                if (current.Terrain == TerrainType.Water) break;

                // Make it a river
                current.Terrain = TerrainType.River;
                current.Biome = Biome.Water; // Technically water biome now
                current.AddWater(10); // Infinite water here

                // Find lowest neighbor
                float lowestHeight = current.Height;
                int nx = -1, ny = -1;

                // Check 8 neighbors
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        
                        int checkX = cx + dx;
                        int checkY = cy + dy;
                        
                        if (checkX >= 0 && checkX < grid.Width && checkY >= 0 && checkY < grid.Height)
                        {
                            var neighbor = grid.GetCell(checkX, checkY);
                            if (neighbor.Height < lowestHeight)
                            {
                                lowestHeight = neighbor.Height;
                                nx = checkX;
                                ny = checkY;
                            }
                        }
                    }
                }

                // If we found a lower spot, flow there
                if (nx != -1)
                {
                    cx = nx;
                    cy = ny;
                }
                else
                {
                    // Local minimum found (Valley/Pit). Form a Lake!
                    FormLake(grid, cx, cy);
                    break; 
                }
            }
        }

        private void GenerateDecorators(Grid grid)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var cell = grid.GetCell(x, y);
                    
                    // Only place resources on empty land
                    if (cell.Resource != ResourceType.None) continue;

                    double roll = _random.NextDouble();

                    if (cell.Terrain == TerrainType.Forest)
                    {
                        if (roll < 0.15) cell.Resource = ResourceType.BerryBush;
                    }
                    else if (cell.Terrain == TerrainType.Mountain)
                    {
                        if (roll < 0.05) cell.Resource = ResourceType.GoldDeposit;
                        else if (roll < 0.15) cell.Resource = ResourceType.IronOre;
                        else if (roll < 0.25) cell.Resource = ResourceType.Boulder;
                    }
                    else if (cell.Terrain == TerrainType.Plains || cell.Terrain == TerrainType.Tundra)
                    {
                        if (roll < 0.02) cell.Resource = ResourceType.AncientRuins;
                        else if (roll < 0.05) cell.Resource = ResourceType.Boulder;
                    }
                    else if (cell.Terrain == TerrainType.Desert)
                    {
                         if (roll < 0.03) cell.Resource = ResourceType.AncientRuins;
                    }
                }
            }
        }

        private void InitializeCellStats(WorldCell cell)
        {
            cell.AddFood(-100); cell.AddWater(-100); cell.AddTimber(-100);

            switch (cell.Biome)
            {
                case Biome.Water: cell.AddWater(10); break;
                case Biome.Desert: cell.AddFood(0.5f); break;
                case Biome.Tundra: cell.AddFood(1.0f); break;
                case Biome.Mountain: cell.AddTimber(2.0f); break;
                case Biome.Plains: cell.AddFood(5.0f); cell.AddTimber(1.0f); break;
                case Biome.Forest: cell.AddFood(3.0f); cell.AddTimber(8.0f); break;
                case Biome.Wetlands: cell.AddFood(4.0f); cell.AddWater(5.0f); break;
            }
        }

        // --- NEW GAMEPLAY LAYERS LOGIC ---

        private void CalculateGameplayLayers(Grid grid)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var cell = grid.GetCell(x, y);

                    // --- 1. Movement Cost (For Pathfinding) ---
                    switch (cell.Terrain)
                    {
                        case TerrainType.Mountain: 
                            cell.MovementCost = 3.0f; // Very hard to climb
                            break;
                        case TerrainType.Wetlands:
                            cell.MovementCost = 2.0f; // Slogging through mud
                            break;
                        case TerrainType.Forest:
                            cell.MovementCost = 1.5f; // Dense underbrush
                            break;
                        case TerrainType.River:
                            cell.MovementCost = 4.0f; // Swimming is slow/dangerous
                            break;
                        case TerrainType.Water:
                            cell.MovementCost = 10.0f; // Deep ocean (impassable without boat)
                            break;
                        default:
                            cell.MovementCost = 1.0f; // Plains/Desert are easy to walk
                            break;
                    }
                    // Hill Penalty: Walking uphill is harder
                    if (cell.Height > 0.6f) cell.MovementCost += 0.5f;


                    // --- 2. Soil Quality (For Farming) ---
                    switch (cell.Biome)
                    {
                        case Biome.Desert:
                            cell.SoilQuality = 0.1f; // Nothing grows
                            break;
                        case Biome.Mountain:
                            cell.SoilQuality = 0.2f; // Too rocky
                            break;
                        case Biome.Tundra:
                            cell.SoilQuality = 0.3f; // Too frozen
                            break;
                        case Biome.Forest:
                            cell.SoilQuality = 1.2f; // Good mulch
                            break;
                        case Biome.Plains:
                            cell.SoilQuality = 1.0f; // Standard farming
                            break;
                        case Biome.Wetlands:
                            cell.SoilQuality = 0.8f; // Too wet, but decent nutrients
                            break;
                    }

                    // Bonus: Rivers irrigate the land (Ancient Egypt style)
                    if (IsNextToFreshWater(grid, x, y) && cell.Biome != Biome.Desert)
                    {
                        cell.SoilQuality += 0.5f; 
                    }
                }
            }
        }

        private bool IsNextToFreshWater(Grid grid, int x, int y)
        {
            // Check 4 neighbors
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };
            for(int i=0; i<4; i++)
            {
                var neighbor = grid.GetCell(x + dx[i], y + dy[i]);
                if(neighbor != null && (neighbor.Terrain == TerrainType.River || neighbor.Biome == Biome.Water))
                    return true;
            }
            return false;
        }

        private void FormLake(Grid grid, int centerX, int centerY)
        {
            // Simple "Flood Fill" to create a small lake
            // In a real simulation, you'd fill until you find an outlet, 
            // but for now, just fill the immediate basin.
            
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue((centerX, centerY));
            
            int lakeSize = 0;
            int maxLakeSize = 15; // Don't flood the whole world

            while (queue.Count > 0 && lakeSize < maxLakeSize)
            {
                var (cx, cy) = queue.Dequeue();
                var cell = grid.GetCell(cx, cy);

                if (cell.Terrain == TerrainType.Water) continue;

                // Turn to water
                cell.Terrain = TerrainType.Water;
                cell.Biome = Biome.Water;
                cell.AddWater(10);
                lakeSize++;

                // Spread to neighbors that are at same height or lower
                // (This simulates water pooling up)
                int[] dx = { 0, 0, 1, -1 };
                int[] dy = { 1, -1, 0, 0 };

                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + dx[i];
                    int ny = cy + dy[i];
                    var neighbor = grid.GetCell(nx, ny);

                    if (neighbor != null && neighbor.Terrain != TerrainType.Water)
                    {
                        // If neighbor is low enough, flood it too
                        if (neighbor.Height <= cell.Height + 0.02f) 
                        {
                            queue.Enqueue((nx, ny));
                        }
                    }
                }
            }
        }
    }
}