# AbilityKit Environment

项目无关的**环境 Profile** 机制：以数据形式声明*关注点*、组合*具名场景 Profile*，再绑定到项目自己的世界装配原语。

## 分工：框架给机制，项目给分类

- **框架**（本包）定义*机制*——四种形状（`EnvironmentConcern`、`EnvironmentProfile`、`EnvironmentPrimitive`、
  `ResolvedEnvironmentProfile`）、带解析与校验的注册表（`EnvironmentProfileCatalog`）、
  常用组展开边界（`IEnvironmentGroupExpander`）、以及适配边界（`IEnvironmentProfileBinder<THandle>`）。
- **项目**声明自己的 *taxonomy*——自己的关注点与取值域、自己的具名 Profile、自己的「常用组 → 原语」展开映射——
  通过 `AddConcern` / `AddProfile` / 实现 expander。新增关注点、取值、原语或场景都是数据声明，永远不需要改框架代码。

这与 `com.abilitykit.gameplaytags` 同构：框架提供标签层级与查询机制，项目提供自己的标签目录。

## 三层

| 层 | 谁拥有 | 形态 | 示例 |
| --- | --- | --- | --- |
| 原语（原子） | 框架定形状，项目执行 | `EnvironmentPrimitive`（Spawn/Obstacle/Tag/Modifier，字段为不透明 token） | `spawn jungle_warrior ×3, hp=5000` |
| 常用组（关注点） | 项目声明 | `EnvironmentConcern`（id + 取值域） | `unit-class`、`target-shape`、`geometry`、`state` |
| 场景 Profile | 项目声明 | `EnvironmentProfile`（base + 选择 + 原语） | `jungle-camp = unit-class:jungle + target-shape:group + state:full` |

- **组内互斥**：每个关注点一个取值，由 `Selections` 字典保证。
- **组间叠加**：不同关注点各自贡献一个取值。
- **常用组是压缩快捷方式，原语是未压缩的真相**：`unit-class: jungle` 经 `IEnvironmentGroupExpander` 展开成原语，
  而 `Primitives` 里的显式原语是直接写死的具体构造（某只怪 HP 特别调、某堵墙的尺寸）。两者都是数据，binder 只认原语。
- Profile 可继承 `BaseProfileId`，派生取值覆盖基础。

## 最小用法

```csharp
var catalog = new EnvironmentProfileCatalog()
    .AddConcern(new EnvironmentConcern("unit-class", new[] { "hero", "minion", "jungle" }, "单位类别"))
    .AddConcern(new EnvironmentConcern("geometry",   new[] { "open", "walled" }, "场景几何"))
    .AddProfile(new EnvironmentProfile
    {
        Id = "jungle-camp",
        Selections = new Dictionary<string, string> { ["unit-class"] = "jungle", ["geometry"] = "walled" },
        Primitives = new EnvironmentPrimitive[]
        {
            // 显式覆盖：单独把某只怪的血调高
            new SpawnPrimitive { EntityKind = "jungle_elite", Alias = "elite", Components = new Dictionary<string,string>{ ["hp"]="5000" } },
        },
    });

catalog.ThrowIfInvalid();
catalog.TryResolve("jungle-camp", jungleExpander, out var resolved);
// resolved.Primitives = 显式原语 + 展开后的常用组原语（扁平、有序）
var result = binder.Bind(in resolved);   // 项目把原语翻译成实体，返回别名 → handle
result.TryGetHandle("j1", out var j1);   // 拿到 caster/target 再施放技能、观测
```

## 预览/测试接缝

本包只做到「**resolve + bind → handles**」为止：拿到 `ResolvedEnvironmentProfile`（扁平原语），`binder.Bind` 返回
`EnvironmentBindResult<THandle>`（别名 → handle，`THandle` 是项目实体类型，如实体 id / 引用 / 接口）。之后「施放技能、推进时间线、采 trace 观测」是**消费方**的组合，
复用既有 DSL 词汇（`TestScenario` 的 timeline/commands + `MobaTraceRegistry` 的 trace），不在这里再造一套 session 抽象。
编辑器预览宿主 = `环境 profile → binder(返回 handle) → 对 target 施放技能 → 观测`，是下一个项目级切片。

## 说明

- 纯 C#（`noEngineReferences`）、无 Unity、无实体系统依赖——可在 .NET 直接测试。
- 原语字段（`EntityKind`、`Components` 键、`Tag`、`Operation`）都是不透明 token，框架不认识 MOBA 的实体系统；
  它只透传，由 binder 解释执行。这正是与 DSL 里 `TestActor`/`TestObstacle` 同一套「载体中立」词汇。
- 本包**刻意不内置**任何关注点 taxonomy 或展开映射：它只是机制，不是框架默认。`demo.moba` 与其他项目各自提供。
