# MOBA Gameplay Resources

`minion_routes.json` is the authoring source for map-bound minion routes and the initial wave schedule. It is intentionally separate from the static `battle_maps` Luban table: routes and schedules describe gameplay behavior, while `battle_maps` describes map geometry and spawn points.

Edit the source through Unity's `Tools/AbilityKit/Demos/MOBA/Edit Minion Routes` window. `Validate And Export` validates the map identity, route geometry, default route, and wave schedule, then publishes identical runtime copies to:

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Resources/moba/minion_routes.json`
- `src/AbilityKit.Demo.Moba.Console/Configs/moba/minion_routes.json`

Do not edit the published copies directly. Resource version 4 requires an explicit `MapId`, `DefaultRouteId`, `WaveSchedule`, `MaxActiveMinionsPerTeam`, and at least one route. The per-team capacity counts both active and queued minions, so a full wave is delayed until both teams have room. Versions 1 through 3 remain readable as migration compatibility; new authoring must use version 4.

`map_structures.json` is the companion map-structure source. It binds structure placements and attribute/model templates to a map without mixing combat state into collision geometry. It is loaded only when the gameplay feature `structures` is enabled; bases and towers are then spawned as regular damageable actors.
