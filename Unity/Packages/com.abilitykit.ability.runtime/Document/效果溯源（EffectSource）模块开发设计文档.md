# Ability-Kit 效果溯源（EffectSource）模块开发设计文档

> **阅读对象**：希望了解效果溯源系统如何设计的开发者
>
> **文档目标**：让你理解"什么是效果溯源"、"上下文树如何构建和管理"、"溯源数据如何用于复杂游戏逻辑"

---

## 一、设计理念：为什么需要效果溯源？

### 1.1 复杂效果链的挑战

```
❌ 传统做法的问题：

┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│  【场景】：A 技能释放了投射物 P，P 命中 B 后施加了 Buff C，            │
│           Buff C 的周期效果触发了一个区域效果 R，R 伤害了 C            │
│                                                                         │
│  问题：                                                                  │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │  1. 如何知道这次伤害的真正来源是技能 A？                         │  │
│  │  2. 如果 B 死亡，Buff C 应该如何清理？                          │  │
│  │  3. 如何计算 A 技能的总伤害贡献？                                │  │
│  │  4. 如何实现"对来源为 A 的伤害增加 20%"的逻辑？                │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  传统实现方式：                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │  // 硬编码引用，耦合严重                                         │  │
│  │  public void OnProjectileHit(Projectile p, Unit target)         │  │
│  │  {                                                              │  │
│  │      target.Buffs.Add(new Buff {                                 │  │
│  │          SourceSkillId = p.SkillId,                              │  │
│  │          SourceActorId = p.CasterId,                             │  │
│  │          Parent = p                                              │  │
│  │      });                                                         │  │
│  │  }                                                              │  │
│  │                                                                  │  │
│  │  问题：无法处理深层嵌套的复杂效果链                               │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 1.2 效果溯源的解决方案

```
✅ EffectSource 的设计思路：

┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│  【核心概念】：上下文树（Context Tree）                               │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │                           Root (Skill A)                         │  │
│  │                           ContextId: 1                           │  │
│  │                           Kind: SkillCast                        │  │
│  │                           Source: Hero_A                         │  │
│  │                           Target: Enemy_B                        │  │
│  │                                                                  │  │
│  │         ┌─────────────┬─────────────┬─────────────┐           │  │
│  │         │             │             │             │             │  │
│  │         ▼             ▼             ▼             ▼             │  │
│  │    ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐        │  │
│  │    │Projectile│  │ Buff C  │  │  ...   │  │  ...   │        │  │
│  │    │Context 2 │  │Context 3 │  │         │  │         │        │  │
│  │    │Kind:Effect│  │Kind:Buff │  │         │  │         │        │  │
│  │    └────┬─────┘  └────┬─────┘  └─────────┘  └─────────┘        │  │
│  │         │             │                                          │  │
│  │         ▼             ▼                                          │  │
│  │    ┌─────────┐  ┌─────────┐                                      │  │
│  │    │ Area R  │  │Interval │                                      │  │
│  │    │Context 4│  │Effect   │                                      │  │
│  │    │Kind:Effect│  │Context 5│                                      │  │
│  │    └─────────┘  └─────────┘                                      │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  核心优势：                                                              │
│  • 任何效果的伤害都可以通过 ContextId 追溯到 Root（技能 A）              │
│  • 根节点结束时，所有子节点可以自动清理                                   │
│  • 可以计算整个效果链的统计信息                                          │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 1.3 参考与设计来源

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    参考 Unreal GAS 的效果溯源设计                        │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Unreal GAS 中的 GameplayEffect 包含 SourceObject 和 ContextData，       │
│  Ability-Kit 的 EffectSource 在此基础上做了以下增强：                    │
│                                                                         │
│  1. 【树形结构】：支持无限层级的父子关系                                │
│  2. 【生命周期管理】：CreateRoot/CreateChild/End 自动计数               │
│  3. 【快照系统】：支持帧同步下的状态记录                                │
│  4. 【根级黑板】：存储元数据供查询使用                                  │
│  5. 【查询接口】：支持向上追溯（BuildChain）和统计（RootStats）         │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 二、核心概念

