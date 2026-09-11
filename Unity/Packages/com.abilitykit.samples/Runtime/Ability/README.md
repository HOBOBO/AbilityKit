# AbilityKit.Samples.Logic.Ability

AbilityKit 技能系统核心设计示例库。

## 概述

本模块从 `com.abilitykit.demo.moba.runtime` 中提炼出高质量、精简且可复用的技能系统设计模式。设计遵循以下原则：

- **精简**：每个模块只做一件事
- **可组合**：通过接口和委托实现灵活组合
- **可测试**：所有组件都配有单元测试
- **工业级**：适合各种游戏项目的技能系统

## 目录结构

```
AbilityKit.Samples.Logic.Ability/
├── Core/                           # 核心抽象层
│   ├── Pipeline/                   # 管线框架
│   │   ├── IPipeline.cs          # 管线接口
│   │   ├── IPipelinePhase.cs     # Phase 接口
│   │   ├── IPipelineContext.cs   # 上下文接口
│   │   ├── IPipelineLibrary.cs   # 管线库接口
│   │   ├── PipelinePhaseBase.cs  # Phase 基类
│   │   └── PipelineRunner.cs     # 管线执行器
│   ├── Bootstrap/                 # 启动框架
│   │   ├── IBootstrapStage.cs    # Stage 接口
│   │   ├── IWorldModule.cs      # 模块接口
│   │   ├── BootstrapPipeline.cs   # 引导管线
│   │   └── WorldBlueprint.cs     # 世界蓝图
│   ├── Component/                 # 组件模式
│   │   ├── IComponentSystem.cs   # 组件系统接口
│   │   └── ComponentContext.cs   # 组件上下文
│   └── Action/                   # 动作框架
│       ├── IAction.cs            # 动作接口
│       ├── IActionFactory.cs     # 工厂接口
│       ├── ActionRegistry.cs     # 注册表
│       └── ActionContext.cs      # 执行上下文
├── Samples/                      # 示例实现
│   ├── 1.Bootstrap/             # Bootstrap 示例
│   ├── 2.Pipeline/              # Pipeline 示例
│   └── 3.Action/                # Action 示例
└── Tests/                        # 单元测试
    ├── BootstrapTests.cs
    ├── PipelineTests.cs
    └── ActionTests.cs
```

## 核心设计

### 1. Pipeline 管线框架

管线框架用于管理技能/行为的执行流程。

```csharp
public interface IPipelinePhase
{
    string PhaseId { get; }
    int Priority { get; }
    PhaseResult Execute(IPipelineContext context);
}

public enum PhaseResult
{
    Success,  // 继续下一个阶段
    Failure,  // 中断管线
    Skip,     // 跳过此阶段
    Pending   // 等待外部信号
}
```

**TimelinePhaseBase** - 基于时间的阶段性执行，适用于技能演出：

```csharp
public class SkillTimelinePhase : TimelinePhaseBase
{
    protected override int DurationMs => 1000;
    protected override TimelineEvent[] GetTimelineEvents() => new[]
    {
        new TimelineEvent(0, "cast_start"),
        new TimelineEvent(200, "projectile_spawn"),
        new TimelineEvent(800, "impact")
    };

    protected override void OnTimelineEvent(IPipelineContext ctx, TimelineEvent e)
    {
        // 处理时间线事件
    }
}
```

### 2. Bootstrap 启动框架

Bootstrap 框架用于管理游戏世界的初始化流程。

```csharp
public interface IBootstrapStage
{
    string StageId { get; }
    int Order { get; }
    Task ExecuteAsync(WorldBlueprint blueprint);
}

public interface IWorldModule
{
    string ModuleId { get; }
    int Priority { get; }
    void Initialize();
    void Destroy();
}
```

**执行流程**：

1. 创建 `WorldBlueprint`
2. 注册 `IWorldModule`
3. 创建 `BootstrapPipeline`
4. 添加 `IBootstrapStage`
5. 执行管线

