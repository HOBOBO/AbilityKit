# Context 使用指南

> 文档类型：使用指南
> 适用范围：`com.abilitykit.context` 0.1.0，2026-11-25 工作区 Runtime 与 .NET 10 镜像
> 核校状态：部分核校；下述实体、Flow、值解析与受管快照契约已对照源码，局部 E3 见第 6 节
> 事实基线：2026-11-25；未验证独立 UPM 安装或 Unity Player

## 1. 定位、安装与前置条件

本包按实体身份保存属性，以 Flow 记录流程归属，并提供内存快照和显式值读取。它不执行系统调度、不自动捕获快照，也不提供完整世界恢复。版本、Unity 2022.3 条件和 `com.abilitykit.core` 依赖以 [package.json](../package.json) 为准。

Unity 宿主通过其 UPM 来源安装本包和依赖；本地包可用 Package Manager 的 **Add package from disk** 选择 `package.json`，并让宿主解析依赖。使用自定义 asmdef 时引用 `AbilityKit.Context`。本轮只验证同源 .NET 镜像，未完成干净宿主的安装验收。

## 2. 完整示例：精确实时引用与独立快照

以下 Console 程序只依赖本包公开 API。Unity 中可将 `Main` 的逻辑放入自己的入口。先注册属性类型，再在单一逻辑线程操作；快照显式复制字段，不保留实时属性。预期输出 `context references and snapshots passed`。

```csharp
using System;
using AbilityKit.Context;

internal static class Program
{
    private static void Main()
    {
        var registry = new ContextRegistry();
        var storage = new SnapshotStorage(maxRecords: 8, maxRecordsPerEntity: 2);
        storage.RegisterType<HealthSnapshot>("example.health", schemaVersion: 1);
        var resolver = new ContextValueResolver(registry, storage);
        long entity;
        ContextEntityReference identity;
        ContextSnapshotReference snapshot;

        using (var flow = registry.BeginFlow("example"))
        {
            entity = flow.Create().With(new HealthProperty(100)).Build();
            Require(registry.TryGetReference(entity, out identity));
            var live = resolver.ReadRealtime<int, HealthProperty>(identity, "health");
            Require(live.Found && live.Value == 100);
            snapshot = storage.SaveManaged(
                new HealthSnapshot(entity, 100, frame: 12),
                new ContextSnapshotCapture(identity.Generation, frame: 12, kind: "checkpoint"));
        }

        // Flow 结束只改变阶段，实体仍由调用方销毁。
        Require(registry.Exists(entity));
        Require(registry.Destroy(entity));
        Require(!resolver.ReadRealtime<int, HealthProperty>(identity, "health").Found);
        var saved = resolver.ReadSnapshot<HealthSnapshot, int>(snapshot, "health");
        Require(saved.Found && saved.Value == 100);
        Require(storage.Remove(entity));
        Require(!resolver.ReadSnapshot<HealthSnapshot, int>(snapshot, "health").Found);
        registry.Clear();
        Console.WriteLine("context references and snapshots passed");
    }

    private static void Require(bool value)
    {
        if (!value) throw new InvalidOperationException("Context example failed.");
    }

    private sealed class HealthProperty : IProperty, IContextValueProvider
    {
        private static readonly int Id = PropertyTypeRegistry.Instance.Register<HealthProperty>().Id;
        public int TypeId => Id;
        public int Health { get; }
        public HealthProperty(int health) { Health = health; }
        public bool TryGetValue<T>(string key, out T value)
        {
            if (key == "health" && Health is T result) { value = result; return true; }
            value = default(T);
            return false;
        }
    }

    private sealed class HealthSnapshot : IImmutableContextSnapshot
    {
        public long EntityId { get; }
        public long CreatedAtMs { get; } = 0; // 示例固定值；实际采集由宿主填入。
        public long Version => 1;
        public int Frame { get; }
        public int Health { get; }
        public HealthSnapshot(long entityId, int health, int frame)
        { EntityId = entityId; Health = health; Frame = frame; }
        public bool TryGetValue<T>(string key, out T value)
        {
            if (key == "health" && Health is T result) { value = result; return true; }
            value = default(T);
            return false;
        }
    }
}
```

## 3. 实体、属性与 Flow 所有权

