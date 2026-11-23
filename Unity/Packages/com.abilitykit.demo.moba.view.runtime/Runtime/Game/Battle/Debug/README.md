# 战斗调试工具边界

[`BattleDebugOnGUIFeature`](BattleDebugOnGUIFeature.cs) 提供示例调试 UI，不是权威战斗入口。工厂映射见[客户端 UI Feature 注册](../../App/Flow/Composition/MobaUiFeatureRegistration.cs)；注册工厂不等于所有流程都启用该 Feature。

正式战斗 Runtime 不应依赖 Debug UI 实现。是否启用由实际 Feature 装配决定，不在本文宣称已有独立 DebugTools 编译门禁。
