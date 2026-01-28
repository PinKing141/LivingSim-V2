# 🏛️ LivingSim: Master Architecture & Rules
**Project Vision:** A realistic, emergent civilization simulation.
**Role:** The Observer (The user does not interfere; the world evolves autonomously).
**Core Tenet:** "The Simulation is Truth. Visuals are just a View."

---

## 1. 📜 Project Philosophy
1.  **No Magic:** All mechanics must be grounded in physical or biological reality. No "Mana", no "Spawning from thin air" (except initial seed). Matter cannot be created or destroyed, only transformed.
2.  **Emergence Over Scripting:** Do not script "A war happens." Instead, program "Scarcity" and "Tribalism," and let the war happen naturally.
3.  **The Observer Principle:** The user interface is strictly a viewing tool. There should be no "God Powers" to smite agents or magically place buildings. The simulation must be able to run `Headless` (without any UI) for rapid data generation.

---

## 2. 🏗️ Core Architecture

### 2.1 The "Headless" Rule (Strict Decoupling)
The Simulation Layer (`Core`, `World`, `Animals`) must **NEVER** reference the Presentation Layer (`Console`, `Visualisation`).
-   **Why:** Allows switching to Unity/Godot later or running 10,000-year simulations in 5 seconds on a server.
-   **Implementation:** The Simulation modifies data. The Visualizer reads data.

### 2.2 Composition Over Inheritance
Stop creating deep inheritance trees (`Object > Living > Animal > Mammal > Human`).
-   **Rule:** Use **Components** to define capabilities.
-   **Future Agent Structure:**
    ```csharp
    class Agent {
        Guid Id;
        List<IComponent> Components; // [Metabolism, Movement, Inventory, Social]
    }
    ```
-   **Why:** A "Wagon" can have `Inventory` and `Movement` but no `Metabolism`.

### 2.3 Deterministic State
-   **Rule:** If the `Seed` is the same, the history of the world must be identical, down to the exact tick a specific wolf dies.
-   **Implementation:** Never use `System.Random()` or `DateTime.Now`. Always use the `WorldRandom` instance passed down from `WorldManager`.

---

## 3. 👮 Coding Standards ("The Law")

### 3.1 The Configuration Law
-   **Illegal:** Hardcoding numbers (`if hunger > 10`).
-   **Legal:** `if (hunger > SimConfig.HungerThreshold)`.
-   **Action:** All simulation constants must live in `Core/SimConfig.cs`.

### 3.2 The Context Rule
-   **Rule:** If a method requires more than 3 context-specific arguments (Grid, Weather, Time, Neighbors), wrap them in a `SimulationContext` object.
-   **Why:** Prevents breaking every method signature when you add a new factor (like "Radiation" or "Disease").

### 3.3 Performance "Hot Paths"
-   **Rule:** No memory allocation inside the `Tick()` loop.
    -   *Bad:* `var neighbors = new List<Animal>();` (inside loop).
    -   *Good:* Reuse a single pre-allocated List, or use `Span<T>` / Arrays.
-   **Rule:** No `LINQ` in Hot Paths (`Where`, `Select`, `ToList`) inside `Tick`. Use `for` loops.

---

## 4. 🌍 The World Model

### 4.1 The Layered Cell
A `WorldCell` is not just a tile. It is a container for multiple layers of reality.
-   **Base Layer:** Geology (Rock, Soil, Water).
-   **Biosphere Layer:** Plants, Bacteria (Plague), Ground Cover.
-   **Construction Layer:** Roads, Walls, Floors.
-   **Atmosphere Layer:** Gas, Temperature, Scent, Sound.

### 4.2 Resource Conservation
-   **Rule:** Resources are finite.
-   **Implementation:** Trees don't "respawn" on a timer. They grow from seeds dropped by other trees. Water doesn't "appear"; it rains from the clouds (Atmosphere Layer) and flows downhill.

---

## 5. 🧠 The Human Mind (Agent Architecture)

To achieve realism, Humans cannot use simple state machines (`If hungry -> Eat`). They must use **GOAP (Goal-Oriented Action Planning)**.

### 5.1 Needs vs. Desires
-   **Physiological (Hard Constraints):** Hunger, Thirst, Temperature. Failure = Death.
-   **Psychological (Soft Constraints):** Social Status, Comfort, Purpose. Failure = Mental Break / Rebellion.

### 5.2 The Action Protocol
Humans interact with the world through **Work Ticks**.
-   *Instant:* Picking a berry.
-   *Process:* Chopping a tree (Takes 50 ticks). Building a wall (Takes 200 ticks).
-   **Why:** This simulates "Labor." A civilization is limited by its available Labor Hours.

---

## 6. 📊 Data, History & The Graph

### 6.1 The Global Event Bus
We do not analyze the simulation state; we analyze the **Events**.
-   **Mechanism:**
    ```csharp
    // Agents publish events to the Bus
    EventBus.Publish(new DeathEvent { Victim = this.Id, Cause = "Starvation", Tick = 1054 });
    ```
-   **Result:** This stream of events allows you to reconstruct the "History of the World" (Lineage, Wars, Migrations).

### 6.2 Universal IDs
-   **Rule:** Every Agent, City, and distinct Item must have a unique `Guid`.
-   **Rule:** Relationships (Marriage, Citizenship) are stored as ID references, not object references.

---

## 7. 🛠️ Development Workflow

Before merging any new feature (e.g., "Farming"), you must verify:

1.  [ ] **Reality Check:** Does this violate thermodynamics? (e.g., Magic food).
2.  [ ] **AI Independence:** Can the agent figure out how to use this *without* me scripting it specifically? (Does it fit into the GOAP system?).
3.  [ ] **Data Integrity:** Is the action recorded in the Graph/History?
4.  [ ] **Configurability:** Are the stats in `SimConfig`?
5.  [ ] **Performance:** Does it scale to 10,000 agents?

---

### 🚀 The Grand Plan (Roadmap)
1.  **Phase 1: Nature (Current)** - Animals, Weather, Biology.
2.  **Phase 2: The Primitives** - Human Body, Inventory, Simple Tools, Shelter.
3.  **Phase 3: The Tribe** - Social Structures, Language (Memes), Hierarchy.
4.  **Phase 4: The City** - Agriculture, Specialization, Trade, Laws.
5.  **Phase 5: The Nations** - War, Diplomacy, Cultural borders.
