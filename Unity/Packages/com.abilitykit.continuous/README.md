# AbilityKit Continuous

> 文档类型：包入口与使用导航
> 核校状态：2026-11-25 对照接口、默认管理器和既有测试；.NET 测试 3 项通过。未运行 Unity Player。

`com.abilitykit.continuous` 负责长生命周期、限定所有者范围的流程领域模型和运行时协调，不依赖 `com.abilitykit.core`。

本包提供生命周期状态和结束原因、持续行为/配置/管理器契约、准入策略、生命周期绑定器、所有者索引及默认管理器实现。玩法专用标签、修改器、定时器和表现层绑定仍留在各自所属包中。

请使用 `AbilityKit.Continuous` 命名空间。原 `AbilityKit.Core.Continuous` API 仅作为弃用兼容接口面保留至下一个主版本移除窗口。

## 安装与使用

包版本和 Unity 最低版本以 [package.json](package.json) 为准。可从[使用契约](Documentation~/Usage.md)了解注册、激活、结束与所有者索引。持续实例由调用方实现 `IContinuous`；管理器不提供默认计时推进。

## 文档

- [文档索引](Documentation~/README.md)。
- [使用契约与限制](Documentation~/Usage.md)：所有者、策略、绑定器、失败和回滚边界。
