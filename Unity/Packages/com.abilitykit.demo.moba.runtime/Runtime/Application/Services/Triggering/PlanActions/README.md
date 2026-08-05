# Moba Plan Action 扩展指南

这份文档描述 MOBA runtime 示例里触发动作的推荐扩展方式。Plan action 属于逻辑世界的触发器服务能力，代码放在 `Application/Services/Triggering/PlanActions`，Bootstrap 只负责在世界启动时调用安装流程。

## 扩展入口

触发动作模块通过 `PlanActionModuleRegistry` 自动注册：

- 模块类必须实现 `IPlanActionModule`。
- 模块类必须带 `[PlanActionModule(order: n)]`。
- 模块类必须有无参构造函数。
- Source Generator 会生成当前 runtime 程序集的模块清单，按 `order` 和类型名稳定排序。
- 反射扫描仅作为兼容 fallback；禁用 fallback 后，生成清单缺失会直接报错。

优先使用 `NamedArgsPlanActionModuleBase<TArgs, IWorldResolver, TModule>`。它让动作保持三段式结构：

- `Args`：动作运行时强类型参数。
- `Schema`：把配置里的命名参数解析为 `Args`。
- `Module`：注册动作并执行逻辑。

## 初始化边界

`PlanTriggeringStage` 只调用 `MobaEffectExecutionService.InitializePlanActions()` 和触发计划数据库注册，不承载动作、条件、上下文解析等业务逻辑。

触发事件 payload 类型通过 `MobaTriggerEventAttribute` 声明，`MobaEventSubscriptionRegistry.DiscoverAndRegister()` 在 Bootstrap 阶段自动发现。新增事件类型优先添加 attribute 映射，不要把 `RegisterExact` / `RegisterPrefix` 调用散落到 Bootstrap。

触发计划通过 `TriggerPlanScope` 区分注册入口：`Global` 由 `PlanTriggeringStage` 统一注册，`OwnerBound` 由 `MobaTriggerPlanSubscriptionService` 按 ownerKey 动态订阅。源格式 JSON 可写 `scope: "owner"` 或 `scope: "owner_bound"`；不写时默认为 `Global` 以兼容旧配置。

`MobaTriggerPlanSubscriptionService` 会在构造时缓存 triggerId 到 payload type 的映射，运行时只在真实订阅时做一次泛型注册转换；如果没有映射，则回退到 object channel。

`PlanActionModuleRegistry` 默认消费 `MobaGeneratedPlanActionManifest`，避免启动时扫描当前 runtime 程序集。生成器和对应的契约 Analyzer 由 `com.abilitykit.demo.moba.codegen` 统一维护；旧 `AutoPlanAction` 路线不参与该正式流程，因为它和当前的 `IActionSchema<TArgs, IWorldResolver>`、`NamedArgsPlanActionModuleBase<TArgs, IWorldResolver, TModule>` 模型并不完全一致。

## 新增动作步骤

1. 在 `TriggeringConstants.Actions` 中增加动作名常量，并暴露对应 `ActionId`。
2. 在 `Application/Services/Triggering/PlanActions/Args` 下新增只读参数结构，例如 `KnockArgs`。
3. 在 `Application/Services/Triggering/PlanActions/Schemas` 下新增 `IActionSchema<TArgs, IWorldResolver>` 实现。
4. 判断动作是否只需要核心执行事实。如果只需要 caster、target、aim、execution context、trace scope，直接使用 `MobaPlanActionInputResolver`。
5. 如果动作需要严格上下文，可调用 `MobaPlanActionInputResolver.TryResolve`，失败时应跳过或记录错误；常规 `Resolve` 目前只作为过渡入口保留 fallback warning。
6. 如果动作需要领域派生数据，新增领域 input/resolver，例如 motion 使用 `MobaMovementActionInput` 和 `MobaMovementActionInputResolver`。
7. 新增 `PlanActionModule` 类，继承 `NamedArgsPlanActionModuleBase<TArgs, IWorldResolver, TModule>`。
8. 给模块类添加 `[PlanActionModule(order: n)]`，确保与同类动作的执行注册顺序一致。
9. 在触发计划配置里使用新的动作名和参数名。

## 模块模板

