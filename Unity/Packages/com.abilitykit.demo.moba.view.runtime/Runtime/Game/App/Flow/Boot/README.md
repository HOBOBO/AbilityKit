# 启动调试/UI 边界

[`Boot`](Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/App/Flow/Boot) 目录当前包含启动菜单与根调试 OnGUI Feature。

约束：

- [`BootMenuOnGUIFeature`](Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/App/Flow/Boot/BootMenuOnGUIFeature.cs) 与 [`RootDebugOnGUIFeature`](Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/App/Flow/Boot/RootDebugOnGUIFeature.cs) 默认只应在 Editor/Development 配置下注册。
- 后续 asmdef 分层阶段可迁移至 DebugTools 或 DemoTools 程序集。
