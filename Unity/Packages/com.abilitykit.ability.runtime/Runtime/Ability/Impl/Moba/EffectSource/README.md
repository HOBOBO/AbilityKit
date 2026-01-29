# Effect Source (溯源树) 设计说明

## 目标
`EffectSourceRegistry` 用于把一次技能/战斗行为在运行时产生的派生对象（Effect/Buff/Projectile/Trigger 等）串成一棵可追溯的树，并为：

- 运行时溯源（谁触发了谁）
- 生命周期一致性（可回收时整棵树一起回收）
- 线上排查（异常 root、疑似漏 End、卡死链路）

提供基础设施。

## 核心概念

- **Context**：树上的一个节点，唯一标识 `ContextId`。
- **Root**：一次顶层行为的根节点（例如一次 `SkillCast`），唯一标识 `RootId`。
- **Parent**：派生来源节点 `ParentId`。

每个节点都有一个不可变快照字段集合（`EffectSourceSnapshot` 可读出）：

- `Kind`：节点类型（`SkillCast / Effect / Buff / Projectile / ...`）
- `ConfigId`：配置或实例标识（推荐填“可定位”的 id，如 effect instanceId / buffId / projectile templateId 等）
- `SourceActorId / TargetActorId`
- `CreatedFrame`
- `EndedFrame / EndReason`

> 注意：溯源树不是用来承载业务逻辑的执行流，它是“审计/追踪/回收”的数据结构。

## 生命周期语义（非常重要）

### End
- `End(contextId, frame, reason)`：**表示该节点的生命周期结束**（EndedFrame 被记录）。
- `End` **不会**立刻删除节点，避免链路断裂（仍可追溯）。

### Purge
- `Purge(frame, keepEndedFrames)`：只会在 root 满足“可回收条件”时删除整棵树。
- Root 可回收条件：
  - `ActiveCount == 0` 且 `ExternalRefCount == 0`
  - 且满足 `frame - LastTouchedFrame >= keepEndedFrames`

### 计数含义
- `ActiveCount`：当前 root 下“未 End 的 context 数”（由 Create 增，End 减）。
- `ExternalRefCount`：业务侧显式 retain/release 的引用计数（用于 root 被外部持有时阻止 purge）。

## Origin（来源对象）存储策略
为了避免隐藏引用导致 GC 压力或泄漏：

- 默认 `StoreOriginObjects = false`
- `originSource/originTarget` 默认仅保存轻量值：`int/long/string`
- 传入其他引用类型时会退化为 actorId（或 fallback）

如确实需要保存对象引用做调试，可将 `StoreOriginObjects = true`（需谨慎）。

## 技能运行时流程中的数据存放在哪里？
项目里“运行时数据”通常分 5 层，各有职责：

### 1) Pipeline 级：`SkillPipelineContext.SharedData`
- 位置：`SkillPipelineContext.SharedData : Dictionary<string, object>`
- 生命周期：一次技能 pipeline 执行期间
- 用途：在各 phase 之间共享数据，例如：
  - `MobaSkillPipelineSharedKeys.SourceContextId`（溯源 root id）
  - `CastSequence / FailReason`
  - AimPos/AimDir、caster/target 等

### 2) 触发/事件级：Trigger Args（`Dictionary<string, object>`）
- 位置：`TriggerEventEffectComponent` 会把 `_args` 合并到触发事件参数里
- 生命周期：一次事件 publish 调用的瞬时数据
- 用途：把 pipeline 或 effect 的关键字段塞到事件侧让 Triggering 系统读取

### 3) Effect 实例级：`EffectInstance.State`
- 位置：`EffectInstance.State : Dictionary<object, object>`
- 生命周期：单个 effect 实例的整个持续期
- 用途：存放 effect 私有运行时状态（计数器、随机种子、临时缓存等）

### 4) Unit/实体级：`IUnitFacade`
- `Tags / Attributes / Effects`
- 生命周期：单位（Actor）存活期间
- 用途：角色状态（属性、标签、持续效果集合）

### 5) Root Scope 级（本模块提供）：Root Blackboard（int->int）
- 位置：`EffectSourceRegistry` 内部 `_rootBlackboards`
- API：
  - `SetRootInt(rootId, IntKey key, int value)`
  - `TryGetRootInt(rootId, IntKey key, out int value)`
- 生命周期：与 root 完全一致；root purge 时自动清理
- 用途：
  - 跨多个派生节点共享的“本次技能流程”级别数据
  - 例如：命中次数、触发次数、流程阶段计数、某些去重标记等

> 建议：业务侧集中定义 `IntKey` 常量/静态字段（类似 `SkillScopeKeys`），避免 magic number。

## 推荐创建节点的约定

- **root（SkillCast）**：由技能入口（如 `SkillExecutor` / pipeline runner）创建，并在技能流程逻辑结束处 End。
- **child（Effect/Buff/Projectile/...）**：由派生对象创建者创建，生命周期结束时 End。
- **避免在中间 phase 兜底 CreateRoot**：会造成没有明确 End 的 root 泄漏。

## 调试与排查

### Editor 窗口
菜单：`Window/AbilityKit/Effect Source Debugger`

支持：
- 树展示（root->children）、折叠/展开、选中 root 子树高亮
- 搜索/过滤（Only Active 等）
- 右侧详情：snapshot/origin/rootState/chain/blackboard
- 异常 root 模式（Anomaly Mode）：
  - `ActiveCount==0 但 ActiveNodeCount>0`（疑似漏 End）
  - `ActiveCount==0 && ExternalRefCount==0 但树过大`（疑似 purge 未触发或 keepEndedFrames 过大）
  - `ActiveCount>0 且长时间未触碰`（疑似卡死链路）

### 常见坑
- **漏 End**：节点不 End 会导致 root 永远无法 purge。
- **错误地 End root**：root 结束应表示“技能流程逻辑结束”，但不会影响子节点继续运行；不要用 root End 代替子节点 End。
- **Origin 强引用**：默认已规避；不要在 origin 里塞大对象。

## 文件/入口点
- Runtime：
  - `EffectSourceRegistry.cs`
  - `EffectSourceSnapshot.cs`
  - `EffectSourceLiveRegistry.cs`（UNITY_EDITOR 下用于 editor 窗口获取运行时实例）
- Editor：
  - `EffectSourceDebuggerWindow.cs`

