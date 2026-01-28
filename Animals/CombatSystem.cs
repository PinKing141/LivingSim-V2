using System.Collections.Generic;
using System.Linq;
using LivingSim.World;
using LivingSim.Core;

namespace LivingSim.Animals
{
    public class CombatSystem
    {
        public void ProcessHunting(List<Animal> animalsThisTick, Dictionary<(int, int), List<Animal>> allAnimalLocations, Grid grid)
        {
            var aggressiveAnimals = animalsThisTick.Where(a => a.IsAlive && a.Type != AnimalType.Herbivore);
            foreach (var animal in aggressiveAnimals)
            {
                if (allAnimalLocations.TryGetValue((animal.X, animal.Y), out var cellMates) && cellMates.Count > 1)
                {
                    animal.Hunt(cellMates, grid);
                }
            }
        }
    }
}
