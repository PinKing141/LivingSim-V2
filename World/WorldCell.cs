using System;
using System.Collections.Generic;

namespace LivingSim.World
{
    /// <summary>
    /// Represents a single cell (tile) in the world grid.
    /// Tracks terrain, resources, and residents.
    /// </summary>
    public class WorldCell
    {
        // Terrain type
        public TerrainType Terrain { get; set; }
        public Biome Biome { get; set; }

        // Resource properties (read-only outside)
        public float Food { get; private set; }
        public float Water { get; private set; }
        public float Timber { get; private set; }
        public Guid? TerritoryOwnerId { get; set; } // Null if unclaimed, otherwise the GroupId of the owner
        public float TerritoryStrength { get; set; }
        public long LastTerritoryRefreshTick { get; set; }
        public List<Scent> Scents { get; } = new List<Scent>();

        // Residents placeholder
        public bool HasResidents { get; set; }

        public WorldCell(TerrainType terrain = TerrainType.Plains)
        {
            Terrain = terrain;
            Biome = Biome.Plains; // Default biome
            Food = 0;
            Water = 0;
            Timber = 0;
            TerritoryOwnerId = null;
            TerritoryStrength = 0f;
            LastTerritoryRefreshTick = -1;
            HasResidents = false;
        }

        // --- Resource Modifiers ---
        public void AddFood(float amount)   => Food = Math.Clamp(Food + amount, 0, 10);
        public void AddWater(float amount)  => Water = Math.Clamp(Water + amount, 0, 10);
        public void AddTimber(float amount) => Timber = Math.Clamp(Timber + amount, 0, 10);

        // --- Resource Consumption ---
        public float ConsumeFood(float amount)
        {
            float consumed = Math.Min(Food, amount);
            Food -= consumed;
            return consumed;
        }
        public float ConsumeWater(float amount)
        {
            float consumed = Math.Min(Water, amount);
            Water -= consumed;
            return consumed;
        }

        public void DecayResources()
        {
            AddFood(-0.01f);
            AddWater(-0.01f);
            AddTimber(-0.005f);
        }
    }
}
