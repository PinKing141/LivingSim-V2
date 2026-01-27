using System;
using System.Collections.Generic;
using System.Linq;

using LivingSim.Core; // Added for Season enum
using LivingSim.World;
using SpeciesEnum = LivingSim.World.Species;

namespace LivingSim.Animals
{
    public class Animal
    {
        public AnimalType Type { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }

        private const float BaseMaxHunger = 10f;
        private const float BaseMaxThirst = 10f;
        private const float BaseMaxHealth = 10f;

        public const int MinReproductionAge = 10;
        private const float ReproductionHungerThreshold = 3.0f; // Must be less hungry than this to reproduce
        private const float BaseReproductionCostPerOffspring = 3.0f; // Reduced cost to make reproduction less taxing
        public const float BaseReproductionChance = 0.05f; // Increased to 5% to allow for more population recovery.
        private const int BaseOffspringCount = 1; // Default number of offspring for an animal with Fertility = 1.0
        public const int MaxGroupSize = 5; // The maximum number of animals in a social group.

        // --- Behavior Weights ---
        private const float CohesionWeight = 0.5f;      // How strongly animals stick together
        private const float SeparationWeight = 1.5f;    // How strongly animals avoid crowding
        private const float FoodSeekingWeight = 1.5f;   // How strongly herbivores seek food
        private const float PreySeekingWeight = 2.5f;   // How strongly carnivores seek prey
        private const float WaterSeekingWeight = 1.5f;  // How strongly animals seek water
        private const float TerritorialAggressionWeight = 2.5f; // How strongly animals defend territory
        private const float TerritorialAversionWeight = 1.8f; // How strongly animals avoid entering rival territory with owners present
        private const float TerritoryStayWeight = 1.0f; // How strongly animals prefer to stay within their own territory
        private const float InjuredHomingWeight = 3.0f; // Very strong pull to den when injured
        private const float HomingWeight = 0.3f; // How strongly animals are drawn to their den
        private const float ScentTrackingWeight = 1.0f; // How strongly predators follow scents
        private const float ScentAversionWeight = 1.5f; // How strongly prey avoid predator scents
        private const float BiomeSeekingWeight = 1.2f;  // How strongly animals seek preferred biomes
        private const float SeparationDistance = 2.0f;  // How close is "too close" for separation
        private const float LingeringFearWeight = 1.0f; // How strongly animals avoid places where predators were recently seen

        // --- Memory ---
        private (int X, int Y)? _lastKnownFoodLocation;
        private long _lastFoodSightingTick = -1;
        private (int X, int Y)? _lastKnownPredatorLocation;
        private long _lastPredatorSightingTick = -1;
        private (int X, int Y)? _lastKnownWaterLocation;
        private long _lastWaterSightingTick = -1;
        private const int MemoryDurationTicks = 10; // How many ticks an animal remembers a location.
        private bool _seekingWater = false; // Persistence flag to prevent dithering

        // --- Stamina ---
        public float Stamina { get; private set; }
        private const float StaminaRegenRate = 0.5f;
        private const float StaminaDrainRate = 2.0f;

        public float Health { get; private set; }
        public float Thirst { get; private set; }
        public float Hunger { get; private set; }
        public int VisionRange { get; private set; }
        public int MaxAge { get; private set; }
        public int Speed { get; private set; }
        public float Aggression { get; private set; } // For carnivores
        public float Size { get; private set; }
        public float Fertility { get; private set; }
        public float Social { get; private set; }
        public float TerritoryStrength { get; private set; }
        public float Courage { get; private set; }
        public int DenX { get; private set; }
        public int DenY { get; private set; }
        public Species Species { get; private set; }
        public bool IsNocturnal { get; private set; }
        public HashSet<Biome> PreferredBiomes { get; private set; }
        public bool CanHibernate { get; private set; }
        public Guid GroupId { get; private set; }
        public bool IsHibernating { get; private set; }
        public bool IsConsumed { get; private set; }
        public float CarcassFoodValue { get; private set; }
        private readonly Random _random;
        public int Age { get; private set; }
        public bool IsAlive { get; private set; } = true;

        // Constructor for initial spawning
        public Animal(Species species, int x, int y, Random random)
        {
            _random = random;
            Species = species;
            X = x;
            Y = y;
            Thirst = 0;
            Hunger = 0;
            Age = 0;

            // Determine AnimalType from Species
            Type = GetAnimalTypeFromSpecies(species);

            // Set base stats for the species
            (MaxAge, VisionRange, Speed, Aggression, Size, Fertility, IsNocturnal, CanHibernate, Social, TerritoryStrength, Courage) = GetBaseStatsForSpecies(species);
            PreferredBiomes = GetPreferredBiomesForSpecies(species);

            DenX = x; // The founding location is the first den.
            DenY = y;
            GroupId = Guid.NewGuid(); // A newly spawned animal founds a new group
            IsHibernating = false;
            Stamina = MaxStamina;
            Health = MaxHealth;
        }

        // Private constructor for reproduction (genetics)
        private Animal(Animal parent, Random random)
        {
            _random = random;
            Species = parent.Species; // Offspring inherit the parent's species
            Type = parent.Type;
            X = parent.X;
            Y = parent.Y;
            Thirst = 0;
            Hunger = 0;
            Age = 0;
            Health = MaxHealth;
            Stamina = MaxStamina;
            GroupId = parent.GroupId; // Offspring inherit the parent's group ID (for now, could change for new groups)
            DenX = parent.DenX; // Offspring share the same den.
            DenY = parent.DenY;
            PreferredBiomes = parent.PreferredBiomes; // Inherit biome preference directly

            // --- Genetics: Inherit and Mutate ---
            const int visionMutationAmount = 1; // Vision can change by +/- 1
            const int ageMutationAmount = 5;    // MaxAge can change by +/- 5
            const int speedMutationAmount = 1;  // Speed can change by +/- 1
            const float aggressionMutationAmount = 0.2f; // Aggression can change by +/- 0.2f
            const float sizeMutationAmount = 0.1f; // Size can change by +/- 0.1
            const float nocturnalMutationChance = 0.05f; // 5% chance to flip nocturnal trait
            const float fertilityMutationAmount = 0.1f; // Fertility can change by +/- 0.1
            const float socialMutationAmount = 0.2f; // Social desire can change by +/- 0.2
            const float territoryStrengthMutationAmount = 0.1f; // Territory strength can change by +/- 0.1
            const float courageMutationAmount = 0.1f; // Courage can change by +/- 0.1
            const float hibernateMutationChance = 0.05f; // 5% chance to gain/lose hibernation

            // Inherit and mutate VisionRange, with a minimum of 1
            VisionRange = Math.Max(1, parent.VisionRange + _random.Next(-visionMutationAmount, visionMutationAmount + 1));

            // Inherit and mutate MaxAge, with a minimum of 10
            MaxAge = Math.Max(10, parent.MaxAge + _random.Next(-ageMutationAmount, ageMutationAmount + 1));

            // Inherit and mutate Speed, with a minimum of 1
            Speed = Math.Max(1, parent.Speed + _random.Next(-speedMutationAmount, speedMutationAmount + 1));

            // Inherit and mutate Aggression for carnivores
            if (Type == AnimalType.Carnivore)
            {
                Aggression = Math.Max(0.1f, parent.Aggression + (_random.NextSingle() * 2f - 1f) * aggressionMutationAmount);
            }

            // Inherit and mutate Size, with a minimum of 0.5
            Size = Math.Max(0.5f, parent.Size + (_random.NextSingle() * 2f - 1f) * sizeMutationAmount);

            // Inherit and mutate Nocturnal trait
            IsNocturnal = parent.IsNocturnal;
            if (_random.NextSingle() < nocturnalMutationChance) {
                IsNocturnal = !IsNocturnal;
            }

            // Inherit and mutate Hibernation trait
            CanHibernate = parent.CanHibernate;
            if (_random.NextSingle() < hibernateMutationChance) {
                CanHibernate = !CanHibernate;
            }

            // Inherit and mutate Fertility, with a minimum of 0.1
            Fertility = Math.Max(0.1f, parent.Fertility + (_random.NextSingle() * 2f - 1f) * fertilityMutationAmount);

            // Inherit and mutate Social trait, with a minimum of 0.1
            Social = Math.Max(0.1f, parent.Social + (_random.NextSingle() * 2f - 1f) * socialMutationAmount);

            // Inherit and mutate TerritoryStrength, with a minimum of 0.5
            TerritoryStrength = Math.Max(0.5f, parent.TerritoryStrength + (_random.NextSingle() * 2f - 1f) * territoryStrengthMutationAmount);

            // Inherit and mutate Courage, clamped between 0.1 and 1.5
            Courage = Math.Clamp(parent.Courage + (_random.NextSingle() * 2f - 1f) * courageMutationAmount, 0.1f, 1.5f);
        }

