# 技能运行时聚合生命周期设计

## 1. 背景

在当前 `moba.runtime` 的技能系统中，技能本身不是一次性的“函数调用”，而是一次具有明确生命周期的业务聚合。

一次技能释放通常会经历：

1. 技能管线启动和推进
2. 效果派发
3. 由效果衍生出子对象，例如 Buff、子弹、召唤物、持续伤害实例等
4. 所有衍生对象全部失效后，才算这次技能真正结束

因此，这里需要的不是单纯的 trace 树，也不是一个只跟着管线走的临时上下文，而是一个可被子对象持有、可保活、可释放的“技能运行时聚合”。

本次接入已经把 [`SkillPipelineRunner`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Pipeline/SkillPipelineRunner.cs:1) 和 Buff 生命周期接到了这个聚合上。

---

## 2. 设计目标

### 2.1 目标

- 把一次技能释放抽象成一个独立的运行时聚合
- 允许技能管线结束后，运行时因为子对象仍然存活而继续存在
- 让 Buff 等衍生对象显式 retain / release 这个运行时
- 提供强类型 handle，避免只靠字符串 KV 传递上下文
- 保留 trace 的溯源价值，但不把 trace 当成生命周期所有权容器
- 让后续子弹、召唤物、连锁效果也能沿用同一套生命周期语义

### 2.2 非目标

- 不把 trace 树改造成生命周期管理器
- 不强制把所有 Buff 拆成独立实体
- 不追求“教科书式纯 ECS”，而是保持可维护性和工程可用性

---

## 3. 核心判断

### 3.1 为什么不能只用 trace

trace 的职责是：

- 记录来源
- 记录传播路径
- 支持回溯、审计、调试、反伤链追踪

trace 不适合承担：

- 聚合对象的引用计数
- 子对象保活
- 生命周期 finalization
- 运行时句柄失效保护

所以 trace 和 runtime 是两层东西：

- trace 负责“从哪来、怎么传下去”
- runtime 负责“这一次技能释放什么时候真正结束”

### 3.2 为什么需要 skill runtime

因为技能的业务语义不是“管线结束即结束”。

例如：

- 技能管线已经完成，但生成的 Buff 仍在持续
- 管线结束后，子弹仍在飞行
- 一个触发型技能已经播完主流程，但它派生的持续效果还活着

这种场景下，真正应该结束的是管线阶段，不是整个技能聚合。

---

## 4. 运行时对象模型

### 4.1 `MobaSkillCastRuntime`

[`MobaSkillCastRuntime`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Runtime/MobaSkillCastRuntime.cs:1) 是一次技能释放的聚合对象。

它承载：

- 运行时身份
- 阶段状态
- 结束原因
- trace 根上下文
- 子对象引用集合
- 待结束计数

### 4.2 Handle 设计

运行时不直接对外暴露裸对象做跨模块持有，而是使用 handle：

- `RuntimeId`
- `Generation`
- `RootContextId`

关键点是 `Generation`，用于防止老引用误命中新一轮复用的 runtime id。

相关结构在 [`MobaSkillCastRuntime.cs`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Runtime/MobaSkillCastRuntime.cs:1) 中定义。

### 4.3 子对象引用

子对象通过 [`MobaSkillRuntimeChildRef`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Runtime/MobaSkillCastRuntime.cs:1) 标识。

当前 child 关注的是：

- `Kind`
- `ChildId`
- `ContextId`
- `ConfigId`

对于 Buff 来说，child 语义是 `MobaSkillRuntimeChildKind.Buff`。对于 Projectile 来说，child 语义是 `MobaSkillRuntimeChildKind.Projectile`，并且 `ChildId` 必须使用实际 `ProjectileId`，`ContextId` 保存投射物来源 execution context。这样连发子弹可以共享来源上下文，但仍然作为多个独立子对象 retain/release。

### 4.4 retain token

