# Projectile 使用契约与接入顺序

> 文档类型：使用指南
> 适用范围：0.1.0 当前 Runtime，含单弹推进与碰撞分文件；依赖见 [package.json](../package.json)。
> 核校状态：已核校本文的发射、时钟、通知、清理及服务级恢复；旧示例与完整业务绑定未整体核校。
> 事实基线：2026-11-25；E0 与局部 .NET E3，未新增场景 E4 或 CI E5。

## 目标与前置条件

本包通过 ProjectileWorld、AreaWorld 和统一 ProjectileService 管理临时对象的运动、检测、命中策略及生命周期通知。调用方提供碰撞世界；目标、阵营、业务实体、伤害和表现由宿主接入。安装见[包入口](../README.md)。

单一逻辑驱动者提供帧号与固定步长（秒），世界尺度、速度与碰撞单位保持一致。不要让 Unity Update、项目物理系统和 ProjectileTickSystem 同时推进同一服务。

## 完整示例与预期结果

以下完整 Console 示例安排第 1 帧发射并验证命中、退出；导入第 0 帧快照后按相同输入重演。目标碰撞体是宿主前置条件，没有随本包快照重建。

```csharp
using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;

public static class Program
{
    public static void Main()
    {
        using var collisions = new CollisionService();
        using var service = new ProjectileService(collisions);
        var target = collisions.World.Add(
            new Transform3(new Vec3(5, 0, 0), Quat.Identity, Vec3.One),
            ColliderShape.CreateSphere(Vec3.Zero, 0.5f), layerId: 1);
        var spawn = new ProjectileSpawnParams(
            ownerId: 10, templateId: 20, launcherActorId: 10, rootActorId: 10,
            spawnFrame: 0, position: Vec3.Zero, direction: Vec3.Right, speed: 10f,
            returnAfterFrames: 0, returnSpeed: 0f, returnStopDistance: 0f,
            lifetimeFrames: 20, maxDistance: 20f, collisionLayerMask: 1 << 1,
            ignoreCollider: ColliderId.Invalid);
        var task = service.ScheduleEmit(new SingleShotPattern(), spawn,
            ProjectileScheduleParams.Once(1));
        var checkpoint = service.ExportRollback(new FrameIndex(0));
        var spawns = new List<ProjectileSpawnEvent>();
        var hits = new List<ProjectileHitEvent>();
        var exits = new List<ProjectileExitEvent>();
        for (var pass = 0; pass < 2; pass++)
        {
            if (pass == 1)
            {
                service.ImportRollback(new FrameIndex(0), checkpoint);
                if (!service.HasSchedule(task) || service.PendingNotificationCount != 0)
                    throw new Exception("恢复未来任务失败");
            }
            spawns.Clear(); hits.Clear(); exits.Clear();
            service.Tick(1, 0.5f);
            service.DrainSpawnEvents(spawns);
            service.DrainHitEvents(hits);
            service.DrainExitEvents(exits);
            if (spawns.Count != 1 || hits.Count != 1 || exits.Count != 1 ||
                spawns[0].Frame != 1 || service.ActiveCount != 0 || service.HasSchedule(task))
                throw new Exception("调度、命中或退出不符合预期");
        }
        service.ImportRollback(new FrameIndex(0), Array.Empty<byte>());
        collisions.World.Remove(target);
        Console.WriteLine("Projectile: schedule, hit, exit, restore and replay OK");
    }
}
```

预期打印末行且不抛异常。目标没有阵营或伤害状态，例子仅验证调度、命中和服务恢复。

## 装配与唯一时钟

构造 ProjectileService 必须提供 ICollisionService。普通直线发射不需额外 provider；追踪、返程、弹射或 Actor 去重需要对应依赖。手工接入可通过 OnInit(IWorldResolver) 绑定，或直接使用 ProjectileWorld 的 setter；构造函数不自动查找宿主服务。

ProjectileWorldModule 只注册 Scoped 的 IProjectileService，不创建碰撞服务。ProjectileSystemsModule 另安装 ProjectileTickSystem；与自动发现组合时宿主须避免重复安装。TickSystem 优先使用 IFrameTime，否则使用 IWorldClock 与本地递增帧；fallback 不等于受恢复管理的权威帧时钟。

Tick 固定顺序为 schedules → AreaWorld → ProjectileWorld。调度新建的弹体可同帧移动；物理后由业务事件创建的 Area 在下一次正常 Tick 检测。不会补跑缺失物理帧，不应为补通知重复 Tick。

## 发射、运动与取消

| 入口或参数 | 当前契约 |
| --- | --- |
| `Spawn` | 立即创建，Spawn 通知帧固定为 0；参数 SpawnFrame 仍用于运行状态时间计算 |
| `ScheduleEmit` | 到期 Tick 构建发射列表，将每个 SpawnFrame 与通知帧设为当前 frame |
| `Once/Repeat/Infinite` | Count 是发射批次数；建议正数或 Infinite；非正间隔实际至少隔一个后续 Tick |
| `CancelSchedule` | 仅取消未来任务，不移除已发射对象；缺失返回 false |
| `LifetimeFrames` | 正数按实际 Tick 消耗，非正数视作无限寿命；挂起不消耗寿命 |
| `MaxDistanceResponse` | 默认 Exit；SuspendAtDestination 发 Arrival 后挂起且不能 Resume；ReturnToSource 与定时返程互斥 |
| `Despawn(id)` | 静默移除；需要 Exit 通知时用带 frame/reason 的重载 |
| `TrySetPosition/ResumeSimulation` | 返回是否成功，不能恢复已到达挂起的弹体 |