### 2.1 效果上下文（EffectSourceContext）

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        EffectSourceContext                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  【定义】：一次游戏效果执行产生的上下文记录                             │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │  ContextRecord (内部存储结构)                                    │  │
│  │  ────────────────────────────────────────────────────────────  │  │
│  │                                                                  │  │
│  │  long ContextId        // 唯一标识                               │  │
│  │  long RootId          // 根节点 ContextId（用于快速定位）        │  │
│  │  long ParentId         // 父节点 ContextId（0表示根节点）       │  │
│  │                                                                  │  │
│  │  EffectSourceKind Kind   // 效果类型                             │  │
│  │  int ConfigId            // 配置 ID（如 SkillId, BuffId）       │  │
│  │  int SourceActorId      // 来源实体 ID                         │  │
│  │  int TargetActorId      // 目标实体 ID                         │  │
│  │                                                                  │  │
│  │  object OriginSource    // 原始来源（可能不同于 SourceActorId）  │  │
│  │  object OriginTarget    // 原始目标（可能不同于 TargetActorId） │  │
│  │                                                                  │  │
│  │  int CreatedFrame       // 创建帧                                │  │
│  │  int EndedFrame         // 结束帧（0表示活跃）                   │  │
│  │  EffectSourceEndReason  // 结束原因                             │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.2 效果类型枚举

```csharp
namespace AbilityKit.Ability.Impl.Moba
{
    /// <summary>
    /// 效果来源类型
    /// </summary>
    public enum EffectSourceKind
    {
        /// <summary>
        /// 无效
        /// </summary>
        None = 0,

        /// <summary>
        /// 技能施法
        /// </summary>
        SkillCast = 1,

        /// <summary>
        /// Buff 效果
        /// </summary>
        Buff = 2,

        /// <summary>
        /// 一般效果
        /// </summary>
        Effect = 3,

        /// <summary>
        /// 触发器动作
        /// </summary>
        TriggerAction = 4,

        /// <summary>
        /// 系统（用于被动技能等）
        /// </summary>
        System = 5,
    }
}
```

### 2.3 结束原因枚举

```csharp
namespace AbilityKit.Ability.Impl.Moba
{
    /// <summary>
    /// 效果结束原因
    /// </summary>
    public enum EffectSourceEndReason
    {
        None = 0,
        Completed = 1,    // 正常完成
        Cancelled = 2,    // 被取消
        Expired = 3,      // 到期
        Dispelled = 4,    // 被驱散
        Dead = 5,         // 目标死亡
        Replaced = 6,     // 被替换
    }
}
```

---

## 三、核心架构

### 3.1 整体架构图

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         EffectSourceRegistry                            │
│                        （核心注册表）                                   │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                        数据存储                                   │  │
│  │                                                                  │  │
│  │  Dictionary<long, ContextRecord> _contexts                       │  │
│  │      ↑ 存储所有效果上下文                                         │  │
│  │                                                                  │  │
│  │  Dictionary<long, RootRecord> _roots                             │  │
│  │      ↑ 存储根节点统计信息                                         │  │
│  │                                                                  │  │
│  │  Dictionary<long, List<long>> _childrenByParent                 │  │
│  │      ↑ 父子关系映射                                               │  │
│  │                                                                  │  │
│  │  Dictionary<long, DictionaryBlackboard> _rootBlackboards        │  │
│  │      ↑ 根级黑板存储元数据                                         │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                        核心功能                                   │  │
│  │                                                                  │  │
│  │  创建：                                                           │  │
│  │  ├── CreateRoot()      → 创建根上下文                           │  │
│  │  ├── CreateChild()     → 创建子上下文                          │  │
│  │  ├── BeginRoot()       → 创建+Scope自动管理                    │  │
│  │  └── EnsureRoot()      → 确保根存在（用于外部创建）             │  │
│  │                                                                  │  │
│  │  生命周期：                                                       │  │
│  │  ├── End()             → 结束上下文                             │  │
│  │  ├── RetainRoot()       → 增加外部引用计数                      │  │
│  │  ├── ReleaseRoot()      → 减少外部引用计数                    │  │
│  │  └── Purge()            → 清理过期数据                         │  │
│  │                                                                  │  │
│  │  查询：                                                           │  │
│  │  ├── TryGetSnapshot()   → 获取快照                             │  │
│  │  ├── TryGetRootStats()  → 获取根统计                          │  │
│  │  ├── TryBuildChain()    → 追溯父链                            │  │
│  │  ├── TryGetChildren()   → 获取子节点                          │  │
│  │  └── TryGetRootState()  → 获取根状态                          │  │
│  │                                                                  │  │
│  │  黑板：                                                           │  │
│  │  ├── GetOrCreateRootBlackboard()                              │  │
│  │  ├── SetRootInt() / TryGetRootInt()                            │  │
│  │  └── HasSkillRootMeta() / TryGetSkillRootMeta()                │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 3.2 根记录（RootRecord）

