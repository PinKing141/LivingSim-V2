using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LivingSim.Core;
using LivingSim.World;
using LivingSim.Environment;
using LivingSim.Generation;
using LivingSim.Observation;
using LivingSim.Visualisation;
using LivingSim.Animals; // <--- ADDED THIS

class Program
{
    static void Main()
    {
        // ----------------------------
        // 1. Create core objects
        // ----------------------------
        var random = new Random(12345);
        var clock = new SimulationClock();
        var grid = new Grid(width: 30, height: 15); // A wider world for better viewing
        var environment = new EnvironmentTickSystem();
        var metrics = new MetricsCollector();
        var animals = new AnimalManager(grid.Width, grid.Height, random);
        var visualizer = new ConsoleVisualizer();

        // ----------------------------
        // 2. Generate world
        // ----------------------------
        // The WorldGenerator now uses the shared 'random' instance to ensure
        // the entire simulation is deterministic from a single source.
        var generator = new WorldGenerator(random);
        generator.Generate(grid, (g) => {
            visualizer.Draw(g, new List<Animal>(), clock, new List<Dictionary<Species, int>>(), false);
        }, 20);

        // ----------------------------
        // 3. Spawn some animals
        // ----------------------------
        // Spawn a small pack of wolves
        for (int i = 0; i < 3; i++)
        {
            animals.SpawnAnimal(AnimalType.Carnivore, random.Next(grid.Width), random.Next(grid.Height));
        }
        // Spawn a herd of herbivores
        for (int i = 0; i < 12; i++) // Further increased starting herbivores
        {
            animals.SpawnAnimal(AnimalType.Herbivore, random.Next(grid.Width), random.Next(grid.Height));
        }
        // Spawn a group of omnivores
        for (int i = 0; i < 8; i++) // Further increased starting omnivores
        {
            animals.SpawnAnimal(AnimalType.Omnivore, random.Next(grid.Width), random.Next(grid.Height));
        }

        // ----------------------------
        // 4. Create world manager
        // ----------------------------
        var manager = new WorldManager(clock, grid, environment, metrics, animals);

        // ----------------------------
        // 5. Run simulation
        // ----------------------------
        int simulationDelay = 100;
        bool isPaused = false;
        bool showStats = false;
        List<Dictionary<Species, int>> history = new List<Dictionary<Species, int>>();
        Console.CursorVisible = false;
        for (int i = 0; i < 2000; ) // Increased duration to see more seasons
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.UpArrow) simulationDelay = Math.Max(0, simulationDelay - 10);
                if (key == ConsoleKey.DownArrow) simulationDelay += 10;
                if (key == ConsoleKey.Spacebar) isPaused = !isPaused;
                if (key == ConsoleKey.S) showStats = !showStats;
            }

            visualizer.Draw(grid, animals.GetAnimals(), clock, history, showStats);
            Console.WriteLine($"Delay: {simulationDelay}ms (Up: Faster, Down: Slower, Space: Pause, S: Stats) {(isPaused ? "[PAUSED]" : "")}".PadRight(Console.WindowWidth > 0 ? Console.WindowWidth - 1 : 80));
            
            if (!isPaused)
            {
                manager.Tick();

                // Record population history
                var currentStats = animals.GetAnimals()
                    .Where(a => a.IsAlive)
                    .GroupBy(a => a.Species)
                    .ToDictionary(g => g.Key, g => g.Count());
                history.Add(currentStats);
                if (history.Count > 60) history.RemoveAt(0); // Keep last 60 ticks (approx width of graph)

                Thread.Sleep(simulationDelay);
                i++;
            }
            else
            {
                Thread.Sleep(100);
            }
        }
        Console.CursorVisible = true;

        Console.WriteLine("Simulation complete.");
    }
}