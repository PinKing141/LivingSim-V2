using System;

namespace LivingSim.Config
{
    public static class SimConfig
    {
        // --- Animal Constants ---
        public const float BaseMaxHunger = 20f;
        public const float BaseMaxThirst = 10f;
        public const float BaseMaxHealth = 10f;

        public const int MinReproductionAge = 10;
        public const float ReproductionHungerThreshold = 1.0f;
        public const float BaseReproductionCostPerOffspring = 3.0f;
        public const float BaseReproductionChance = 0.01f;
        public const int BaseOffspringCount = 1;
        public const int MaxGroupSize = 5;

        // --- Behavior Weights ---
        public const float CohesionWeight = 0.5f;
        public const float SeparationWeight = 1.5f;
        public const float FoodSeekingWeight = 1.5f;
        public const float PreySeekingWeight = 2.5f;
        public const float WaterSeekingWeight = 1.5f;
        public const float TerritorialAggressionWeight = 2.5f;
        public const float TerritorialAversionWeight = 1.8f;
        public const float TerritoryStayWeight = 1.0f;
        public const float InjuredHomingWeight = 3.0f;
        public const float HomingWeight = 0.3f;
        public const float ScentTrackingWeight = 1.0f;
        public const float ScentAversionWeight = 1.5f;
        public const float BiomeSeekingWeight = 1.2f;
        public const float SeparationDistance = 2.0f;
        public const float LingeringFearWeight = 1.0f;
        public const float ShelterSeekingWeight = 2.5f;

        // --- Memory ---
        public const int MemoryDurationTicks = 10;

        // --- Stamina ---
        public const float StaminaRegenRate = 0.5f;
        public const float StaminaDrainRate = 2.0f;

        // --- Metabolism ---
        public const float BaseMetabolism = 0.01f;
        public const float SizeMetabolismFactor = 0.05f;
        public const float SpeedMetabolismFactor = 0.02f;
        public const float VisionMetabolismFactor = 0.01f;
        public const float AggressionMetabolismFactor = 0.01f;

        // --- Movement ---
        public const float MovementHungerCost = 0.005f;
        public const float MovementThirstCost = 0.01f;

        // --- Eating ---
        public const float HerbivoreEatAmount = 2.0f;

        // --- Plague ---
        public const int PlagueCheckInterval = 100;
        public const float PlagueThresholdRatio = 0.20f;
        public const float PlagueChance = 0.05f;
        public const float PlagueMortalityRate = 0.4f;

        // --- Environment ---
        public const float ScentDecayRateBase = 0.5f;
        public const float ScentDecayRateRain = 5.0f;
        public const float WaterReplenishAmount = 5.0f;
        public const float WaterEvaporateAmount = 2.0f;
        public const float RegrowthChanceBase = 0.001f;
        public const float RegrowthChanceNeighbor = 0.05f;
    }
}
