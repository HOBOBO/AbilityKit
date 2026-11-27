# SkillLibrary 使用契约与限制

> 文档类型：使用指南
> 适用范围：0.1.0 当前 Runtime；环境与依赖见 [package.json](../package.json)。
> 核校状态：已核校本文的派生索引、替换更新、结果视图和失败边界；历史正文未整体刷新。
> 事实基线：2026-11-25；E0 与完整本地示例/限制探针的局部 E3，无专属测试工程。

## 目标与前置条件

SkillLibrary 保存主键到技能数据的关系，通过 selector 派生分类 bucket。它不加载配置、不施放技能，也不驱动冷却。安装与命名空间见[包入口](../README.md)。调用方维护数据资源、稳定键与单线程写入入口。

## 完整示例与预期结果

以下完整 Console 示例使用只读分类对象，更新时创建新对象，让 selector 能读取旧、新分类。后加多键索引会回填已有记录。

```csharp
using System;
using System.Linq;
using AbilityKit.Ability.Battle.SkillLibrary;

public static class Program
{
    private sealed class SkillData
    {
        public string School { get; }
        public string[] Tags { get; }
        public SkillData(string school, params string[] tags)
        { School = school; Tags = tags; }
    }

    public static void Main()
    {
        var skills = new SkillLibrary<int, SkillData>();
        var bySchool = skills.CreateDerivedKeyedIndex(d => d.School);
        if (!skills.Add(100, new SkillData("Fire", "damage"))) throw new Exception("添加失败");
        var byTag = skills.CreateDerivedMultiKeyIndex(d => d.Tags);
        if (byTag.Get("damage").Count != 1) throw new Exception("回填失败");
        var fireBefore = bySchool.Get("Fire").ToArray();
        if (!skills.Update(100, new SkillData("Frost", "control"), new SkillUpdate(1, null)))
            throw new Exception("更新失败");
        if (bySchool.Get("Fire").Count != 0 || bySchool.Get("Frost").Count != 1 ||
            byTag.Get("damage").Count != 0 || byTag.Get("control").Count != 1 ||
            fireBefore.Length != 1)
            throw new Exception("索引差分或复制失败");
        if (!skills.TryGet(100, out var data) || data.School != "Frost")
            throw new Exception("主数据不匹配");
        skills.Remove(100);
        if (skills.Count != 0 || byTag.Get("control").Count != 0)
            throw new Exception("删除未清理");
        Console.WriteLine("SkillLibrary: backfill, replace, snapshot, remove OK");
    }
}
```

预期打印末行且不抛异常。示例不修改已提交的数据；数组也由调用方保持只读约定。

## 数据、索引与所有权

| 操作 | 当前行为 |
| --- | --- |
| `Add/Remove` | 重复添加或删除缺失键返回 false；成功先改主字典，再通知索引 |
| `TryGet/Get` | 前者返回是否存在，后者缺键抛 KeyNotFoundException；数据不会克隆 |
| `CreateDerivedKeyedIndex` | 一条数据派生一个分类键 |
| `CreateDerivedMultiKeyIndex` | 一条数据派生多个键；selector 返回 null 等同无分类 |
| `Update` | 先替换主数据，再把 old/new data 交给索引；缺失键返回 false |
| `NotifyUpdated` | 不替换数据；把同一当前对象同时作为 old/new 传入。缺失键不通知 |
| `AddIndex` | 先挂载再遍历已有数据回填；失败可能留下挂载和部分 bucket |
| `RemoveIndex` | 仅停止后续通知，不清空或释放旧索引 |

索引保存主键，不复制技能对象。容器没有总 Clear 或 Dispose；宿主管理生命周期，外部资源由原拥有者释放。配置替换可先构建新 Library、校验后切换，但原子切换和观察者协调由项目实现。

## 原地修改的限制

**不能用 NotifyUpdated 修复原地变更的分类。** 两个内建索引都从 old/new data 读取键，没有保存每条技能的旧分类。先原地改成 Frost，old/new 都是 Frost，Fire bucket 就不会移除；多键数组原地修改也会丢失旧集合。此后即便删除主数据，回调仍只看到修改后的分类，旧 bucket 可能残留。

使用不可变记录或新数据对象，通过 Update 统一替换；已经污染的索引从权威数据重建。NotifyUpdated 可供有独立旧值缓存的自定义索引处理通知，不等于对象变更追踪。内建索引也不按 SkillUpdate.Type 过滤。

## 查询、比较器与异常

- 分类 Get 缺键返回空集合，TryGet 失败输出 null。结果是内部 HashSet 的只读接口视图，Keys 也不是快照；没有固定排序、查询规划或分页，保存时自行复制。
- 主字典 comparer 不会传入派生 bucket 内的技能主键 HashSet；单键索引 old/new 分类比较使用默认 comparer。复杂等价关系须专项验证，优先规范化键并采用一致默认语义。
- selector 应稳定、无副作用、不抛异常；多键延迟枚举异常也可能留下部分写入。主字典与各索引不是事务，失败会传播，不能假设重试能修复。
- 不支持并发读写；遍历结果时不要重入修改。多键更新会建立差分集合，没有零分配承诺。

## 常见问题与迁移

| 现象 | 定位与处理 |
| --- | --- |
| 新旧分类同时查到主键 | 是否原地改了对象或数组；改为替换数据，重建受影响容器 |
| 查到主键却没有主数据 | 索引已失配或持有解除挂载的旧视图；从权威数据重建 |
| 加索引中途失败 | 索引已挂载且部分回填，不要暴露为有效查询入口 |
| 旧说明称自动同步 | 仅指受控 Add/Remove/Update 回调，不表示跟踪任意对象变更 |

## 验证范围与关联文档

2026-11-25 在 Windows / .NET 10 Release 编译运行完整示例，并用本地探针复现原地变更后 NotifyUpdated 无法移除旧 bucket。仓库暂未发现本包专属测试工程，这些局部 E3 不代表全部索引或失败矩阵通过。

源码见 [SkillLibrary](../Runtime/SkillLibrary/SkillLibrary.cs)、[单键索引](../Runtime/SkillLibrary/DerivedKeyedSkillIndex.cs)、[多键索引](../Runtime/SkillLibrary/DerivedMultiKeySkillIndex.cs)及[包内样例](../Runtime/SkillLibrary/SkillLibraryExample.cs)。未运行 Unity、干净 UPM 安装、真实技能执行、热重载、压力或 CI；旧资料见[历史索引](../Document/README.md)。