        /// <summary>
        /// Returns the AnimalType corresponding to a given Species.
        /// </summary>
        private static AnimalType GetAnimalTypeFromSpecies(Species species) => species switch
        {
            Species.Rabbit => AnimalType.Herbivore,
            Species.Deer => AnimalType.Herbivore,
            Species.Boar => AnimalType.Omnivore,
            Species.Bear => AnimalType.Omnivore,
            Species.Fox => AnimalType.Carnivore,
            Species.Wolf => AnimalType.Carnivore,
            _ => throw new ArgumentOutOfRangeException(nameof(species), "Unknown Species for AnimalType derivation.")
        };

        /// <summary>
        /// Defines the base genetic traits for each species.
        /// </summary>
        private static (int MaxAge, int VisionRange, int Speed, float Aggression, float Size, float Fertility, bool IsNocturnal, bool CanHibernate, float Social, float TerritoryStrength, float Courage) GetBaseStatsForSpecies(Species species) => species switch
        {
            Species.Rabbit => (
                MaxAge: 20, VisionRange: 2, Speed: 2, Aggression: 0.1f, Size: 0.5f, Fertility: 1.2f, IsNocturnal: true, CanHibernate: false, Social: 1.2f, TerritoryStrength: 0.8f, Courage: 0.2f
            ),
            Species.Deer => (
                MaxAge: 60, VisionRange: 4, Speed: 3, Aggression: 0.2f, Size: 1.5f, Fertility: 1.5f, IsNocturnal: false, CanHibernate: false, Social: 1.5f, TerritoryStrength: 1.0f, Courage: 0.3f
            ),
            Species.Boar => (
                MaxAge: 35, VisionRange: 3, Speed: 2, Aggression: 0.8f, Size: 1.2f, Fertility: 0.9f, IsNocturnal: true, CanHibernate: false, Social: 0.8f, TerritoryStrength: 1.2f, Courage: 0.7f
            ),
            Species.Bear => (
                MaxAge: 80, VisionRange: 4, Speed: 2, Aggression: 1.5f, Size: 2.5f, Fertility: 0.8f, IsNocturnal: false, CanHibernate: true, Social: 0.5f, TerritoryStrength: 1.5f, Courage: 0.9f
            ),
            Species.Fox => (
                MaxAge: 30, VisionRange: 5, Speed: 5, Aggression: 1.6f, Size: 0.8f, Fertility: 1.5f, IsNocturnal: true, CanHibernate: false, Social: 1.0f, TerritoryStrength: 1.1f, Courage: 0.8f
            ),
            Species.Wolf => (
                MaxAge: 70, VisionRange: 7, Speed: 6, Aggression: 2.5f, Size: 1.8f, Fertility: 1.0f, IsNocturnal: false, CanHibernate: false, Social: 2.0f, TerritoryStrength: 1.8f, Courage: 1.2f
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(species), "No base stats defined for this species.")
        };

        /// <summary>
        /// Defines the preferred biomes for each species.
        /// </summary>
        private static HashSet<Biome> GetPreferredBiomesForSpecies(Species species) => species switch
        {
            Species.Rabbit => new HashSet<Biome> { Biome.Plains, Biome.Forest },
            Species.Deer => new HashSet<Biome> { Biome.Forest, Biome.Plains },
            Species.Boar => new HashSet<Biome> { Biome.Forest, Biome.Plains },
            Species.Bear => new HashSet<Biome> { Biome.Forest, Biome.Mountain },
            Species.Fox => new HashSet<Biome> { Biome.Forest, Biome.Plains },
            Species.Wolf => new HashSet<Biome> { Biome.Forest, Biome.Plains, Biome.Mountain },
            _ => new HashSet<Biome> { Biome.Plains }
        };

        // --- Metabolism Costs ---
        private const float BaseMetabolism = 0.2f; // Increased base cost of living
        private const float SizeMetabolismFactor = 0.1f; // Larger bodies burn much more energy
        private const float SpeedMetabolismFactor = 0.05f; // High speed is expensive
        private const float VisionMetabolismFactor = 0.02f; // Better senses cost more energy
        private const float AggressionMetabolismFactor = 0.01f; // Aggression now has a noticeable metabolic cost

        public float MaxHunger => BaseMaxHunger * Size;
        public float MaxThirst => BaseMaxThirst * Size;
        public float MaxStamina => 20f * Size;
        public float MaxHealth => BaseMaxHealth * Size;

        /// <summary>
        /// Calculates the passive hunger increase per tick based on genetic traits.
        /// More powerful traits result in a higher metabolism.
        /// </summary>
        public float MetabolicRate
        {
            get
            {
                if (IsHibernating)
                {
                    return BaseMetabolism * 0.1f; // Drastically reduced metabolism
                }

                // Start with the base passive hunger increase.
                float totalMetabolism = BaseMetabolism;

                // Add costs for advanced genetic traits.
                totalMetabolism += (Size - 1.0f) * SizeMetabolismFactor; // A base size of 1.0 has no extra cost.
                totalMetabolism += (Speed - 1) * SpeedMetabolismFactor; // A base speed of 1 has no extra cost.
                totalMetabolism += (VisionRange - 3) * VisionMetabolismFactor; // A base vision of 3 has no extra cost.
                if (Type == AnimalType.Carnivore)
                {
                    totalMetabolism += (Aggression - 1.0f) * AggressionMetabolismFactor; // A base aggression of 1.0 has no extra cost.
                }

                // Ensure metabolic rate doesn't go below the base rate.
                return Math.Max(BaseMetabolism, totalMetabolism);
            }
        }

