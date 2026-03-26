# HFSM Action/Behavior 系统设计文档

## 1. 概述

将 HFSM 从纯状态标记系统扩展为支持在状态中执行行为 (Behavior) 的系统，类似于 NodeCanvas 的 Action 概念。行为可以是原子的（如播放动画、等待），也可以是复合的（序列、选择器、并行等）。

## 2. 设计目标

### 2.1 核心目标
- 状态可以在激活时执行行为
- 行为可以是原子的或复合的
- 支持行为树的核心组合模式
- 与现有 HFSM 系统无缝集成

### 2.2 行为分类

| 类别 | 描述 | 示例 |
|------|------|------|
| 原子行为 | 不可分割的最小行为单元 | Wait, Log, SetVariable, PlayAnimation |
| 复合行为 | 由多个子行为组成 | Sequence, Selector, Parallel, RandomSelector |
| 修饰器行为 | 修改子行为的行为 | Repeat, Invert, TimeLimit, UntilSuccess |

## 3. 行为接口设计

```csharp
namespace UnityHFSM.Actions
{
    /// <summary>
    /// 行为执行结果
    /// </summary>
    public enum BehaviorStatus
    {
        /// <summary>行为正在运行</summary>
        Running,
        /// <summary>行为成功完成</summary>
        Success,
        /// <summary>行为失败</summary>
        Failure
    }

    /// <summary>
    /// 行为上下文，包含执行所需的所有信息
    /// </summary>
    public class BehaviorContext
    {
        public float deltaTime;
        public float elapsedTime;
        public MonoBehaviour mono;  // 用于协程
        public object userData;      // 自定义数据
    }

    /// <summary>
    /// 所有行为的基类接口
    /// </summary>
    public interface IAction
    {
        /// <summary>行为名称（用于编辑器显示）</summary>
        string Name { get; }

        /// <summary>执行行为，返回执行状态</summary>
        BehaviorStatus Execute(BehaviorContext context);

        /// <summary>重置行为状态（当行为所属状态进入时调用）</summary>
        void Reset();

        /// <summary>强制终止行为（当行为所属状态退出时调用）</summary>
        void ForceEnd();
    }

    /// <summary>
    /// 支持协程的行为基类
    /// </summary>
    public interface IYieldAction : IAction
    {
        /// <summary>如果有正在运行的协程，返回它</summary>
        IEnumerator GetYieldEnumerator(BehaviorContext context);
    }
}
```

## 4. 行为类型分类

### 4.1 原子行为 (Primitive Actions)

| 行为类型 | 描述 | 参数 |
|----------|------|------|
| `Wait` | 等待指定时间 | `float duration` |
| `WaitUntil` | 等待条件满足 | `Func<bool> condition` |
| `Log` | 输出日志 | `string message` |
| `SetFloatVariable` | 设置浮点变量 | `string variableName, float value` |
| `SetBoolVariable` | 设置布尔变量 | `string variableName, bool value` |
| `TriggerTransition` | 触发状态转换 | `string transitionName` |
| `PlayAnimation` | 播放动画 | `string animationName, float crossFade = 0.1f` |
| `SetActive` | 设置 GameObject 激活状态 | `GameObject target, bool active` |
| `MoveTo` | 移动到目标位置 | `Transform target, float speed` |
| `CustomMethod` | 调用自定义方法 | `string methodName` (通过反射或 delegate) |

### 4.2 复合行为 (Composite Actions)

| 行为类型 | 描述 | 参数 |
|----------|------|------|
| `Sequence` | 顺序执行子行为，任一失败则整体失败 | `List<IAction> children` |
| `Selector` | 选择执行子行为，任一成功则整体成功 | `List<IAction> children` |
| `Parallel` | 同时执行所有子行为 | `List<IAction> children, bool failOnAny = false` |
| `RandomSelector` | 随机选择一个子行为执行 | `List<IAction> children, List<float> weights` |
| `RandomSequence` | 随机顺序执行子行为 | `List<IAction> children` |

### 4.3 修饰器行为 (Decorator Actions)

| 行为类型 | 描述 | 参数 |
|----------|------|------|
| `Repeat` | 重复执行子行为指定次数或无限 | `IAction child, int count = -1 (无限)` |
| `Invert` | 反转子行为的结果（成功↔失败） | `IAction child` |
| `TimeLimit` | 限制子行为的最大执行时间 | `IAction child, float limit` |
| `UntilSuccess` | 重复执行直到成功 | `IAction child` |
| `UntilFailure` | 重复执行直到失败 | `IAction child` |
| `Cooldown` | 限制执行频率 | `IAction child, float cooldown` |
| `If` | 条件执行 | `Func<bool> condition, IAction thenAction, IAction elseAction = null` |

