# AbilityKit SyncRuntime Starter

SyncRuntime 组合的最小可运行入口：在战斗逻辑之上加入 `world.framesync` + `world.statesync` + `record`，
演示「帧驱动 → 状态快照/哈希采样 → 录制 → 回放重算 → 确定性校验」的完整闭环。

## 运行

```bash
dotnet run --project src/AbilityKit.Samples.SyncRuntime
```

## 它证明了什么

> 同一串输入，跑两遍，每一帧的状态哈希都逐帧一致 → 逻辑是**确定性的**，因此可回放、可断线重连恢复、可录像复盘。

```
录制  30 帧：每帧 driver.Step → StateManager.CaptureState → StateHashComputer 算哈希 → 累积成帧记录
回放  30 帧：FrameRecordReplaySource 按帧吐出录制的输入 → 同一个 driver 重跑 → 重算哈希 → 与录制逐帧比对
校验        ：FrameRecordDiffAnalyzer 比对两份帧记录 → Status=Identical
```

运行输出末尾：
```
[Replay] 逐帧哈希匹配 30/30
[DiffAnalyzer] Status=Identical
[结论] 两份帧记录的状态哈希轨道完全一致 —— 逻辑确定性闭环成立
```

## 它演示了什么

| 能力 | 包 | 在示例中的位置 |
|---|---|---|
| 帧同步驱动（FrameIndex 推进 + Tick） | world.framesync | `WorldManagerFrameDriver.Step`：每帧驱动 `IWorldManager.Tick` 后帧号 +1 |
| 帧输入命令 | world.framesync / host | `PlayerInputCommand(FrameIndex, PlayerId, opCode, payload)` |
| 状态快照与回滚 | world.statesync | `StateManager.CaptureState / TryRestore` + `IRollbackable`（`SyncHero`） |
| 状态哈希采样 | world.statesync | `StateHashComputer.ComputeWithBusinessData(snapshot, IBusinessHashProvider)` |
| 业务自报哈希 | world.statesync | `SyncHero : IHashableState`（`ComputeHash`）+ `BattleHashProvider` 聚合 |
| 帧记录数据模型 | record | `FrameRecordFile`（Inputs / StateHashes / Meta） |
| 回放源 | record | `FrameRecordReplaySource.TryGetInputs / TryGetStateHash` |
| 确定性校验 | record | `FrameRecordDiffAnalyzer.Compare(left, right)` → `Status` |

## 关键设计点

- **确定性是回放的前提**：`SyncHero` 没有任何随机源，输入序列固定（每帧右移、每 5 帧受击一次）。只要逻辑含非确定源（浮点漂移、未播种随机、哈希表遍历顺序），回放就会发散——这正是哈希轨道要抓的问题。
- **三包的组合点**：record 的 `AppendStateHash(frame, version, uint)` 想要的哈希值，正是 statesync 的 `StateHashComputer` 产出的 `StateHash`。framesync 的帧号是三者的公共时间轴。
- **本演示用 record 的内存数据模型**（`FrameRecordFile`）+ 回放源 + DiffAnalyzer，聚焦同步机制本身。record 另有 `Optimized/Binary/Json` 三套文件编解码（持久化层），用于落盘 / 传输 / 跨进程比对，是独立的一层能力，此处不展开。
- **聚焦同步机制，世界装配从简**：Foundation/SkillCore/BattleRuntime 已演示 world DI 装配；SyncRuntime 的核心对象（driver / stateManager / 帧记录）生命周期简单，直接构造更清晰。

## 下一步

- **接网络**：把帧记录换成 `protocol` 编码，经 `network.transport.*` 在多端之间广播输入帧，`world.snapshot` 路由把服务端快照解码回业务字段——即多人帧同步 / 状态同步。
- **接战斗**：把 `SyncHero` 换成 [BattleRuntime Starter](../AbilityKit.Samples.BattleRuntime) 的 targeting/projectile/damage 链路（需先确定性化：固定 RNG、关暴击随机），让真实战斗也具备可回放 / 可重连。

组合分级的完整定义见 `Unity/Packages/README.md`。
