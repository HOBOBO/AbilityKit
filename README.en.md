# Ability Kit

## Introduction

Ability Kit is a high-performance game ability and logic framework built on the Unity engine. It employs a modular design to decouple core functionalities such as skill editing, trigger logic, combat systems, and attribute calculations, and provides a suite of visual editor tools to help developers rapidly construct complex game logic.

## Key Features

1. **Visual Skill Editor (Timeline Editor)**
   *   Timeline-based skill editing experience supporting multiple tracks: Animation, Audio, Effect, and Transform (position/rotation/scale).
   *   Supports configuration of logic nodes and export to JSON for runtime parsing (`ActionSchema`).

2. **Flexible Trigger System (Trigger System)**
   *   Event-driven architecture.
   *   Supports powerful conditional combinations (AND/OR/NOT) and behavior lists.
   *   Provides an editor interface (`AbilityListWindow`) for managing Trigger configurations, with support for filtering and exporting.

3. **Target Search (SearchTarget)**
   *   High-performance target finding module supporting various shape detections: circular, conical, and oriented rectangular areas.
   *   Supports fast filtering and scoring排序 (TopK) based on indices (Tag/Key).
   *   Offers a composable Provider/Selector pattern to decouple candidate sources from filtering logic.

4. **General Game Modules (Common Modules)**
   *   **Attribute System**: Supports formula-based calculations, dependencies, and constraints (Clamp).
   *   **Motion System**: Grouped, priority-based, and stackable action control system with support for blending and cross-group suppression.
   *   **Projectile System**: Complete projectile management (launch, collision, rollback, area detection).
   *   **Object Pool**: High-performance, zero-GC object pool implementation.

5. **Server & Frame Synchronization**
   *   Provides foundational infrastructure for a logic world server (`LogicWorldServer`).
   *   Supports frame packet distribution and snapshot rollback.

## Project Structure

```
Assets/Scripts/Ability/
├── Editor/                          # Unity editor extension code
│   ├── Triggering/                  # Trigger list window, toolbar, variable key management
│   └── ...
├── Impl/                            # Core runtime implementations
│   ├── ActionEditorImpl/            # Runtime parsing and playback of skill timelines (Tracks, Clips)
│   ├── Triggering/                  # Runtime execution logic for triggers (DebugLog, ExecuteEffect)
│   ├── BattleDemo/                  # Combat-related demos
│   └── Server/                      # Frame synchronization server implementation
├── Share/
│   ├── Base/                        # Basic definitions (ActionDef, ConditionDef, TriggerContext, etc.)
│   ├── ActionSchema/                # Data structures (DTO) and runtime loading for skill timelines
│   ├── Battle/                      # Core combat modules
│   │   ├── EntityManager/           # Entity management (EntityRegistry, KeyedEntityIndex)
│   │   ├── SearchTarget/            # Target search system (Rules, Scorers, Selectors)
│   │   └── SkillLibrary/            # Skill library indexing and management
│   └── Common/                      # General utility libraries
│       ├── AttributeSystem/         # Attribute system
│       ├── MotionSystem/            # Motion system
│       ├── Projectile/              # Projectile system
│       └── Pool/                    # Object pool
```

## Dependencies

This project uses the following third-party plugins; ensure they are properly referenced in your Unity project:

*   **DOTween** (Demigiant): Used for animation transitions and path animations.
*   **Odin Inspector** (Sirenix): Used to enhance editor UI and complex property serialization validation.

## License

This project is licensed under the terms specified in the LICENSE file.