## 5. 编辑器数据结构

### 5.1 HfsmBehaviorItem (行为项)

```csharp
[System.Serializable]
public class HfsmBehaviorItem
{
    public string id;  // 唯一标识
    public HfsmBehaviorType type;
    public List<string> childIds;  // 复合行为的子项
    public string parentId;        // 父行为项 ID

    // 行为特定参数（使用可扩展的参数系统）
    public List<HfsmBehaviorParameter> parameters;
}

[System.Serializable]
public class HfsmBehaviorParameter
{
    public string name;
    public HfsmBehaviorParameterType valueType;
    public float floatValue;
    public int intValue;
    public bool boolValue;
    public string stringValue;
    public UnityEngine.Object objectValue;
}

public enum HfsmBehaviorType
{
    // Primitive
    Wait,
    WaitUntil,
    Log,
    SetFloatVariable,
    SetBoolVariable,
    TriggerTransition,
    PlayAnimation,
    SetActive,

    // Composite
    Sequence,
    Selector,
    Parallel,
    RandomSelector,

    // Decorator
    Repeat,
    Invert,
    TimeLimit,
    UntilSuccess,
    UntilFailure,
    Cooldown,
    If
}
```

### 5.2 HfsmStateNode 扩展

```csharp
[System.Serializable]
public class HfsmStateNode
{
    // ... 现有字段 ...

    // 新增：行为项列表
    public List<HfsmBehaviorItem> behaviorItems;
    public string rootBehaviorId;  // 根行为项 ID
}
```

## 6. 运行时执行

### 6.1 BehaviorExecutor

```csharp
public class BehaviorExecutor
{
    private Dictionary<string, IAction> _actionCache;
    private IAction _rootAction;
    private BehaviorContext _context;

    public void Initialize(List<HfsmBehaviorItem> items, string rootId, MonoBehaviour mono)
    {
        // 1. 根据 items 构建行为树
        // 2. 创建所有 IAction 实例
        // 3. 设置父子关系
        // 4. 缓存根行为
    }

    public BehaviorStatus Tick(float deltaTime, float elapsedTime)
    {
        _context.deltaTime = deltaTime;
        _context.elapsedTime = elapsedTime;

        // 处理协程
        while (_runningCoroutine != null)
        {
            if (!_runningCoroutine.MoveNext())
            {
                _runningCoroutine = null;
                break;
            }
            _context.currentYield = _runningCoroutine.Current;
            return BehaviorStatus.Running;
        }

        return _rootAction.Execute(_context);
    }

    public void ForceEnd()
    {
        _rootAction?.ForceEnd();
    }

    public void Reset()
    {
        _rootAction?.Reset();
    }
}
```

### 6.2 状态机集成

```csharp
// 在 HybridStateMachine 或新创建的 ActionStateMachine 中
public class ActionStateMachine<TStateId, TEvent>
{
    private Dictionary<string, BehaviorExecutor> _behaviorExecutors;

    private void OnStateEnter(HfsmStateNode node)
    {
        if (node.behaviorItems != null && node.behaviorItems.Count > 0)
        {
            var executor = GetOrCreateExecutor(node);
            executor.Reset();
        }
    }

    private void OnStateLogic(HfsmStateNode node)
    {
        if (_behaviorExecutors.TryGetValue(node.Id, out var executor))
        {
            var status = executor.Tick(Time.deltaTime, Time.time);
            // 根据 status 可能触发转换
        }
    }

    private void OnStateExit(HfsmStateNode node)
    {
        if (_behaviorExecutors.TryGetValue(node.Id, out var executor))
        {
            executor.ForceEnd();
        }
    }
}
```

## 7. 编辑器扩展

### 7.1 行为项可视化

```
┌─────────────────────────────────────────┐
│  Idle State                             │
├─────────────────────────────────────────┤
│  [Sequence ▼]                          │
│    ├─ [Play Animation: Idle]             │
│    ├─ [Wait: 1.0s]                      │
│    └─ [Set Bool: IsIdle = true]         │
└─────────────────────────────────────────┘
```

### 7.1 拖拽支持

- 支持将行为项拖入状态
- 支持调整行为项顺序
- 支持缩进创建子行为
- 支持右键菜单删除/复制/粘贴

### 7.2 快捷操作

