# Shooter 多人状态同步性能分析与测试

## 同步链路

服务端链路：`BattleLogicHostGrain` Tick 世界，按同步模板判断是否发布快照；AOI 模式通过 `PublishPerObserver` 为每个观察者同步构建并序列化快照，然后异步投递到 Gateway。

客户端链路：TCP 接收线程触发 `RawServerPushReceived`，`ShooterBattleDataPlane` 入队；Unity PlayerLoop 调用 Launcher Drain，在主线程完成 wire/payload 解码、状态导入、Presentation 映射和 View 渲染。2k 配置默认使用 GPU Instanced 后端，GameObject 后端仅用于调试和兼容回退。

输入链路：Unity 每帧产生本地输入，有限并发窗口提交到 Gateway；窗口满时保留并合并最新输入，Fire 边沿不会因移动输入覆盖而丢失。

## 已确认的主要原因

1. `mass-battle-lod-aoi` 原插值延迟为 90 帧。在 60 Hz 下这是 1.5 秒固定可见延迟，远大于网络 RTT。当前调整为 20 帧，即两个 10 帧快照周期，基础播放延迟约 333 ms。
2. 原远程输入队列只允许一个 RPC 在途，因此有效提交频率上限约为 `1 / RTT`。100 ms RTT 时只能达到约 10 Hz。当前 Shooter 使用 4 请求有限窗口，100 ms RTT 下理论上可达到约 40 Hz，同时保留背压。
3. 快照解码、状态应用和 View Render 都在 Unity 主线程。原诊断只有整个 Drain 时长，无法判断卡顿属于排队、快照应用还是渲染。
4. AOI 快照在服务端按观察者同步构建。观察者数和可见实体数同时增长时，快照构建/序列化会直接占用 Grain Tick 时间；投递异步只能缓解下游阻塞，不能消除构建成本。
5. Delta 依赖基线，不能任意丢弃。客户端落后时队列年龄会持续增长；完整快照到达时可安全淘汰旧 Full/Delta，现有 DataPlane 已执行该合并策略。
6. Mapper 原每批固定分配 1,708 B，不是 List 容量问题，而是 7 个池化 List 每次 `Get` 都在 `ConditionalWeakTable` 重建归还句柄和闭包。归还句柄改为对象首次使用时注册后，稳态 Mapper 为 0 B。
7. 变长 payload 原来同时在 encode 精确长度副本和 MemoryPack 数组反序列化处产生分配。生产发送路径改用复用 backing buffer 的 `ArraySegment<byte>`，解码器使用倍增容量数组和有效前缀计数后，`0 ↔ N` 实体交替解码稳态为 0 B。
8. Projection 原按“现有实体数 + EntityChange 数”预扩容；2000 个现有实体的全量 Update 被误判成 2000 个新增实体，导致 5 个 Dictionary 和稠密数组扩到 4000。现在仅在理论容量不足时统计真正新增且存活的实体。

## 新增指标

热路径使用固定桶直方图，记录时不创建 List、字符串或 LINQ 枚举：

- Editor update gap、snapshot arrival gap、输入 RTT 的 P50/P95/P99/max。
- push queue depth、peak depth、oldest age、queue wait P95/P99。
- push decode/apply P95/P99、累计和最大 payload bytes。
- 服务端快照打戳到客户端应用完成的 source age P95/P99（要求两端 UTC 时钟已同步）。
- 整帧、Launcher、Session Tick、Presentation Build、View Render 的 P50/P95/P99。
- 每帧 GC 平均值/最大值、超过 33.333 ms 的 hitch 数和比率。
- 当前客户端帧与最后收到快照帧的 frame age。

所有指标写入 owner/member 的 headless result JSON。协调脚本默认对 Frame P95、Apply P99、Queue Wait P99、Peak Queue、hitch rate 和平均 GC 执行门禁，阈值均可通过参数覆盖。

## 验证命令

单场 2k 单位、Mobile 4G：

```powershell
.\tools\run_shooter_unity_headless_multiplayer.ps1 `
  -EnemyBudget 2000 `
  -NetworkEnvironmentId mobile4g
```

512/2k 与 Ideal、4G、Poor WiFi、Limited Bandwidth 矩阵：

```powershell
.\tools\run_shooter_sync_performance_matrix.ps1
```

专项 .NET 回归：