        /// <summary>
        /// Ages the animal, increases hunger, and handles plant-based eating.
        /// </summary>
        public void EatAndAge(WorldCell? cell, List<Animal> cellMates, Grid grid)
        {
            if (!IsAlive || cell == null) return;

            Eat(cell, cellMates);

            // --- Drinking ---
            float amountToDrink = 2.0f * Size;
            float waterConsumed = 0;

            var waterSource = cell.Water > 0 ? cell : FindAdjacentWater(grid);
            if (waterSource != null)
            {
                waterConsumed = waterSource.ConsumeWater(amountToDrink);
            }

            Thirst -= waterConsumed;
            if (Thirst < 0) Thirst = 0;

            Age++;
            Hunger += MetabolicRate; // Hunger increases based on metabolic rate from genetic traits.
            Thirst += MetabolicRate * 1.2f; // Thirst also increases, maybe at a different rate.

            // Passively regenerate a small amount of stamina each tick.
            Stamina = Math.Min(MaxStamina, Stamina + StaminaRegenRate);

            // Regenerate health if well-fed and not at full health
            if (Hunger < (MaxHunger * 0.25f) && Thirst < (MaxThirst * 0.25f) && Health < MaxHealth)
            {
                Health += 0.5f; // Small regeneration amount per tick
                if (Health > MaxHealth) Health = MaxHealth;
            }

            if (Hunger >= MaxHunger || Thirst >= MaxThirst || Age >= MaxAge)
                Die();
        }

        private void TryMoveTo(int newX, int newY, Grid grid, long currentTick, int moveSpeed)
        {
            var targetCell = grid.GetCell(newX, newY);
            if (targetCell == null) return; // Cannot move to an invalid cell

            // --- Stamina Cost ---
            // Movement costs hunger, with penalties for speed and difficult terrain.
            // The cost is incurred for the attempt, even if the move fails.
            int deltaX = newX - X;
            int deltaY = newY - Y;
            // Use Euclidean distance for a more accurate cost on diagonal moves.
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            float terrainMultiplier = targetCell.Terrain switch
            {
                TerrainType.Mountain => 3.0f, // Mountains are very tiring
                TerrainType.Water => 5.0f,    // Swimming is extremely tiring
                TerrainType.Desert => 1.5f,   // Sand is tiring
                TerrainType.Tundra => 1.2f,   // Cold and rough terrain
                TerrainType.Wetlands => 2.0f, // Mud is hard to move through
                TerrainType.River => 2.0f,    // Shallow water/swimming
                _ => 1.0f,
            };

            // --- Territory Cost ---
            if (targetCell.TerritoryOwnerId.HasValue && targetCell.TerritoryOwnerId.Value != GroupId)
            {
                terrainMultiplier *= 2.0f; // Significantly harder to enter enemy territory
            }

            // Base hunger cost per unit of distance moved.
            const float movementHungerCost = 0.05f;
            const float movementThirstCost = 0.07f;
            Hunger += (float)distance * movementHungerCost * terrainMultiplier * Size;
            Thirst += (float)distance * movementThirstCost * terrainMultiplier * Size;

            // --- Stamina Drain ---
            if (moveSpeed > 1)
            {
                Stamina -= StaminaDrainRate;
                if (Stamina < 0) Stamina = 0;
            }

            // Water should be very hard to cross, mountains difficult.
            double moveSuccessChance = targetCell.Terrain switch
            {
                TerrainType.Mountain => 0.5, // 50% chance to enter
                TerrainType.Water => 0.2,    // 20% chance to enter
                _ => 1.0,                   // 100% chance for Plains, Forest
            };

            if (_random.NextDouble() < moveSuccessChance)
            {
                X = newX;
                Y = newY;

                // Claim territory upon successful move
                if (targetCell != null)
                {
                    if (targetCell.TerritoryOwnerId == null || targetCell.TerritoryOwnerId == this.GroupId)
                    {
                        // Claim or refresh the territory
                        targetCell.TerritoryOwnerId = this.GroupId;
                        targetCell.LastTerritoryRefreshTick = currentTick;
                        targetCell.TerritoryStrength = this.TerritoryStrength;
                    }

                    // --- Leave Scent ---
                    var existingScent = targetCell.Scents.FirstOrDefault(s => s.GroupId == this.GroupId);
                    float scentStrength = 20.0f * this.Size; // Larger animals leave stronger scents

                    if (existingScent != null)
                    {
                        existingScent.Strength = scentStrength; // Refresh scent
                    }
                    else
                    {
                        targetCell.Scents.Add(new Scent(this.GroupId, this.Type, scentStrength));
                    }
                }
            }
            // If the move fails, the animal stays in its current position (X and Y are not updated).
        }

        private void MoveRandomly(Grid grid, long currentTick, int moveSpeed)
        {
            int width = grid.Width;
            int height = grid.Height;

            // Random movement uses the provided speed (usually walking speed 1)

            int dx = _random.Next(-1, 2);
            int dy = _random.Next(-1, 2);

            int newX = Math.Clamp(X + (dx * moveSpeed), 0, width - 1);
            int newY = Math.Clamp(Y + (dy * moveSpeed), 0, height - 1);
            TryMoveTo(newX, newY, grid, currentTick, moveSpeed);
        }

        /// <summary>
        /// The main decision-making method for an animal's movement each tick.
        /// This method has been refactored from a monolithic block into a dispatcher that calls
        /// prioritized sub-routines for clarity and maintainability.
        /// </summary>
        public void Move(Grid grid, IReadOnlyList<Animal> allAnimals, long currentTick, bool isNight, Season currentSeason)
        {
            if (!ShouldBeActive(isNight, currentSeason, grid, currentTick))
            {
                return;
            }

            // 1. Identify all nearby animals (mates, predators, rivals)
            var (localMates, nearbyPredators, rivalsOnHomeTurf) = IdentifyNearbyEntities(allAnimals, grid);

            // 2. Handle high-priority "fight or flight" responses. If any of these trigger, the animal acts and the turn ends.
            if (HandleFear(nearbyPredators, grid, currentTick)) return;
            if (HandleInjury(grid, currentTick)) return;
            if (HandleTerritorialAggression(allAnimals, grid, currentTick)) return;

            // 3. If no immediate threats, perform complex goal-seeking behavior.
            PerformGoalSeekingMove(grid, allAnimals, currentTick, localMates, rivalsOnHomeTurf);
        }

        #region AI Behavior Sub-methods

