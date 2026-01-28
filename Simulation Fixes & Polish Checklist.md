# 🛠️ Simulation Fixes & Polish Checklist
**Goal:** Stabilize the current "Nature Sim" before adding new complexity.
**Focus:** Logic Bugs, Math Errors, and Performance bottlenecks.

---

## 1. 🚨 Critical Logic Fixes (The "Simulation Killers")
*These issues effectively break the simulation over long runtimes.*

- [ ] **The "Drought" Bug (Water Logic)**
    - **Issue:** Your map is 250,000 tiles (`500x500`). `ReplenishWater()` currently picks only **20 random tiles** per tick to refill.
    - **Result:** Animals drink faster than rain refills. The world will inevitably turn into a desert.
    - **Fix:** Scale replenishment with map size (e.g., `Grid.Width * Grid.Height * 0.01` tiles per tick) or use a "Chunk" update system.

- [ ] **The "Starvation Loop" (Food Regrowth)**
    - **Issue:** Similar to water, Grass/Bushes regrow too slowly for the population density.
    - **Result:** Herbivores eat everything, die out, Predators starve, Simulation ends.
    - **Fix:** Implement a cellular automata rule (e.g., "If neighbor has grass, empty tile has 5% chance to grow grass"). This makes nature "spread" naturally.

- [ ] **The "Ping-Pong" Movement**
    - **Issue:** Animals sometimes get stuck bouncing between two tiles (Cell A -> Cell B -> Cell A) because the "Desire Vectors" perfectly cancel each other out.
    - **Fix:** Add a small "hysteresis" or momentum. If moving North, give a 10% bonus to continue moving North next tick.

## 2. ⚖️ Gameplay Balancing
*The math works, but it feels wrong.*

- [ ] **Predator Efficiency**
    - **Issue:** Wolves currently have `Aggression` and `Damage`. If they are too strong, they kill the entire deer population in Year 1.
    - **Fix:** Introduce "Hunt Failure Chance". Even if a Wolf touches a Deer, there should be a 50% chance the Deer escapes.

- [ ] **Reproduction Explosion**
    - **Issue:** Rabbits breed exponentially. `newborns.AddRange(...)`.
    - **Result:** Can crash the CPU with 50,000 rabbits.
    - **Fix:** Hard cap on population per species or stricter "Overcrowding" checks (e.g., "If > 3 rabbits in 5x5 area, fertility = 0").

- [ ] **Age & Time Scale**
    - **Issue:** We changed the year to 120 Days.
    - **Check:** Ensure `MetabolicRate` isn't too high. Animals shouldn't starve to death in 2 days if a Year is 120 days. They need fat reserves.

## 3. 💻 Performance & Code Quality
*Things that make the game lag or hard to read.*

- [ ] **Console Flicker**
    - **Issue:** `Console.SetCursorPosition(0,0)` is slow.
    - **Fix:** Only redraw tiles that *changed* or use a Windows API buffer (advanced). Or, just accept it for now.

- [ ] **Magic Numbers**
    - **Issue:** `Animal.cs` is full of numbers like `0.5f`, `2.0f`, `10f`.
    - **Fix:** Move these to a `SimConfig` static class so you can tweak the whole game from one file.

- [ ] **The "God" Manager**
    - **Issue:** `AnimalManager.Tick` is doing too much (Movement, Breeding, Plague, Eating).
    - **Fix:** Extract `ReproductionSystem`, `CombatSystem`, and `MetabolismSystem` into their own classes.

## 4. 🐛 Known Minor Bugs
- [ ] **Input Lag:** Holding Arrow Keys to move the camera freezes the simulation thread. (Need to move Input to a separate thread).
- [ ] **Visual Glitch:** Animals on the edge of the map might not render if the Camera is centered wrongly.
