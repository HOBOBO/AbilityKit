# Record 使用契约与限制

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：已核校本页记录入口、轨道、所有权、路径和加载边界；随包 Unity 样例与完整世界回放未验证。

安装条件和最小示例见[包入口](../README.md)。

## 选择记录入口

| 要记录的内容 | 用法 |
| --- | --- |
| 按帧组织的自定义事件、调试或战斗日志 | 选 `RecordSession` 和约定好的 `RecordProfile`；按 `RecordTrackId` 取 writer，编码负载后用 `Append` 写入。用 `Serialize()` 导出，用 `TryLoad(...)` 成功加载后再取 reader。 |
| 按帧记录输入、快照并供回放读取 | 使用 `FrameRecordCodecs` 对应的 writer/reader，按需通过 `FrameRecordReplaySource` 查询。不要用 `RecordSession.TryLoad` 读取 FrameRecord 文件。 |

## 轨道与回放

- 给轨道和事件类型采用稳定且明确的名字，读写双方使用相同的 ID 派生约定和负载 codec。按消费者拆分轨道，不把调试日志当作权威输入。自定义轨道见 [Battle Log Sample](../Samples~/BattleLogSample/Runtime/BattleLogRecordSample.cs)。
- 同一帧可以有多条快照。需要完整读取时使用 `TryGetSnapshots`；兼容接口 `TryGetSnapshot` 只返回第一条。消费方负责将记录匹配到自身的恢复流程，读取成功不表示模拟已恢复。

## 纯内存完整示例

此例不写文件、不驱动模拟，只证明自定义事件可被序列化再读取。名称与载荷为本地示例约定；默认工厂只自动建立启用的 inputs/statehash/snapshots 轨道，自定义轨道必须先加入容器。

```csharp
using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Recording.Core;

var profile = new RecordProfile();
var serializer = new JsonRecordContainerSerializer();
var factory = new DefaultRecordTrackFactory();
using var session = new RecordSession(profile, null, serializer, factory, factory);
var track = RecordTrackId.FromName("local.example");
session.Container.Tracks[track] = new RecordTrack
{
    Id = track, Version = 1, Schema = "local.example", Events = new EventTrack()
};
if (!session.TryGetWriter(track, out var writer)) throw new InvalidOperationException();
writer.Append(new FrameIndex(1), RecordEventType.FromName("local.value"), new byte[] { 42 });
byte[] data = session.Serialize();
using var loaded = new RecordSession(profile, null, serializer, factory, factory);
if (!loaded.TryLoad(data) || !loaded.TryGetReader(track, out var reader)
    || !reader.TryGetEvents(new FrameIndex(1), out var events) || events.Count != 1)
    throw new InvalidOperationException("记录往返失败");
```

## 录制文件路径

绝对路径不经过平台适配。独立 .NET 运行时以进程当前工作目录为相对路径的基准。Unity 编辑器加载脚本时自动装配 Unity 路径策略，播放器在 `SubsystemRegistration` 阶段装配。相对路径默认基于 `Application.persistentDataPath`；该值为空时，JSON、基础 Binary 与容器文件回退到 `Application.dataPath`，优化二进制不回退、按原有行为保留相对路径。

`AbilityKit.Record` 不直接引用 Unity 引擎；正常接入不需要调用装配方法。若必须在自动装配回调之前使用相对路径，先调用 `AbilityKit.Record.Unity.UnityRecordPaths.Install()`，并让调用方的 Unity 程序集引用 `AbilityKit.Record.Unity`，或直接传入绝对路径。路径策略不改变录制格式、索引或回放语义。

## 所有权、加载与失败

默认 `EventTrack.Append` 保留 payload 引用，不深拷贝；读取事件列表和 `Export()` 也不是独立不可变副本。交付后勿复用可写数组，需跨异步或所有者边界时主动复制。集合和进程级默认 codec 不承诺并发安全，读写及配置由同一受控执行线安排。

随包 `DeltaRecordSample` 和 `BattleLogRecordSample` 当前均以默认工厂直接请求尚未建立的自定义轨道，会在取 writer 处提前返回；两者保留为 codec/回放接线参考，不能作为开箱可运行的证明。接入时按上例先登记轨道，或提供明确支持自定义轨道的 factory。

`RecordSession.TryLoad` 只在 serializer 返回 null 时返回假；serializer 抛异常仍会向外传播，不能仅凭方法名视为所有坏数据都返回假。成功加载替换容器内容，先前取得的 reader/writer 可能仍指向旧轨道，应重新取得。`RecordSessionFactory.FromBytes` 不暴露 TryLoad 的布尔结果；要求检查成功时使用显式 TryLoad 并处理异常。

首发候选只验证内置的两条文件路径：`.bin`（不区分大小写）写入优化二进制 `AKFR` v4，其余扩展名（含无扩展名）写入 JSON。两者均保留元数据、输入、状态哈希及其版本、快照；默认 JSON 可生成分块索引，优化二进制不输出该索引。选型只看扩展名，不检测文件内容，也不会在加载失败时尝试其他格式。基础 Binary 和可选 MemoryPack 不属于这份默认格式契约。

`FrameRecordCodecs.Current` 是进程级覆盖入口，设置为 `null` 恢复内置分派。覆盖只应由应用在装配阶段设置；不会转换已有文件，也不会影响已经创建的 writer。写入与读取必须使用相匹配的 codec 和版本。

`RecordSession.Dispose` 当前为空实现，不清轨道，也不提供文件落盘或终态保证；文件 writer 则按其实际生命周期关闭。输入、快照或 hash 已记录不表示捕获了全部权威状态；帧同步重演和状态同步导入仍各用自己的恢复契约，不从记录帧号推导输入终结。

源码：[RecordSession](../Runtime/Record/Core/Session/RecordSession.cs)、[EventTrack](../Runtime/Record/Core/Tracks/EventTrack.cs)、[默认 FrameRecord codec](../Runtime/Record/FrameRecord/FrameRecordCodecs.cs)、[ReplaySource](../Runtime/Record/FrameRecord/FrameRecordReplaySource.cs)。