- `Create()` / `CreateInFlow()` 当场建立实体并派发事件，`With()` 逐项写属性，`Build()` 只返回 ID；Builder 不是事务，也没有取消构造回滚。
- 属性以泛型实际注册类型存储，`Add<T>` 和 `Set<T>` 都可覆盖该类型。使用具体属性类型，不以 `IProperty` 泛型掩盖实际类型。运行时 TypeId 按进程首次注册顺序分配，不应写进协议或存档。
- `Get<T>` 返回原对象引用。注册表的锁不保护属性内部可变字段；直接修改对象不会自动产生 Updated，需按宿主规则通过 `Set` 发布变化。
- `BeginFlow` 创建 Running 流程；默认 `Dispose` 置 Completed，异常离开 using 也不会自动置 Failed，需要调用者显式 `Fail()`。完成、取消或失败都不级联销毁实体或子 Flow；最终由所有者 `Destroy` / `Clear`。
- `Clear()` 清理实体和 Flow；长寿命全局订阅由订阅方成对 `Subscribe` / `Unsubscribe`，不把 Clear 当作解除所有观察关系的接口。
- 查询返回本次结果集合，不是响应式视图；整条 Query 并非事务。事件锁外同步派发，观察者异常逐个隔离，通过 `EventHandlerException` 可选报告，不能否决已发生的状态变化。

`ContextRegistry` 的单次操作受锁保护，但全局 `PropertyTypeRegistry` 无并发保护。应在启动期串行注册类型；并发查询和结构变更、属性变更仍由宿主约束。`FlowContext` 只组织流程，不等同于其他包同名的执行 Context。

## 4. 精确读取与受管快照

| 对象 | 使用规则 |
| --- | --- |
| `ContextEntityReference` | 包含 RegistryId、EntityId、Generation；从 `TryGetReference` 获取。销毁、同 ID 恢复或跨 Registry 后，旧引用不能自动绑定新对象 |
| `ReadRealtime<TValue,TProperty>` | 先核身份，再读属性字段，并复核身份；检查 `Found` / `Status`，数值 0 可能是有效命中 |
| `ContextSnapshotReference` | 包含 StorageId、SnapshotId、EntityId、Generation，只在所属存储与记录保留期间有效 |
| `SaveManaged` | 先注册具体不可变类型及稳定 TypeId/SchemaVersion；显式传入 Generation、Frame、Kind，载荷 Frame 必须一致 |
| `ReadSnapshot<TSnapshot,TValue>` | 精确读取引用；无存储、记录已淘汰、身份/类型不匹配、字段缺失分别返回状态，不回退最新记录或实时值 |

`IImmutableContextSnapshot` 是调用者承诺：实现和集合必须脱离运行态并保持不可变，存储不会深拷贝或自动校验对象图。Generation 是所有者的逻辑代次，不能从业务 Version 猜测；示例明确使用实时引用代次。

容量默认总记录 4096、每实体 16；上限按 EntityId 统计，跨代次合并计数。未保留记录可按保存顺序淘汰；`Retain(reference)` 返回 `IDisposable` 租约，用 using / Dispose 解除保留，没有公开 Release 方法。容量全部被保留时 `TrySaveManaged` 返回 false，`SaveManaged` 抛异常。显式 `Remove` / `Clear` 仍可清理记录，Retain 不是持久化保障。调用者应检查失败，不能依赖无限历史。

旧 `Save(IContextSnapshot)` / `Get(entityId)` 是兼容的最新视图，不提供受管身份。不可变快照必须走 SaveManaged；已有同实体受管历史时不能用旧 Save 覆盖。不要混用裸 ID 最新视图与需要代次保证的读取。快照存储不自动订阅实体销毁，也不会落盘、发布网络消息或自动恢复世界。

## 5. 常见问题与迁移

- “Flow 结束后实体还在”：这是当前所有权；显式销毁，按需求保留快照。
- “实时值缺失但快照里有”：读取来源必须显式选择，不会隐式回退。记录 Status，再由业务决定是否改读快照。
- “复用 ID 后旧引用失效”：重新取得新身份只适用于业务确认的新实体，不能补绑原操作。
- `TraceContextProperty` 由可选 Trace 包持有，不在 Context Runtime 内；本包不依赖 Trace。跨包桥接由宿主装配。
- 旧 README 的分段示例不是完整程序；首次接入使用第 2 节。旧设计中快照章节保留演进背景，受管路径以本指南和源码为准。

## 6. 验证范围与关联文档

2026-11-25 已核对 [ContextRegistry](../Runtime/Registry/ContextRegistry.cs)、[FlowContext](../Runtime/Context/FlowContext.cs)、[值解析器](../Runtime/Value/ContextValueResolver.cs)、[快照管理](../Runtime/Snapshot/SnapshotStorage.Managed.cs)与[兼容存储](../Runtime/Snapshot/SnapshotStorage.cs)。同源 .NET 的 Context suite 与本页完整示例结果记录于仓库 D4 核校记录，证据限 E0 与局部 E3。

未验证 Unity、独立安装、跨平台确定性、性能预算、落盘或完整恢复链。后续从[包内导航](README.md)按需阅读局部设计；修改公开契约时同步更新本指南。
