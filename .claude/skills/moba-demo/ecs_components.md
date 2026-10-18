# Entitas ECS 组件（2026-10-12 生成器接管后）

位置：`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Common/Shared/Entitas/Generated/`

## 生成链（重要：手改生成物的时代已结束）

`Generated/` 由 `tools/AbilityKit.Entitas.CodeGen` 生成并校验（Jenny 已死，2026-10-12 接管）：

```powershell
./tools/generate-entitas.ps1 -Project moba                 # 基线校验（gate: entitas-codegen）
./tools/generate-entitas.ps1 -Project moba -UpdatePin      # 新增组件后锁定索引
./tools/generate-entitas.ps1 -Project moba -Validate       # 组件矩阵 + 序列化策略 + 覆盖矩阵
./tools/generate-entitas.ps1 -Project moba -DiffGeneratedDir  # 与包内目录差异（应为 0）
```

组件定义源：`Runtime/Domain/Components/`（手写 `[Actor] IComponent` 类，只允许公共字段；属性/方法/非 public 字段会被工具报错拒绝）。

## Context 全景（2 个：`Actor` + `Game`）

2026-10-14 从 4 个收敛为 2 个：删掉 `Input`/`Service` 两个**空壳**（0 组件 0 消费者——输入走 `MobaSkillInputCommandHandler`→`SkillCastCoordinator`，服务走 `[WorldService]`→DI，与 ECS 职责重复）。`Feature.cs` 是编辑器可视化调试基类，**不是** context。

- `Actor`（43 组件，全部业务实体）
- `Game`（**0 组件**，战斗内全局的显式容器，等真有全局实体事实时再填）

context 列表在 `tools/entitas-codegen.moba.json` 的 `contexts` 字段；删 context 要同步删 pin 里对应条目再重跑生成器。

### 往`Game` 加组件的准则

**服务事实进 DI，实体事实进 `[Game]`。**

| | 归属 | 例子 |
|---|---|---|
| ✅ 进 `[Game]` | 由 Actor 组事件驱动、可 Entitas 可视化的**全局派生量** | 全局 buff 计数、某 tag 是否存在 |
| ❌ 留 DI | 权威写入 + 只读读取 + **无组语义**的全局事实 | 帧号、战斗阶段、random seed |

留在 DI 的理由：那些量由既有手写回滚 provider 兜底（已验证 71/71）；搬进实体会新增一套回滚清单却不降低 provider 规模。

### `[Game]` 单例机制（Entitas 无"每 context 恰好一个实体"的一等概念）

`Runtime/Infrastructure/Entitas/MobaGameSingleton.cs` 用「标记组件 + `GetSingleEntity()`」补上这个能力，**无业务语义**——标记哪个组件由业务决定：

```csharp
MobaGameSingleton.Write(ctx, GameComponentsLookup.Xxx, () => new XxxComponent { ... }, createIfMissing: true);
var comp = MobaGameSingleton.Read<XxxComponent>(ctx, GameComponentsLookup.Xxx);   // 单例不存在返回 null
```

实测结论（Entitas 1.13，勿凭印象改）：

| 事实 | 影响 |
|---|---|
| `IGroup.GetSingleEntity()`：空组返 `null`、**多于一个抛 `GroupSingleEntityException`** | 自带"恰好一个"fail-fast，最省事也最安全 |
| `EntityIndex<TKey>` **只有 `GetEntities(key)`→HashSet，没有单值 `GetEntity(key)`** | 且**键冲突是静默的**，框架不强制唯一 → 不用它做单例 |
| `IContext.Reset()` 会把 `creationIndex` 重置回 0 | 单例键必须用**固定组件索引**，不能拿实体 id，否则 Reset 后撞号 |
| 标记索引越界（如 Game 0 组件时用 0） | 裸 `IndexOutOfRangeException`；已加前置校验转成指名 `ArgumentOutOfRangeException` |