```csharp
// 根节点统计信息
private sealed class RootRecord
{
    public int ActiveCount;         // 活跃子节点数量
    public int ExternalRefCount;    // 外部引用计数
    public int LastTouchedFrame;    // 最后操作帧
}
```

### 3.3 根状态与统计

```csharp
// 根状态（对外暴露）
public readonly struct RootState
{
    public readonly long RootId;
    public readonly int ActiveCount;
    public readonly int ExternalRefCount;
    public readonly int LastTouchedFrame;
}

// 根统计（更详细的统计信息）
public readonly struct RootStats
{
    public readonly long RootId;
    public readonly int SubtreeNodeCount;       // 子树节点总数
    public readonly int ActiveNodeCount;       // 活跃节点数
    public readonly int OldestActiveCreatedFrame; // 最老活跃节点创建帧
    public readonly int ActiveCount;
    public readonly int ExternalRefCount;
    public readonly int LastTouchedFrame;
}
```

---

## 四、生命周期管理

### 4.1 根上下文创建

```csharp
public long CreateRoot(
    EffectSourceKind kind,
    int configId,
    int sourceActorId,
    int targetActorId,
    int frame,
    object originSource = null,
    object originTarget = null)
{
    if (!Enabled) return 0;
    if (kind == EffectSourceKind.None) return 0;

    var id = NextId();

    var r = new ContextRecord
    {
        ContextId = id,
        RootId = id,           // 根节点的 RootId 指向自己
        ParentId = 0,          // 根节点没有父节点
        Kind = kind,
        ConfigId = configId,
        SourceActorId = sourceActorId,
        TargetActorId = targetActorId,
        OriginSource = originSource ?? sourceActorId,
        OriginTarget = originTarget ?? targetActorId,
        CreatedFrame = frame,
        EndedFrame = 0,        // 未结束
        EndReason = EffectSourceEndReason.None,
    };

    _contexts[id] = r;
    _roots[id] = new RootRecord { ActiveCount = 1, ExternalRefCount = 0, LastTouchedFrame = frame };
    return id;
}
```

### 4.2 子上下文创建

```csharp
public long CreateChild(
    long parentContextId,
    EffectSourceKind kind,
    int configId,
    int sourceActorId,
    int targetActorId,
    int frame,
    object originSource = null,
    object originTarget = null)
{
    if (!Enabled) return 0;
    if (kind == EffectSourceKind.None) return 0;
    if (parentContextId <= 0) return 0;
    if (!_contexts.TryGetValue(parentContextId, out var parent)) return 0;

    var id = NextId();
    var rootId = parent.RootId;   // 继承根节点 ID

    var r = new ContextRecord
    {
        ContextId = id,
        RootId = rootId,          // 指向同一个根
        ParentId = parentContextId,
        Kind = kind,
        ConfigId = configId,
        SourceActorId = sourceActorId,
        TargetActorId = targetActorId,
        OriginSource = originSource ?? parent.OriginSource ?? parent.SourceActorId,  // 向上传播
        OriginTarget = originTarget ?? parent.OriginTarget ?? parent.TargetActorId,
        CreatedFrame = frame,
        EndedFrame = 0,
        EndReason = EffectSourceEndReason.None,
    };

    _contexts[id] = r;

    // 更新父子关系
    if (!_childrenByParent.TryGetValue(parentContextId, out var list))
    {
        list = new List<long>(2);
        _childrenByParent[parentContextId] = list;
    }
    list.Add(id);

    // 更新根节点计数
    TouchRoot(rootId, frame);
    if (_roots.TryGetValue(rootId, out var root))
    {
        root.ActiveCount++;  // 活跃计数 +1
    }

    return id;
}
```

### 4.3 上下文结束

```csharp
public bool End(long contextId, int frame, EffectSourceEndReason reason)
{
    if (!Enabled) return false;
    if (contextId <= 0) return false;
    if (!_contexts.TryGetValue(contextId, out var r)) return false;
    if (r.EndedFrame > 0) return false;  // 已经结束

    r.EndedFrame = frame;
    r.EndReason = reason;

    // 更新根节点活跃计数
    TouchRoot(r.RootId, frame);
    if (_roots.TryGetValue(r.RootId, out var root))
    {
        if (root.ActiveCount > 0)
        {
            root.ActiveCount--;
        }
    }

    return true;
}
```

