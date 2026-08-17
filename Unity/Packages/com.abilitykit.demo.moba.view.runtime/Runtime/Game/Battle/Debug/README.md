# 战斗调试工具边界

[`Debug`](Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Debug) 目录只承载开发期调试能力，例如 [`BattleDebugOnGUIFeature`](Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Debug/BattleDebugOnGUIFeature.cs)。

约束：

- Debug Feature 注册应通过 Editor/Development 条件控制。
- 正式战斗 Runtime 不应依赖 Debug Tools 的具体实现。
- 后续 asmdef 分层阶段可迁移到 DebugTools 程序集并通过 define constraints 控制编译。
