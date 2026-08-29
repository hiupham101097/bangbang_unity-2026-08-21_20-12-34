# Logic, Data Structures, and Algorithms Standards

## Data Structure Selection
1. **Performance over Convenience**: Always choose the appropriate data structure for the time/space complexity required. E.g., Use `Set` for fast lookups, `Map` for key-value associations rather than generic Objects if keys are dynamic, and Typed Arrays when dealing with raw binary data or performance-critical math.
2. **Immutability vs Mutability**: Default to immutable operations for state management to avoid side-effects. Only use mutable structures in hot-paths (e.g., game loops, physics calculations) where garbage collection overhead would cause frame drops.
3. **Graph and Spatial Data**: For 2D/3D spaces, utilize spatial partitioning structures (Quadtrees, Octrees, or Grid-based spatial hashing) instead of O(N^2) naive collision/proximity checks.

## Algorithmic Patterns
1. **Early Exits**: Write guard clauses at the top of functions. Fail fast.
2. **Complexity limits**: Avoid nested loops exceeding O(N^2) without a documented justification. 
3. **Memoization & Caching**: Cache expensive function calls. In games, pre-calculate sine/cosine tables or heavy pathfinding maps where appropriate.
4. **State Machines**: Use explicit Finite State Machines (FSM) or Behavior Trees for complex logic (e.g., AI behaviors, UI state flows) rather than deeply nested `if/else` or `switch` statements.

## Code Quality
- Add clear Big-O notation comments on complex algorithms.
- Separate logic from presentation (MVC/ECS principles).
- Keep pure functions pure. Side effects should be strictly contained and obvious.
