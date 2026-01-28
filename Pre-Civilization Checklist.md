# 🛠️ Pre-Civilization Checklist
**Goal:** Prepare the engine for Human Agents (`Homo Sapiens`).
**Current Status:** Nature Simulation (Reactive/Instinctual).
**Target Status:** Civilization Simulation (Planned/Intentional).

---

## 1. 🧠 Intelligence & Navigation (CRITICAL)
*Unlike animals that "wander" or "flee", humans need to go to specific destinations (e.g., Home -> Mine -> Storage).*

- [ ] **A* Pathfinding Implementation**
    - Implement a proper `Pathfinder` class.
    - Logic to calculate the shortest route avoiding Water (unless using boats) and Mountains.
    - *Why:* Humans need to walk *around* obstacles to get to work.
- [ ] **Movement Cost Maps**
    - The Grid needs a cached "Cost Map" (e.g., Road = 1, Grass = 2, Forest = 5).
    - *Why:* Humans should prefer walking on paths rather than through swamps.

## 2. 🎒 Inventory & Items
*Animals consume resources instantly. Humans carry, store, and refine them.*

- [ ] **The `Item` Class**
    - Create a data structure for items (e.g., `Name`, `Weight`, `Type`).
    - Examples: `WoodLog`, `StoneChunk`, `Meat`, `Berry`.
- [ ] **Inventory System**
    - Create an `Inventory` class (List of Items + Max Weight capacity).
    - *Why:* Humans need to chop a tree, put `Wood` in their bag, and carry it to a construction site.
- [ ] **Stockpiles / Containers**
    - The `WorldCell` needs to be able to hold dropped items (a pile of wood on the ground).

## 3. 🏗️ The Structural Layer
*The world currently only supports `Terrain` and `Resources`. Humans need to place artificial objects.*

- [ ] **Update `WorldCell` for Structures**
    - Add a `Structure` property to the cell (separate from Terrain).
    - Add `Walkable` boolean flag (Walls block movement, Floors do not).
- [ ] **Basic Structures**
    - Define: `Wall`, `Door`, `Floor`, `StorageBin`.
    - *Why:* Humans need to build shelter to survive the Winter/Night you just implemented.

## 4. 🌡️ Advanced Biology (Status Effects)
*Humans are fragile. They need distinct states that affect their efficiency.*

- [ ] **Temperature Simulation**
    - Calculate "Local Temperature" based on Season + Time + Weather + Shelter.
    - *Why:* If a Human sleeps outside in Winter, they should freeze/die. This drives the motivation to build houses.
- [ ] **Health System Upgrade**
    - Replace simple HP with specific body parts or conditions? (recommended).
    - Add `Stamina` recovery logic (Sleeping in a bed vs. sleeping on the floor).

## 5. 🖱️ UI & Inspection (Quality of Life)
*With humans, there is too much data to just look at colors. You need to inspect specific agents.*

- [ ] **"Inspector" Mode**
    - Ability to pause and click/select a specific coordinate.
    - Display: "Human #45 (Alice) | Inventory: 5 Wood | Current Task: Going to Build Wall".
    - *Why:* Debugging pathfinding and AI tasks is impossible without seeing what the agent is *thinking*.

## 6. 🧹 Code Architecture Refactor
*The `WorldManager` and `Animal` classes are becoming "God Objects".*

- [ ] **Behavior Tree / Task System**
    - Move away from `if(hungry) Eat()` loops.
    - Implement a `Task` queue system: `Queue: [GoToTree, ChopTree, PickupWood, GoHome]`.
- [ ] **Event Bus**
    - Ensure the `GraphRecorder` and `Visualizer` are decoupled from the simulation logic using C# Events.