```powershell
dotnet test .\src\AbilityKit.Demo.Shooter.Runtime.Tests\AbilityKit.Demo.Shooter.Runtime.Tests.csproj `
  --filter "FullyQualifiedName~ShooterSyncPerformanceMetricsTests|FullyQualifiedName~ShooterRemoteInputSubmitQueueTests|FullyQualifiedName~ShooterRoomGatewayConnectionTests.BattleDataPlane"
```

2000 单位三场景零分配门禁：

```powershell
.\tools\run_shooter_sync_allocation_gate.ps1 `
  -Entities 2000 `
  -MaxP99Milliseconds 16.7
```

## 定位方式

- `Launcher P95` 高且 `PushApply P99` 高：优先查 codec、状态导入和 Presentation Apply。
- `QueueWait P99` 高但 `PushApply P99` 低：主线程消费不足或其他系统长期占帧。
- `ViewRender P95` 高：查 GameObject 创建/销毁、Transform 更新和 Renderer 操作。
- `SnapshotArrivalGap P99` 高：查服务端 Tick 漂移、按观察者构建、Gateway 队列或网络抖动。
- `Frame P95` 高且上述阶段均低：查同一 PlayerLoop 中的 RVO、AI、物理或其他系统。
- `frame age` 持续增长：消费速率低于推送速率，需降低发送频率、异步预解码或在完整基线处主动追帧。

## 2026-08-19 千单位 Delta 优化结果

服务端现在为每个 AOI 观察者保存最后一次已发送的量化实体状态。Spawn、Enter、Leave 和 Despawn 始终发送；普通 Update 只有在量化位置、速度、HP、Score、Lifetime、Owner 或 Flags 变化后才进入发送队列。变化实体仍受 Near/Mid/Far LOD 最小间隔控制，静止实体使用 `min(BaselineIntervalFrames, LowFrequencyIntervalFrames)` 定期刷新，避免 Gateway 背压丢弃旧 Delta 后长期不自愈。

预算游标按实际扫描数推进，而不是只按成功发送数推进，避免大量静止或 LOD 未到期实体让扫描起点长期停留。预算未裁剪候选集时不再执行每观察者 `O(N log N)` 优先级排序。

2000 敌人、8 次预热、64 次测量的正式 allocation gate：

| 场景 | Mean | P99 | 平均 Payload | 稳态分配 |
| --- | ---: | ---: | ---: | ---: |
| 空 Delta | 0.745 ms | 1.467 ms | 89 B | 0 B |
| 每帧 5% 实体变化 | 0.829 ms | 1.554 ms | 7,289 B | 0 B |
| 每 8 帧 2000 实体刷新 | 1.122 ms | 12.946 ms | 18,089 B | 0 B |

- 5% 场景平均 100 个实体 Delta；旧夹具每帧发送全部 2000 个静态实体时为 144,089 B，payload 下降约 94.9%。
- 空 Delta 与 5% 场景的 Projection P99 分别为 0.005 ms 和 0.079 ms。
- 周期全刷仍是当前尾延迟上界，Mapper P99 2.630 ms、Projection P99 9.451 ms，但总 P99 仍低于 16.7 ms 门禁。
- exporter、encode、decode、mapper、projection、release 在三个场景中均为稳态 0 B。
- 结果目录：`artifacts/shooter-sync-investigation/allocation-gate-2000-incremental-view/`。
- 10000 实体、64 观察者 steady AOI 为 9.013 ms/tick，0 B/tick，结果文件为 `artifacts/shooter-sync-investigation/aoi-e10000-o64-suppressed-final.json`。

流水线门禁支持 `-ChangedEntityFraction` 和 `-RefreshIntervalFrames`，同时输出 `MeanEntityDeltas`、`PayloadBytesPerEntityDelta`、`UnchangedSuppressionRatio` 和 `ObservedMaxEntityAgeFrames`，可区分真实变化成本与伪全量 Delta 成本。

## GPU Instanced View 增量更新

原 GPU 后端每收到一个新 Batch 都会清空三类 Matrix List、扫描完整 DenseStore、重建全部矩阵，再对三个 ComputeBuffer 执行全量 `SetData`。因此 100 个实体变化时，CPU 和 GPU 上传仍按 2000 个实体付费。

当前实现为 Player、Bullet、Enemy 分别维护 `EntityKey -> stable slot`：

- Transform Delta 只重算对应槽位；新增实体追加槽位，删除实体使用 swap-remove 并修正被搬移实体的槽位。
- Indirect 路径按连续脏槽区间局部 `SetData`；脏区超过 16 段时退化为一次全量上传，避免大量驱动调用。
- Full replace、WorldScale 或 ControlledPlayer 改变时仍执行全量重建，保证重连和视图参数变化语义不变。
- `DrawAuthorityOverlay=false` 时只保留 authority projection 诊断，不再构建 authority Matrix、上传 ComputeBuffer 或发出无效 DrawCall。
- 新增 `AbilityKit.Shooter.View.GpuInstanced.ApplyInstanceDelta` marker；保留 `RebuildInstanceBuffer`、`UploadIndirectBuffer` 和 `DrawBuffer` marker，便于区分增量 CPU 成本、全量重建和 GPU 上传。
- 2048 槽位、每轮约 5% 更新的 64 轮测试在预热后为 0 B；swap-remove 和脏槽正确性有独立回归。

Unity 2022.3 生成工程已编译通过。仍需在可控 PlayMode 场次采集上述 marker、Frame P95/P99、GC/frame 和实际上传范围，并验证 Indirect shader 缺失时 `DrawMeshInstanced` fallback 的画面和性能。

## 自动化回归结果

- Shooter Runtime：519/519。
- Core：124/124。
- AOI benchmark：14/14。
- Unity 2022.3 Shooter View Runtime 生成工程：0 error。

## 后续优化优先级

1. 在 Unity PlayMode 采集增量 View marker，确认 5% Delta 不再进入 `RebuildInstanceBuffer`，并记录实际 ComputeBuffer 上传元素数。
2. 继续拆解周期 2000 实体刷新中 Projection 的 9.451 ms P99，优先检查 Full/Delta 批次的 Dictionary 查找和重复 Entity/Transform 遍历。
3. 若 Push Apply 主导，将 wire/payload 预解码移到后台线程，只在主线程提交不可并行的状态变更。
4. 若 arrival gap 主导，在服务端增加 Tick/build/serialize/delivery histogram，并缓存观察者间可共享的分层快照。
5. 插值延迟最终应根据到达抖动自适应，保持约 2 到 3 个快照样本，而不是再次写死大帧数。

## 2026-08-19 后续：Projection 融合与 GPU headless 契约

Projection 热路径现在会把索引对齐、Key 相同且存活的 EntityChange 和 TransformChange 融合为一次 Store 查找和一次实例更新。错序、稀疏、死亡实体仍保留原有 fallback 语义。`ShooterProjectionFusionTests` 覆盖对齐融合、错序回退和死亡玩家恢复。

融合后重新运行 512 样本、2000 实体、每 8 帧周期刷新的基准：

| 阶段 | 优化前 Mean / P95 / P99 | 优化后 Mean / P95 / P99 | 稳态分配 |
| --- | ---: | ---: | ---: |
| Projection | 0.190 / 1.440 / 2.293 ms | 0.152 / 0.753 / 3.005 ms | 0 B |
| Total | 1.565 / 3.743 / 20.144 ms | 2.725 / 5.206 / 8.752 ms | 0 B |

旧的 9.451 ms Projection P99 只来自 8 个全刷样本。512 样本说明 Projection P95 通常低于 1 ms，P99 仍受机器调度离群点影响，应结合完整样本判断，不宜因此再次重构 Store。重跑后的 Total P99 已低于 16.7 ms。

多人 headless 脚本和性能矩阵新增 `-ViewBackend gameobject|gpu`。GPU 模式不再传 `-nographics`，每个客户端结果会记录实际后端、全量重建次数、增量批次数、Indirect 上传批次、上传调用数、上传 Matrix 数、全量上传次数和局部上传区间数。门禁要求全量和增量路径都被覆盖；当 Indirect 可用时，还要求实际 Matrix 上传非零。GPU AOI 视图数量改为读取渲染 diagnostics，不再依赖查找 GameObject。

本批验证：Projection 定向测试 44/44；Shooter Runtime 522/522；Core 126/126；AOI benchmark 14/14；Shooter View Runtime 与 Editor 的 Unity 2022.3 生成工程均为 0 error；512 样本流水线 0 B 分配并通过。Debug 三场景 allocation gate 全部通过，2000 实体的空 Delta、5% 变化和 8 帧周期刷新 P99 分别为 6.338、6.151 和 8.306 ms，均为 0 B。Release allocation gate 当前仍被脏工作区中与本批无关的 `AbilityKit.Core` Public API analyzer 错误阻塞（`ObjectPoolOptions`/`PoolKey` 的 RS0016/RS0017），构建在 Shooter benchmark 启动前失败。
