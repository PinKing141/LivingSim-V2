using System;
using System.Collections.Generic;
using System.Linq;
using LivingSim.World;
using LivingSim.Core; // Added for Season enum

namespace LivingSim.Environment
{
    public class AnimalManager
    {
        private readonly List<Animal> _animals = new List<Animal>();
        private readonly int _width;
        private readonly int _height;
        private readonly Random _random;

        // Plague Settings
        private const int PlagueCheckInterval = 100; // Check every 100 ticks
        private const float PlagueThresholdRatio = 0.20f; // Species is overpopulated if > 20% of map area
        private const float PlagueChance = 0.05f; // 5% chance to trigger plague if overpopulated
        private const float PlagueMortalityRate = 0.4f; // Wipes out 40% of the population

        public AnimalManager(int width, int height, Random random)
        {
            _width = width;
            _height = height;
            _random = random;
        }

        public void SpawnAnimal(AnimalType type, int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
                return;
            
            // Randomly assign a species based on the AnimalType
            Species species = type switch
            {
                AnimalType.Herbivore => _random.NextDouble() < 0.5 ? Species.Deer : Species.Rabbit,
                AnimalType.Omnivore => _random.NextDouble() < 0.5 ? Species.Bear : Species.Boar,
                AnimalType.Carnivore => _random.NextDouble() < 0.5 ? Species.Wolf : Species.Fox,
                _ => throw new ArgumentOutOfRangeException(nameof(type), "Unknown AnimalType for species assignment.")
            };

            _animals.Add(new Animal(species, x, y, _random));

            // Note: The Animal constructor now determines AnimalType internally from Species.
            // This ensures consistency between Species and Type.
        }

        public void Tick(Grid grid, long currentTick, bool isNight, Season currentSeason)
        {
            var animalsThisTick = _animals.ToList(); // Work on a copy to avoid modification issues.

            // 1. All animals move.
            foreach (var animal in animalsThisTick)
            {
                if (animal.IsAlive)
                {
                    animal.Move(grid, animalsThisTick, currentTick, isNight, currentSeason);
                }
            }

            // 2. Social Dynamics: Expel members from oversized groups.
            var groups = animalsThisTick.Where(a => a.IsAlive).GroupBy(a => a.GroupId);
            foreach (var group in groups)
            {
                if (group.Count() > Animal.MaxGroupSize)
                {
                    // Expel the youngest, non-hibernating adult to encourage new group formation.
                    var expellee = group
                        .Where(a => a.Age >= Animal.MinReproductionAge && !a.IsHibernating)
                        .OrderBy(a => a.Age)
                        .FirstOrDefault();

                    if (expellee != null)
                    {
                        // Try to find a new, smaller group of the same species to join.
                        var potentialNewGroups = groups
                            .Where(g => g.Key != expellee.GroupId) // Not its own group
                            .Where(g => g.First().Species == expellee.Species) // Same species
                            .Where(g => g.Count() < Animal.MaxGroupSize) // Not full
                            .Select(g => new { Group = g, CenterX = g.Average(m => m.X), CenterY = g.Average(m => m.Y) }) // Pre-calculate center
                            .ToList();
                        
                        if (potentialNewGroups.Any())
                        {
                            // Find the closest suitable group to join, based on the group's center.
                            var closestGroup = potentialNewGroups
                                .OrderBy(g => Math.Pow(expellee.X - g.CenterX, 2) + Math.Pow(expellee.Y - g.CenterY, 2))
                                .First();
                            
                            var newGroup = closestGroup.Group;
                            expellee.JoinGroup(newGroup.Key);
                            expellee.SetDen(newGroup.First().DenX, newGroup.First().DenY);
                        }
                        else
                        {
                            // If no suitable group is found, the animal becomes a founder of a new group.
                            expellee.LeaveGroup();
                        }
                    }
                }
            }

            // 3. Aggressive animals hunt for prey or defend territory.
            var allAnimalLocations = animalsThisTick
                .GroupBy(a => (a.X, a.Y))
                .ToDictionary(g => g.Key, g => g.ToList());

            var aggressiveAnimals = animalsThisTick.Where(a => a.IsAlive && a.Type != AnimalType.Herbivore);
            foreach (var animal in aggressiveAnimals)
            {
                if (allAnimalLocations.TryGetValue((animal.X, animal.Y), out var cellMates) && cellMates.Count > 1)
                {
                    animal.Hunt(cellMates, grid);
                }
            }

            // 4. All animals eat (plants), age, and metabolize.
            foreach (var animal in animalsThisTick)
            {
                if (animal.IsAlive) // Animal might have been killed in step 3
                {
                    allAnimalLocations.TryGetValue((animal.X, animal.Y), out var cellMates);
                    animal.EatAndAge(grid.GetCell(animal.X, animal.Y), cellMates ?? new List<Animal>(), grid);
                }
            }

            // 5. Animals reproduce if conditions are met.
            var newborns = new List<Animal>();
            foreach (var animal in animalsThisTick)
            {
                // Density Check: Count same-species neighbors in 3x3 area
                int nearbySameSpecies = 0;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (allAnimalLocations.TryGetValue((animal.X + dx, animal.Y + dy), out var neighbors))
                        {
                            nearbySameSpecies += neighbors.Count(n => n.Species == animal.Species && n.IsAlive);
                        }
                    }
                }

                if (!animal.IsCrowded(nearbySameSpecies) && animal.IsReadyToReproduce() && _random.NextDouble() < (Animal.BaseReproductionChance * animal.Fertility))
                {
                    newborns.AddRange(animal.CreateOffspring(_random)); // Add all new offspring
                }
            }

            // Add newborns to the main list. They will start their lifecycle in the next tick.
            _animals.AddRange(newborns);

            // 6. Decay carcasses that are not fully eaten.
            foreach (var animal in animalsThisTick.Where(a => !a.IsAlive))
            {
                animal.DecayCarcass();
            }

            // 7. Remove all dead and fully consumed/decayed animals from the simulation.
            _animals.RemoveAll(animal => !animal.IsAlive && animal.CarcassFoodValue <= 0);

            // 8. Occasional Plague Event to control overpopulation
            if (currentTick % PlagueCheckInterval == 0)
            {
                HandlePlagueEvents();
            }
        }

        public IReadOnlyList<Animal> GetAnimals()
        {
            return _animals;
        }

        private void HandlePlagueEvents()
        {
            var speciesCounts = _animals.Where(a => a.IsAlive)
                                        .GroupBy(a => a.Species)
                                        .ToDictionary(g => g.Key, g => g.Count());
            
            int mapArea = _width * _height;

            foreach (var kvp in speciesCounts)
            {
                Species species = kvp.Key;
                int count = kvp.Value;

                // If a species is dominating the map
                if (count > mapArea * PlagueThresholdRatio)
                {
                    if (_random.NextDouble() < PlagueChance)
                    {
                        TriggerPlague(species);
                    }
                }
            }
        }

        private void TriggerPlague(Species species)
        {
            var targets = _animals.Where(a => a.IsAlive && a.Species == species).ToList();
            int killCount = (int)(targets.Count * PlagueMortalityRate);

            // Plague targets the weak: Lowest Health and Oldest first
            var victims = targets.OrderBy(a => a.Health)
                                 .ThenByDescending(a => a.Age)
                                 .Take(killCount);

            foreach (var victim in victims)
            {
                // Disease strikes!
                victim.TakeDamage(victim.MaxHealth * 2, null); 
            }
        }
    }
}
