using LivingSim.World;
using LivingSim.Environment;
using LivingSim.Observation;
using LivingSim.Core; // Added for Season enum

namespace LivingSim.Core
{
    /// <summary>
    /// Central orchestrator for managing the simulation world.
    /// Owns systems but contains no simulation logic itself.
    /// </summary>
    public sealed class WorldManager
    {
        public SimulationClock Clock { get; }
        public Grid WorldGrid { get; }

        private readonly EnvironmentTickSystem _environmentTick;
        private readonly MetricsCollector _metrics;
        private readonly AnimalManager _animals;

        public WorldManager(
            SimulationClock clock,
            Grid worldGrid,
            EnvironmentTickSystem environmentTick,
            MetricsCollector metrics,
            AnimalManager animals)
        {
            Clock = clock;
            WorldGrid = worldGrid;
            _environmentTick = environmentTick;
            _metrics = metrics;
            _animals = animals;
        }

        /// <summary>
        /// Advances the simulation by one tick.
        /// </summary>
        public void Tick()
        {
            long previousTick = Clock.CurrentTick;

            // 1. Advance clock
            Clock.AdvanceTick();

            // 2. Update environment (resources)
            _environmentTick.Tick(WorldGrid, Clock.CurrentSeason, Clock.CurrentTick);

            // 3. Tick animals (consume resources)
            _animals.Tick(WorldGrid, Clock.CurrentTick, Clock.IsNight, Clock.CurrentSeason);

            // 4. Collect metrics
            _metrics.CollectMetrics(WorldGrid, Clock);
            _metrics.PrintMetrics(Clock.CurrentTick);
        }
    }
}
