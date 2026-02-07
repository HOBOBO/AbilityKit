---
name: state-handles-controllers
section: when_to_use
---

# When to use

适用于：

- `BattleSessionFeature` 这类“会话/流程/聚合根”持续变大、难以维护
- 文件职责混杂：数据字段、资源句柄、流程编排、业务逻辑都在一个类/文件
- SubFeature 里出现大量业务逻辑或直接访问 feature 内部字段
- 需要把逻辑拆成可测试单元（Controllers）并收敛生命周期边界