        /// <summary>
        /// Determines if the animal should be active based on hibernation, time of day, and random chance.
        /// </summary>
        private bool ShouldBeActive(bool isNight, Season currentSeason, Grid grid, long currentTick)
        {
            // --- Hibernation Check ---
            if (CanHibernate)
            {
                if (currentSeason == Season.Winter) IsHibernating = true;
                else if (IsHibernating && currentSeason != Season.Winter) IsHibernating = false; // Wake up
            }

            if (IsHibernating) return false;

            // --- Activity Check based on Day/Night Cycle ---
            bool isActiveTime = (IsNocturnal && isNight) || (!IsNocturnal && !isNight);
            if (!isActiveTime)
            {
                if (_random.NextDouble() < 0.8) return false; // 80% chance to rest

                // If not resting, perform a slow, random move.
                MoveRandomly(grid, currentTick, 1);
                return false; // Action was taken, so end turn.
            }

            return true; // Animal is active and should proceed with normal AI.
        }

        /// <summary>
        /// Scans the surroundings for other animals and categorizes them.
        /// </summary>
        private (List<Animal> localMates, List<Animal> nearbyPredators, List<Animal> rivalsOnHomeTurf) IdentifyNearbyEntities(IReadOnlyList<Animal> allAnimals, Grid grid)
        {
            var localMates = new List<Animal>();
            var nearbyPredators = new List<Animal>();
            var rivalsOnHomeTurf = new List<Animal>();

            // PERF: This iterates all animals in the simulation for each animal's move.
            // This is an O(N^2) operation and a major performance bottleneck.
            // A spatial partitioning system (e.g., passing in a list of only nearby animals) would be much better.
            foreach (var other in allAnimals)
            {
                if (other == this || !other.IsAlive) continue;

                int dX = X - other.X;
                int dY = Y - other.Y;
                double distSq = dX * dX + dY * dY;

                if (distSq > VisionRange * VisionRange) continue; // Out of sight

                // Is it a predator? (Only for herbivores/omnivores)
                if (this.Type != AnimalType.Carnivore && other.Type == AnimalType.Carnivore)
                {
                    nearbyPredators.Add(other);
                }
                // Is it a herd/pack mate?
                if (other.GroupId == this.GroupId)
                {
                    localMates.Add(other);
                }
                // Is it a rival on its own territory?
                else
                {
                    var rivalCell = grid.GetCell(other.X, other.Y);
                    if (rivalCell != null && rivalCell.TerritoryOwnerId == other.GroupId)
                    {
                        rivalsOnHomeTurf.Add(other);
                    }
                }
            }
            return (localMates, nearbyPredators, rivalsOnHomeTurf);
        }

        /// <summary>
        /// Handles the highest priority behavior: fleeing from predators.
        /// </summary>
        /// <returns>True if the animal fled, false otherwise.</returns>
        private bool HandleFear(IReadOnlyList<Animal> nearbyPredators, Grid grid, long currentTick)
        {
            if (!nearbyPredators.Any()) return false;

            var closestPredator = FindClosest(nearbyPredators);
            if (closestPredator != null)
            {
                // Update memory of the predator's location
                _lastKnownPredatorLocation = (closestPredator.X, closestPredator.Y);
                _lastPredatorSightingTick = currentTick;
                // Vector pointing away from the predator
                float fearDx = X - closestPredator.X;
                float fearDy = Y - closestPredator.Y;

                int fleeSpeed = (Stamina > 0) ? Speed : 1;

                // Move directly away
                int fleeMoveDx = Math.Sign(fearDx);
                int fleeMoveDy = Math.Sign(fearDy);

                // If we are right on top of it, pick a random direction to flee
                if (fleeMoveDx == 0 && fleeMoveDy == 0)
                {
                    fleeMoveDx = _random.Next(-1, 2); fleeMoveDy = _random.Next(-1, 2);
                }

                int newX = Math.Clamp(X + (fleeMoveDx * fleeSpeed), 0, grid.Width - 1);
                int newY = Math.Clamp(Y + (fleeMoveDy * fleeSpeed), 0, grid.Height - 1);
                TryMoveTo(newX, newY, grid, currentTick, fleeSpeed);
                return true; // Fear overrides all other behaviors
            }
            return false;
        }

        /// <summary>
        /// Handles the high-priority behavior of returning to the den when injured.
        /// </summary>
        /// <returns>True if the animal moved towards its den, false otherwise.</returns>
        private bool HandleInjury(Grid grid, long currentTick)
        {
            const float injuredHealthThreshold = 0.4f; // 40% health
            if (this.Health < (this.MaxHealth * injuredHealthThreshold))
            {
                float injuredHomingDx = DenX - X;
                float injuredHomingDy = DenY - Y;

                if (Math.Abs(injuredHomingDx) < 1 && Math.Abs(injuredHomingDy) < 1) return false; // Already at den
                
                int injuredHomingSpeed = (Stamina > 0) ? Speed : 1;

                // This is a high-priority goal.
                float injuredFinalDx = injuredHomingDx * InjuredHomingWeight;
                float injuredFinalDy = injuredHomingDy * InjuredHomingWeight;

                int injuredMoveDx = Math.Sign(injuredFinalDx);
                int injuredMoveDy = Math.Sign(injuredFinalDy);
                int injuredNewX = Math.Clamp(X + (injuredMoveDx * injuredHomingSpeed), 0, grid.Width - 1);
                int injuredNewY = Math.Clamp(Y + (injuredMoveDy * injuredHomingSpeed), 0, grid.Height - 1);
                TryMoveTo(injuredNewX, injuredNewY, grid, currentTick, injuredHomingSpeed);
                return true; // Overrides other behaviors.
            }
            return false;
        }

