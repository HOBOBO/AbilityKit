# 远端帧数据使用契约

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：已核校以下集合和所有权行为；线上会话及 Unity 未验证。

## 选择数据结构

| 入口 | 当前行为 | 调用方责任 |
| --- | --- | --- |
| `FramePacket` | 保存 WorldId、Frame、Inputs 与可空 Snapshot；null 输入转换为空集合 | `Inputs` 和快照 payload 不深拷贝，交付后保持不可变或明确复制 |
| `RemoteFrameBuffer<TFrame>` | 按非负帧号覆盖写入；`TryGet` 不消费；`initialCapacity` 不是硬上限 | 自行决定裁剪时机、就绪条件及每个世界/会话的隔离 |
| `RemoteFrameAggregator` | 按包到达顺序追加同帧输入和信封；忽略 null 包和负帧 | 去重、排序、代际校验与输入完整性由宿主负责 |

本地缓冲完整示例见[包入口](../README.md)。这些容器不保证并发安全，接收线程应先将输入交给宿主的串行执行线。

## 水位与裁剪

`MaxReceivedFrame` 初始为 `-1`，只随更大帧号前移；`TargetFrame = MaxReceivedFrame - DelayFrames` 是计算值，不检查缺帧、不作非负钳制。即使第 10 帧已到，第 9 帧仍可能缺失。`TrimBefore(n)` 只删除帧号小于 n 的内容，不降低收件高水位；`Dispose` 清集合并将水位重置为 `-1`，当前实现没有 disposed 门禁。`DelayFrames` 不因释放而重置。

聚合器的 `BuildInputFrame` / `BuildSnapshotFrame` 返回新数组，但不移除原条目；信封、输入内部引用和 payload 不深拷贝。重复添加同一包会重复累积；没有帧结束标识，空数组不能区分“权威空帧”和“尚未收到”。完成消费后由宿主 `TrimBefore` 或 `Clear`。

聚合键只有帧号，没有 WorldId 或会话代际。适配层先校验路由，并为不同作用域持有独立实例；切换作用域时清理旧数据。快照 ID、事件水位、帧确认和状态同步输入确认均不能从这些集合推导。

## 快照 drain

`SnapshotProviderDrain.DrainSnapshots` 优先调用批量 provider，一轮收集后逐条生成仅含快照的 `FramePacket`；否则最多调用单条 provider 指定次数。provider、回调为空或预算非正时返回零。单条 provider 必须自行消费或前移，否则会重复返回同一条；本工具不去重。provider 或 feed 抛错向外传播，已经交付的包不撤销，也不自动推进世界或提交同步游标。

`Runtime/Frames/` 存放数据类型与缓冲，`Runtime/Adapters/` 存放提供者适配。两者仍在 `AbilityKit.World.NetworkFragments` 程序集；即使不使用该适配器，快照信封也依赖 `host` 定义的 `WorldStateSnapshot`。

## 核校依据

已对照 [FramePacket](../Runtime/Frames/FramePacket.cs)、[提供者适配](../Runtime/Adapters/SnapshotProviderDrain.cs)、[缓冲](../Runtime/Frames/RemoteFrameBuffer.cs)、[聚合器](../Runtime/Frames/RemoteFrameAggregator.cs)。完整恢复和传输适配由上层声明及验证。
