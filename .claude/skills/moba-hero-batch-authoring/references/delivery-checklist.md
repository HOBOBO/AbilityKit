# 英雄技能正式交付清单

## 设计和依赖

- [ ] 英雄 ID、技能槽位、技能/被动 ID 已确认且没有重复。
- [ ] Flow、Effect/Trigger、Buff、Area、Projectile、Launcher、Query、VFX、Model、PresentationTemplate ID 已列出。
- [ ] 每个引用都标明正式来源和运行时消费者。
- [ ] 每个机制已标注生命周期、目标维度、标签、表现和回滚/重连影响。
- [ ] 没有把生成物、历史归档或迁移文件当成设计来源。

## 正式配置源

- [ ] 需要的 Skill Flow 已在 `SkillFlowCO.asset` 中编辑并发布。
- [ ] 需要的 ActionEditor 源已保存，并生成匹配的 logic/presentation JSON；不需要时间轴时已明确记录。
- [ ] Trigger Authoring Workspace 模块与 `Sources/*.trigger.json` 同步。
- [ ] 角色、技能、效果、Buff、Area、Projectile、Query、表现资源等已在 Luban Excel 编辑。
- [ ] 状态机变更已通过状态机编辑器保存和发布。
- [ ] 没有直接修改 `Resources/moba/*.json`、`Resources/luban`、生成代码或 Console 副本。

## 行为验收

- [ ] 技能能从正式配置加载并按槽位释放。
- [ ] 资源、冷却、目标条件和拒绝释放行为正确。
- [ ] 位移验证实际位置、墙体穿越、落地和目标控制。
- [ ] Buff 验证授予、刷新、叠层、过期、移除和多来源行为。
- [ ] 投射物验证生成、移动、命中/退出、重复命中策略和目标跟随。
- [ ] 区域验证生成、进入/离开、命中间隔和生命周期结束。
- [ ] 多段技能验证阶段顺序、等待、插入/中断和最终结束。
- [ ] 标签验证授予源、消费规则、控制免疫和表现 Cue 生命周期。
- [ ] 复杂机制验证关键 trace，同时验证真实状态或运动结果。

## 表现验收

- [ ] 使用的 VFX、模型、表现模板和 prefab 存在于正式资源包。
- [ ] 逻辑时间轴只引用 logic 导出，表现轨道只进入 presentation 导出。
- [ ] 投射物/区域/单位表现绑定正确的实体或目标。
- [ ] Cue 的添加、暂停、恢复、刷新、结束和多来源叠加行为正确。
- [ ] 表现资源加载失败时不会伪装成逻辑成功。

## 发布和门禁

- [ ] ActionEditor 导出完成。
- [ ] Skill Flow 发布完成并运行 `MobaSkillPipelineExporter.Check`。
- [ ] Trigger 发布完成并运行 `TriggerAuthoringMobaMigration.CheckRuntimeBatch`。
- [ ] 状态机变更运行 `MobaStateMachinePublisher.Check`。
- [ ] 执行 `python LubanConfig/Moba/export_pipeline.py --apply`。
- [ ] 执行 `python LubanConfig/Moba/export_pipeline.py --check-published`。
- [ ] 执行受影响英雄 Unity EditMode 验收。
- [ ] 执行六英雄 .NET 冒烟和 `MobaLubanConfigPipelineTests`。
- [ ] 执行 `moba-content-contracts` 及改动范围要求的其他门禁。
- [ ] 新增英雄同步更新 acceptance coverage manifest、gate 和 validator 登记点。

## 暂停条件

出现以下情况不要继续猜测实现：

- [ ] 权威源、字段消费者或生命周期不明确。
- [ ] 需要新增组件、属性、服务、PlanAction、Targeting 规则、命中策略、协议字段或状态机类型。
- [ ] 需要创建/销毁实体但回滚、Actor ID、重连或快照边界未确认。
- [ ] 需求与现有英雄、设计文档、生产配置或门禁结果冲突。
- [ ] 发布检查失败且原因无法归因到当前改动。

暂停时向用户报告：已确认事实、缺口、候选方案、影响范围和一个具体问题。