当子对象成功 retain 运行时时，会拿到一个 [`MobaSkillRuntimeRetainHandle`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Runtime/MobaSkillCastRuntime.cs:1)。

这个 token 的职责是：

- 让 release 能精确定位到一次 retain
- 避免只靠 runtimeId 和 childRef 的宽松释放
- 便于在 Buff 销毁、替换、失败清理时做兜底释放

---

## 5. 生命周期语义

### 5.1 阶段语义

技能运行时分成两个层面：

- `PipelineEnded`：技能管线是否结束
- `IsEnded`：整个运行时聚合是否真正结束

这两个状态不能混为一谈。

### 5.2 结束规则

在 [`MobaSkillCastRuntimeService`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Runtime/MobaSkillCastRuntimeService.cs:26) 中：

- `MarkPipelineEnded(...)` 只标记管线结束
- `Cancel(...)` 也只结束管线语义，不会强制立刻销毁整个聚合
- `ForceTerminate(...)` 才是硬清理语义

真正 finalization 的条件是：

- pipeline 已结束
- `PendingChildren == 0`
- 当前没有被强制终止前置条件打断

### 5.3 这样做的原因

这是为了支撑“主流程结束，但衍生对象继续活”的常见玩法模型。

这类模型如果把管线结束和 runtime 销毁绑定死，会导致：

- Buff 找不到父运行时
- trace 无法继续挂接
- 反伤、延迟结算、持续效果等逻辑无法稳妥表达

---

## 6. Buff 接入方式

### 6.1 Buff 上下文携带 runtime

Buff 入口上下文 [`BuffOriginContext`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Buffs/BuffRuntimeContexts.cs:1) 已加入 `MobaSkillCastRuntimeHandle`。

这意味着 Buff 不是只知道“谁施加的”，还知道“属于哪一次技能运行时”。

### 6.2 Buff runtime 持有 retain token

[`BuffRuntime`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Domain/Components/BuffComponent.cs:1) 增加了：

- `SkillRuntimeHandle`
- `SkillRuntimeRetainHandle`

这让 Buff 自己成为 runtime 的显式持有者。

### 6.3 申请 retain 的时机

在 [`BuffLifecycleExecutor`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Buffs/BuffLifecycleExecutor.cs:60) 中：

- Buff 新建时尝试 retain
- Buff replace / remove / fail cleanup 时 release
- 释放发生在 remove 事件发布之后，保证 on-remove 效果还能读取到 runtime 信息

### 6.4 Buff 释放规则

Buff 结束时会：

1. 停止持续逻辑
2. 清理 continuous 绑定
3. 结束正式 Execution Context；Trace Adapter 可选跟随
4. 发布 remove 事件
5. release skill runtime
6. 清空 runtime 绑定
7. 从 active list 中移除

这样做的顺序是为了让“移除事件”仍然能看到完整来源信息。

---

## 7. Projectile 接入方式

### 7.1 投射物来源缓存

投射物基础运动和碰撞逻辑属于通用 projectile 模块，MOBA 业务来源通过 [`ProjectileSourceContext`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Projectile/ProjectileSourceContext.cs:1) 保存在 MOBA sidecar 中。

[`MobaProjectileLinkService`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Projectile/MobaProjectileLinkService.cs:1) 当前负责：

- `ProjectileId -> ActorId` 映射
- `ProjectileId -> ProjectileSourceContext` 来源缓存
- `ProjectileId -> MobaSkillRuntimeRetainHandle` 保活 token
- `LauncherActorId -> ProjectileSourceContext` 连发发射器临时来源缓存

### 7.2 retain / release 时机

[`MobaProjectileService`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Projectile/MobaProjectileService.cs:41) 在立即发射或调度发射时创建投射物来源，并在实际 `ProjectileId` 可用后 retain 技能运行时。

[`MobaProjectileSpawnSyncHandler`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Projectile/MobaProjectileSpawnSyncHandler.cs:209) 会把 launcher 缓存的来源绑定到实际 projectile，并对 runtime 申请 child retain。

