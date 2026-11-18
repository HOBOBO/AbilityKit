# 集合、缓冲区与数组所有权契约

本文是 `Runtime/Collections` 与 `Runtime/Buffers` 的行为契约。Core 的职责准入见 [CoreBoundary.md](./CoreBoundary.md)。这些类型只提供有序存储、容量保留和数组所有权，不负责网络窗口策略、回滚调度、对象复制或玩法规则。

## 选择与源码

| 需求 | 类型与源码 | 成本及限制 |
|---|---|---|
| 唯一整数及区间定位 | `SortedIntSet`，`Collections/SortedIntSet.cs` | 查询采用二分查找；插入、删除可能搬移元素；通过索引读取，不提供枚举接口 |
| 小型注册管线的稳定排序 | `StablePriorityList<T>`，`Collections/StablePriorityList.cs` | 二分定位、列表插入；枚举器需要分配，枚举期间禁止变更 |
| 稀疏或乱序整数帧键 | `SparseFrameIndexedBuffer<T>`，`Buffers/FrameIndexedBuffer.cs` | 字典查询与有序索引组合，索引变更可能搬移元素 |
| 频繁追加、淘汰的整数帧存储 | `RingFrameIndexedBuffer<T>`，同上 | 查询和存储定位采用二分查找；尾部写入、头部淘汰无需搬移；乱序插入及中间删除可能搬移元素 |
| 按插入顺序保留元素 | `ListSequentialBuffer<T>` / `RingSequentialBuffer<T>`，`Buffers/SequentialBuffer.cs` | 列表满容量追加需要搬移；环形后端追加为常数时间 |
| 跨调用边界持有租用数组 | `PooledBufferOwner<T>`，`Buffers/PooledBufferOwner.cs` | 分配引用类型所有者；提供逻辑视图及最多一次的归还尝试 |

除 `PooledBufferOwner<T>.Dispose` 自身支持并发释放外，所有访问都要求单一所有者或外部同步。特别是缓冲区的读取、写入和容量变化不能并发执行。

## 顺序与匹配

`SortedIntSet` 按完整 `int` 升序且不保留重复值。`LowerBound` 返回首个大于等于目标的位置，`UpperBound` 返回首个严格大于目标的位置；没有符合项时返回 `Count`，空集合返回零。`RemoveRange` 按索引及数量删除，沿用底层列表的参数校验。

`StablePriorityList<T>` 按整数优先级升序或降序排列，同优先级按最初注册顺序排列。元素可以重复。`RemoveFirst` 和 `TryUpdatePriority` 查找的是当前排序中的首个匹配项；更新优先级不改变其最初注册顺序，即使后来再次进入同一优先级分组也如此。找不到时返回 `false`；找到且优先级未变化时仍返回 `true`。`Clear` 清空元素并重置注册序号。

匹配条件在调用线程同步执行，异常直接传播；匹配期间不得变更集合。枚举器直接访问当前列表，不是快照，也不承诺检测并抛出枚举期间变更异常；调用方必须遵守禁止变更的要求。初始容量允许为零，负容量抛出 `ArgumentOutOfRangeException`。

## 帧键与容量

`IFrameIndexedBuffer<T>` 的“最新”表示整数键的数值最大，和调用到达顺序无关；负数和 `int.MinValue` / `int.MaxValue` 都是普通键，不提供帧号回绕比较。重复键直接替换值。满容量时插入比全部保留键更小的新键会被忽略；其它新键插入后淘汰最小键。`RemoveBefore` 与 `RemoveAfter` 均为严格比较，保留边界键。查找缺失时返回 `false` 并输出 `default(T)`；已存储的值本身为空引用时仍然返回 `true`。

`ISequentialBuffer<T>` 按插入时间从最旧到最新读取，满容量追加时淘汰最旧项。两类缓冲区的具体实现均要求构造容量为正数，非正数构造参数抛出 `ArgumentOutOfRangeException`。`TrySetCapacity` 对非正数返回 `false` 且不改变状态；相同容量直接成功；缩容保留最新项及其原顺序，扩容不恢复已淘汰内容。索引必须在 `[0, Count)` 内，越界抛出 `ArgumentOutOfRangeException`。

`Capacity` 表示逻辑保留上限。列表和稀疏后端不保证缩容时收回底层已分配空间；环形后端在容量变化时分配新数组。`Clear` 断开保留值的内部引用并保留容量。`TrySetCapacity` 不把资源分配异常转换成 `false`。接口 `IBufferCapacityControl` 允许其它实现拒绝运行时容量变化，不保证多个缓冲区的容量请求具有原子性。

所有存储类型都按值赋值：引用类型仍共享同一个对象，不创建防御性副本。替换、忽略、删除、淘汰、清空和缩容不调用元素的 `Dispose`，也不替调用方归还其持有的资源；移除后内部存储不继续持有被移除对象。需要复制、回收或逐元素释放的上层包须自行管理。

## 租用数组的所有权

`PooledBufferOwner<T>` 从指定 `ArrayPool<T>` 或共享池租用数组。`Length`、`Memory`、`Span` 和 `Segment.Count` 都限制在逻辑长度内；`Segment.Array` 暴露的物理数组可能更大，调用方仍须遵守逻辑边界。零长度在完成参数校验后使用空数组，不调用池的 `Rent` 或 `Return`。

清理标志可以组合：`None` 不请求清理；`OnRent` 在租用后清空整个物理数组；`OnReturn` 将整数组清理请求传给池，实际清理由池实现负责。长度为负、池为空或标志包含未定义位时抛出参数异常。池返回空引用时抛出 `InvalidOperationException`；池返回的数组过小时，先按归还清理策略尝试归还，再抛出 `InvalidOperationException`。如果池在该归还过程中抛出异常，则直接传播池异常。

`Dispose` 先原子撤销访问权，再向池尝试归还一次。重复、并发或归还回调中的重入释放都不会再次归还。池的归还异常直接传播，不恢复访问权且不重试；因此 `IsDisposed` 表示已撤销访问权，不能作为池已成功接收数组的证明。释放后 `Length` 仍可读，重新获取视图会抛出 `ObjectDisposedException`。

已取得的视图无法由所有者主动撤销；释放后即失效，调用方不得继续使用。视图访问也不得与释放并发。短同步操作若已具有局部 `try/finally`，可以直接使用 `ArrayPool<T>.Rent/Return` 避免创建所有者对象。

## 验证入口

- `src/AbilityKit.Core.Tests/StablePriorityListTests.cs`、`SortedIntSetTests.cs`：顺序、稳定决胜及整数边界定位。
- `BufferStorageTests.cs`：混合操作与参考模型比较、完整整数键、环形回绕、容量及非法索引。
- `BufferOwnershipContractTests.cs`：引用释放、元素所有权、空引用值，以及列表首次满容量追加的局部分配断言。
- `PooledBufferOwnerTests.cs`：逻辑视图、零长度、清理范围、并发释放、归还异常及重入。

本地 .NET 测试不替代 Unity、IL2CPP 或其它正式支持平台的验收，单个操作的分配断言也不代表全包零分配承诺。
