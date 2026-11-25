# Host Extensions 使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：已核校本页选择、步进和装配边界；其他 Client/Server/CatchUp 子系统不是本批全量验收范围。

## 从固定步长开始

以下完整示例不创建网络或世界。真实应用将回调绑定到唯一的模拟步进入口，避免同一世界被多个 runner 重复推进。

```csharp
using System;
using AbilityKit.Ability.Host.Extensions.Time;

var runner = new FixedStepTickRunner(30);
int ticks = 0;
runner.RunFrames(2, _ => ticks++);
if (ticks != 2 || runner.Frame != 2)
    throw new InvalidOperationException("固定帧推进不符合预期");
```

`Step(elapsedSeconds, tick)` 适用于累计宿主时间；`RunFrames` 适用于显式离线推进。二者均不替调用方判断权威帧就绪，也不提供每次调用的追帧预算。`SpeedMultiplier <= 0` 暂停按时间推进，但不阻止 `RunFrames`。回调成功后才提交 runner 帧号与时间；回调异常传播且不撤销世界副作用。把 `HostRuntime.Tick` 作为回调时还要注意其内部捕获异常：runner 的成功不能替代世界提交证明。

网络连接心跳/重连须用真实墙钟，不能直接采用快进 runner 的模拟 delta。帧同步模拟则按权威输入与固定步长运行，不用网络 Tick 代替模拟时钟。

## 模块选择与安装

| 需求 | 入口与前提 |
| --- | --- |
| 世界可解析的帧时钟 | `ServerFrameTimeModule`；在创建世界前装配，需要跟随帧同步时先安装驱动 |
| 非回滚的延迟调度 | `TimerSchedulerModule`；按宿主 PostTick delta 推进，不因此具备确定性或恢复语义 |
| 条件满足后自动开始 | `WorldAutoStartModule` + 世界提供的 `IWorldAutoStartHandler`；启动条件仍归应用 |
| Host 帧输入驱动 | `FrameSyncDriverModule`；世界提供对应输入/帧执行能力，宿主负责输入资格与就绪 |
| 服务端可选回滚 | `ServerRollbackModule`；安装时强制要求已有 `IFrameSyncDriverEvents`，状态须由完整 Provider 集合恢复 |
| 客户端帧预测 | `ClientPredictionDriverModule`；是帧同步驱动，不是状态同步固定基线导入器 |

同一次安装与卸载使用同一 runtime/options；在世界创建前装配工厂和依赖，退出时按依赖逆序收尾。模块列表不是失败自动补偿容器；不要在活动世界中热替换时钟或并发调用安装与驱动。逐世界服务及上下文如何处理销毁、同 ID 重建和漏通知，以[时间与世界启动契约](TimeAndWorldStart.md)为准，不在本页复制状态账本。

帧同步恢复帧末 N 后从 N+1 重演，不能把“收到快照”解释成可恢复检查点。状态同步在所属会话完成基线校验、重建与导入后提交，拥有独立的快照、事件和输入终结语义。本包的可选模块不统一这些游标，也不替项目决定共享世界的恢复范围。

## 失败定位与验证边界

- 安装 ServerRollbackModule 即失败：先确认驱动 feature 已安装，不绕过依赖检查。
- 新建世界拿不到时间服务：核对模块是否在创建前安装、上层是否覆盖服务注册。
- 同 ID 新世界继承旧记录：确认所有查询与驱动都使用当前世界实例，不只比较 WorldId。
- 固定帧进度正常而世界未提交：核对回调内错误和帧提交门禁，不以 runner 或 Host Tick 返回替代提交证明。

已核对 [固定步进器](../Runtime/Time/FixedStepTickRunner.cs)、[帧驱动模块](../Runtime/FrameSync/FrameSyncDriverModule.cs)及[服务端回滚模块](../Runtime/Rollback/ServerRollbackModule.cs)。仓库 .NET 镜像仅包含包源码的选定子目录；本轮测试通过不等于 Unity 全程序集、完整联机或全部补帧链验证。