### 4.4 生命周期时序图

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      上下文生命周期时序图                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  施法者                        系统                       目标           │
│    │                            │                         │             │
│    │  释放技能 A                │                         │             │
│    │  ─────────────────────────▶│                         │             │
│    │                            │                         │             │
│    │                            │  CreateRoot(1)          │             │
│    │                            │  RootId=1, Kind=Skill   │             │
│    │                            │  ActiveCount=1          │             │
│    │                            │  ───────────────        │             │
│    │                            │                         │             │
│    │                            │  CreateChild(1, 2)      │             │
│    │                            │  Parent=1, Kind=Effect  │             │
│    │                            │  RootId=1 (继承)        │             │
│    │                            │  ActiveCount=2          │             │
│    │                            │  ───────────────        │             │
│    │                            │                         │             │
│    │                            │  CreateChild(2, 3)      │             │
│    │                            │  Parent=2, Kind=Effect  │             │
│    │                            │  RootId=1 (继承)        │             │
│    │                            │  ActiveCount=3          │             │
│    │                            │  ───────────────        │             │
│    │                            │                         │             │
│    │                            │  End(3, reason=Expired)│             │
│    │                            │  EndedFrame=100         │             │
│    │                            │  ActiveCount=2          │             │
│    │                            │  ───────────────        │             │
│    │                            │                         │             │
│    │                            │  End(1, reason=Completed)│            │
│    │                            │  EndedFrame=105         │             │
│    │                            │  ActiveCount=1          │             │
│    │                            │  ───────────────        │             │
│    │                            │                         │             │
│    │                            │  Purge()                │             │
│    │                            │  清理所有已结束节点      │             │
│    │                            │  ───────────────        │             │
│    │                            │                         │             │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 五、Scope 管理

### 5.1 EffectSourceScope

```csharp
/// <summary>
/// 效果源作用域
/// 使用 using 语句自动管理生命周期
/// </summary>
public readonly struct EffectSourceScope : IDisposable
{
    private readonly EffectSourceRegistry _registry;
    private readonly long _contextId;
    private readonly int _frame;
    private readonly EffectSourceEndReason _reason;

    public long ContextId => _contextId;

    public void Dispose()
    {
        if (_registry == null) return;
        if (_contextId <= 0) return;
        _registry.End(_contextId, _frame, _reason);
    }
}
```

### 5.2 使用示例

```csharp
// 传统写法
public void CastSkill(SkillRequest request)
{
    var contextId = _effectSource.CreateRoot(...);
    try
    {
        // 执行技能逻辑
        ExecuteSkillLogic();

        // 创建子效果
        var childId = _effectSource.CreateChild(contextId, ...);

        // 结束子效果
        _effectSource.End(childId, frame, EffectSourceEndReason.Completed);
    }
    finally
    {
        // 结束根效果
        _effectSource.End(contextId, frame, EffectSourceEndReason.Completed);
    }
}

// 使用 Scope（推荐）
public void CastSkill(SkillRequest request)
{
    using (var scope = _effectSource.BeginRoot(...))
    {
        // 执行技能逻辑
        ExecuteSkillLogic();

        // 创建子效果（需要手动 End）
        var childId = _effectSource.CreateChild(scope.ContextId, ...);

        // childId 超出 scope 范围后，scope.Dispose() 自动调用
    }
    // childId 会被自动 End（如果还未 End）
}
```

---

## 六、根级黑板（Root Blackboard）

### 6.1 黑板概念

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        根级黑板（RootBlackboard）                        │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  【设计目的】：在根节点存储元数据，供后续查询使用                         │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │  根节点 (SkillCast)                                             │  │
│  │  ────────────────────────────────────────────────────────────  │  │
│  │                                                                  │  │
│  │  RootBlackboard:                                                │  │
│  │  ┌─────────────────────────────────────────────────────────┐  │  │
│  │  │  effectsource.kind      = SkillCast (int)              │  │  │
│  │  │  skill.id               = 1001 (int)                   │  │  │
│  │  │  skill.slot             = 1 (int)                      │  │  │
│  │  │  skill.level            = 5 (int)                     │  │  │
│  │  │  skill.sequence         = 3 (int)                     │  │  │
│  │  │  skill.caster.actorId   = 50 (int)                    │  │  │
│  │  │  skill.target.actorId   = 100 (int)                  │  │  │
│  │  └─────────────────────────────────────────────────────────┘  │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  【使用场景】：                                                          │
│  • 查询技能的施法者、目标、等级等信息                                    │
│  • 实现跨效果链的数据传递                                               │
│  • 调试和日志记录                                                       │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 6.2 预定义键