[`MobaProjectileExitSyncHandler`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Projectile/MobaProjectileExitSyncHandler.cs:16) 在投射物退出时：

1. 结束 `ProjectileLaunch` 正式 Execution Context。
2. release skill runtime child retain。
3. 注销 actor 和 link sidecar。

### 7.3 命中后继续传递

[`ProjectileHitArgs`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Projectile/ProjectileHitArgs.cs:7) 已实现 actor、origin、trace、skill runtime、data provider。命中触发后，后续 Trigger Plan 可以像处理 Buff tick 或 DamageResult 一样从 payload 中读取统一来源。

---

## 8. 技能管线接入方式

### 7.1 管线结束只结束管线

[`SkillPipelineRunner`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Pipeline/SkillPipelineRunner.cs:1) 现在在结束技能流程时，会把 runtime 标记为 pipeline ended，而不是直接强杀。

这保证了：

- 业务主流程结束
- 但 Buff、子弹、召唤物还能继续持有运行时

### 7.2 触发上下文传递

效果执行链路里已经把 runtime handle 通过强类型上下文传递下去：

- [`IMobaTriggerSkillRuntimeContext`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Effect/MobaTriggerContext.cs:1)
- [`EffectContextWrapper`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Effect/EffectContextWrapper.cs:1)
- [`BuffTriggerContext`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Buffs/BuffStageEffectExecutor.cs:1)

执行环境本身不再直接暴露为具体 `executeCtx` 对象，而是由 [`MobaTriggerExecutionSnapshot`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Context/Snapshots/MobaTriggerExecutionSnapshot.cs:1) 提供只读快照。它负责把 kind、source/target actor、source/root/owner context、triggerId、configId、frame 和 runtime handle 这些稳定执行事实交给条件层；stack、elapsed、remaining、duration 这类 buff/periodic/channel 阶段事实则通过 [`MobaTriggerStageSnapshot`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Context/Snapshots/MobaTriggerStageSnapshot.cs:1) 从 payload provider 单独解析。底层 `EffectExecutionContext` 或 Triggering `ExecCtx<TCtx>` 仍只留在具体执行器内部。

同时保留 dictionary fallback，用于兼容已有计划动作路径。

---

## 8. 当前调用链

### 8.1 主链路

1. 技能开始，创建 `MobaSkillCastRuntime`
2. skill runner 推进管线并把 runtime handle 放入触发上下文
3. 效果执行阶段把 handle 继续往下传
4. `AddBuff` 从触发上下文读取 runtime handle
5. Buff apply 时对 runtime retain
6. Buff remove / replace / expire 时 release
7. 最后一个子对象释放后，runtime 才真正结束

### 8.2 结果

这样就把：

- 技能流程
- Buff 生命周期
- trace 溯源
- 子对象保活

统一到了同一个语义闭环里。

---

## 10. 技能运行时黑板

### 9.1 职责定位

[`MobaSkillRuntimeBlackboard`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Runtime/MobaSkillCastRuntime.cs:132) 是一次技能 cast 作用域内的动态状态容器。

它适合保存：

- 本次技能已经伤害过的目标
- 本次技能命中次数
- 本次技能的衰减系数
- 本次传播链路的循环保护标记
- 后续配置化效果需要读写的 cast 级运行时变量

它不适合保存：

- 静态技能配置
- actor 长期状态
- Buff 自身生命周期状态
- trace 关系数据

### 9.2 强类型 key

黑板不推荐用裸字符串作为业务入口，而是通过 [`MobaSkillRuntimeBlackboardKey`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Runtime/MobaSkillCastRuntime.cs:61) 描述字段。

key 包含：

- `Id`
- `Name`
- `ValueKind`
- `Scope`
- `Flags`
- `OwnerModuleId`

