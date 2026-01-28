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
using LivingSim.Animals;

class Program
{
    static void Main()
    {
        // ----------------------------
        // 1. Setup Simulation (Large Grid)
        // ----------------------------
        var random = new Random(12345);
        var clock = new SimulationClock();
        var grid = new Grid(width: 500, height: 500);

        var environment = new EnvironmentTickSystem();
        var metrics = new MetricsCollector();
        var animals = new AnimalManager(grid.Width, grid.Height, random);
        var visualizer = new ConsoleVisualizer();

        // ----------------------------
        // 2. Camera Setup
        // ----------------------------
        int camX = 0, camY = 0;
        int viewWidth = 60, viewHeight = 30;

        // ----------------------------
        // 3. Generate world
        // ----------------------------
        var generator = new WorldGenerator(random);
        Console.WriteLine("Generating World...");
        generator.Generate(grid, null, 0);

        // ----------------------------
        // 4. Spawn animals (Clusters)
        // ----------------------------
        void SpawnCluster(AnimalType type, int count, int radius)
        {
            int cx = random.Next(grid.Width);
            int cy = random.Next(grid.Height);
            for (int i = 0; i < count; i++) {
                int x = Math.Clamp(cx + random.Next(-radius, radius), 0, grid.Width-1);
                int y = Math.Clamp(cy + random.Next(-radius, radius), 0, grid.Height-1);
                animals.SpawnAnimal(type, x, y);
            }
        }

        for(int i=0; i<15; i++) SpawnCluster(AnimalType.Carnivore, 4, 5); // Wolf Packs
        for(int i=0; i<40; i++) SpawnCluster(AnimalType.Herbivore, 6, 10); // Deer Herds
        for(int i=0; i<20; i++) SpawnCluster(AnimalType.Omnivore, 2, 5);   // Bear Families

        var manager = new WorldManager(clock, grid, environment, metrics, animals);

        // ----------------------------
        // 5. Game Loop with TIME WARP
        // ----------------------------
        int simulationDelay = 50;
        bool isPaused = false;
        bool showStats = false;
        bool showTerritories = false;
        bool isTimeWarp = false; // <--- NEW FLAG

        List<Dictionary<Species, int>> history = new List<Dictionary<Species, int>>();
        Console.CursorVisible = false;

        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                // Controls
                if (key == ConsoleKey.LeftArrow)  camX = Math.Max(0, camX - 5);
                if (key == ConsoleKey.RightArrow) camX = Math.Min(grid.Width - viewWidth, camX + 5);
                if (key == ConsoleKey.UpArrow)    camY = Math.Max(0, camY - 5);
                if (key == ConsoleKey.DownArrow)  camY = Math.Min(grid.Height - viewHeight, camY + 5);
                if (key == ConsoleKey.Spacebar) isPaused = !isPaused;
                if (key == ConsoleKey.S) showStats = !showStats;
                if (key == ConsoleKey.T) showTerritories = !showTerritories;
                if (key == ConsoleKey.Tab) isTimeWarp = !isTimeWarp; // TOGGLE WARP
                if (key == ConsoleKey.OemPlus || key == ConsoleKey.Add) simulationDelay = Math.Max(0, simulationDelay - 10);
                if (key == ConsoleKey.OemMinus || key == ConsoleKey.Subtract) simulationDelay += 10;
            }

            if (isTimeWarp)
            {
                // --- WARP MODE ---
                // No Drawing, No Sleep. Just raw calculation.
                manager.Tick();

                // Draw a simple status bar occasionally so you know it's working
                if (clock.CurrentTick % 50 == 0)
                {
                    Console.SetCursorPosition(0, 0);
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[TIME WARP ACTIVE] Year {clock.Year} | Day {clock.DayOfYear} | Tick {clock.CurrentTick}      ");
                    Console.ResetColor();
                }
            }
            else
            {
                // --- NORMAL MODE ---
                visualizer.Draw(grid, animals.GetAnimals(), clock, history, showStats, showTerritories, camX, camY, viewWidth, viewHeight);
                Console.WriteLine($"Arrows: Cam | Tab: Time Warp | Space: Pause | T: Territory".PadRight(Console.WindowWidth - 1));

                if (!isPaused)
                {
                    manager.Tick();

                    // Update History
                    var currentStats = animals.GetAnimals()
                        .Where(a => a.IsAlive)
                        .GroupBy(a => a.Species)
                        .ToDictionary(g => g.Key, g => g.Count());
                    history.Add(currentStats);
                    if (history.Count > viewWidth) history.RemoveAt(0);

                    Thread.Sleep(simulationDelay);
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
}