```csharp
public sealed partial class EffectSourceRegistry
{
    // IntKey 用于高效存储整数类型数据
    public readonly struct IntKey
    {
        public readonly int Id;
    }

    // 预定义的根级黑板键
    public static class RootIntKeys
    {
        public static readonly IntKey EffectSourceKind = new IntKey(
            StableStringId.Get("effectsource.kind"));

        public static readonly IntKey SkillId = new IntKey(
            StableStringId.Get("skill.id"));

        public static readonly IntKey SkillSlot = new IntKey(
            StableStringId.Get("skill.slot"));

        public static readonly IntKey SkillLevel = new IntKey(
            StableStringId.Get("skill.level"));

        public static readonly IntKey SkillSequence = new IntKey(
            StableStringId.Get("skill.sequence"));

        public static readonly IntKey SkillCasterActorId = new IntKey(
            StableStringId.Get("skill.caster.actorId"));

        public static readonly IntKey SkillTargetActorId = new IntKey(
            StableStringId.Get("skill.target.actorId"));
    }

    // 技能根元数据结构
    public readonly struct SkillRootMeta
    {
        public readonly int SkillId;
        public readonly int SkillSlot;
        public readonly int SkillLevel;
        public readonly int Sequence;
        public readonly int CasterActorId;
        public readonly int TargetActorId;
    }
}
```

### 6.3 使用示例

```csharp
// 设置技能元数据
public void SetSkillMeta(long rootId, int skillId, int slot, int level, int caster, int target)
{
    var registry = _effectSource;

    registry.SetRootInt(rootId, EffectSourceRegistry.RootIntKeys.EffectSourceKind, (int)EffectSourceKind.SkillCast);
    registry.SetRootInt(rootId, EffectSourceRegistry.RootIntKeys.SkillId, skillId);
    registry.SetRootInt(rootId, EffectSourceRegistry.RootIntKeys.SkillSlot, slot);
    registry.SetRootInt(rootId, EffectSourceRegistry.RootIntKeys.SkillLevel, level);
    registry.SetRootInt(rootId, EffectSourceRegistry.RootIntKeys.SkillCasterActorId, caster);
    registry.SetRootInt(rootId, EffectSourceRegistry.RootIntKeys.SkillTargetActorId, target);
}

// 查询技能元数据
public bool TryGetSkillMeta(long contextId, out SkillRootMeta meta)
{
    // 通过 RootId 获取黑板
    if (!_contexts.TryGetValue(contextId, out var r)) { meta = default; return false; }

    return _effectSource.TryGetSkillRootMeta(r.RootId, out meta);
}
```

---

## 七、查询接口

### 7.1 获取快照

```csharp
public bool TryGetSnapshot(long contextId, out EffectSourceSnapshot snapshot)
{
    if (_contexts.TryGetValue(contextId, out var r))
    {
        snapshot = new EffectSourceSnapshot(
            contextId: r.ContextId,
            rootId: r.RootId,
            parentId: r.ParentId,
            kind: r.Kind,
            configId: r.ConfigId,
            sourceActorId: r.SourceActorId,
            targetActorId: r.TargetActorId,
            createdFrame: r.CreatedFrame,
            endedFrame: r.EndedFrame,
            endReason: r.EndReason);
        return true;
    }

    snapshot = default;
    return false;
}

/// <summary>
/// 快照结构（可序列化）
/// </summary>
[Serializable]
public readonly struct EffectSourceSnapshot
{
    public readonly long ContextId;
    public readonly long RootId;
    public readonly long ParentId;
    public readonly EffectSourceKind Kind;
    public readonly int ConfigId;
    public readonly int SourceActorId;
    public readonly int TargetActorId;
    public readonly int CreatedFrame;
    public readonly int EndedFrame;
    public readonly EffectSourceEndReason EndReason;

    public bool IsEnded => EndedFrame > 0;
}
```

### 7.2 追溯父链

