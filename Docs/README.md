# AbilityKit 文档导航

从这里按任务选择入口。使用包先读包文档，理解跨包协作再读设计；新增或整理文档遵循[文档体系与维护规范](design/10-EngineeringQuality/20-DocumentationSystemStandard.md)。

## 阅读入口

| 需求 | 入口 | 以什么为准 |
| --- | --- | --- |
| 查框架能力、模块边界与当前证据 | [设计文档总索引](design/00-index.md) | `Docs/design/` 的对应权威章节；当前行为以源码和可复现证据核对 |
| 上手示例 | [Tiny 教程](tutorials/tiny/00-从这里开始.md)、[示例设计入口](design/09-ImplementationExamples/) | 按各示例与教程区分框架能力和示例策略 |
| 安装包、查 API 或编辑器用法 | [Unity 包文档索引](../Unity/Packages/README.md) → 包根 `README.md` → `Documentation~/`（旧包可能仍在 `Document/`） | 包内负责独立接入与使用契约；顶层负责跨包设计和仓库级指南。详见[包文档分工](design/10-EngineeringQuality/10-FrameworkPackageEngineeringStandards.md#6-文档的权威来源) |
| 找工具、运行或新增测试 | [工具入口](../tools/README.md)、[测试门禁与批量回归规范](AbilityKit测试门禁与批量回归规范.md) | 实际脚本、测试工程和 gate 配置；运行产物与测试基线分开 |
| 查实施范围、待确认问题和历史决策 | [实施计划与决策记录](../plans/README.md) | 明确确认的方案范围；当前行为仍看对应设计、源码和验收证据 |
| 修改 MOBA 配置、技能和生产链 | [机制治理规范](MobaMechanismGovernance.md)、[技能生产管线](MobaSkillPipeline.md) | 领域契约和正式发布入口不由仓库结构治理替代 |
| 编写、核校或整理文档 | [文档体系与维护规范](design/10-EngineeringQuality/20-DocumentationSystemStandard.md)、[治理路线图](design/11-DocumentationCompletionPlan.md#10-文档体系分层整理2026-11-24) | 规范负责分工、模板和验收；路线图负责实施批次；内容逐篇对照当前源码 |

## 文档如何分工

| 层次 | 回答的问题 | 维护方式 |
| --- | --- | --- |
| 包根 README 与包内使用文档 | 如何安装和正确使用本包？ | 使用契约随包维护，独立安装后可读 |
| `Docs/design/` | 为什么这样设计，多个包如何协作？ | 按能力组织权威专题，引用包契约 |
| 教程与仓库级指南 | 如何完成一个跨包任务？ | 串联操作、预期结果与排错，链接设计及包文档 |
| 计划、审计与验收记录 | 当时确认、实现或验证到了哪里？ | 明确日期、状态和范围，不能替代当前契约 |

文件名含“设计”不决定归属；为正确使用本包必需的局部设计可以随包保留。详细分类、模板和迁移规则只在[维护规范](design/10-EngineeringQuality/20-DocumentationSystemStandard.md)维护。

## 当前整理状态

本轮先统一规范与入口，再逐包核校正文。[首轮结构盘点与批次清单](../plans/2026-11-24-DocumentationSystemInventory.md)记录缺入口、多目录和待核项目；清单中的“有文档”只表示文件存在，不表示内容准确或可发布。

旧文档未注明核校状态时，不能默认与当前实现一致。阅读历史审计或提案时先看其适用日期、确认范围与继任入口；仅修复导航不刷新正文事实基线。

`Docs/` 顶层保留已有领域规范、验收记录、专项方案和阶段审计。新写跨模块通用设计先找 `design/` 的现有章节；历史结论不要提升为当前规范，历史源码移除后保留可追溯的名称而非失效链接。仅本地留存的证据写成 `local/...` 路径，不作为其他检出的共享链接。

文档路径不因分类调整而批量迁移。如何检查文档入口和散落产物见[仓库结构治理 skill](../.claude/skills/repo-structure-governance/SKILL.md)。
