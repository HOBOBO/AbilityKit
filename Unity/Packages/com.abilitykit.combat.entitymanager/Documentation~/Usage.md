# EntityManager 使用指南

> 文档类型：使用指南
> 适用范围：0.1.0 当前 Runtime；环境与依赖见 [package.json](../package.json)。
> 核校状态：已核校本文的注册、显式索引、查询视图与失败契约；历史说明未整体刷新。
> 事实基线：2026-11-25；E0 与完整本地示例的局部 E3，既有 suite 仅覆盖更新信封。

## 目标与前置条件

本包保存 ID 及分类关系，实体组件和分类值由调用方提供。单分类使用 KeyedEntityIndex，多个标签使用 MultiKeyEntityIndex。在一个逻辑线程中集中写入，让领域对象与索引使用同一注册、变更和删除入口。安装见[包入口](../README.md)。

## 完整示例与预期结果

以下为完整 Console 示例，也可将调用主体放在 Unity 单一逻辑入口中。它验证显式换键、标签清理和跨写入保留结果时的复制方式。

```csharp
using System;
using System.Linq;
using AbilityKit.Ability.Battle.EntityManager;

public static class Program
{
    public static void Main()
    {
        var manager = new BattleEntityManager<int>();
        var byTeam = manager.CreateKeyedIndex<string>();
        var byTag = manager.CreateMultiKeyIndex<string>();
        manager.Add(1);
        manager.Add(2);
        byTeam.SetKey(1, "Blue");
        byTeam.SetKey(2, "Red");
        byTag.AddKey(1, "hero");
        byTag.AddKey(2, "hero");
        var before = byTeam.Get("Blue").ToArray();
        byTeam.SetKey(1, "Red");
        if (before.Length != 1 || before[0] != 1 || byTeam.Get("Blue").Count != 0)
            throw new Exception("换键或结果复制不符合预期");
        if (byTeam.Get("Red").Count != 2) throw new Exception("换键失败");
        manager.Remove(2);
        if (manager.Registry.Contains(2) || byTeam.Get("Red").Count != 1 ||
            byTag.Get("hero").Count != 1)
            throw new Exception("删除未清理索引");
        manager.Remove(1);
        if (manager.Registry.Count != 0 || byTag.Get("hero").Count != 0)
            throw new Exception("清理失败");
        Console.WriteLine("EntityManager: register, rekey, snapshot, remove OK");
    }
}
```

预期打印末行且不抛异常。`before` 是调用方复制的数组，不随后续换键变化。

## 写入与索引生命周期

| 操作 | 当前行为与调用责任 |
| --- | --- |
| `Registry.Add/Remove` | 返回是否实际增删；重复 Add 或缺失 Remove 不通知索引。门面 `manager.Add/Remove` 返回 void |
| `AddRange/RemoveRange` | 返回实际变更数量，null 输入返回 0；逐项提交，没有批事务 |
| `SetKey` | 同一 ID 最多一个键；换键清除旧 bucket，空 bucket 被移除 |
| `AddKey/RemoveKey/ClearKeys` | 管理多键关系；删除 ID 时通过反向关系清理标签 |
| `NotifyUpdated` | 只通知已注册 ID；自定义 IUpdateTypeAwareIndex 可按 Type 过滤。两个内建索引的更新回调为空 |
| `AddIndex` | 先挂载、再回放已有 ID。内建 OnEntityAdded 为空，晚加索引须从领域数据手工补键 |
| `RemoveIndex` | 仅停止通知，不清空或释放索引；旧引用会看到陈旧数据 |

SetKey / AddKey 不检查 ID 是否已注册。未注册 ID 也能进入 bucket，但 Registry.Remove 不会为不存在的 ID 触发清理。应先注册再设置键；不要把 SetKeyUpdate 等 DTO 的存在理解为内建自动更新协议。

容器没有总 Clear 或 Dispose 入口。结束战局时由拥有者解除引用，实体对象由原拥有者销毁。整体重建时创建新容器，从权威数据补齐并校验后再切换查询引用。

## 查询、比较器与失败边界

- `Get` 缺键返回空集合；`TryGet` 失败输出 null。TryGetKey / TryGetKeys 可查询反向关系。
- bucket 通过 IReadOnlyCollection 暴露内部 HashSet，Registry.Entities 也是主集合视图。没有固定排序；不应强制转换后写入。跨帧、跨回调保存时自行复制，稳定排序由上层实现。
- 遍历中不要重入写入，也没有并发读写保证。按键定位平均接近 O(1)，枚举成本仍取决于结果数。
- ID comparer 会传给内建索引；单键换键比较使用默认键 comparer，不完全等同于 bucket 字典的自定义 comparer。优先采用稳定、规范化的键，复杂等价语义须专项验证。
- Registry 先改 ID 集，再依次通知索引；异常向外传播且保留部分写入。AddIndex 回填失败后索引仍挂载。不要直接重试并假定幂等，应检查一致性或重建。

## 常见问题与迁移

| 现象 | 原因与处理 |
| --- | --- |
| 发了更新通知，分类没变 | 内建索引不处理通知；显式改键，或实现自定义索引 |
| 后加索引查不到旧实体 | Registry 只有 ID，不知道领域键；手工回填 |
| 删除后仍有未知 ID | 曾绕过注册表写键；核对统一写入口并从领域数据重建 |
| 结果遍历失效 | 活动集合发生写入；先复制，再执行业务 |

## 验证范围与关联文档

2026-11-25 在 Windows / .NET 10 Release 执行完整示例和现有 EntityManager.Tests 3/3；该 suite 仅验证三个更新 DTO，不能代表 Registry、回填、异常一致性或比较器矩阵的完整覆盖。源码见 [EntityRegistry](../Runtime/EntityManager/EntityRegistry.cs)及[包内基础样例](../Runtime/EntityManager/BattleEntityManagerExample.cs)。

未运行 Unity GUI/Player、干净 UPM 安装、业务实体恢复、压力或并发验收，不授予 E4/E5 结论。旧背景见[历史说明](../Document/实体管理模块开发设计文档.md)，现行调用以本文和 Runtime 为准。