```csharp
public bool TryBuildChain(long contextId, List<EffectSourceSnapshot> chain)
{
    chain.Clear();

    var cur = contextId;
    var guard = 0;

    // 沿着 ParentId 向上追溯到根
    while (cur > 0 && guard++ < 1024)
    {
        if (!_contexts.TryGetValue(cur, out var r)) break;

        chain.Add(new EffectSourceSnapshot(
            contextId: r.ContextId,
            rootId: r.RootId,
            parentId: r.ParentId,
            kind: r.Kind,
            configId: r.ConfigId,
            sourceActorId: r.SourceActorId,
            targetActorId: r.TargetActorId,
            createdFrame: r.CreatedFrame,
            endedFrame: r.EndedFrame,
            endReason: r.EndReason));

        cur = r.ParentId;
    }

    return chain.Count > 0;
}
```

### 7.3 查询根统计

```csharp
public bool TryGetRootStats(long rootId, out RootStats stats)
{
    // 递归遍历子树，统计节点数量
    var nodeCount = 0;
    var activeNodeCount = 0;
    var oldestActiveCreatedFrame = int.MaxValue;

    Walk(rootId);  // 递归统计

    stats = new RootStats(
        rootId: rootId,
        subtreeNodeCount: nodeCount,
        activeNodeCount: activeNodeCount,
        oldestActiveCreatedFrame: oldestActiveCreatedFrame,
        activeCount: root.ActiveCount,
        externalRefCount: root.ExternalRefCount,
        lastTouchedFrame: root.LastTouchedFrame);

    return true;

    void Walk(long id)
    {
        if (!_contexts.TryGetValue(id, out var r)) return;

        nodeCount++;
        if (r.EndedFrame == 0)  // 活跃节点
        {
            activeNodeCount++;
            if (r.CreatedFrame < oldestActiveCreatedFrame)
                oldestActiveCreatedFrame = r.CreatedFrame;
        }

        // 递归子节点
        if (_childrenByParent.TryGetValue(id, out var children))
        {
            foreach (var childId in children)
                Walk(childId);
        }
    }
}
```

---

## 八、与 Buff/技能系统的集成

### 8.1 集成架构

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    EffectSource 与游戏系统的集成                        │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  【技能施法】                                                           │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │  SkillPipelineRunner                                            │  │
│  │       │                                                          │  │
│  │       ▼                                                          │  │
│  │  CreateRoot(Kind=SkillCast, configId=SkillId)                  │  │
│  │       │                                                          │  │
│  │       ▼                                                          │  │
│  │  设置 RootBlackboard 元数据                                      │  │
│  │       │                                                          │  │
│  │       ▼                                                          │  │
│  │  SkillTimeline → 创建子上下文（Kind=Effect）                     │  │
│  │       │                                                          │  │
│  │       ▼                                                          │  │
│  │  技能完成 → End(ContextId)                                      │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  【Buff 应用】                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │  MobaBuffApplySystem                                            │  │
│  │       │                                                          │  │
│  │       ▼                                                          │  │
│  │  CreateChild(Kind=Buff, parentContextId=来源ContextId)          │  │
│  │       │                                                          │  │
│  │       ▼                                                          │  │
│  │  BuffRuntime.SourceContextId = childId                          │  │
│  │       │                                                          │  │
│  │       ▼                                                          │  │
│  │  Buff 周期效果/移除 → End(childId)                              │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  【被动技能】                                                           │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │  PassiveSkillTriggerListenerManager                               │  │
│  │       │                                                          │  │
│  │       ▼                                                          │  │
│  │  CreateRoot(Kind=System, configId=PassiveSkillId)               │  │
│  │       │                                                          │  │
│  │       ▼                                                          │  │
│  │  触发时 → 触发器系统查找匹配 → 执行技能管线                      │  │
│  │       │                                                          │  │
│  │       ▼                                                          │  │
│  │  被动技能注销 → End(ContextId)                                   │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 8.2 代码示例

