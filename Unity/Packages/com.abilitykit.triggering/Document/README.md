# 触发器模块文档索引

> Ability-Kit 触发器模块官方文档

---

## 📚 文档列表

### 1. [强类型触发器模块开发设计文档](./强类型触发器模块开发设计文档.md)

**阅读对象**：首次接触触发器模块的开发者

**内容概要**：
- 为什么需要强类型触发器（类型安全、零装箱、确定性保障）
- 核心概念：ITrigger<TArgs, TCtx>、EventBus、TriggerRunner、ExecCtx
- 强类型接口定义和事件派发流程
- 三种触发器实现：DelegateTrigger、CompiledTrigger、PlannedTrigger
- TriggerPlan 配置化触发器详解
- NumericValueRef 数值引用系统和表达式编译
- Blackboard 黑板系统
- 设计模式总结
- 与旧版本触发器的对比

**推荐阅读顺序**：从本文档开始

---

### 2. [触发器模块开发设计文档](./触发器模块开发设计文档.md)

**阅读对象**：想深入了解触发器内部实现的开发者

**内容概要**：
- 触发器模块整体设计理念
- 详细的架构图和执行流程
- EventBus、TriggerRunner、ExecCtx 组件详解
- Blackboard 黑板和表达式系统
- 使用指南和示例代码

**推荐阅读顺序**：作为进阶阅读

---

## 🎯 快速入门

### 想了解触发器是什么？

👉 阅读 [触发器模块开发设计文档](./触发器模块开发设计文档.md) 第一章「设计理念」

### 想学习如何使用？

👉 阅读 [触发器模块开发设计文档](./触发器模块开发设计文档.md) 第六章「使用指南」

### 想了解 RPN 表达式？

👉 阅读 [触发器模块开发设计文档](./触发器模块开发设计文档.md) 第三章「条件求值流程」

---

## 📖 概念速查

### 核心类

| 类 | 职责 |
|------|------|
| `EventBus` | 事件总线，负责派发事件 |
| `TriggerRunner<TCtx>` | 触发器调度器，管理所有触发器 |
| `ExecCtx<TCtx>` | 执行上下文，包含所有可用资源 |
| `TriggerPlan<TArgs>` | 触发器计划，定义规则配置 |
| `PlannedTrigger` | 计划执行器，核心逻辑 |

### 条件

| 类型 | 说明 |
|------|------|
| `EPredicateKind.None` | 无条件，永远通过 |
| `EPredicateKind.Function` | 委托函数，可写任意逻辑 |
| `EPredicateKind.Expr` | RPN 布尔表达式 |

### 布尔节点

| 节点 | 说明 |
|------|------|
| `BoolExprNode.Const(bool)` | 常量布尔 |
| `BoolExprNode.Not()` | 取反 |
| `BoolExprNode.And()` | 且 |
| `BoolExprNode.Or()` | 或 |
| `BoolExprNode.Compare(op, l, r)` | 比较运算 |

### 数值引用

| 来源 | 说明 |
|------|------|
| `NumericValueRef.Const(double)` | 常量值 |
| `NumericValueRef.PayloadField(id)` | 事件负载字段 |
| `NumericValueRef.Blackboard(boardId, keyId)` | 黑板变量 |
| `NumericValueRef.Var(domainId, key)` | 域变量 |

---

## 🔗 相关文档

- [录像模块](../com.abilitykit.record/Document/) - 确定性回放
- [能力管线模块](../com.abilitykit.pipeline/Document/能力管线模块开发设计文档.md) - 技能执行
- [实体管理模块](../com.abilitykit.combat.entitymanager/Document/) - 实体查询

---

## 💡 典型使用场景

| 场景 | 说明 |
|------|------|
| 游戏技能系统 | 事件触发技能效果 |
| UI 响应系统 | 数据变化触发界面更新 |
| 统计系统 | 战斗事件触发数据统计 |
| 规则引擎 | 可配置的业务规则 |
| 帧同步回放 | 确定性触发器保证回放准确 |

---

## 📁 源码路径

```
com.abilitykit.triggering/Runtime/
├── Runtime/
│   ├── TriggerRunner.cs              # 触发器调度器
│   ├── PlannedTrigger.cs             # 计划执行器
│   ├── ExecCtx.cs                   # 执行上下文
│   └── ExecutionControl.cs           # 执行控制
├── Eventing/
│   ├── EventBus.cs                   # 事件总线
│   └── EventChannel.cs              # 事件通道
├── Plan/
│   ├── TriggerPlan.cs               # 触发器计划
│   ├── PredicateExprPlan.cs         # 布尔表达式
│   └── Json/                        # JSON 加载器
├── Blackboard/
│   ├── DictionaryBlackboard.cs      # 黑板实现
│   └── BlackboardKeyRegistry.cs     # 键注册表
├── Variables/Numeric/
│   ├── Expression/                  # 数值表达式
│   └── Domains/                     # 数值域
└── Registry/
    ├── FunctionRegistry.cs           # 函数注册表
    └── ActionRegistry.cs            # 动作注册表
```

---

*最后更新：2026-03-19*
