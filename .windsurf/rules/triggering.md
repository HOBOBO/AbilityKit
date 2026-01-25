---
trigger: manual
---

# Triggering（触发器）规则

## 1) 核心对象

- **IEventBus**
  - 事件派发/订阅
- **TriggerRunner**
  - 执行触发器（conditions + actions）
- **TriggerRegistry**
  - 从程序集扫描 `TriggerActionTypeAttribute` / `TriggerConditionTypeAttribute` 自动注册 factory
- **TriggerEvent**
  - 结构：`{ Id, Payload, Args }`
- **PooledTriggerArgs**
  - `Dictionary<string, object>` 对象池（Release 时必须 Clear）

## 2) 生命周期与一致性

- **EventBus 一致性**：Subscribe 与 Publish 必须使用同一 `IEventBus` 实例（DI singleton）。
- **系统反注册**：不要在 Entitas `Cleanup()` 做一次性反注册；一次性逻辑放 `TearDown()`。

## 3) 扩展方式（新增 Action/Condition）

- **新增 runtime factory**：实现 `IActionFactory`/`IConditionFactory` + 标注 `[TriggerActionType]`/`[TriggerConditionType]`
- **新增 Editor 强类型配置**：继承 `ActionEditorConfigBase`/`ConditionEditorConfigBase` + 标注对应 attribute
- **新增 RuntimeConfig（导出用）**：继承 `ActionRuntimeConfigBase`/`ConditionRuntimeConfigBase`，实现 `ToActionDef`/`ToConditionDef`

## 4) 高频路径性能规则（Triggering 相关）

- 禁止在 Update/Tick/Execute 等高频路径中产生 GC（new List/Dictionary/LINQ/闭包/装箱 等）。
- Args/Defs 优先使用池化：`PooledTriggerArgs` / `PooledDefArgs`。
