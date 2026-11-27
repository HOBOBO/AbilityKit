# AbilityKit Combat Projectile

`com.abilitykit.combat.projectile` 提供通用投射物和区域运行时、按帧发射调度与生命周期通知。玩法效果、伤害结算和表现由使用方接入。

## 安装与接入

在 Unity Package Manager 中从项目配置的来源安装；本地开发可选择本目录 `package.json`。本次核校版本 0.1.0、Unity 最低声明 2022.3，直接依赖 Core、World DI/Entitas/FrameSync/ECS、Collision Abstractions、Deterministic 和 MemoryPack，版本以 [`package.json`](package.json) 为准。本批没有重做干净 UPM 安装。

在世界装配处提供 ICollisionService，用 ProjectileService 接入；公开命名空间为 `AbilityKit.Combat.Projectile`。确认唯一帧推进者与通知消费者，再执行[完整调度与命中示例](Documentation~/Usage.md#完整示例与预期结果)。通知描述生命周期，伤害与表现由上层执行。

- [包内文档索引](Documentation~/README.md)。
- [使用契约与接入顺序](Documentation~/Usage.md)：发射方式、时钟、通知和恢复边界。
- [历史设计入口](Document/README.md)。

## 生命周期与限制

宿主拥有碰撞世界、目标 provider、业务绑定与时钟。Tick 顺序为 schedules → Area → Projectile；已有驱动安装后不能再手工 Tick。Drain 会消费队列并追加到调用方 List，不能作为持久事件日志。

服务快照包含弹体、Area 与未来发射任务，不含未消费通知及宿主业务状态。Dispose 当前只清空通知和调度，不移除仍活动的 Projectile/Area；结束前按指南显式清理，由宿主释放绑定和引用。

> 核校状态：2026-11-25 对照当前 Runtime（含单弹推进/碰撞分文件）、.NET suite 和完整本地示例核校；局部 E3 不代表 Unity、完整技能或跨平台恢复验收。
