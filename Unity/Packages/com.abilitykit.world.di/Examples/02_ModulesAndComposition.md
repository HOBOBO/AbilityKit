# 02 - 模块拆分与组合（Composition）

本示例演示：

- 一个 world 类型通常如何组合多个模块
- 模块之间如何表达依赖（概念层面）

## 推荐组合方式（示意）

```csharp
// 中文说明：
// 这里是“组合边界”（composition root）的伪代码示意。
// 真实项目里可能在 WorldFactory / WorldComposer / Installer 中实现。

var modules = new IWorldModule[]
{
    new BaseModule(),
    new TimeModule(),
    new EventBusModule(),
    new UnitModule(),
    new CombatModule(),
    new SkillModule(),
    new ProjectileModule(),
    new SummonModule(),

    // 可选模块：调试/热更/埋点等
    // new DebugModule(),
};

var builder = new WorldContainerBuilder();
foreach (var module in modules)
{
    builder.AddModule(module);
}
using var container = builder.Build();
using var scope = container.CreateScope();
```

## 模块拆分的经验规则

- **按子域拆**：让每个模块内部依赖闭包更清晰
- **按可开关拆**：可选能力（debug/hotreload/telemetry）单独模块，避免污染主链路
- **按复用拆**：package 对外提供模块，world 类型只做组合

## 多模块重复注册同一服务：容器的行为与推荐用法

`WorldContainerBuilder` 的行为是“最后写入 wins / 或者 first wins”，取决于你用哪种 API：

- `Register<T>(...)`
  - 同一 `serviceType` 被重复 `Register` 时，**后注册会覆盖先注册**。
- `TryRegister<T>(...)`
  - 同一 `serviceType` 已注册时，后续 `TryRegister` 会被**忽略**。

推荐模式：

- **提供默认实现（可被覆盖）**
  - Base/Infra 模块用 `TryRegister` 注册默认实现
  - 业务模块用 `Register` 显式覆盖（让覆盖成为一种“有意为之”的动作）

- **禁止重复注册（强一致治理）**
  - 约定每个 `serviceType` 只能由一个模块负责
  - 遇到需要替换实现时，改为组合时选择不同模块，而不是重复注册

- **同一个实现暴露多个接口**
  - 先注册实现类型，再用 `RegisterAlias(typeof(TService), typeof(TImpl), lifetime)` 将接口指向同一实例。需要共享实例时，别名和实现使用相同的缓存生命周期（`Singleton` 或 `Scoped`）。