```csharp
// Buff 上下文服务
public void EnsureBuffContext(BuffRuntime rt, int buffId, int sourceActorId, int targetActorId,
    object originSource, object originTarget, long parentContextId)
{
    if (rt.SourceContextId != 0) return;

    var frame = GetFrame();

    if (parentContextId != 0)
    {
        // 创建子上下文，继承父节点的溯源链
        rt.SourceContextId = _effectSource.CreateChild(
            parentContextId,
            kind: EffectSourceKind.Buff,
            configId: buffId,
            sourceActorId: sourceActorId,
            targetActorId: targetActorId,
            frame: frame,
            originSource: originSource,
            originTarget: originTarget);
    }
    else
    {
        // 没有父上下文，创建根上下文
        rt.SourceContextId = _effectSource.CreateRoot(
            kind: EffectSourceKind.Buff,
            configId: buffId,
            sourceActorId: sourceActorId,
            targetActorId: targetActorId,
            frame: frame,
            originSource: originSource,
            originTarget: originTarget);
    }
}

// 效果执行时追溯来源
public void ExecuteEffect(long contextId)
{
    // 1. 获取当前效果的信息
    if (_effectSource.TryGetSnapshot(contextId, out var snapshot))
    {
        // 2. 追溯到根节点
        var chain = new List<EffectSourceSnapshot>();
        _effectSource.TryBuildChain(contextId, chain);

        // 3. 查找根节点（通常是技能）
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (chain[i].Kind == EffectSourceKind.SkillCast)
            {
                // 4. 获取技能元数据
                if (_effectSource.TryGetSkillRootMeta(chain[i].RootId, out var meta))
                {
                    // 施法者: meta.CasterActorId
                    // 技能ID: meta.SkillId
                    // 技能等级: meta.SkillLevel
                }
                break;
            }
        }
    }
}
```

---

## 九、复杂效果链示例

### 9.1 完整效果链

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      复杂效果链示例                                     │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  【场景】：盖伦释放"审判"技能（Q技能）                                │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │  1. SkillCast (Root)                                           │  │
│  │     ContextId: 1                                                │  │
│  │     Kind: SkillCast                                             │  │
│  │     ConfigId: Skill_1001 (Q技能)                                │  │
│  │     Source: Hero_Garen (ActorId: 50)                            │  │
│  │     Target: Enemy_Teemo (ActorId: 100)                         │  │
│  │     ─────────────────────────────────────────────────────────   │  │
│  │     RootBlackboard:                                            │  │
│  │       skill.id = 1001, skill.level = 5, skill.slot = 1        │  │
│  │                                                                  │  │
│  │     ┌──────────────────┬──────────────────┬──────────────┐     │  │
│  │     │                  │                  │              │     │  │
│  │     ▼                  ▼                  ▼              ▼     │  │
│  │  ┌─────────┐     ┌─────────┐     ┌─────────────┐ ┌────────┐ │  │
│  │  │Effect   │     │Buff     │     │PeriodicDamage│ │  ...   │ │  │
│  │  │(Q命中)  │     │(韧性)   │     │(每秒伤害)   │ │        │ │  │
│  │  │CtxId:2 │     │CtxId:3  │     │CtxId:4      │ │        │ │  │
│  │  │Kind:3  │     │Kind:2   │     │Kind:3        │ │        │ │  │
│  │  │Parent:1 │     │Parent:1 │     │Parent:3      │ │        │ │  │
│  │  └────┬────┘     └────┬────┘     └──────┬──────┘ └────────┘ │  │
│  │       │               │                  │                  │  │
│  │       │               ▼                  │                  │  │
│  │       │          ┌─────────┐            │                  │  │
│  │       │          │Interval │            │                  │  │
│  │       │          │Effect   │            │                  │  │
│  │       │          │CtxId:5  │            │                  │  │
│  │       │          │Parent:3 │            │                  │  │
│  │       │          └─────────┘            │                  │  │
│  │       │                                 │                  │  │
│  │       ▼                                 ▼                  │  │
│  │  ┌─────────┐                   ┌─────────────┐          │  │
│  │  │Damage   │                   │Damage       │          │  │
│  │  │(Q伤害)  │                   │(DOT伤害)    │          │  │
│  │  │CtxId:6  │                   │CtxId:7      │          │  │
│  │  │Kind:3   │                   │Kind:3       │          │  │
│  │  │Parent:2 │                   │Parent:5     │          │  │
│  │  │Origin:1│                   │Origin:1     │          │  │
│  │  │         │                   │(追溯到技能) │          │  │
│  │  └─────────┘                   └─────────────┘          │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 9.2 溯源应用场景