### 3. Action 动作框架

动作框架用于定义和执行原子操作。

```csharp
public interface IAction
{
    string ActionId { get; }
    ActionResult Execute(IActionContext context);
}

public interface IActionFactory
{
    string FactoryId { get; }
    IAction Create(string actionType, IReadOnlyDictionary<string, object> args);
}
```

**示例：伤害动作**：

```csharp
public sealed class DamageAction : IAction
{
    public ActionResult Execute(IActionContext context)
    {
        var target = context.Target as ITarget;
        var damage = context.GetArg<int>("damage");
        target.ReceiveDamage(damage, context.GetArg<string>("damage_type"));
        return ActionResult.Succeeded();
    }
}

public class DamageActionFactory : IActionFactory
{
    public IAction Create(string actionType, IReadOnlyDictionary<string, object> args)
        => new DamageAction(args["damage"], args["damage_type"]);
}
```

## 使用示例

### 创建技能管线

```csharp
var pipeline = new DefaultPipeline("skill_fireball");
pipeline.AddPhase(new CheckTargetPhase());      // 检查目标
pipeline.AddPhase(new CalculateDamagePhase());  // 计算伤害
pipeline.AddPhase(new PlayAnimationPhase());    // 播放动画
pipeline.AddPhase(new SpawnProjectilePhase()); // 生成弹道
pipeline.AddPhase(new ApplyDamagePhase());     // 应用伤害

var context = new SkillContext(source, target, args);
var result = pipeline.Execute(context);
```

### 初始化游戏世界

```csharp
var blueprint = new WorldBlueprint();
blueprint.RegisterModule(new CombatModule());
blueprint.RegisterModule(new BuffModule());
blueprint.RegisterModule(new SkillModule());

var pipeline = new BootstrapPipeline();
pipeline.AddStage(new InitCoreServicesStage());
pipeline.AddStage(new InitCombatStage());
pipeline.AddStage(new InitSkillStage());

await pipeline.ExecuteAsync(blueprint);
```

### 执行动作

```csharp
var registry = new ActionRegistry();
registry.Register(new DamageActionFactory());
registry.Register(new BuffActionFactory());

var action = registry.Create("damage", new Dictionary<string, object>
{
    ["damage"] = 100,
    ["damage_type"] = "fire"
});

var context = new ActionContext(executor, source, target);
var result = action.Execute(context);
```

## 运行测试

```bash
cd src/AbilityKit.Samples.Logic
dotnet test
```

## 设计来源

以下设计模式提炼自 `com.abilitykit.demo.moba.runtime`：

| 原设计 | 抽象层 | 说明 |
|--------|--------|------|
| `MobaWorldBootstrapModule.Pipeline` | `BootstrapPipeline` | 分阶段初始化 |
| `SkillTimelinePhase` | `TimelinePhaseBase` | 时间线驱动 |
| `AutoSystemInstaller` | `IWorldModule` | 模块化组织 |
| `*ActionFactory` | `IActionFactory` | 配置驱动执行 |
| `*SpecParser` | `IActionSpec` | 规格解析模式 |

## 扩展指南

### 添加新的 Phase

```csharp
public class MyPhase : PipelinePhaseBase
{
    public override string PhaseId => "my_phase";

    protected override PhaseResult OnExecute(IPipelineContext context)
    {
        // 实现逻辑
        return PhaseResult.Success;
    }
}
```

### 添加新的 Action

```csharp
public class MyAction : IAction
{
    public ActionResult Execute(IActionContext context)
    {
        // 实现逻辑
        return ActionResult.Succeeded();
    }
}

public class MyActionFactory : IActionFactory
{
    public IAction Create(string actionType, IReadOnlyDictionary<string, object> args)
        => new MyAction();
}
```

## 许可证

继承 AbilityKit 项目许可证。
