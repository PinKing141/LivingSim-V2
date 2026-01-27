using LivingSim.World;
using LivingSim.Core;

namespace LivingSim.Environment
{
    /// <summary>
    /// Updates the world grid each tick: regenerates and decays resources.
    /// </summary>
    public sealed class EnvironmentTickSystem
    {
        private const int BaseTerritoryDecayDurationTicks = 500; // Base duration for a claim with strength 1.0
        private const float ScentDecayRate = 0.05f; // How quickly scent fades per tick

        public void Tick(Grid grid, Season currentSeason, long currentTick)
        {
            // Determine resource regeneration rates based on the current season.
            (float foodRate, float waterRate) rates = currentSeason switch
            {
                Season.Spring => (0.2f, 0.2f),   // Reduced bountiful growth
                Season.Summer => (0.15f, 0.15f),  // Reduced steady growth
                Season.Autumn => (0.08f, 0.1f),  // Growth slows significantly
                Season.Winter => (0.01f, 0.05f), // Harsh winter
                _ => (0.1f, 0.1f)
            };

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var cell = grid.GetCell(x, y);

                    // --- Overgrazing Mechanic ---
                    // If a cell has been eaten bare, its food regenerates much slower.
                    float currentFoodRate = rates.foodRate;
                    if (cell.Food < 1.0f) // If the cell is overgrazed
                        currentFoodRate *= 0.3f;

                    cell.AddFood(currentFoodRate);
                    cell.AddWater(rates.waterRate);

                    // --- Scent Decay ---
                    for (int i = cell.Scents.Count - 1; i >= 0; i--)
                    {
                        var scent = cell.Scents[i];
                        scent.Strength -= ScentDecayRate;
                        if (scent.Strength <= 0)
                            cell.Scents.RemoveAt(i);
                    }

                    // --- Territory Decay ---
                    if (cell.TerritoryOwnerId.HasValue)
                    {
                        float effectiveDecayDuration = BaseTerritoryDecayDurationTicks * cell.TerritoryStrength;
                        if (currentTick - cell.LastTerritoryRefreshTick > effectiveDecayDuration)
                            cell.TerritoryOwnerId = null; // Territory is lost
                    }

                    // Optional: decay resources
                    cell.DecayResources();
                }
            }
        }
    }
}
