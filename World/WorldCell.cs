using System;
using System.Collections.Generic;

namespace LivingSim.World
{
    public class WorldCell
    {
        // --- Core Properties ---
        public TerrainType Terrain { get; set; }
        public Biome Biome { get; set; }
        
        // --- NEW: Simulation Layers ---
        public float Height { get; set; }       // 0.0 (Deep Ocean) to 1.0 (Peak)
        public float Temperature { get; set; }  // 0.0 (Cold) to 1.0 (Hot)
        public float Moisture { get; set; }     // 0.0 (Dry) to 1.0 (Wet)
        public ResourceType Resource { get; set; } // The physical object on the tile
        public float MovementCost { get; set; } = 1.0f; // 1.0 = Normal speed. Higher = Slower.
        public float SoilQuality { get; set; } = 1.0f; // 0.0 = Barren. 2.0 = Super fertile.

        // --- Resource Values (Simulation) ---
        public float Food { get; private set; }
        public float Water { get; private set; }
        public float Timber { get; private set; }

        // --- Territory & Residents ---
        public Guid? TerritoryOwnerId { get; set; } 
        public float TerritoryStrength { get; set; }
        public long LastTerritoryRefreshTick { get; set; }
        public List<Scent> Scents { get; } = new List<Scent>();
        public bool HasResidents { get; set; }

        public WorldCell(TerrainType terrain = TerrainType.Plains)
        {
            Terrain = terrain;
            Biome = Biome.Plains;
            Resource = ResourceType.None; 
            
            // Default Simulation Values
            Height = 0.5f;
            Temperature = 0.5f;
            Moisture = 0.5f;
            
            Food = 0;
            Water = 0;
            Timber = 0;
            TerritoryOwnerId = null;
            TerritoryStrength = 0f;
            LastTerritoryRefreshTick = -1;
            HasResidents = false;
        }

        public void AddFood(float amount)   => Food = Math.Clamp(Food + amount, 0, 10);
        public void AddWater(float amount)  => Water = Math.Clamp(Water + amount, 0, 10);
        public void AddTimber(float amount) => Timber = Math.Clamp(Timber + amount, 0, 10);

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