        /// <summary>
        /// Handles aggressive behavior towards intruders within the animal's territory.
        /// </summary>
        /// <returns>True if the animal chased an intruder, false otherwise.</returns>
        private bool HandleTerritorialAggression(IReadOnlyList<Animal> allAnimals, Grid grid, long currentTick)
        {
            var currentCell = grid.GetCell(this.X, this.Y);
            // Only non-herbivores exhibit territorial aggression
            if (this.Type != AnimalType.Herbivore && currentCell != null && currentCell.TerritoryOwnerId.HasValue && currentCell.TerritoryOwnerId.Value == this.GroupId)
            {
                // We are in our own territory. Look for intruders.
                var intruders = allAnimals
                    .Where(a => a.IsAlive && a.GroupId != this.GroupId) // Different group
                    .Where(a => (X - a.X) * (X - a.X) + (Y - a.Y) * (Y - a.Y) <= VisionRange * VisionRange); // Within vision

                var closestIntruder = FindClosest(intruders);

                if (closestIntruder != null)
                {
                    if (_random.NextDouble() < this.Courage)
                    {
                        // Aggressively move towards the intruder.
                        float aggressionDx = closestIntruder.X - X;
                        float aggressionDy = closestIntruder.Y - Y;

                        int aggressionSpeed = (Stamina > 0) ? Speed : 1;

                        // The aggression vector is weighted by the animal's Aggression trait
                        float aggressionFinalDx = aggressionDx * (TerritorialAggressionWeight * this.Aggression);
                        float aggressionFinalDy = aggressionDy * (TerritorialAggressionWeight * this.Aggression);

                        int aggressionMoveDx = Math.Sign(aggressionFinalDx);
                        int aggressionMoveDy = Math.Sign(aggressionFinalDy);
                        int aggressionNewX = Math.Clamp(X + (aggressionMoveDx * aggressionSpeed), 0, grid.Width - 1);
                        int aggressionNewY = Math.Clamp(Y + (aggressionMoveDy * aggressionSpeed), 0, grid.Height - 1);
                        TryMoveTo(aggressionNewX, aggressionNewY, grid, currentTick, aggressionSpeed);
                        return true; // Territorial aggression overrides other goal-seeking.
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// The main logic for combining various environmental and social factors into a single movement decision.
        /// This is called when no high-priority threats are present.
        /// </summary>
        private void PerformGoalSeekingMove(Grid grid, IReadOnlyList<Animal> allAnimals, long currentTick, List<Animal> localMates, List<Animal> rivalsOnHomeTurf)
        {
            // This method calculates a series of "steering vectors" based on the animal's current state and environment.
            // Each vector is weighted and then combined to produce a final movement direction.

            // --- Vector Initialization ---
            float cohesionDx = 0, cohesionDy = 0;
            float separationDx = 0, separationDy = 0;
            float lingeringFearDx = 0, lingeringFearDy = 0;
            float scentAttractionDx = 0, scentAttractionDy = 0;
            float scentAversionDx = 0, scentAversionDy = 0;
            float homingDx = 0, homingDy = 0;
            float territorialAversionDx = 0, territorialAversionDy = 0;
            float territoryStayDx = 0, territoryStayDy = 0;
            float biomeSeekingDx = 0, biomeSeekingDy = 0;
            float goalDx = 0, goalDy = 0;

            // --- Social Vectors (Cohesion & Separation) ---
            if (localMates.Any())
            {
                // Cohesion: steer towards center of local mates
                float centerX = (float)localMates.Average(m => m.X);
                float centerY = (float)localMates.Average(m => m.Y);
                cohesionDx = centerX - X;
                cohesionDy = centerY - Y;

                // Separation: steer away from mates that are too close
                foreach (var mate in localMates)
                {
                    int dX = X - mate.X;
                    int dY = Y - mate.Y;
                    double distSq = dX * dX + dY * dY;
                    if (distSq < SeparationDistance * SeparationDistance)
                    {
                        separationDx += dX;
                        separationDy += dY;
                    }
                }
            }

            // --- Environmental/Memory Vectors ---
            if (Type != AnimalType.Carnivore && _lastKnownPredatorLocation.HasValue && currentTick - _lastPredatorSightingTick < MemoryDurationTicks)
            {
                // Create a vector pointing away from the remembered predator location
                var (predX, predY) = _lastKnownPredatorLocation.Value;
                lingeringFearDx = X - predX;
                lingeringFearDy = Y - predY;
            }

            if (rivalsOnHomeTurf.Any())
            {
                float rivalCenterX = (float)rivalsOnHomeTurf.Average(r => r.X);
                float rivalCenterY = (float)rivalsOnHomeTurf.Average(r => r.Y);
                territorialAversionDx = X - rivalCenterX;
                territorialAversionDy = Y - rivalCenterY;
            }

            var currentCellForTerritory = grid.GetCell(X, Y);
            if (currentCellForTerritory != null && currentCellForTerritory.TerritoryOwnerId == GroupId)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = X + dx;
                        int ny = Y + dy;
                        if (nx >= 0 && nx < grid.Width && ny >= 0 && ny < grid.Height)
                        {
                            var nCell = grid.GetCell(nx, ny);
                            if (nCell.TerritoryOwnerId == GroupId)
                            {
                                territoryStayDx += dx;
                                territoryStayDy += dy;
                            }
                        }
                    }
                }
            }

            var currentCellBiome = grid.GetCell(this.X, this.Y)?.Biome;
            if (currentCellBiome.HasValue && !PreferredBiomes.Contains(currentCellBiome.Value))
            {
                // Scan vision range for the closest cell with a preferred biome.
                (int x, int y)? closestPreferredCell = null;
                double min_dist_sq = double.MaxValue;

                for (int dx = -VisionRange; dx <= VisionRange; dx++)
                {
                    for (int dy = -VisionRange; dy <= VisionRange; dy++)
                    {
                        int nx = X + dx;
                        int ny = Y + dy;
                        if (nx >= 0 && nx < grid.Width && ny >= 0 && ny < grid.Height)
                        {
                            var cell = grid.GetCell(nx, ny);
                            if (cell != null && PreferredBiomes.Contains(cell.Biome))
                            {
                                double distSq = dx * dx + dy * dy;
                                if (distSq < min_dist_sq)
                                {
                                    min_dist_sq = distSq;
                                    closestPreferredCell = (nx, ny);
                                }
                            }
                        }
                    }
                }
                if (closestPreferredCell.HasValue)
                {
                    biomeSeekingDx = closestPreferredCell.Value.x - X;
                    biomeSeekingDy = closestPreferredCell.Value.y - Y;
                }
            }

            // --- Scent Vectors ---
            if (this.Type == AnimalType.Carnivore)
            {
                // Carnivores track prey scents
                float maxPreyScentStrength = 0;
                (int x, int y) preyScentLocation = (0, 0);

                for (int dx = -VisionRange; dx <= VisionRange; dx++)
                {
                    for (int dy = -VisionRange; dy <= VisionRange; dy++)
                    {
                        int nx = X + dx;
                        int ny = Y + dy;
                        if (nx >= 0 && nx < grid.Width && ny >= 0 && ny < grid.Height)
                        {
                            var cell = grid.GetCell(nx, ny);
                            foreach (var scent in cell.Scents)
                            {
                                if (scent.Type != AnimalType.Carnivore && scent.Strength > maxPreyScentStrength)
                                {
                                    maxPreyScentStrength = scent.Strength;
                                    preyScentLocation = (nx, ny);
                                }
                            }
                        }
                    }
                }
                if (maxPreyScentStrength > 0)
                {
                    scentAttractionDx = preyScentLocation.x - X;
                    scentAttractionDy = preyScentLocation.y - Y;
                }
            }
            else // Herbivores and Omnivores avoid predator scents
            {
                float maxPredatorScentStrength = 0;
                (int x, int y) predatorScentLocation = (0, 0);

                for (int dx = -VisionRange; dx <= VisionRange; dx++)
                {
                    for (int dy = -VisionRange; dy <= VisionRange; dy++)
                    {
                        int nx = X + dx;
                        int ny = Y + dy;
                        if (nx >= 0 && nx < grid.Width && ny >= 0 && ny < grid.Height)
                        {
                            var cell = grid.GetCell(nx, ny);
                            if (cell == null) continue;
                            foreach (var scent in cell.Scents)
                            {
                                if (scent.Type == AnimalType.Carnivore && scent.Strength > maxPredatorScentStrength)
                                {
                                    maxPredatorScentStrength = scent.Strength;
                                    predatorScentLocation = (nx, ny);
                                }
                            }
                        }
                    }
                }
                if (maxPredatorScentStrength > 0)
                {
                    scentAversionDx = X - predatorScentLocation.x;
                    scentAversionDy = Y - predatorScentLocation.y;
                }
            }

            homingDx = DenX - X;
            homingDy = DenY - Y;

            // --- Goal-Seeking Vector (Food, Water, Prey) ---
            float hungerUrgency = Hunger / MaxHunger;
            float thirstUrgency = Thirst / MaxThirst;
            
            // Hysteresis: Stick to the current goal unless the other need is significantly more urgent
            // or the current need is satisfied. This prevents "dithering" between two similar needs.
            const float urgencyHysteresis = 0.15f; // 15% buffer
            if (_seekingWater)
            {
                if (thirstUrgency < 0.05f || hungerUrgency > thirstUrgency + urgencyHysteresis) _seekingWater = false;
            }
            else if (hungerUrgency < 0.05f || thirstUrgency > hungerUrgency + urgencyHysteresis) _seekingWater = true;
            
            bool isThirstier = _seekingWater;

            if (isThirstier)
            {
                // --- Seek Water ---
                (int x, int y)? bestWaterCell = null;
                float maxWater = -1f;
                for (int dx = -VisionRange; dx <= VisionRange; dx++)
                {
                    for (int dy = -VisionRange; dy <= VisionRange; dy++)
                    {
                        int nx = X + dx;
                        int ny = Y + dy;
                        if (nx >= 0 && nx < grid.Width && ny >= 0 && ny < grid.Height)
                        {
                            var cell = grid.GetCell(nx, ny);
                            if (cell != null && cell.Water > maxWater)
                            {
                                maxWater = cell.Water;
                                bestWaterCell = (nx, ny);
                            }
                        }
                    }
                }

                if (bestWaterCell.HasValue)
                {
                    var (targetX, targetY) = bestWaterCell.Value;
                    goalDx = targetX - X; // Corrected to use targetX, targetY
                    goalDy = targetY - Y; // Corrected to use targetX, targetY
                    _lastKnownWaterLocation = bestWaterCell;
                    _lastWaterSightingTick = currentTick;
                }
                else if (_lastKnownWaterLocation.HasValue && currentTick - _lastWaterSightingTick < MemoryDurationTicks)
                {
                    var (targetX, targetY) = _lastKnownWaterLocation.Value;
                    if (targetX == X && targetY == Y)
                    {
                        // Arrived at remembered location but found nothing. Forget it immediately.
                        _lastKnownWaterLocation = null;
                    }
                    else
                    {
                        goalDx = targetX - X;
                        goalDy = targetY - Y;
                    }
                }
            }
            else // Is hungrier, seek food
            {
                if (Type == AnimalType.Carnivore) // Carnivores seek prey
                {
                    // --- Carnivore AI: Prioritize smaller, safer prey ---
                    // 1. Find the closest, smallest prey first.
                    var smallPrey = allAnimals
                        .Where(a => a.IsAlive && a.Type != AnimalType.Carnivore && a.Size < this.Size && IsInVision(a));
                    var closestFoodSource = FindClosest(smallPrey);

                    // 2. If no small prey is found, look for any prey or a carcass (more desperate).
                    if (closestFoodSource == null)
                    {
                        var anyPreyOrCarcass = allAnimals
                            .Where(a => a != this)
                            .Where(a => IsInVision(a))
                            .Where(a => (a.IsAlive && a.Type != AnimalType.Carnivore) || (!a.IsAlive && !a.IsConsumed));
                        
                        closestFoodSource = FindClosest(anyPreyOrCarcass);
                    }

                    if (closestFoodSource != null)
                    {
                        goalDx = closestFoodSource.X - X;
                        goalDy = closestFoodSource.Y - Y;
                    }
                }
                else if (Type == AnimalType.Omnivore) // Omnivores weigh their options
                {
                    // --- Omnivore AI: Weigh plants vs. carcasses ---
                    // 1. Find best plant source in vision
                    (int x, int y)? bestPlantCell = null;
                    float maxPlantFood = -1f;
                    for (int dx = -VisionRange; dx <= VisionRange; dx++)
                    {
                        for (int dy = -VisionRange; dy <= VisionRange; dy++)
                        {
                            int nx = X + dx;
                            int ny = Y + dy;
                            if (nx >= 0 && nx < grid.Width && ny >= 0 && ny < grid.Height)
                            {
                                var cell = grid.GetCell(nx, ny);
                                if (cell != null && cell.Food > maxPlantFood)
                                {
                                    maxPlantFood = cell.Food;
                                    bestPlantCell = (nx, ny);
                                }
                            }
                        }
                    }

                    // 2. Find closest carcass in vision
                    var carcasses = allAnimals
                        .Where(a => !a.IsAlive && !a.IsConsumed && IsInVision(a));
                    var closestCarcass = FindClosest(carcasses);

                    // 3. Compare scores and decide target
                    float plantScore = 0;
                    if (bestPlantCell.HasValue && maxPlantFood > 0)
                    {
                        float plantDist = (float)Math.Sqrt(Math.Pow(bestPlantCell.Value.x - X, 2) + Math.Pow(bestPlantCell.Value.y - Y, 2));
                        plantScore = maxPlantFood / (plantDist + 1f); // Score is food amount discounted by distance
                    }

                    float carcassScore = 0;
                    if (closestCarcass != null)
                    {
                        float carcassDist = (float)Math.Sqrt(Math.Pow(closestCarcass.X - X, 2) + Math.Pow(closestCarcass.Y - Y, 2));
                        const float meatValue = 10f; // Meat is highly valuable
                        carcassScore = meatValue / (carcassDist + 1f);
                    }

                    // 4. Set goal vector based on the best option
                    if (carcassScore > plantScore && closestCarcass != null)
                    {
                        goalDx = closestCarcass.X - X;
                        goalDy = closestCarcass.Y - Y;
                    }
                    else if (plantScore > 0 && bestPlantCell.HasValue)
                    {
                        var (targetX, targetY) = bestPlantCell.Value;
                        goalDx = targetX - X;
                        goalDy = targetY - Y;
                        _lastKnownFoodLocation = bestPlantCell; // Remember plant locations
                        _lastFoodSightingTick = currentTick;
                    }
                    else if (_lastKnownFoodLocation.HasValue && currentTick - _lastFoodSightingTick < MemoryDurationTicks)
                    {
                        // No food in sight, use memory
                        var (targetX, targetY) = _lastKnownFoodLocation.Value;
                        if (targetX == X && targetY == Y)
                        {
                            // Arrived at remembered location but found nothing. Forget it immediately.
                            _lastKnownFoodLocation = null;
                        }
                        else
                        {
                            goalDx = targetX - X;
                            goalDy = targetY - Y;
                        }
                    }
                }
                else // Herbivores only seek plants
                {
                    var bestFoodCells = new List<(int x, int y)>();
                    float maxFood = -1f;

                    // Find the best food spots within vision range
                    for (int dx = -VisionRange; dx <= VisionRange; dx++)
                    {
                        for (int dy = -VisionRange; dy <= VisionRange; dy++)
                        {
                            int nx = X + dx;
                            int ny = Y + dy;
                            if (nx >= 0 && nx < grid.Width && ny >= 0 && ny < grid.Height)
                            {
                                var cell = grid.GetCell(nx, ny);
                                if (cell == null) continue;
                                float foodInCell = cell.Food;
                                if (foodInCell > maxFood)
                                {
                                    maxFood = foodInCell;
                                    bestFoodCells.Clear();
                                    bestFoodCells.Add((nx, ny));
                                }
                                else if (foodInCell == maxFood && maxFood > 0)
                                {
                                    bestFoodCells.Add((nx, ny));
                                }
                            }
                        }
                    }

                    if (bestFoodCells.Any())
                    {
                        // If food is in sight, update memory and set goal
                        var (targetX, targetY) = bestFoodCells[_random.Next(bestFoodCells.Count)];
                        _lastKnownFoodLocation = (targetX, targetY);
                        _lastFoodSightingTick = currentTick;
                        goalDx = targetX - X;
                        goalDy = targetY - Y;
                    }
                    else if (_lastKnownFoodLocation.HasValue && currentTick - _lastFoodSightingTick < MemoryDurationTicks)
                    {
                        // If no food is in sight, move towards remembered location
                        var (targetX, targetY) = _lastKnownFoodLocation.Value;
                        if (targetX == X && targetY == Y)
                        {
                            // Arrived at remembered location but found nothing. Forget it immediately.
                            _lastKnownFoodLocation = null;
                        }
                        else
                        {
                            goalDx = targetX - X;
                            goalDy = targetY - Y;
                        }
                    }
                }
            }

            // --- Final Combination & Movement ---
            float goalUrgency = Math.Max(hungerUrgency, thirstUrgency);
            
            // Dynamic Weighting: If needs are critical, ignore social/territorial niceties.
            float desperationFactor = 1.0f;
            float socialFactor = 1.0f;
            float territoryFactor = 1.0f;

            if (goalUrgency > 0.75f) // Critical need
            {
                desperationFactor = 2.5f; // Strong pull towards goal
                socialFactor = 0.1f;      // Ignore friends
                territoryFactor = 0.2f;   // Ignore borders
            }
            else if (goalUrgency > 0.5f) // Moderate need
            {
                desperationFactor = 1.5f;
                socialFactor = 0.5f;
            }

            float currentGoalWeight = isThirstier ? WaterSeekingWeight : (Type == AnimalType.Carnivore ? PreySeekingWeight * Aggression : FoodSeekingWeight);
            float goalWeight = currentGoalWeight * (goalUrgency + 0.1f) * desperationFactor;

            // Decide if the animal should run based on urgency.
            bool isUrgent = goalUrgency > 0.5f;
            int moveSpeed = (isUrgent && Stamina > 0) ? Speed : 1;

            float finalDx = (cohesionDx * (CohesionWeight * Social * socialFactor)) + (separationDx * SeparationWeight) + (goalDx * goalWeight) + (lingeringFearDx * LingeringFearWeight) + (territorialAversionDx * TerritorialAversionWeight * territoryFactor) + (territoryStayDx * TerritoryStayWeight * territoryFactor) + (homingDx * HomingWeight) + (scentAttractionDx * ScentTrackingWeight) + (scentAversionDx * ScentAversionWeight) + (biomeSeekingDx * BiomeSeekingWeight);
            float finalDy = (cohesionDy * (CohesionWeight * Social * socialFactor)) + (separationDx * SeparationWeight) + (goalDy * goalWeight) + (lingeringFearDy * LingeringFearWeight) + (territorialAversionDy * TerritorialAversionWeight * territoryFactor) + (territoryStayDy * TerritoryStayWeight * territoryFactor) + (homingDx * HomingWeight) + (scentAttractionDx * ScentTrackingWeight) + (scentAversionDx * ScentAversionWeight) + (biomeSeekingDx * BiomeSeekingWeight);

            // If no strong impulse, move randomly to explore.
            if (Math.Abs(finalDx) < 0.1f && Math.Abs(finalDy) < 0.1f)
            {
                MoveRandomly(grid, currentTick, 1); // Casual exploration is always at walking speed.
                return;
            }

            int finalMoveDx = Math.Sign(finalDx);
            int finalMoveDy = Math.Sign(finalDy);
            int finalNewX = Math.Clamp(X + (finalMoveDx * moveSpeed), 0, grid.Width - 1);
            int finalNewY = Math.Clamp(Y + (finalMoveDy * moveSpeed), 0, grid.Height - 1);
            TryMoveTo(finalNewX, finalNewY, grid, currentTick, moveSpeed);
        }
        
        /// <summary>
        /// Finds the closest animal from a given list of potential targets.
        /// More efficient than LINQ's OrderBy().FirstOrDefault().
        /// </summary>
        private Animal? FindClosest(IEnumerable<Animal> potentialTargets)
        {
            Animal? closest = null;
            double min_dist_sq = double.MaxValue;

            foreach (var target in potentialTargets)
            {
                double distSq = (X - target.X) * (X - target.X) + (Y - target.Y) * (Y - target.Y);
                if (distSq < min_dist_sq)
                {
                    min_dist_sq = distSq;
                    closest = target;
                }
            }
            return closest;
        }

        /// <summary>
        /// Checks if another animal is within the current animal's vision range.
        /// </summary>
        private bool IsInVision(Animal other)
        {
            return (X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y) <= VisionRange * VisionRange;
        }

        /// <summary>
        /// Finds an adjacent cell with water, for drinking from shorelines.
        /// </summary>
        private WorldCell? FindAdjacentWater(Grid grid)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var neighbor = grid.GetCell(X + dx, Y + dy);
                    if (neighbor != null && neighbor.Water > 0)
                    {
                        return neighbor;
                    }
                }
            }
            return null;
        }

        #endregion
        
        public void Hunt(List<Animal> cellMates, Grid grid)
        {
            // Non-herbivores can engage in combat.
            if (Type == AnimalType.Herbivore) return;

            // --- Territorial Defense ---
            var currentCell = grid.GetCell(this.X, this.Y);
            if (currentCell != null && currentCell.TerritoryOwnerId.HasValue && currentCell.TerritoryOwnerId.Value == this.GroupId)
            {
                // We are in our own territory. Attack any intruder in the same cell.
                var intruder = cellMates.FirstOrDefault(other => other != this && other.IsAlive && other.GroupId != this.GroupId);
                if (intruder != null)
                {
                    // Only attack if courageous enough.
                    if (_random.NextDouble() < this.Courage)
                    {
                        // Territorial attacks are vicious but do not restore hunger.
                        float damageDealt = (5.0f * Size) + (2.5f * Aggression);
                        intruder.TakeDamage(damageDealt, this);
                        return; // A territorial fight takes precedence over hunting for food.
                    }
                }
            }

            // --- Hunting for Food (Carnivores Only) ---
            if (Type == AnimalType.Carnivore)
            {
                var prey = cellMates.FirstOrDefault(other => other != this && other.IsAlive && other.Type != AnimalType.Carnivore);
                if (prey != null)
                {
                    // Successfully hunted for food.
                    float damageBonus = 0;
                    if (Species == SpeciesEnum.Wolf)
                    {
                        int otherWolvesInCell = cellMates.Count(a => a != this && a.Species == SpeciesEnum.Wolf && a.IsAlive);
                    if (otherWolvesInCell > 0) damageBonus = 2.0f * otherWolvesInCell; // Slightly reduced pack hunting damage bonus
                    }

                    float damageDealt = (10.0f * Size) + damageBonus; // Significantly increased base hunting damage
                    prey.TakeDamage(damageDealt, this);

                    // Pack Sharing Logic: If the prey was killed, the pack feasts immediately.
                    if (!prey.IsAlive)
                    {
                        // Identify pack members in the same cell (including self)
                        var packMates = cellMates.Where(a => a.IsAlive && a.GroupId == this.GroupId).ToList();

                        foreach (var member in packMates)
                        {
                            float hungerDeficit = member.MaxHunger - member.Hunger;
                            if (hungerDeficit > 0)
                            {
                                float amountConsumed = prey.EatFromCarcass(hungerDeficit);
                                member.Hunger = Math.Max(0, member.Hunger - amountConsumed);
                            }
                        }
                    }
                }
            }
        }

        public void TakeDamage(float amount, Animal? attacker)
        {
            if (!IsAlive) return;

            Health -= amount;

            // Retaliation logic
            if (attacker != null && attacker.IsAlive && Health > 0)
            {
                // Aggressive species (Omnivores, Carnivores) and large herbivores might fight back.
                bool canRetaliate = Type != AnimalType.Herbivore || Size > 1.0f;
                float retaliationChance = Courage * 0.5f; // Chance is proportional to courage

                if (canRetaliate && _random.NextDouble() < retaliationChance)
                {
                    // Damage is based on size and aggression. Herbivores are less damaging.
                    float aggressionFactor = (Type == AnimalType.Herbivore) ? 1.0f : Aggression;
                    float retaliationDamage = (2.0f * Size) * aggressionFactor;
                    // Directly deal damage to avoid recursive TakeDamage calls in one tick
                    attacker.Health -= retaliationDamage;
                    if (attacker.Health <= 0) attacker.Die();
                }
            }

            if (Health <= 0)
            {
                Die();
            }
        }

        private void Eat(WorldCell? cell, List<Animal> cellMates)
        {
            if (cell == null) return;
            float foodEaten = 0;
            switch (Type)
            {
                case AnimalType.Herbivore:
                    foodEaten = cell.ConsumeFood(1.0f);
                    break;

                case AnimalType.Omnivore:
                    // Omnivores are opportunistic: they prefer to scavenge but will eat plants if no meat is available.
                    var omniCarcass = cellMates?.FirstOrDefault(a => a != this && !a.IsAlive && a.CarcassFoodValue > 0);
                    if (omniCarcass != null)
                    {
                        // Eat from the carcass until full, but not more than what's available.
                        float amountToEat = MaxHunger - Hunger;
                        float foodFromCarcass = omniCarcass.EatFromCarcass(amountToEat);
                        Hunger -= foodFromCarcass;
                    }
                    else
                    {
                        foodEaten = cell.ConsumeFood(0.5f);
                    }
                    break;

                case AnimalType.Carnivore:
                    // In the Eat phase, carnivores will scavenge if possible. Hunting is a separate action.
                    var carnivoreCarcass = cellMates?.FirstOrDefault(a => a != this && !a.IsAlive && a.CarcassFoodValue > 0);
                    if (carnivoreCarcass != null)
                    {
                        float amountToEat = MaxHunger - Hunger;
                        float foodFromCarcass = carnivoreCarcass.EatFromCarcass(amountToEat);
                        Hunger -= foodFromCarcass;
                    }
                    break;
            }

            Hunger -= foodEaten;
            if (Hunger < 0) Hunger = 0;
        }

        /// <summary>
        /// Allows another animal to consume a portion of this animal's carcass.
        /// </summary>
        /// <param name="amount">The amount of food the scavenger wants to eat.</param>
        /// <returns>The actual amount of food consumed.</returns>
        public float EatFromCarcass(float amount)
        {
            if (IsAlive || CarcassFoodValue <= 0) return 0;

            float consumed = Math.Min(amount, CarcassFoodValue);
            CarcassFoodValue -= consumed;
            return consumed;
        }

        /// <summary>
        /// Reduces the food value of a carcass over time.
        /// </summary>
        public void DecayCarcass()
        {
            // Only non-living animals with remaining food value can decay.
            if (!IsAlive && CarcassFoodValue > 0)
            {
                CarcassFoodValue -= 0.1f; // Simple linear decay per tick
            }
        }

        private void Die()
        {
            IsAlive = false;
            // When an animal dies, it becomes a carcass with a food value based on its size.
            CarcassFoodValue = 15.0f * Size;
        }

        /// <summary>
        /// Forces the animal to leave its current social group and form a new one.
        /// </summary>
        public void LeaveGroup()
        {
            // The animal becomes the founder of a new group.
            GroupId = Guid.NewGuid();
            DenX = this.X; // Its current location becomes the new den.
            DenY = this.Y;
        }

        /// <summary>
        /// Allows the animal to join an existing social group.
        /// </summary>
        /// <param name="newGroupId">The ID of the group to join.</param>
        public void JoinGroup(Guid newGroupId)
        {
            GroupId = newGroupId;
        }

        /// <summary>
        /// Sets the location of the animal's den.
        /// </summary>
        public void SetDen(int x, int y)
        {
            DenX = x;
            DenY = y;
        }

        /// <summary>
        /// Checks if the animal is old enough and healthy enough to reproduce.
        /// </summary>
        public bool IsReadyToReproduce()
        {
            return IsAlive && Age >= MinReproductionAge && Hunger < ReproductionHungerThreshold && Thirst < ReproductionHungerThreshold;
        }

        /// <summary>
        /// Checks if the local area is too crowded for reproduction.
        /// </summary>
        /// <param name="nearbySameSpeciesCount">The number of same-species animals in the 3x3 vicinity.</param>
        public bool IsCrowded(int nearbySameSpeciesCount)
        {
            // Carnivores are more territorial and require more space.
            // Omnivores are somewhat territorial.
            // Herbivores tolerate higher densities.
            int threshold = Type == AnimalType.Carnivore ? 5 : 
                            Type == AnimalType.Omnivore ? 8 : 
                            15;
            return nearbySameSpeciesCount >= threshold;
        }

        /// <summary>
        /// Creates an offspring, applying a hunger cost to the parent.
        /// </summary>
        /// <returns>A new Animal instance representing the offspring.</returns>
        public List<Animal> CreateOffspring(Random random)
        {
            var offspringList = new List<Animal>();
            int numOffspring = Math.Max(1, (int)Math.Round(BaseOffspringCount * Fertility)); // Ensure at least 1 offspring if fertile enough

            for (int i = 0; i < numOffspring; i++)
            {
                Hunger += BaseReproductionCostPerOffspring; // Cost per offspring
                offspringList.Add(new Animal(this, random));
            }
            return offspringList;
        }
    }
}
