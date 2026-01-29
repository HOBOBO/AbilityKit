---
trigger: manual
---

# EffectSource（事件溯源树）规则

本规则补充并细化 `EffectSourceRegistry`（溯源树）相关的工程约束，用于保证：

- 溯源链路可追踪（线上排查 / 工具可视化）
- 生命周期可回收（root 可被 purge）
- 低 GC / 无隐式强引用

> 总体溯源契约请同时参考：`origin_and_event_contract.md`。

## 1) 核心对象与术语

- **Context**：溯源树上的一个节点，唯一标识 `ContextId`。
- **Root**：一次顶层行为的根节点（例如一次 `SkillCast`），唯一标识 `RootId`。
- **Parent**：派生来源节点 `ParentId`。

### 快照字段（对外观测语义）

每个节点至少应能被描述为：

- `Kind`（`EffectSourceKind`）
- `ConfigId`（推荐填可定位的配置/实例 id）
- `SourceActorId` / `TargetActorId`
- `CreatedFrame`
- `EndedFrame` / `EndReason`


## 2) 生命周期规则（最重要）

### Rule A：创建者负责 End（No leaked nodes）

- 创建 root 的地方必须负责在“技能流程逻辑结束”时 `End(rootContextId, frame, reason)`。
- 创建 child 的地方必须在其生命周期结束时 `End(childContextId, frame, reason)`。

禁止：

- 在中间 phase/系统“兜底 CreateRoot”（容易漏 End，导致 root 永远无法 purge）。

### Rule B：End 不等于删除（No breaking the chain）

- `End(...)` 仅记录结束帧与原因，不应立刻删除节点。
- 删除由 `Purge(frame, keepEndedFrames)` 控制，并以 root 为单位回收整棵树。

### Rule C：Purge 条件不得破坏

root 可 purge 的条件（抽象语义）：

- `ActiveCount == 0`（root 下未 End 的 context 数为 0）
- `ExternalRefCount == 0`（业务侧没有 retain）
- `frame - LastTouchedFrame >= keepEndedFrames`

因此：

- 漏 End 会让 `ActiveCount` 永远不为 0。
- 忘记 release 会让 `ExternalRefCount` 永远不为 0。


## 3) 引用与 GC 规则（避免隐式强引用）

### Rule D：默认不保存 origin 对象引用

- 默认 `EffectSourceRegistry.StoreOriginObjects = false`。
- `originSource/originTarget` 应尽量为轻量类型（`int/long/string`）。
- 传入引用类型时会被规范化（例如退化为 actorId/fallback）。

只有在明确需要调试且可控时，才允许把 `StoreOriginObjects = true`。


## 4) Root scope 数据（Blackboard）规则

- Root scope 数据用于“与 root 同生命周期”的轻量共享数据。
- 推荐通过 `SetRootInt(rootId, IntKey key, int value)` / `TryGetRootInt(...)`。
- 业务侧应集中定义 `IntKey` 常量（避免 magic number）。


## 5) Debug/Editor 边界

- `EffectSourceLiveRegistry` 仅用于 `UNITY_EDITOR`，提供 editor 窗口获取运行时 registry 的入口。
- Editor 窗口：`EffectSourceDebuggerWindow`（菜单：`Window/AbilityKit/Effect Source Debugger`）。


## 6) 常见问题（排错）

- **漏 End**：root 永远无法 purge，树持续膨胀。
- **错误地 End root**：root End 表示“技能流程逻辑结束”，不等于 child 生命周期结束，child 仍必须各自 End。
- **origin 强引用**：把大对象塞进 origin 造成 GC 压力或泄漏。


## 7) 关键文件

- Runtime：
  - `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceRegistry.cs`
  - `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceSnapshot.cs`
  - `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceLiveRegistry.cs`
  - `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceKeys.cs`
- Editor：
  - `Unity/Packages/com.abilitykit.ability.runtime/Editor/EffectSource/EffectSourceDebuggerWindow.cs`