这样做的目的是让字段具备稳定 id、值类型、作用域和归属信息，后续可以接配置、回滚、调试面板或同步规则。

### 9.3 当前内置 key

[`MobaSkillRuntimeBlackboardKeys`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Runtime/MobaSkillCastRuntime.cs:132) 当前提供了几个基础 cast 级字段：

- `DamagedTargets`：本次技能已伤害目标集合
- `HitCount`：本次技能命中次数
- `DecayFactor`：本次技能衰减系数
- `LoopGuards`：本次技能传播链路循环保护集合

这些字段对应前面讨论过的典型需求：命中过谁、命中过几次、是否需要递减伤害、是否要避免反伤或传播死循环。

### 9.4 值类型与集合

黑板当前支持两类数据：

- 标量：`Int`、`Long`、`Float`、`Bool`、`String`、`ActorId`、`ContextId`、`Vec3`
- 集合：`ActorIdSet`、`ContextIdSet`

集合不是用 `object` 透传，而是由黑板内部维护 `HashSet`，这样常见的“是否已经命中过目标”和“是否已经处理过上下文”可以直接用强类型接口判断。

### 9.5 访问入口

运行时服务 [`MobaSkillCastRuntimeService`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Runtime/MobaSkillCastRuntimeService.cs:11) 提供了按 handle 访问黑板的接口：

- `TryGetBlackboard(...)`
- `SetBlackboardValue(...)`
- `TryGetBlackboardValue(...)`
- `AddBlackboardInt(...)`
- `AddBlackboardActorId(...)`
- `AddBlackboardContextId(...)`

触发上下文扩展 [`MobaTriggerContextDataExtensions`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Effect/MobaTriggerContext.cs:177) 提供了更贴近玩法语义的入口：

- `TryMarkDamagedTarget(...)`
- `HasDamagedTarget(...)`
- `AddSkillRuntimeHitCount(...)`
- `TryAddLoopGuard(...)`
- `HasLoopGuard(...)`

这让效果、Buff 触发、计划动作等模块可以通过 `IMobaTriggerSkillRuntimeContext` 访问同一份 cast 黑板，而不需要把循环保护或命中记录塞进 `AttackInfo` 这类窄语义 DTO。

### 9.6 与执行预算的边界

技能运行时黑板解决的是玩法语义问题，例如：

- 本次技能是否已经命中过某个目标。
- 某条反伤或连锁链路是否已经处理过。
- 当前衰减系数、命中次数、传播次数应该是多少。

`MobaTriggerExecutionBudget` 解决的是执行安全问题，例如：

- 配置错误导致同一 trigger 在同一 root 下无限递归。
- 多个监听器互相触发，单帧执行次数异常增长。
- Buff tick、Projectile hit、Area tick 等持续对象在同一帧形成爆发式链路。

二者不互相替代。黑板 guard 应该表达“这条玩法规则是否允许继续”，执行预算只作为最后防线，确保即使 guard 漏配或配置错误，执行链路也不会无限展开。trace 仍然只负责记录路径和诊断，不作为高频 loop guard 的主要数据结构。

相关实现：

- [`MobaTriggerExecutionSnapshot`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Context/Snapshots/MobaTriggerExecutionSnapshot.cs:1) 提供 MOBA 层执行环境快照，避免条件层依赖具体 execute context。
- [`IMobaTriggerExecutionSnapshotProvider`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Context/Snapshots/MobaTriggerExecutionSnapshot.cs:1) 让 payload 或 wrapper 暴露执行环境只读视图。
- [`MobaTriggerStageSnapshot`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Context/Snapshots/MobaTriggerStageSnapshot.cs:1) 提供阶段/领域事实快照，避免 stack、elapsed、remaining、duration 污染基础执行快照。
- [`MobaTriggerConditionContext`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Effect/MobaTriggerConditionContext.cs:1) 提供复杂条件只读查询视图，并统一查询 payload、origin、trace、execution snapshot、stage snapshot 和 skill runtime blackboard。
- [`MobaTriggerPayloadResolverRegistry`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Effect/MobaTriggerPayloadResolverRegistry.cs:1) 提供 payload 到条件上下文的可插拔解析入口。
- [`MobaTriggerConditionRegistry`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Effect/MobaTriggerConditionRegistry.cs:1) 提供 MOBA trigger condition 注册、triggerId 绑定和执行入口。
- [`MobaTriggerExecutionBudget`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Effects/MobaTriggerExecutionBudget.cs:1) 提供深度、帧级、root 级执行预算。
- [`MobaEffectExecutionService`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Effects/MobaEffectExecutionService.cs:22) 在创建 execution scope 前执行预算检查，并在执行 Trigger Plan 前执行 MOBA 业务条件。

