# When to use

适用于：

- `BattleSessionFeature` 这类"会话/流程/聚合根"持续变大、难以维护（当前已是 40+ partial 文件）
- 文件职责混杂：数据字段、资源句柄、流程编排、业务逻辑都在一个类/文件
- SubFeature 里出现大量业务逻辑或直接访问 feature 内部字段（应通过 `FeatureModuleContext<BattleSessionFeature>` + Runtime 契约访问）
- 需要把逻辑拆成可测试单元（Controllers：`Session*Controller`）并收敛生命周期边界
- 需要拆分巨型 partial class（按生命周期阶段 / Sim 变体 / dispose helpers / accessors / host 契约 / 领域子目录）
- Entitas ECS 系统（继承 `WorldSystemBase`）的反注册时机选择（`OnTearDown` 而非 `OnCleanup`）

## 不要在本 skill 找的内容

- ECS 实体系统设计 → 看 `com.abilitykit.world.entitas`
- 帧同步预测 → 看 [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)
- 技能/BUFF 业务 → 看 [ability-kit/skill_buff/README.md](../ability-kit/skill_buff/README.md)
