# Tiny Unity 示例

Tiny 是 AbilityKit 的最小双人实时联机示例。它提供 Unity 客户端、场景和 Room 装配，演示 State、Frame 与 Hybrid，不是独立游戏框架。纯规则由 `com.abilitykit.demo.tiny.logic` 提供；可选回合制由独立的 `com.abilitykit.demo.tiny.turn` 提供。

## 安装与首次使用

1. 在项目中安装本包，确认 Unity 版本和依赖可按 [package.json](package.json) 解析。
2. 准备包含 Tiny 玩法模块的配套 Host 和 Room Gateway；仅安装 Unity 包不能启动联机。
3. 从 Package Manager 导入 [`07 Project Entry`](Samples~/07-ProjectEntry/README.md)，按[接入指南](Documentation~/IntegrationGuide.md)从自有大厅启动。Starter 不是必需依赖。

其余样例与限制见[文档索引](Documentation~/README.md)。
