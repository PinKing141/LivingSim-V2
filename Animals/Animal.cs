using System;
using System.Collections.Generic;
using System.Linq;

using LivingSim.Core;
using LivingSim.World;
using LivingSim.Environment;
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
        private const float ReproductionHungerThreshold = 1.0f;
        private const float BaseReproductionCostPerOffspring = 3.0f;
        public const float BaseReproductionChance = 0.01f;
        private const int BaseOffspringCount = 1;
        public const int MaxGroupSize = 5;

        // --- Behavior Weights ---
        private const float CohesionWeight = 0.5f;
        private const float SeparationWeight = 1.5f;
        private const float FoodSeekingWeight = 1.5f;
        private const float PreySeekingWeight = 2.5f;
        private const float WaterSeekingWeight = 1.5f;
        private const float TerritorialAggressionWeight = 2.5f;
        private const float TerritorialAversionWeight = 1.8f;
        private const float TerritoryStayWeight = 1.0f;
        private const float InjuredHomingWeight = 3.0f;
        private const float HomingWeight = 0.3f;
        private const float ScentTrackingWeight = 1.0f;
        private const float ScentAversionWeight = 1.5f;
        private const float BiomeSeekingWeight = 1.2f;
        private const float SeparationDistance = 2.0f;
        private const float LingeringFearWeight = 1.0f;
        private const float ShelterSeekingWeight = 2.5f;

        // --- Memory ---
        private (int X, int Y)? _lastKnownFoodLocation;
        private long _lastFoodSightingTick = -1;
        private (int X, int Y)? _lastKnownPredatorLocation;
        private long _lastPredatorSightingTick = -1;
        private (int X, int Y)? _lastKnownWaterLocation;
        private long _lastWaterSightingTick = -1;
        private const int MemoryDurationTicks = 10;
        private bool _seekingWater = false;
        private int _ticksSinceLastAge = 0;

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
        public float Aggression { get; private set; }
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

        public Animal(Species species, int x, int y, Random random)
        {
            _random = random;
            Species = species;
            X = x; Y = y;
            Thirst = 0; Hunger = 0; Age = 0;
            Type = GetAnimalTypeFromSpecies(species);
            (MaxAge, VisionRange, Speed, Aggression, Size, Fertility, IsNocturnal, CanHibernate, Social, TerritoryStrength, Courage) = GetBaseStatsForSpecies(species);
            PreferredBiomes = GetPreferredBiomesForSpecies(species);
            DenX = x; DenY = y;
            GroupId = Guid.NewGuid();
            IsHibernating = false;
            Stamina = MaxStamina; Health = MaxHealth;
        }

        private Animal(Animal parent, Random random)
        {
            _random = random;
            Species = parent.Species;
            Type = parent.Type;
            X = parent.X; Y = parent.Y;
            Thirst = 0; Hunger = 0; Age = 0;
            Health = MaxHealth; Stamina = MaxStamina;
            GroupId = parent.GroupId;
            DenX = parent.DenX; DenY = parent.DenY;
            PreferredBiomes = parent.PreferredBiomes;

            VisionRange = parent.VisionRange;
            if (random.NextDouble() < 0.1) VisionRange = Math.Max(1, parent.VisionRange + random.Next(-1, 2));

            Speed = parent.Speed;
            if (random.NextDouble() < 0.05) Speed = Math.Max(1, parent.Speed + random.Next(-1, 2));

            Size = Math.Max(0.5f, parent.Size + ((random.NextSingle() * 0.1f) - 0.05f));

            Aggression = parent.Aggression;
            if (Type == AnimalType.Carnivore)
                Aggression = Math.Max(0.1f, parent.Aggression + ((random.NextSingle() * 0.2f) - 0.1f));

            IsNocturnal = parent.IsNocturnal;
            if (random.NextDouble() < 0.05) IsNocturnal = !IsNocturnal;

            MaxAge = Math.Max(10, parent.MaxAge + random.Next(-5, 6));
            Fertility = Math.Max(0.1f, parent.Fertility + ((random.NextSingle() * 0.1f) - 0.05f));
            Social = Math.Max(0.1f, parent.Social + ((random.NextSingle() * 0.1f) - 0.05f));
            TerritoryStrength = Math.Max(0.5f, parent.TerritoryStrength + ((random.NextSingle() * 0.1f) - 0.05f));
            Courage = Math.Clamp(parent.Courage + ((random.NextSingle() * 0.1f) - 0.05f), 0.1f, 1.5f);
            CanHibernate = parent.CanHibernate;
        }

        private static AnimalType GetAnimalTypeFromSpecies(Species species) => species switch
        {
            Species.Rabbit => AnimalType.Herbivore,
            Species.Deer => AnimalType.Herbivore,
            Species.Boar => AnimalType.Omnivore,
            Species.Bear => AnimalType.Omnivore,
            Species.Fox => AnimalType.Carnivore,
            Species.Wolf => AnimalType.Carnivore,
            _ => throw new ArgumentOutOfRangeException(nameof(species))
        };

        private static (int MaxAge, int VisionRange, int Speed, float Aggression, float Size, float Fertility, bool IsNocturnal, bool CanHibernate, float Social, float TerritoryStrength, float Courage) GetBaseStatsForSpecies(Species species) => species switch
        {
            Species.Rabbit => (100, 2, 2, 0.1f, 0.5f, 1.2f, true, false, 1.2f, 0.8f, 0.2f),
            Species.Deer => (360, 4, 3, 0.2f, 1.5f, 1.5f, false, false, 1.5f, 1.0f, 0.3f),
            Species.Boar => (300, 3, 2, 0.8f, 1.2f, 0.9f, true, false, 0.8f, 1.2f, 0.7f),
            Species.Bear => (600, 4, 2, 1.5f, 2.5f, 0.8f, false, true, 0.5f, 1.5f, 0.9f),
            Species.Fox => (240, 5, 5, 1.6f, 0.8f, 1.5f, true, false, 1.0f, 1.1f, 0.8f),
            Species.Wolf => (480, 7, 6, 2.5f, 1.8f, 1.0f, false, false, 2.0f, 1.8f, 1.2f),
            _ => throw new ArgumentOutOfRangeException(nameof(species))
        };

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

        private const float BaseMetabolism = 0.01f; 
        private const float SizeMetabolismFactor = 0.05f;
        private const float SpeedMetabolismFactor = 0.02f;
        private const float VisionMetabolismFactor = 0.01f;
        private const float AggressionMetabolismFactor = 0.01f;

        public float MaxHunger => BaseMaxHunger * Size;
        public float MaxThirst => BaseMaxThirst * Size;
        public float MaxStamina => 20f * Size;
        public float MaxHealth => BaseMaxHealth * Size;

        public int GetEffectiveVision(WeatherType weather)
        {
            if (weather == WeatherType.Fog || weather == WeatherType.Storm) return Math.Max(1, VisionRange / 2);
            if (weather == WeatherType.Snow && Type != AnimalType.Carnivore) return Math.Max(1, VisionRange - 1);
            return VisionRange;
        }

        public float MetabolicRate
        {
            get
            {
                if (IsHibernating) return BaseMetabolism * 0.1f;
                float totalMetabolism = BaseMetabolism;
                totalMetabolism += (Size - 1.0f) * SizeMetabolismFactor;
                totalMetabolism += (Speed - 1) * SpeedMetabolismFactor;
                totalMetabolism += (VisionRange - 3) * VisionMetabolismFactor;
                if (Type == AnimalType.Carnivore) totalMetabolism += (Aggression - 1.0f) * AggressionMetabolismFactor;
                return Math.Max(BaseMetabolism, totalMetabolism);
            }
        }

        public void EatAndAge(WorldCell? cell, List<Animal> cellMates, Grid grid, long currentTick)
        {
            if (!IsAlive || cell == null) return;
            Eat(cell, cellMates, currentTick);

            float amountToDrink = 2.0f * Size;
            float waterConsumed = 0;
            var waterSource = cell.Water > 0 ? cell : FindAdjacentWater(grid);
            if (waterSource != null) waterConsumed = waterSource.ConsumeWater(amountToDrink);
            Thirst -= waterConsumed;
            if (Thirst < 0) Thirst = 0;

            _ticksSinceLastAge++;
            if (_ticksSinceLastAge >= SimulationClock.TicksPerDay)
            {
                Age++;
                _ticksSinceLastAge = 0;
            }

            Hunger += MetabolicRate;
            Thirst += MetabolicRate * 1.2f;
            Stamina = Math.Min(MaxStamina, Stamina + StaminaRegenRate);

            if (Hunger < (MaxHunger * 0.25f) && Thirst < (MaxThirst * 0.25f) && Health < MaxHealth)
            {
                Health += 0.5f;
                if (Health > MaxHealth) Health = MaxHealth;
            }

            if (Hunger >= MaxHunger || Thirst >= MaxThirst || Age >= MaxAge) Die();
        }

        private void TryMoveTo(int newX, int newY, Grid grid, long currentTick, int moveSpeed, WeatherType currentWeather)
        {
            var targetCell = grid.GetCell(newX, newY);
            if (targetCell == null) return;

            int deltaX = newX - X;
            int deltaY = newY - Y;
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            float terrainMultiplier = targetCell.Terrain switch
            {
                TerrainType.Mountain => 3.0f,
                TerrainType.Water => 5.0f,
                TerrainType.Desert => 1.5f,
                TerrainType.Tundra => 1.2f,
                TerrainType.Wetlands => 2.0f,
                TerrainType.River => 2.0f,
                _ => 1.0f,
            };

            if (targetCell.TerritoryOwnerId.HasValue && targetCell.TerritoryOwnerId.Value != GroupId)
                terrainMultiplier *= 2.0f;

            const float movementHungerCost = 0.005f;
            const float movementThirstCost = 0.01f;

            float weatherPenalty = 1.0f;
            if (currentWeather == WeatherType.Snow)
            {
                if (targetCell.Terrain != TerrainType.Forest) weatherPenalty = 1.5f;
            }

            Hunger += (float)distance * movementHungerCost * terrainMultiplier * weatherPenalty * Size;
            Thirst += (float)distance * movementThirstCost * terrainMultiplier * weatherPenalty * Size;

            if (moveSpeed > 1)
            {
                Stamina -= StaminaDrainRate;
                if (Stamina < 0) Stamina = 0;
            }

            double moveSuccessChance = targetCell.Terrain switch
            {
                TerrainType.Mountain => 0.5,
                TerrainType.Water => 0.0,
                TerrainType.River => 0.3,
                _ => 1.0,
            };

            if (_random.NextDouble() < moveSuccessChance)
            {
                X = newX;
                Y = newY;
                if (targetCell != null)
                {
                    if (targetCell.TerritoryOwnerId == null || targetCell.TerritoryOwnerId == this.GroupId)
                    {
                        targetCell.TerritoryOwnerId = this.GroupId;
                        targetCell.LastTerritoryRefreshTick = currentTick;
                        targetCell.TerritoryStrength = this.TerritoryStrength;
                    }
                    var existingScent = targetCell.Scents.FirstOrDefault(s => s.GroupId == this.GroupId);
                    float scentStrength = 20.0f * this.Size;
                    if (existingScent != null) existingScent.Strength = scentStrength;
                    else targetCell.Scents.Add(new Scent(this.GroupId, this.Type, scentStrength));
                }
            }
        }

        private void MoveRandomly(Grid grid, long currentTick, int moveSpeed, WeatherType currentWeather)
        {
            int dx = _random.Next(-1, 2);
            int dy = _random.Next(-1, 2);
            int newX = Math.Clamp(X + (dx * moveSpeed), 0, grid.Width - 1);
            int newY = Math.Clamp(Y + (dy * moveSpeed), 0, grid.Height - 1);
            TryMoveTo(newX, newY, grid, currentTick, moveSpeed, currentWeather);
        }

        public void Move(Grid grid, IReadOnlyList<Animal> allAnimals, long currentTick, bool isNight, Season currentSeason, WeatherType currentWeather)
        {
            if (!ShouldBeActive(isNight, currentSeason, grid, currentTick, currentWeather)) return;

            var (localMates, nearbyPredators, rivalsOnHomeTurf) = IdentifyNearbyEntities(allAnimals, grid);

            if (HandleFear(nearbyPredators, grid, currentTick, currentWeather)) return;
            if (HandleInjury(grid, currentTick, currentWeather)) return;
            if (HandleTerritorialAggression(allAnimals, grid, currentTick, currentWeather)) return;

            PerformGoalSeekingMove(grid, allAnimals, currentTick, localMates, rivalsOnHomeTurf, currentWeather, isNight, currentSeason);
        }

        #region AI Behavior Sub-methods

        private bool ShouldBeActive(bool isNight, Season currentSeason, Grid grid, long currentTick, WeatherType currentWeather)
        {
            if (CanHibernate)
            {
                if (currentSeason == Season.Winter) IsHibernating = true;
                else if (IsHibernating && currentSeason != Season.Winter) IsHibernating = false;
            }
            if (IsHibernating) return false;

            bool isActiveTime = (IsNocturnal && isNight) || (!IsNocturnal && !isNight);
            if (!isActiveTime)
            {
                if (_random.NextDouble() < 0.8) return false;
                MoveRandomly(grid, currentTick, 1, currentWeather);
                return false;
            }
            return true;
        }

        private (List<Animal> localMates, List<Animal> nearbyPredators, List<Animal> rivalsOnHomeTurf) IdentifyNearbyEntities(IReadOnlyList<Animal> allAnimals, Grid grid)
        {
            var localMates = new List<Animal>();
            var nearbyPredators = new List<Animal>();
            var rivalsOnHomeTurf = new List<Animal>();

            foreach (var other in allAnimals)
            {
                if (other == this || !other.IsAlive) continue;
                int dX = X - other.X;
                int dY = Y - other.Y;
                double distSq = dX * dX + dY * dY;
                if (distSq > VisionRange * VisionRange) continue;

                if (this.Type != AnimalType.Carnivore && other.Type == AnimalType.Carnivore) nearbyPredators.Add(other);
                if (other.GroupId == this.GroupId) localMates.Add(other);
                else
                {
                    var rivalCell = grid.GetCell(other.X, other.Y);
                    if (rivalCell != null && rivalCell.TerritoryOwnerId == other.GroupId) rivalsOnHomeTurf.Add(other);
                }
            }
            return (localMates, nearbyPredators, rivalsOnHomeTurf);
        }

        private bool HandleFear(IReadOnlyList<Animal> nearbyPredators, Grid grid, long currentTick, WeatherType currentWeather)
        {
            if (!nearbyPredators.Any()) return false;
            var closestPredator = FindClosest(nearbyPredators);
            if (closestPredator != null)
            {
                _lastKnownPredatorLocation = (closestPredator.X, closestPredator.Y);
                _lastPredatorSightingTick = currentTick;
                float fearDx = X - closestPredator.X;
                float fearDy = Y - closestPredator.Y;
                int fleeSpeed = (Stamina > 0) ? Speed : 1;
                int fleeMoveDx = Math.Sign(fearDx);
                int fleeMoveDy = Math.Sign(fearDy);
                if (fleeMoveDx == 0 && fleeMoveDy == 0) { fleeMoveDx = _random.Next(-1, 2); fleeMoveDy = _random.Next(-1, 2); }
                int newX = Math.Clamp(X + (fleeMoveDx * fleeSpeed), 0, grid.Width - 1);
                int newY = Math.Clamp(Y + (fleeMoveDy * fleeSpeed), 0, grid.Height - 1);
                TryMoveTo(newX, newY, grid, currentTick, fleeSpeed, currentWeather);
                return true;
            }
            return false;
        }

        private bool HandleInjury(Grid grid, long currentTick, WeatherType currentWeather)
        {
            const float injuredHealthThreshold = 0.4f;
            if (this.Health < (this.MaxHealth * injuredHealthThreshold))
            {
                float injuredHomingDx = DenX - X;
                float injuredHomingDy = DenY - Y;
                if (Math.Abs(injuredHomingDx) < 1 && Math.Abs(injuredHomingDy) < 1) return false;
                int injuredHomingSpeed = (Stamina > 0) ? Speed : 1;
                float injuredFinalDx = injuredHomingDx * InjuredHomingWeight;
                float injuredFinalDy = injuredHomingDy * InjuredHomingWeight;
                int injuredMoveDx = Math.Sign(injuredFinalDx);
                int injuredMoveDy = Math.Sign(injuredFinalDy);
                int injuredNewX = Math.Clamp(X + (injuredMoveDx * injuredHomingSpeed), 0, grid.Width - 1);
                int injuredNewY = Math.Clamp(Y + (injuredMoveDy * injuredHomingSpeed), 0, grid.Height - 1);
                TryMoveTo(injuredNewX, injuredNewY, grid, currentTick, injuredHomingSpeed, currentWeather);
                return true;
            }
            return false;
        }

        private bool HandleTerritorialAggression(IReadOnlyList<Animal> allAnimals, Grid grid, long currentTick, WeatherType currentWeather)
        {
            var currentCell = grid.GetCell(this.X, this.Y);
            if (this.Type != AnimalType.Herbivore && currentCell != null && currentCell.TerritoryOwnerId.HasValue && currentCell.TerritoryOwnerId.Value == this.GroupId)
            {
                var intruders = allAnimals.Where(a => a.IsAlive && a.GroupId != this.GroupId).Where(a => (X - a.X) * (X - a.X) + (Y - a.Y) * (Y - a.Y) <= VisionRange * VisionRange);
                var closestIntruder = FindClosest(intruders);
                if (closestIntruder != null && _random.NextDouble() < this.Courage)
                {
                    float aggressionDx = closestIntruder.X - X;
                    float aggressionDy = closestIntruder.Y - Y;
                    int aggressionSpeed = (Stamina > 0) ? Speed : 1;
                    float aggressionFinalDx = aggressionDx * (TerritorialAggressionWeight * this.Aggression);
                    float aggressionFinalDy = aggressionDy * (TerritorialAggressionWeight * this.Aggression);
                    int aggressionMoveDx = Math.Sign(aggressionFinalDx);
                    int aggressionMoveDy = Math.Sign(aggressionFinalDy);
                    int aggressionNewX = Math.Clamp(X + (aggressionMoveDx * aggressionSpeed), 0, grid.Width - 1);
                    int aggressionNewY = Math.Clamp(Y + (aggressionMoveDy * aggressionSpeed), 0, grid.Height - 1);
                    TryMoveTo(aggressionNewX, aggressionNewY, grid, currentTick, aggressionSpeed, currentWeather);
                    return true;
                }
            }
            return false;
        }

        private void PerformGoalSeekingMove(Grid grid, IReadOnlyList<Animal> allAnimals, long currentTick, List<Animal> localMates, List<Animal> rivalsOnHomeTurf, WeatherType currentWeather, bool isNight, Season currentSeason)
        {
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
            float shelterDx = 0, shelterDy = 0;

            // 1. Shelter Seeking (Night & Winter)
            bool needsShelter = (isNight || currentSeason == Season.Winter) && !IsHibernating;
            if (needsShelter)
            {
                var myCell = grid.GetCell(X, Y);
                bool inShelter = myCell != null && (myCell.Terrain == TerrainType.Forest || myCell.Terrain == TerrainType.Mountain);
                if (!inShelter)
                {
                    (int x, int y)? closestShelter = null;
                    double minShelterDist = double.MaxValue;
                    for (int dx = -VisionRange; dx <= VisionRange; dx++)
                    {
                        for (int dy = -VisionRange; dy <= VisionRange; dy++)
                        {
                            int nx = X + dx;
                            int ny = Y + dy;
                            if (nx >= 0 && nx < grid.Width && ny >= 0 && ny < grid.Height)
                            {
                                var cell = grid.GetCell(nx, ny);
                                if (cell.Terrain == TerrainType.Forest || cell.Terrain == TerrainType.Mountain)
                                {
                                    double distSq = dx * dx + dy * dy;
                                    if (distSq < minShelterDist) { minShelterDist = distSq; closestShelter = (nx, ny); }
                                }
                            }
                        }
                    }
                    if (closestShelter.HasValue) { shelterDx = closestShelter.Value.x - X; shelterDy = closestShelter.Value.y - Y; }
                }
            }

            // 2. Social (Leadership)
            if (localMates.Any())
            {
                var groupMembers = localMates.Append(this).ToList();
                var alpha = groupMembers.OrderByDescending(a => a.Age + (a.Size * 10) + (a.TerritoryStrength * 5)).First();
                if (alpha == this)
                {
                    float centerX = (float)localMates.Average(m => m.X);
                    float centerY = (float)localMates.Average(m => m.Y);
                    cohesionDx = (centerX - X) * 0.1f;
                    cohesionDy = (centerY - Y) * 0.1f;
                }
                else
                {
                    cohesionDx = alpha.X - X;
                    cohesionDy = alpha.Y - Y;
                }
                foreach (var mate in localMates)
                {
                    int dX = X - mate.X;
                    int dY = Y - mate.Y;
                    double distSq = dX * dX + dY * dY;
                    if (distSq < SeparationDistance * SeparationDistance) { separationDx += dX; separationDy += dY; }
                }
            }

            if (Type != AnimalType.Carnivore && _lastKnownPredatorLocation.HasValue && currentTick - _lastPredatorSightingTick < MemoryDurationTicks)
            {
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
                            if (nCell.TerritoryOwnerId == GroupId) { territoryStayDx += dx; territoryStayDy += dy; }
                        }
                    }
                }
            }

            var currentCellBiome = grid.GetCell(this.X, this.Y)?.Biome;
            if (currentCellBiome.HasValue && !PreferredBiomes.Contains(currentCellBiome.Value))
            {
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
                                if (distSq < min_dist_sq) { min_dist_sq = distSq; closestPreferredCell = (nx, ny); }
                            }
                        }
                    }
                }
                if (closestPreferredCell.HasValue) { biomeSeekingDx = closestPreferredCell.Value.x - X; biomeSeekingDy = closestPreferredCell.Value.y - Y; }
            }

            int effVision = GetEffectiveVision(currentWeather);
            if (this.Type == AnimalType.Carnivore)
            {
                float maxPreyScentStrength = 0;
                (int x, int y) preyScentLocation = (0, 0);
                for (int dx = -effVision; dx <= effVision; dx++)
                {
                    for (int dy = -effVision; dy <= effVision; dy++)
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
                if (maxPreyScentStrength > 0) { scentAttractionDx = preyScentLocation.x - X; scentAttractionDy = preyScentLocation.y - Y; }
            }
            else
            {
                float maxPredatorScentStrength = 0;
                (int x, int y) predatorScentLocation = (0, 0);
                for (int dx = -effVision; dx <= effVision; dx++)
                {
                    for (int dy = -effVision; dy <= effVision; dy++)
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
                if (maxPredatorScentStrength > 0) { scentAversionDx = X - predatorScentLocation.x; scentAversionDy = Y - predatorScentLocation.y; }
            }

            homingDx = DenX - X;
            homingDy = DenY - Y;

            float hungerUrgency = Hunger / MaxHunger;
            float thirstUrgency = Thirst / MaxThirst;
            const float urgencyHysteresis = 0.15f;
            if (_seekingWater)
            {
                if (thirstUrgency < 0.05f || hungerUrgency > thirstUrgency + urgencyHysteresis) _seekingWater = false;
            }
            else if (hungerUrgency < 0.05f || thirstUrgency > hungerUrgency + urgencyHysteresis) _seekingWater = true;

            bool isThirstier = _seekingWater;

            if (isThirstier)
            {
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
                            if (cell != null && cell.Water > maxWater) { maxWater = cell.Water; bestWaterCell = (nx, ny); }
                        }
                    }
                }
                if (bestWaterCell.HasValue)
                {
                    var (targetX, targetY) = bestWaterCell.Value;
                    goalDx = targetX - X; goalDy = targetY - Y;
                    _lastKnownWaterLocation = bestWaterCell; _lastWaterSightingTick = currentTick;
                }
                else if (_lastKnownWaterLocation.HasValue && currentTick - _lastWaterSightingTick < MemoryDurationTicks)
                {
                    var (targetX, targetY) = _lastKnownWaterLocation.Value;
                    if (targetX == X && targetY == Y) _lastKnownWaterLocation = null;
                    else { goalDx = targetX - X; goalDy = targetY - Y; }
                }
            }
            else
            {
                if (Type == AnimalType.Carnivore)
                {
                    var smallPrey = allAnimals.Where(a => a.IsAlive && a.Type != AnimalType.Carnivore && a.Size < this.Size && IsInVision(a));
                    var closestFoodSource = FindClosest(smallPrey);
                    if (closestFoodSource == null)
                    {
                        var anyPreyOrCarcass = allAnimals.Where(a => a != this && IsInVision(a)).Where(a => (a.IsAlive && a.Type != AnimalType.Carnivore) || (!a.IsAlive && !a.IsConsumed));
                        closestFoodSource = FindClosest(anyPreyOrCarcass);
                    }
                    if (closestFoodSource != null) { goalDx = closestFoodSource.X - X; goalDy = closestFoodSource.Y - Y; }
                }
                else if (Type == AnimalType.Omnivore || Type == AnimalType.Herbivore)
                {
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
                                if (cell != null && cell.Food > maxPlantFood) { maxPlantFood = cell.Food; bestPlantCell = (nx, ny); }
                            }
                        }
                    }
                    if (Type == AnimalType.Omnivore)
                    {
                        var carcasses = allAnimals.Where(a => !a.IsAlive && !a.IsConsumed && IsInVision(a));
                        var closestCarcass = FindClosest(carcasses);
                        float plantScore = 0;
                        if (bestPlantCell.HasValue && maxPlantFood > 0)
                        {
                            float plantDist = (float)Math.Sqrt(Math.Pow(bestPlantCell.Value.x - X, 2) + Math.Pow(bestPlantCell.Value.y - Y, 2));
                            plantScore = maxPlantFood / (plantDist + 1f);
                        }
                        float carcassScore = 0;
                        if (closestCarcass != null)
                        {
                            float carcassDist = (float)Math.Sqrt(Math.Pow(closestCarcass.X - X, 2) + Math.Pow(closestCarcass.Y - Y, 2));
                            const float meatValue = 10f;
                            carcassScore = meatValue / (carcassDist + 1f);
                        }
                        if (plantScore > carcassScore) {
                            goalDx = bestPlantCell.Value.x - X;
                            goalDy = bestPlantCell.Value.y - Y;
                        } else if (closestCarcass != null) {
                            goalDx = closestCarcass.X - X;
                            goalDy = closestCarcass.Y - Y;
                        }
                    }
                    else if (Type == AnimalType.Herbivore)
                    {
                        if (bestPlantCell.HasValue) {
                            goalDx = bestPlantCell.Value.x - X;
                            goalDy = bestPlantCell.Value.y - Y;
                        }
                    }
                }

                // Calculate total direction
                float totalDx = cohesionDx * CohesionWeight + separationDx * SeparationWeight + lingeringFearDx * LingeringFearWeight + territorialAversionDx * TerritorialAversionWeight + territoryStayDx * TerritoryStayWeight + biomeSeekingDx * BiomeSeekingWeight + scentAttractionDx * ScentTrackingWeight + scentAversionDx * ScentAversionWeight + homingDx * HomingWeight + goalDx * (isThirstier ? WaterSeekingWeight : (Type == AnimalType.Carnivore ? PreySeekingWeight : FoodSeekingWeight));
                float totalDy = cohesionDy * CohesionWeight + separationDy * SeparationWeight + lingeringFearDy * LingeringFearWeight + territorialAversionDy * TerritorialAversionWeight + territoryStayDy * TerritoryStayWeight + biomeSeekingDy * BiomeSeekingWeight + scentAttractionDy * ScentTrackingWeight + scentAversionDy * ScentAversionWeight + homingDy * HomingWeight + goalDy * (isThirstier ? WaterSeekingWeight : (Type == AnimalType.Carnivore ? PreySeekingWeight : FoodSeekingWeight));

                if (needsShelter) {
                    totalDx += shelterDx * ShelterSeekingWeight;
                    totalDy += shelterDy * ShelterSeekingWeight;
                }

                if (Type != AnimalType.Herbivore && rivalsOnHomeTurf.Any()) {
                    totalDx += territorialAversionDx * TerritorialAversionWeight;
                    totalDy += territorialAversionDy * TerritorialAversionWeight;
                }

                if (totalDx != 0 || totalDy != 0) {
                    double length = Math.Sqrt(totalDx * totalDx + totalDy * totalDy);
                    totalDx /= (float)length;
                    totalDy /= (float)length;
                }

                int moveSpeed = (Stamina > 0) ? Speed : 1;
                int newX = Math.Clamp(X + (int)(totalDx * moveSpeed), 0, grid.Width - 1);
                int newY = Math.Clamp(Y + (int)(totalDy * moveSpeed), 0, grid.Height - 1);
                TryMoveTo(newX, newY, grid, currentTick, moveSpeed, currentWeather);
            }
        }

        #endregion

        private void Eat(WorldCell cell, List<Animal> cellMates, long currentTick)
        {
            if (Type == AnimalType.Carnivore || Type == AnimalType.Omnivore)
            {
                var edibleCarcasses = cellMates.Where(a => !a.IsAlive && !a.IsConsumed).ToList();
                if (edibleCarcasses.Any())
                {
                    var target = edibleCarcasses.OrderBy(c => (X - c.X) * (X - c.X) + (Y - c.Y) * (Y - c.Y)).First();
                    float amountToEat = Math.Min(target.CarcassFoodValue, MaxHunger - Hunger);
                    Hunger -= amountToEat;
                    target.CarcassFoodValue -= amountToEat;
                    if (target.CarcassFoodValue <= 0) target.IsConsumed = true;
                    _lastKnownFoodLocation = (X, Y);
                    _lastFoodSightingTick = currentTick;
                }
            }
            if (Type == AnimalType.Herbivore || Type == AnimalType.Omnivore)
            {
                if (cell != null && cell.Food > 0)
                {
                    float amountToEat = Math.Min(cell.Food, 2.0f * Size);
                    Hunger -= amountToEat;
                    cell.ConsumeFood(amountToEat);
                    _lastKnownFoodLocation = (X, Y);
                    _lastFoodSightingTick = currentTick;
                }
            }
        }

        public void Die()
        {
            IsAlive = false;
            CarcassFoodValue = Size * 10f;
        }

        private WorldCell FindAdjacentWater(Grid grid)
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
                        var cell = grid.GetCell(nx, ny);
                        if (cell != null && cell.Water > 0) return cell;
                    }
                }
            }
            return null;
        }

        private Animal FindClosest(IEnumerable<Animal> candidates)
        {
            Animal closest = null;
            double minDistSq = double.MaxValue;
            foreach (var candidate in candidates)
            {
                double distSq = (X - candidate.X) * (X - candidate.X) + (Y - candidate.Y) * (Y - candidate.Y);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    closest = candidate;
                }
            }
            return closest;
        }

        private bool IsInVision(Animal other)
        {
            int dX = X - other.X;
            int dY = Y - other.Y;
            double distSq = dX * dX + dY * dY;
            return distSq <= VisionRange * VisionRange;
        }

        public void JoinGroup(Guid newGroupId)
        {
            GroupId = newGroupId;
        }

        public void SetDen(int denX, int denY)
        {
            DenX = denX;
            DenY = denY;
        }

        public void LeaveGroup()
        {
            GroupId = Guid.NewGuid();
        }

        public void Hunt(List<Animal> cellMates, Grid grid)
        {
            var prey = cellMates.Where(a => a.IsAlive && a.Type != AnimalType.Carnivore && a.Size < this.Size).ToList();
            if (prey.Any())
            {
                var target = prey.OrderBy(p => (X - p.X) * (X - p.X) + (Y - p.Y) * (Y - p.Y)).First();
                target.TakeDamage(this.Aggression * this.Size, this);
                if (!target.IsAlive)
                {
                    // Maybe move to the carcass or something, but for now, just eat later
                }
            }
        }

        public bool IsCrowded(int nearbySameSpecies)
        {
            return nearbySameSpecies > 5; // arbitrary
        }

        public bool IsReadyToReproduce()
        {
            return Age >= MinReproductionAge && Hunger < (MaxHunger * ReproductionHungerThreshold) && Thirst < (MaxThirst * ReproductionHungerThreshold) && Health > (MaxHealth * 0.5f);
        }

        public List<Animal> CreateOffspring(Random random)
        {
            var offspring = new List<Animal>();
            int count = random.Next(1, BaseOffspringCount + 1);
            for (int i = 0; i < count; i++)
            {
                offspring.Add(new Animal(this, random));
            }
            Hunger += BaseReproductionCostPerOffspring * count;
            return offspring;
        }

        public void DecayCarcass()
        {
            CarcassFoodValue = Math.Max(0, CarcassFoodValue - 0.1f);
        }

        public void TakeDamage(float damage, Animal? attacker)
        {
            Health -= damage;
            if (Health <= 0)
            {
                Die();
            }
        }
    }
}
