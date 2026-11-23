# AbilityKit Combat Projectile

`com.abilitykit.combat.projectile` 提供通用投射物和区域运行时、按帧发射调度与生命周期通知。玩法效果、伤害结算和表现由使用方接入。

## 安装与接入

从项目配置的包来源安装本包；Unity 版本及依赖见 [`package.json`](package.json)。在世界装配处提供 `ICollisionService`，用 `ProjectileService` 接入。在正式运行前先确认由哪个系统按帧推进并消费通知，避免重复调用 `Tick`。

- [使用契约与接入顺序](Documentation~/Usage.md)：发射方式、时钟、通知和恢复边界。
- [历史设计入口](Document/README.md)。
