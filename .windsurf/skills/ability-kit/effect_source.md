---
name: ability-kit
section: effect-source
---

# EffectSource（事件溯源树）速查

## 目标

- 把一次技能/战斗行为产生的派生对象（Effect/Buff/Projectile/Trigger 等）串成一棵可追溯的树。
- 支持：
  - 运行时溯源（谁触发了谁）
  - 生命周期一致性（可回收时整棵树一起回收）
  - 线上排查（异常 root、疑似漏 End、卡死链路）


## 关键概念

- `ContextId`：节点 id
- `RootId`：顶层行为根 id
- `ParentId`：派生来源 id

节点快照：`EffectSourceSnapshot`（`Kind/ConfigId/SourceActorId/TargetActorId/CreatedFrame/EndedFrame/EndReason`）。


## 生命周期要点

- `End(contextId, frame, reason)`：标记节点结束（不会立刻删除）。
- `Purge(frame, keepEndedFrames)`：按 root 条件回收整棵树。
- 计数语义：
  - `ActiveCount`：root 下未 End 的 context 数
  - `ExternalRefCount`：业务侧 retain/release 的引用计数


## Origin 对象存储策略

- 默认 `StoreOriginObjects = false`。
- 仅保存轻量值（`int/long/string`），避免隐藏引用造成 GC 压力。


## Root scope（Root Blackboard）

- 与 root 同生命周期的轻量共享数据：
  - `SetRootInt(rootId, IntKey key, int value)`
  - `TryGetRootInt(rootId, IntKey key, out int value)`


## 调试窗口

- 菜单：`Window/AbilityKit/Effect Source Debugger`
- 支持：
  - 树展示（root->children）、折叠/展开、高亮子树
  - 搜索/过滤（Only Active 等）
  - 右侧详情：snapshot/origin/rootState/chain/blackboard
  - Anomaly Mode（疑似漏 End / purge 未触发 / 卡死链路）


## 关键文件（入口）

- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceRegistry.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceSnapshot.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceLiveRegistry.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceKeys.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Editor/EffectSource/EffectSourceDebuggerWindow.cs`


## 相关规则

- `.windsurf/rules/origin_and_event_contract.md`（事件溯源契约）
- `.windsurf/rules/effect_source.md`（本模块规则）
