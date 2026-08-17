# AbilityKit Continuous

`com.abilitykit.continuous` 负责长生命周期、限定所有者范围的流程领域模型和运行时协调，不依赖 `com.abilitykit.core`。

本包提供生命周期状态和结束原因、持续行为/配置/管理器契约、准入策略、生命周期绑定器、所有者索引及默认管理器实现。玩法专用标签、修改器、定时器和表现层绑定仍留在各自所属包中。

请使用 `AbilityKit.Continuous` 命名空间。原 `AbilityKit.Core.Continuous` API 仅作为弃用兼容接口面保留至下一个主版本移除窗口。