- 双击行为项展开/折叠
- 拖拽边线创建转换
- 快捷键: Del 删除, Ctrl+C 复制, Ctrl+V 粘贴

## 8. 扩展性设计

### 8.1 自定义行为注册

```csharp
// 用户可以注册自己的行为
public static class HfsmBehaviorRegistry
{
    private static Dictionary<HfsmBehaviorType, Type> _behaviorTypes = new();

    public static void Register<T>(HfsmBehaviorType type) where T : IAction, new()
    {
        _behaviorTypes[type] = typeof(T);
    }

    public static IAction CreateInstance(HfsmBehaviorType type)
    {
        if (_behaviorTypes.TryGetValue(type, out var type))
        {
            return Activator.CreateInstance(type) as IAction;
        }
        return null;
    }
}

// 注册默认行为
HfsmBehaviorRegistry.Register<WaitAction>(HfsmBehaviorType.Wait);
HfsmBehaviorRegistry.Register<LogAction>(HfsmBehaviorType.Log);
// ...
```

### 8.2 自定义行为创建器（用于编辑器）

```csharp
public interface IBehaviorEditorHandler
{
    // 创建编辑器 UI
    void OnDrawInspector(HfsmBehaviorItem item, SerializedProperty parameterProp);

    // 创建行为实例
    IAction CreateRuntimeAction(HfsmBehaviorItem item, BehaviorContext context);

    // 获取行为图标
    Texture2D GetIcon();
}
```

## 9. 与现有系统集成

### 9.1 保持向后兼容

- 现有的 `ActionState` 仍然可用
- 现有的 `CoState` 仍然可用
- 新系统是可选的增强

### 9.2 混合使用

```
┌─────────────────────────────────────┐
│  Patrol State (ActionState)         │
│  - 复用现有协程逻辑                  │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│  Chase State (HfsmStateNode)        │
│  - 使用新的行为系统                  │
│    Sequence:                        │
│      ├─ SetActive: showIndicator    │
│      ├─ MoveTo: target              │
│      └─ WaitUntil: distance < 2     │
└─────────────────────────────────────┘
```

## 10. 实现优先级

1. **Phase 1: 核心框架**
   - IAction 接口
   - BehaviorContext
   - 基础复合行为 (Sequence, Selector)
   - 基础原子行为 (Wait, Log)

2. **Phase 2: 运行时集成**
   - BehaviorExecutor
   - 状态机集成
   - 协程支持

3. **Phase 3: 编辑器扩展**
   - 行为项列表 UI
   - 拖拽支持
   - 基础可视化

4. **Phase 4: 高级功能**
   - 更多行为类型
   - 自定义行为注册
   - 调试可视化

## 11. 文件结构

```
Runtime/HFSM/
  Actions/
    Core/
      IAction.cs
      BehaviorStatus.cs
      BehaviorContext.cs
      IYieldAction.cs
    Primitive/
      WaitAction.cs
      LogAction.cs
      SetVariableAction.cs
      TriggerTransitionAction.cs
      PlayAnimationAction.cs
    Composite/
      SequenceAction.cs
      SelectorAction.cs
      ParallelAction.cs
      RandomSelectorAction.cs
    Decorator/
      RepeatAction.cs
      InvertAction.cs
      TimeLimitAction.cs
      UntilSuccessAction.cs
    Runtime/
      BehaviorExecutor.cs
      BehaviorTreeBuilder.cs

Editor/HFSM/
  BehaviorEditor/
    Action/
      HfsmBehaviorItem.cs
      HfsmBehaviorParameter.cs
      HfsmBehaviorType.cs
    Inspector/
      HfsmBehaviorInspector.cs
      HfsmBehaviorItemDrawer.cs
```

---

## 附录 A: 行为树模式示例

### A1: 巡逻行为

```
Sequence:
  ├─ MoveTo: waypoint1
  ├─ Wait: 2s
  ├─ MoveTo: waypoint2
  ├─ Wait: 2s
  └─ (Loop)
```

### A2: 追击行为

```
Selector:
  ├─ Sequence (If health < 30%):
  │   ├─ RunTo: safety
  │   └─ Wait: 5s
  └─ Sequence:
      ├─ MoveTo: target
      └─ WaitUntil: distance < 2
```

### A3: 攻击模式

```
Parallel:
  ├─ Sequence:
  │   ├─ PlayAnimation: attack
  │   └─ Wait: 0.5s
  └─ Sequence:
      ├─ ApplyDamage: target
      └─ Cooldown: 1s
```
