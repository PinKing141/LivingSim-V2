using LivingSim.World;
using LivingSim.Core;

namespace LivingSim.Observation
{
    /// <summary>
    /// Aggregates global metrics from the world for observation or logging.
    /// </summary>
    public class MetricsCollector
    {
        public float TotalFood { get; private set; }
        public float TotalWater { get; private set; }
        public float TotalTimber { get; private set; }

        public void CollectMetrics(Grid grid, SimulationClock clock)
        {
            float food = 0;
            float water = 0;
            float timber = 0;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var cell = grid.GetCell(x, y);
                    food += cell.Food;
                    water += cell.Water;
                    timber += cell.Timber;
                }
            }

            TotalFood = food;
            TotalWater = water;
            TotalTimber = timber;
        }

        public void PrintMetrics(long tick)
        {
            System.Console.WriteLine($"Tick {tick}: Food={TotalFood:0.00}, Water={TotalWater:0.00}, Timber={TotalTimber:0.00}");
        }
    }
}
