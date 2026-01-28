using System;
using System.Collections.Generic; // Added for List if needed
using LivingSim.Core;
using LivingSim.World;

namespace LivingSim.Environment
{
    public class EnvironmentTickSystem
    {
        // FIX: This property was missing, causing CS1061 in WorldManager
        public WeatherType CurrentWeather { get; private set; } = WeatherType.Clear;
        
        private readonly Random _random = new Random();
        private int _weatherDuration = 0;

        public void Tick(Grid grid, Season season, long currentTick)
        {
            // 1. Update Weather (Change periodically)
            if (_weatherDuration <= 0)
            {
                ChangeWeather(season);
                // Weather lasts between 50 and 150 ticks (5-15 seconds)
                _weatherDuration = _random.Next(50, 150); 
            }
            _weatherDuration--;

            // 2. Determine effects based on weather
            float scentDecayRate = 0.5f; // Base decay

            switch (CurrentWeather)
            {
                case WeatherType.Rain:
                case WeatherType.Storm:
                    scentDecayRate = 5.0f; // Rain washes scents away fast
                    ReplenishWater(grid);  // Refills puddles
                    break;
                case WeatherType.Heatwave:
                    EvaporateWater(grid);  // Dries up puddles
                    break;
                case WeatherType.Snow:
                    // Snow logic placeholder
                    break;
            }

            // 3. Process the Grid (Decay Scents & Regrow Resources)
            ProcessGrid(grid, scentDecayRate);
        }

        private void ChangeWeather(Season season)
        {
            double roll = _random.NextDouble();
            
            switch (season)
            {
                case Season.Summer:
                    if (roll < 0.70) CurrentWeather = WeatherType.Clear;
                    else if (roll < 0.90) CurrentWeather = WeatherType.Rain;
                    else CurrentWeather = WeatherType.Heatwave;
                    break;
                case Season.Winter:
                    if (roll < 0.50) CurrentWeather = WeatherType.Clear;
                    else if (roll < 0.90) CurrentWeather = WeatherType.Snow;
                    else CurrentWeather = WeatherType.Fog;
                    break;
                case Season.Spring:
                case Season.Autumn:
                    if (roll < 0.50) CurrentWeather = WeatherType.Clear;
                    else if (roll < 0.85) CurrentWeather = WeatherType.Rain;
                    else CurrentWeather = WeatherType.Fog;
                    break;
            }
        }

        private void ProcessGrid(Grid grid, float scentDecayRate)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var cell = grid.GetCell(x, y);

                    // Decay Scents
                    if (cell.Scents.Count > 0)
                    {
                        foreach (var scent in cell.Scents)
                        {
                            scent.Strength -= scentDecayRate;
                        }
                        cell.Scents.RemoveAll(s => s.Strength <= 0);
                    }
                    
                    // Simple Regrowth (Grass/Food)
                    if (cell.Resource == ResourceType.None && cell.Terrain == TerrainType.Plains)
                    {
                         if (_random.NextDouble() < 0.001) cell.Resource = ResourceType.BerryBush;
                    }
                }
            }
        }

        private void ReplenishWater(Grid grid)
        {
            for (int i = 0; i < 20; i++)
            {
                int x = _random.Next(grid.Width);
                int y = _random.Next(grid.Height);
                var cell = grid.GetCell(x, y);
                if (cell != null && (cell.Terrain == TerrainType.Wetlands || cell.Terrain == TerrainType.River))
                {
                    cell.Water = Math.Min(cell.Water + 5.0f, 100f);
                }
            }
        }

        private void EvaporateWater(Grid grid)
        {
            for (int i = 0; i < 20; i++) 
            {
                int x = _random.Next(grid.Width);
                int y = _random.Next(grid.Height);
                var cell = grid.GetCell(x, y);
                if (cell.Water > 0)
                {
                    cell.AddWater(-2.0f);
                }
            }
        }
    }
}