```csharp
// 场景1：实现"对技能Q命中的敌人增加20%伤害"
public float CalculateDamage(DamageContext ctx)
{
    // 1. 追溯伤害来源
    var chain = new List<EffectSourceSnapshot>();
    _effectSource.TryBuildChain(ctx.SourceContextId, chain);

    // 2. 查找原始技能
    foreach (var node in chain)
    {
        if (node.Kind == EffectSourceKind.SkillCast && node.ConfigId == 1001)
        {
            // 3. 应用增伤
            return ctx.BaseDamage * 1.2f;
        }
    }

    return ctx.BaseDamage;
}

// 场景2：计算技能总伤害贡献
public int CalculateTotalDamageContribution(long skillContextId)
{
    if (!_effectSource.TryGetSnapshot(skillContextId, out var snapshot))
        return 0;

    var totalDamage = 0;

    // 递归遍历子树，累加所有伤害
    void Walk(long id)
    {
        if (_contexts.TryGetValue(id, out var r))
        {
            if (r.Kind == EffectSourceKind.Effect && r.ConfigId == DamageEffectId)
            {
                totalDamage += GetDamageFromContext(r);
            }

            if (_childrenByParent.TryGetValue(id, out var children))
            {
                foreach (var childId in children)
                    Walk(childId);
            }
        }
    }

    Walk(snapshot.RootId);
    return totalDamage;
}

// 场景3：技能结束时清理所有关联Buff
public void OnSkillEnded(long skillContextId)
{
    if (!_effectSource.TryGetSnapshot(skillContextId, out var snapshot))
        return;

    // 查找所有 Buff 节点
    var chain = new List<EffectSourceSnapshot>();
    _effectSource.TryBuildChain(skillContextId, chain);

    foreach (var node in chain)
    {
        if (node.Kind == EffectSourceKind.Buff)
        {
            // 驱散该 Buff
            DispelBuff(node.TargetActorId, node.ConfigId);
        }
    }

    // 结束技能上下文
    _effectSource.End(skillContextId, CurrentFrame, EffectSourceEndReason.Completed);
}
```

---

## 十、设计总结

### 10.1 核心优势

| 特性 | 说明 |
|------|------|
| **树形溯源** | 任何效果都可以追溯到根节点（技能） |
| **生命周期管理** | 自动计数和清理，支持 Scope 管理 |
| **高效查询** | 通过 RootId 快速定位，通过 ParentId 追溯 |
| **元数据存储** | RootBlackboard 支持存储和查询元数据 |
| **帧同步友好** | 支持快照和状态记录 |
| **内存管理** | Purge 机制自动清理过期数据 |

### 10.2 使用场景

| 场景 | 解决方案 |
|------|----------|
| 计算技能总伤害 | 遍历子树累加 |
| 实现"对技能X命中的目标增加效果" | 追溯来源匹配 |
| 技能结束时清理所有关联Buff | 遍历子树查找Buff类型 |
| 驱散特定来源的Buff | 按RootId过滤 |
| 调试效果链 | BuildChain追溯完整路径 |

### 10.3 注意事项

- **性能考虑**：深度过大的效果链可能影响性能，建议限制层级
- **内存管理**：定期调用 Purge 清理过期数据
- **循环引用**：系统不直接支持循环引用，需通过外部机制处理
- **ExternalRefCount**：外部持有根引用时不会被 Purge 清理

---

## 十一、文件清单

| 文件路径 | 说明 |
|----------|------|
| `Runtime/Ability/Impl/Moba/EffectSource/EffectSourceRegistry.cs` | 核心注册表 |
| `Runtime/Ability/Impl/Moba/EffectSource/EffectSourceRegistry.Create.cs` | 创建方法 |
| `Runtime/Ability/Impl/Moba/EffectSource/EffectSourceRegistry.Lifecycle.cs` | 生命周期管理 |
| `Runtime/Ability/Impl/Moba/EffectSource/EffectSourceRegistry.Query.cs` | 查询接口 |
| `Runtime/Ability/Impl/Moba/EffectSource/EffectSourceRegistry.RootBlackboard.cs` | 根级黑板 |
| `Runtime/Ability/Impl/Moba/EffectSource/EffectSourceSnapshot.cs` | 快照结构 |
| `Runtime/Ability/Impl/Moba/EffectSource/EffectSourceKeys.cs` | 键定义常量 |
| `Runtime/Ability/Impl/Moba/EffectSource/EffectSourceLiveRegistry.cs` | 编辑器调试注册表 |
| `Runtime/Ability/Impl/Moba/Enum/EffectSourceEnums.cs` | 枚举定义 |