调度迟到时每次 Tick 对每个到期任务最多发一批，以下次 frame = 当前 frame + interval 继续，不补发遗漏批次。PatternProvider 返回 null 时任务保留待重试。服务保留模式对象/Provider 引用，调用方保证行为稳定；回调异常向外传播，前面已生成的对象不自动撤销。

命中默认 ExitOnHit，Pierce/Bounce 由 kind/param 和预算控制。碰撞体忽略、冷却、Actor 身份及分类响应分层处理；OncePerActor/PerLeg 需身份 provider，追踪和弹射需目标 provider。稳定选取由宿主负责。单步最多处理 8 次接触扫描，不代表无限密度穿透保证。

## 通知队列与 Area 生命周期

Drain 将队列**追加**到调用方 List 并清空队列，传 null 不消费；Peek 只复制，重复调用会重复追加。复用 List 前自行 Clear。每种通知由唯一 drain 者读取后分发给多个消费者；分类队列不构成统一全序日志。

Arrival 通过 IProjectileArrivalEventSource 获取。PendingNotificationCount 统计十类临时通知，不含未来任务。通知不执行伤害、Trigger 或表现，不替宿主绑定实体。

Area 使用 SpawnArea(params, frame)，正常 Tick 产生 Enter/Stay/Exit/Expire。ConsumeOnFirstEnter 的业务接纳需调用 TryConsumeAreaEnter / TryAcceptAreaEnter；候选 Enter 不等于效果已提交。ActiveCount 只统计投射物，不含 Area 和 schedules。

- DespawnArea(id, frame) 是玩法移除，会产生 Expire。
- IAreaRollbackService.CancelArea(id) 静默取消并删除未消费的对应 Spawn 通知，不撤回此前产生/消费的 Enter 或外部效果。
- Dispose 当前只清空通知和调度，不清空活动 ProjectileWorld/AreaWorld。结束时停止驱动，按业务语义删除或以空载荷导入静默清空，再释放外部绑定与引用；不能在复用服务上把 Dispose 当完整重置。

## 可选恢复契约

普通使用不要求回滚。启用时优先捕获整个服务；单独 ProjectileWorld 载荷不能代替服务级捕获。

| 状态 | 当前服务快照范围 |
| --- | --- |
| Projectile | 物理、分配器、身份历史、挂起/返程等；子载荷版本 18 |
| Area | 区域、重叠/边界采样、组资格及分配器；当前导出版本 5 |
| 未来任务 | 顺序、NextFrame、Remaining、模式描述、基础参数、过滤描述、NextScheduleId；服务 envelope 版本 2 |
| 未消费通知 | 不捕获；Import 成功后清空十类通知，不补历史事件 |
| 外部依赖 | 碰撞世界、目标/provider、随机流、Actor/Cast/来源、伤害与表现由宿主恢复 |

内建 SingleShot/Burst/Fan/Scatter 支持值描述。自定义 pattern/provider 需描述与 IProjectileSchedulePatternResolver；计划发射显式 HitPolicy 实例不支持捕获，应使用可恢复 kind/param。自定义 filter 须按 key 或值描述绑定目标世界，不能静默回退默认过滤。

非空服务快照仅接受当前版本并校验帧、名单和依赖；旧版拒绝。Import 的 null/空载荷是静默清空。预检不承诺外部 resolver 副作用或跨 provider 原子事务，宿主仍要处理失败。帧同步和状态同步由会话分别选择输入重演或权威基线恢复；本包不决定同步模式，不提供输入确认或可靠事件水位。

## 常见问题与验证范围

| 现象 | 检查方向 |
| --- | --- |
| 一帧移动两次 | 手工 Tick、自动发现与 SystemsModule 是否重复安装 |
| 第二个消费者没有事件 | 首个已 Drain，应统一读取后分发 |
| 导入后没有历史 Spawn/Hit | 通知不恢复；按恢复状态重建投影，按宿主合同重演后续输入 |
| 自定义策略捕获失败 | 是否缺值描述/解析器，或保存不可恢复实例 |
| Dispose 后仍有活动对象 | Dispose 不清物理状态，需显式删除或空载荷导入 |

2026-11-25 在 Windows / .NET 10 Release 经 core-stability 的 Combat projectile deterministic kinematics tests 单步运行直属 suite，非完整 gate。完整示例编译运行，覆盖调度、命中、Exit、服务恢复与两次一致执行。

源码：[服务](../Runtime/Projectile/Services/ProjectileService.cs)、[服务恢复](../Runtime/Projectile/Services/ProjectileService.Rollback.cs)、[单弹推进](../Runtime/Projectile/Runtime/ProjectileWorld.TickProcessing.cs)、[碰撞处理](../Runtime/Projectile/Runtime/ProjectileWorld.Collision.cs)。未执行 Unity GUI/Player、干净 UPM 安装、完整技能/表现/绑定恢复、跨平台确定性、压力预算或 CI；旧资料见[历史索引](../Document/README.md)。
