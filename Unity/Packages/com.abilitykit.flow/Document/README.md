# Flow 模块文档索引

> Ability-Kit Flow 模块官方文档

---

## 📚 文档列表

### 1. [Flow模块开发设计文档](./Flow模块开发设计文档.md)

**阅读对象**：首次接触 Flow 模块的开发者

**内容概要**：
- 为什么需要 Flow 模块（解决回调地狱、状态追踪等问题）
- 核心概念：节点、流程、执行器、上下文
- 架构图和执行流程
- 节点详解：基础节点 + 组合节点
- 完整使用示例
- 设计模式总结

**推荐阅读顺序**：从本文档开始

---

### 2. [Flow模块扩展指南](./Flow模块扩展指南.md)

**阅读对象**：想要扩展 Flow 模块的开发者

**内容概要**：
- 何时该写新节点 vs 组合现有节点
- 自定义节点开发模板
- 高级节点：组合节点、循环节点、进度节点
- Flow 与其他模块协作
- 完整示例：登录流程、技能释放流程
- 最佳实践与调试技巧

**推荐阅读顺序**：掌握基础后阅读

---

## 🎯 快速入门

### 想了解 Flow 是什么？

👉 阅读 [Flow模块开发设计文档](./Flow模块开发设计文档.md) 第一章「设计理念」

### 想学习如何使用？

👉 阅读 [Flow模块开发设计文档](./Flow模块开发设计文档.md) 第五章「使用指南」

### 想开发自定义节点？

👉 阅读 [Flow模块扩展指南](./Flow模块扩展指南.md) 第二章「自定义节点开发」

### 想看完整代码示例？

👉 参考以下示例文件：

| 示例 | 路径 | 说明 |
|------|------|------|
| 01_BasicSessionExample | `Samples~/FlowExamples/Runtime/` | 基础会话示例 |
| 02_WakePumpExample | `Samples~/FlowExamples/Runtime/` | Wake/Pump 机制 |
| 03_TimeoutAndRaceExample | `Samples~/FlowExamples/Runtime/` | 超时和竞速 |
| 04_ParallelAllExample | `Samples~/FlowExamples/Runtime/` | 并行执行 |
| 05_UsingResourceExample | `Samples~/FlowExamples/Runtime/` | 资源管理 |
| 06_ExceptionHandlingExample | `Samples~/FlowExamples/Runtime/` | 异常处理 |

---

## 📖 概念速查

### 核心类

| 类 | 职责 |
|------|------|
| `IFlowNode` | 节点接口（Enter/Tick/Exit/Interrupt） |
| `FlowRunner` | 流程执行引擎 |
| `FlowSession` | 会话封装（推荐入口） |
| `FlowContext` | 上下文容器（轻量 DI） |
| `FlowWakeUp` | 唤醒机制（回调推进） |

### 常用节点

| 节点 | 用途 |
|------|------|
| `DoNode` | 执行自定义逻辑 |
| `SequenceNode` | 顺序执行 |
| `RaceNode` | 竞速（首个完成决定结果） |
| `ParallelAllNode` | 并行执行（全部成功才成功） |
| `IfNode` | 条件分支 |
| `TimeoutNode` | 超时控制 |
| `FinallyNode` | try-finally 语义 |
| `UsingResourceNode` | RAII 资源管理 |
| `AwaitCallbackNode` | 等待外部回调 |
| `WaitSecondsNode` | 等待指定秒数 |

### 状态

| 状态 | 含义 |
|------|------|
| `Running` | 运行中 |
| `Succeeded` | 成功完成 |
| `Failed` | 执行失败 |
| `Canceled` | 被取消 |

---

## 🔗 相关文档

- [Host模块开发设计文档](../com.abilitykit.host.extension/Document/Host模块开发设计文档.md) - Flow 模块所在的运行时框架
- [Host模块扩展指南](../com.abilitykit.host.extension/Document/Host模块扩展指南.md) - 如何在 Host 中扩展 Flow 相关功能

---

*最后更新：2026-03-19*