```powershell
# 新增 [Game] 组件的完整流程（Game 当前 0 组件，必须先有组件才能用单例机制）
# 1) 在 Runtime/Domain/Components 定义 [Game] IComponent（手写，只允许公共字段）
# 2) ./tools/generate-entitas.ps1 -Project moba -UpdatePin   # fail-closed：不登记就报错
# 3) 核对 pin 里 coverage 分类（auto 模式默认全量序列化，与设计意图不符时要手改成 projection 等）
# 4) 重跑生成 + 固化基线（ps1 不暴露 --out，需直接调 CLI）：
dotnet run --project tools/AbilityKit.Entitas.CodeGen/AbilityKit.Entitas.CodeGen.csproj -- --project moba --write --out Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Common/Shared/Entitas/Generated --force-generated-dir --update-baseline
```

> 组件 API 生成在 `Game/Components/*.cs` 的 `partial GameEntity` 里（`GameEntity.cs` 本体是空壳，只有类声明）——找 API 别只看 `GameEntity.cs`。

## Actor 43 组件按回滚覆盖分类（pin coverage 注册表）

`--validate` 实时输出；**serialization.mode=auto：默认全量+豁免制**（新平值组件自动入集，blob 不豁免报错）：

- **serialized（10，生成序列化覆盖）**：Lifetime / ProjectileLauncher / SummonMeta / ProjectileEffectSnapshot / SkillLoadout / SkillCastTimelineRuntime / SkillCastRunningTag / SkillCastCancelRequest / SkillCastDestroyRequest / ActorDespawnRequest
- **provider（9，专属回滚 provider）**：Transform / MoveInput / Motion（→TransformProvider，v3 含 Velocity/Time）；ResourceContainer（→Resource）；AttributeGroup（→Hp）；Buffs（→BuffTimer）；ActorBrain（→Brain）；ActorStateMachine / CharacterHfsm（→各自 provider）
- **projection（13，每帧重投影）**：SkillCast* 10 个 + EffectListeners / OngoingTriggerPlans / PassiveSkillTriggerListeners
- **immutable（11，出生赋值）**：ActorId / Team / EntityMainType / UnitSubType / ModelId / OwnerLink / OwnerPlayerId / FlyingProjectileTag / Collider / CollisionLayer / CollisionId

## 生成物构成（55 文件）

- Contexts.cs + Feature.cs；2 context × 5 件套（Attribute/Context/Entity/Matcher/ComponentsLookup）
- `Actor/Components/` 42 个组件 API 文件 + **1 个 custom 手写**（`ActorStateMachineComponent.cs`——Remove 带 Dispose，唯一豁免）
- `Actor/ActorComponentSerialization.cs`（生成序列化，`MobaEntitasComponentRollbackProvider` 委托它）
- `Game/` 目前只有空壳五件套（0 组件，`Components/` 目录不生成）

## 已迁出/删除（别再找）

- `CharacterHfsm` 的 Dispose 语义 → `Runtime/Domain/Components/EntityApiExtensions.cs` 的 `RemoveAndDisposeCharacterHfsm()` 扩展（3 调用点：despawn 清理 / HfsmSystem / 回滚 Provider）
- `ShieldContainerComponent`/`SharedShieldPoolsComponent` 定义已删（2026-10-12，零消费者死代码；护盾领域模型保留给 MobaShieldService，ECS 化待重新规划）

## Domain 层 vs 生成层

Domain/Components 是**唯一权威定义源**（含嵌套数据类 ActiveSkillRuntime/PassiveSkillRuntime 等，供序列化展开）；Generated/ 是投影。改组件 → `-UpdatePin` → 重新生成 → gate 绿。

## System 基类

继承 `WorldSystemBase`（OnInit/OnExecute/OnCleanup/OnTearDown）或 `ReactiveWorldSystemBase<T>`；`[WorldSystem(order: MobaSystemOrder.*, Phase=...)]` 声明顺序（顺序常量带依赖校验）。
