# Projectile 文档索引

> 文档类型：使用导航
> 事实基线：2026-11-25；服务、时钟、通知及恢复契约已限定核校，历史说明保留旧路径。

| 阅读目标 | 入口 |
| --- | --- |
| 定位、环境与安装 | [包入口](../README.md) |
| 完整调度/命中示例、所有权和恢复 | [使用指南](Usage.md) |
| 核对服务端口 | [IProjectileService](../Runtime/Projectile/Services/IProjectileService.cs) |
| 核对构造参数 | [ProjectileSpawnParams](../Runtime/Projectile/Runtime/ProjectileSpawnParams.cs) / [AreaSpawnParams](../Runtime/Projectile/Area/AreaSpawnParams.cs) |
| 访问早期材料 | [历史索引](../Document/README.md) |

普通使用不要求回滚；启用时由宿主选择一致的帧边界并准备恢复依赖。来源对象、业务副作用和表现由上层交接。
