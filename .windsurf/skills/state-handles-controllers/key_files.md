---
name: state-handles-controllers
section: key_files
---

# Key files (typical)

以 battle session 为例：

- `Runtime/Game/Flow/Battle/Features/Session/Core/BattleSessionState.cs`
- `Runtime/Game/Flow/Battle/Features/Session/Core/BattleSessionHandles.cs`
- `Runtime/Game/Flow/Battle/Features/Session/Controllers/*Controller.cs`
- `Runtime/Game/Flow/Battle/Features/Session/SubFeatures/*SubFeature.cs`
- `Runtime/Game/Flow/Battle/Features/Session/Core/BattleSessionFeature.*.cs`（accessors/host ports/dispose helpers 等）