```csharp
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct MyActionArgs
    {
        public readonly int TargetId;

        public MyActionArgs(int targetId)
        {
            TargetId = targetId;
        }

        public static MyActionArgs Default => new MyActionArgs(0);
    }

    public sealed class MyActionSchema : IActionSchema<MyActionArgs, IWorldResolver>
    {
        public static readonly MyActionSchema Instance = new MyActionSchema();

        public ActionId ActionId => TriggeringConstants.GetActionId("my_action");
        public System.Type ArgsType => typeof(MyActionArgs);

        public MyActionArgs ParseArgs(
            System.Collections.Generic.Dictionary<string, ActionArgValue> namedArgs,
            ExecCtx<IWorldResolver> ctx)
        {
            var targetId = 0;
            if (namedArgs != null && namedArgs.TryGetValue("target_id", out var value))
            {
                var raw = value.Ref.Kind == ENumericValueRefKind.Const
                    ? value.Ref.ConstValue
                    : ActionSchemaRegistry.ResolveNumericRef(value.Ref, ctx);
                targetId = (int)System.Math.Round(raw);
            }

            return new MyActionArgs(targetId);
        }

        public bool TryValidateArgs(
            System.ReadOnlySpan<System.Collections.Generic.KeyValuePair<string, ActionArgValue>> args,
            out string error)
        {
            error = null;
            return true;
        }
    }

    [PlanActionModule(order: 100)]
    public sealed class MyActionModule : NamedArgsPlanActionModuleBase<MyActionArgs, IWorldResolver, MyActionModule>
    {
        protected override ActionId ActionId => TriggeringConstants.GetActionId("my_action");
        protected override IActionSchema<MyActionArgs, IWorldResolver> Schema => MyActionSchema.Instance;

        protected override void Execute(object triggerArgs, MyActionArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (ctx.Context == null) return;

            // Resolve logic-world services here and apply deterministic logic changes.
        }
    }
}
```

## Action 输入边界

Plan action 不负责创建正式 execution context。正式路径应来自 `MobaEffectExecutionService` 当前执行 session，或由 payload 显式提供 `IMobaCombatExecutionContextProvider`。`MobaPlanActionExecutionContextResolver.Resolve` 中的 fallback create 只作为过渡兜底，并会记录 warning；新代码如果需要判断上下文是否完整，应优先使用 `TryResolve`。

`MobaPlanActionInput` 是核心执行事实视图，不是所有 action 参数的统一容器。它只应该保留跨领域稳定复用的信息：

- execution context。
- 当前 effect trace scope。
- caster/source actor。
- target actor。
- 通用 aim position / aim direction。

新增需求按下面规则放置：

- 配置参数放进动作自己的 `Args`，由 `Schema` 解析，例如伤害系数、buff id、位移距离。
- 跨 action 的执行事实才允许进入 `MobaPlanActionInput`。
- 领域派生能力使用专用 input/resolver，例如 motion 已使用 `MobaMovementActionInput`。
- payload 独有信息通过 typed provider/interface 暴露，再由对应领域 resolver 读取。
- 运行时服务从 `ExecCtx<IWorldResolver>.Context` 解析，不挂到核心 input 上。

后续如果 targeting 数据继续增长，例如命中列表、区域形状、目标集合、投射物生成点，应新增 `MobaTargetingActionInput` 一类的领域输入，而不是继续扩展 `MobaPlanActionInput` 字段。

## 最佳实践

- 动作执行只写逻辑世界状态，不直接访问表现层对象。
- 外部资源、随机数、时间等必须通过世界服务注入，避免非确定性来源散落在动作内部。
- 参数解析集中在 `Schema`，执行模块只接收强类型 `Args`。
- 需要运行时服务时从 `ExecCtx<IWorldResolver>.Context` 解析接口，避免依赖具体实现。
- action module 不直接创建 `MobaCombatExecutionContext`；正式上下文由 effect execution session 或 typed payload provider 提供。
- action module 不直接调用 legacy fallback resolver；优先消费 `MobaPlanActionInputResolver` 或领域专用 resolver。
- 日志类动作可以保留为调试工具，但业务动作不要把日志作为主要控制流。
- 动作 `order` 只表达注册顺序，不应该依赖它处理同一帧的业务先后关系；业务先后应由触发计划或系统阶段表达。