---

## 11. 为什么不是把一切都塞进 trace

这次设计刻意没有把 runtime 存进 trace 树里当唯一真相，原因很直接：

- trace 是关系记录，不是引用计数器
- trace 更适合表达路径，不适合表达所有权
- 生命周期和溯源的关注点不同

更稳妥的做法是：

- runtime 管生命周期
- trace 管路径
- context 管局部语义

这三者各司其职，后续扩展会更稳。

---

## 12. 目前的实现边界

### 11.1 已实现

- 技能运行时聚合对象
- 强类型 runtime handle
- generation 防陈旧引用
- retain / release token
- skill runner 管线接入
- Buff 创建 / 移除接入
- Projectile 发射 / 命中 / 退出接入
- buff trigger 上下文强类型化
- projectile hit 上下文强类型化
- periodic trigger 上下文强类型化
- effect 触发链路 runtime handle 传播
- 技能运行时黑板基础能力
- cast 级命中目标、命中次数、衰减系数、循环保护 key
- 触发上下文访问技能运行时黑板的扩展方法
- 复杂条件只读查询上下文
- MOBA 触发执行环境快照与 provider
- payload resolver registry
- MOBA trigger condition registry
- 效果执行服务级递归深度和帧级预算保护

### 11.2 仍可继续扩展

后续可以继续接入：

- 区域 runtime
- 召唤物 runtime
- 延迟结算 runtime
- 连锁 / 反伤 / 传播型效果
- 黑板字段注册表与配置表绑定
- 黑板字段回滚 / 快照 / 网络同步策略
- 执行预算阈值配置化
- 条件声明从配置表自动绑定到 `MobaTriggerConditionRegistry`
- Damage、Buff、Projectile、Area、Summon 的专用 payload resolver

这些对象都可以沿用同一个 retain / release 语义。

---

## 13. 需要注意的约束

### 13.1 runtime 不是临时字典

它应该承载“一个技能 cast 的业务聚合状态”，不是简单的 KV 容器。

### 13.2 trace 不是生命周期管理器

不要把“从哪来”与“什么时候结束”混成一件事。

### 13.3 child 释放必须成对

所有 retain 必须在最终结束路径上释放，否则 runtime 会被错误保活。

### 13.4 强类型优先

只在兼容旧链路时保留 dictionary fallback，新代码应优先走强类型上下文。

---

## 14. 设计结论

这次接入后的结构，已经满足大型项目里常见的技能运行时分层：

- 技能释放有独立聚合
- 管线结束和聚合结束分离
- Buff、Projectile 等衍生对象显式持有 runtime
- trace 负责溯源，不负责生命周期
- 强类型上下文逐步替代字符串 KV
- 技能黑板和 MOBA trigger condition 负责玩法 guard，执行预算负责安全兜底

这个方向是可扩展的，也符合后续补接子弹、召唤物、持续结算等模块的需求。

如果后续要继续深化，下一步最自然的是把区域、召唤物、持续结算模块也接入同一套 retain / release 与黑板访问语义，让一次技能 cast 真正成为所有派生对象共享的业务聚合根。
