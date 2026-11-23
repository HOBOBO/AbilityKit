# 更新记录

## Unreleased

- 新增 `AttributeContext.GetValueSilently(AttributeId)`：在同步、同上下文的嵌套求值期间抑制 Instance/Group/Context 属性通知，仍执行正常公式、Modifier、约束、缓存更新及依赖标脏；异常传播并退出静默模式，不补发历史通知。
- 正常 `GetValue` 通知行为不变。静默范围不传播到其他 Context，不提供并发、事务、外部公式副作用撤销或属性状态序列化能力。适用于状态恢复后的派生投影。
- 2026-11-18 属性测试 13/13 通过，包含既有热路径分配验收；MOBA 资源恢复专项 28/28 通过。详细契约见[静默实时求值](Documentation~/Usage.md#静默实时求值)。
