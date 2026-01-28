using System;
using System.Collections.Generic;
using System.Linq;

namespace LivingSim.Animals
{
    public class ReproductionSystem
    {
        private readonly Random _random;

        public ReproductionSystem(Random random)
        {
            _random = random;
        }

        public List<Animal> ProcessReproduction(List<Animal> animalsThisTick, Dictionary<(int, int), List<Animal>> allAnimalLocations)
        {
            var newborns = new List<Animal>();

            // Group animals by group for easier mate finding
            var animalsByGroup = animalsThisTick.Where(a => a.IsAlive).GroupBy(a => a.GroupId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var group in animalsByGroup.Values)
            {
                var readyToReproduce = group.Where(a => a.IsReadyToReproduce()).ToList();

                foreach (var animal in readyToReproduce)
                {
                    // Find a mate: another ready animal in the same group
                    var potentialMates = readyToReproduce.Where(m => m != animal).ToList();

                    if (potentialMates.Any())
                    {
                        // Select a random mate
                        var mate = potentialMates[_random.Next(potentialMates.Count)];

                        // Reproduction chance
                        if (_random.NextDouble() < Animal.BaseReproductionChance)
                        {
                            // Create offspring
                            var offspring = animal.CreateOffspring(_random);
                            newborns.AddRange(offspring);
                        }
                    }
                }
            }

            return newborns;
        }
    }
}
