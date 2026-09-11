# MOBA Trigger Authoring Test IDs

The JSON fixtures in this directory are callable Trigger Plans, so their `id`
field is a trigger ID rather than a row in the Skill config table.

MOBA trigger authoring fixtures reserve `99,100,000-99,199,999`. These IDs are
test-only and must never be exported into the formal MOBA runtime database.
Future tests that require real Skill config rows reserve
`99,200,000-99,299,999`; that range is intentionally unused by this batch.

These fixtures are Trigger Authoring integration smoke tests. They verify that
source JSON exports and delegates to the existing MOBA runtime; they do not
replace the behavioral acceptance tests owned by Buff, projectile, movement,
targeting, shield, resource, or Area systems. Add another fixture only when it
proves an authoring/export/composition contract that those system tests cannot.

| Range | Purpose |
| --- | --- |
| `99,100,000-99,109,999` | Authoring infrastructure and control flow |
| `99,110,000-99,119,999` | Target search and combat effects |
| `99,120,000-99,129,999` | Buffs and status interactions |
| `99,130,000-99,139,999` | Projectiles |
| `99,140,000-99,149,999` | Movement and displacement |
| `99,190,000-99,199,999` | Cross-system composite scenarios |

Current composite fixtures use `99,190,001` for shield plus resource gain,
`99,190,002` for bounded group pull, `99,190,003` for an Area Runtime-owned
persistent area, and `99,190,004` for owner-bound overheal-to-shield event
composition. Cast phases and charge state remain responsibilities of the skill
pipeline rather than Trigger Plan nodes.

The matching constants and range assertions live in
`Tests/TriggerAuthoring/MobaTriggerAuthoringTestIds.cs`.
