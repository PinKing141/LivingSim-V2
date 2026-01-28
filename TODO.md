# Simulation Fixes & Polish TODO

## Critical Logic Fixes
- [x] Fix food regrowth with cellular automata (EnvironmentTickSystem.cs)
- [x] Add movement hysteresis to prevent ping-pong (Animal.cs)
- [x] Add hunt failure chance for predators (Animal.cs)
- [x] Stricter reproduction checks (AnimalManager.cs)
- [x] Adjust metabolism rates for 120-day year (Animal.cs)

## Performance & Code Quality
- [x] Create SimConfig class for magic numbers (Config/SimConfig.cs)
- [x] Extract ReproductionSystem (Animals/ReproductionSystem.cs)
- [x] Extract CombatSystem (Animals/CombatSystem.cs)
- [x] Extract MetabolismSystem (Animals/MetabolismSystem.cs)
- [x] Move magic numbers to SimConfig

## Minor Bugs
- [x] Fix console flicker (ConsoleVisualizer.cs)
- [x] Fix visual glitch on map edges (ConsoleVisualizer.cs)
