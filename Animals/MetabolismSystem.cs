using System.Collections.Generic;
using LivingSim.World;

namespace LivingSim.Animals
{
    public class MetabolismSystem
    {
        public void ProcessMetabolism(List<Animal> animalsThisTick, Dictionary<(int, int), List<Animal>> allAnimalLocations, Grid grid, long currentTick)
        {
            foreach (var animal in animalsThisTick)
            {
                if (animal.IsAlive) // Animal might have been killed in step 3
                {
                    allAnimalLocations.TryGetValue((animal.X, animal.Y), out var cellMates);
                    animal.EatAndAge(grid.GetCell(animal.X, animal.Y), cellMates ?? new List<Animal>(), grid, currentTick);
                }
            }
        }
    }
}
