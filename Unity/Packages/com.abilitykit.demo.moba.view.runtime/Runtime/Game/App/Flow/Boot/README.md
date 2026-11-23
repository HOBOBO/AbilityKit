# 启动调试 / UI 入口

本目录保留旧导航入口；[`BootMenuOnGUIFeature`](../../../../UI/App/Debug/BootMenuOnGUIFeature.cs) 和 [`RootDebugOnGUIFeature`](../../../../UI/App/Debug/RootDebugOnGUIFeature.cs) 位于 `Runtime/UI/App/Debug/`。

从[客户端 UI Feature 注册](../Composition/MobaUiFeatureRegistration.cs)核对接线。当前 `boot_menu` 受 `UNITY_EDITOR || DEVELOPMENT_BUILD` 编译条件约束；`root_debug` 的工厂注册不在该条件内，不把历史约束写成已实现的统一编译开关。
