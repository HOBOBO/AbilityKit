# MOBA 英雄执行特例收敛实施与验收记录

> 日期：2026-11-07。授权依据为方案 41 中用户明确批准的 A–E。
> 本记录只评价本批已审计链；不宣称整个项目已无英雄特例，也不把局部门禁通过解释为可以稳定批量接入英雄。

## 实施结果

| 方案 | 落地与迁移 |
|---|---|
| A：Counter 与准确 Cast 分离 | Counter 使用显式 `HfsmValueScale/HfsmValueOffset`；准备阶段采样候选参数，Commit 时发布准确 Cast 来源的幂等贡献，结束只撤销自身。四发普攻 Flow 增加正式 CommitPoint。参数 12 不再具有共享层保留语义。 |
| B：HFSM 声明绑定 | 新增按 DefinitionId 关联的 `character_bindings.json`；条件有限组合、状态 Cast 职责和技能类别由声明决定。删除烬图工厂和固定换弹算法，2.5 秒来自图的 MinimumActiveDurationRaw。资源按 `character_hfsm_<HeroId>` / default 统一解析。 |
| C：瞄准作用域 | 显式 Input / StateAimSnapshot；状态作用域捕获、保留、退出清理，重新进入分配独立身份。准确 Cast 在最后一次逻辑 Tick 后、清理前同步解析瞄准，覆盖零时长入口；缺快照明确拒绝。 |
| D：Brain 产生意图 | 行为树新增生成方向移动与按方向瞄准节点；生成方向进入 Brain 黑板快照。`launch_heading_combat_bt` / Brain 101 绑定飞行器，扫描对象不变成瞄准锁定。输出层删除两处召唤物 1008 分支，按当前 Motion 的世界坐标输入契约应用意图。 |
| E：击退方向与力度分离 | Pull 增加显式参考距离及近/远倍率，默认恒为 1；嬴政 10060211、鲁班 10080211 显式使用 12 / 1.25 / 0.75 保持结果。Schema、Authoring 与运行时共同校验。 |

补充边界：`Casts.CastKind` 显式区分 Active / BasicAttack，默认 LoadoutSlot 兼容普通槽位分类；替换到普攻槽的主动技能不再被误判。声明指纹参与恢复校验，HFSM 回滚序列化版本为 v4，旧快照不静默补默认事实。英雄参数与图构造只保留在权威内容及测试 fixture。

Authoring 编辑器支持配套声明；`MobaMechanismPublisher.PublishAndCheck` 统一执行状态机发布检查、Trigger Source 导入、Flow 发布检查、Trigger 发布检查。仓库技能规范与状态机文档已同步说明权威源和禁止身份/参数/路径推断规则。

## 发布与配置修复

正式组合发布日志 `local/moba-convergence/unity-publish9.log` 确认全部发布检查完成且 return code 0。随后 `luban-apply-final4.log` 发布 Excel 改动，`luban-check-final3.log` 检查通过。

迁移验收同时修复了直接调用契约错误：R 状态生命周期 Trigger 30010416–30010419 改为 Global；安妮计数回调 30030003 改 Global 并从 OwnerBound 被动订阅清单移除；30030101 改 Global。全部修改权威 Source/Excel 后经正式工具发布，没有放宽 Scope 校验。

收尾时发现安妮 Source/Excel 再次与发布物不一致，已在最新文件上窄范围合并修复，并同步修正 `migrations/add_annie_1010.py` 中相应生成声明，避免该脚本再生成错误 Scope/订阅。没有重跑该脚本的整批英雄迁移，也没有覆盖其他内容改动。最终构建已不再报告安妮 Scope 错误。

Source、Unity、Console 及 .NET 测试输出副本已比对；生成 JSON 的 Global 枚举 0 使用省略字段的默认值表示，不能把未出现 Scope 字段当作未发布。

## 验收证据

日志与 XML/TRX 保存在 `local/moba-convergence/`，旧失败日志保留供核对。

| 检查 | 当前结果 | 证据 |
|---|---|---|
| HFSM、通用机制与 BT 合同 | 76/76 通过 | state-contracts-final3.trx；最终全量构建再次覆盖 |
| View.Runtime 全量 | 198/198 通过 | view-final3.trx |
| Luban Python 全量 | 135/135 通过 | python-final2.log |
| Unity 受影响英雄、表现、HFSM | 127/128 通过，唯一失败为 HUD 会话启动 Scope 校验 | unity-acceptance4.xml/log |
| 最终发布后的 Unity HUD 单项复验 | 0/1 通过；启动校验因同一 Scope 冲突失败 | unity-hud-final.xml/log |
| Unity 鲁班真实技能/方向恢复 | 17/17 通过，亦包含在上述合集内 | unity-luban-final2.xml/log |
| BT 正式 CLI 产物一致性 | 0 导出 / 6 未变 / 0 错误 | bt-check-final.log |
| MOBA 主模块全量（正常构建并复制配置后） | 914/1010 通过，96 失败 | main-final7.trx/log |

主模块失败的 96 个用例均带有孙尚香 Trigger 10070101 的同一 Scope 冲突：95 个在配置启动校验被阻断，另一个批跑汇总用例期望 2 个成功、实际 0，其 Case Summary 也显示同一启动错误。全部仍登记失败，不将其计为已修复或已通过。新增 .NET 飞行器方向/恢复真实场景同样被启动校验阻断，不能以合同测试替代其验收；Unity 使用现有英雄 harness 验证了等价真实场景，涵盖三个世界坐标方向、转向后 Brain 重建恢复以及持续前进。

生产链静态复核未再发现本批列出的 Jhin 工厂、固定参数 12 特殊所有权、按 R 状态路径推断、Counter 攻击组补写或 SummonId=1008 输出覆盖分支；该结果只覆盖本批审计范围。恢复同时校验声明指纹，HFSM 快照为 v4，BT 为 `MobaBTree.Runtime.v2`。

## 剩余阻断与范围

孙尚香 10070101 的显式调用要求 Global，但其查询集合/遍历依赖只允许 OwnerBound 的 LocalBlackboard。当前不能仅改 Scope，也不能把临时结果搬到共享黑板。独立设计见 [方案 43](43-MobaExplicitTriggerQueryScopeProposal.md)；推荐增加目标存在查询谓词，该新增机制尚未获授权，因此没有实施。

本批只删除了清单所列执行特例，没有承诺召唤物跨会话恢复等既有未覆盖能力。全量启动门禁仍有阻断，当前不应宣布“全部验收通过”或“已经适合无约束堆量”。
