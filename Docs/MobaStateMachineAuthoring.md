# MOBA 状态机创作与发布

2026-11-24 已批准的 Cast 等级来源：`Casts` 中 `LevelSource` 缺省为 `CurrentSlot`；`Slot` 配合 `LevelSourceSlot` 显式读取另一槽；`ActivationBuff` 配合 `LevelSourceBuffId` 读取本人有效模式 Buff 持有的原 Cast 冻结等级。来源缺失、歧义、跨施法者或失效时拒绝，不从英雄身份推断。Prepare 一次采样后仍使用子技能自己的等级表；激活来源身份写入既有 Pipeline 纯数据 SharedData，在原提交点前核对同一来源。模式中升级不追溯本次激活的等级；退出、死亡与来源撤销沿既有 Buff 生命周期失效。Bindings 指纹与既有 Buff、Cast、Runner 恢复共同约束来源，不新增第二份可变等级或历史事件回放。

角色 `character_bindings` 可显式声明 `DeathCasting`：`PermissionBuffId` 指向本人持有的限时许可 Buff，
`Skills` 逐项声明有效技能 ID 与输入时的 `MinimumCompletionMs`，`RetainedBuffIds` 声明死亡清理期间保留的本人 Buff。
许可只读取对应 Active Continuous 的剩余时长，默认未配置时拒绝所有死亡施法；输入时剩余时长必须严格大于提交跨度，
执行中只要求许可仍有效。派生技能离开死亡阶段后不可继续执行。死亡 HFSM 的进入/退出 Trigger 负责创建/撤销来源，
Modifier 提供阶段参数与技能候选，通用门禁不会更改 HP、推迟死亡事件或放开移动/普攻。
该声明随 Bindings 指纹校验和状态机发布；许可 Buff 的计时、来源与投影使用现有 Buff/Continuous 恢复合同。

投射物阶段扩展（2026-11-13 已确认）：`projectile.awaitArrival` 恢复飞行后等待一次到达，
`projectile.awaitCastNumber` 按 `key,threshold` 读取投射物来源的原 Cast 通用数值，
`projectile.remove` 显式回收自身。使用 `delay` 与动作组合配置尾段；不从 Animator 完成回调提交玩法。
`actor_state_presentations.json` 是 profile/state 到 Animator 状态、片段时长及播放窗口的独立权威绑定，
随状态机发布并校验引用。它不向严格的逻辑 Profile JSON 混入表现字段。
投射物阶段快照携带 Actor 版本、profile/path、阶段实例（进入帧）、进入帧、快照帧及本地秒游标；
客户端按游标定位动画，同 Actor 保留单个桶。恢复只重建/定位表现，不重放到达、Area 或伤害。
`projectile-event.push` 协商版本为 2；载荷内同样记录 Version=2，使聚合快照内的路由也拒绝旧版或未知版。
目录 revision 不能替代协商版本。

角色 HFSM 和投射物状态机的编辑源位于
`Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaStateMachineAuthoring/`。
使用 **Window > AbilityKit > MOBA 状态机编辑器** 按角色选择 HFSM 定义。
所有英雄按 `character_hfsm_<HeroId>.json` 命名（烬为 `character_hfsm_3001.json`），
`character_hfsm_default.json` 是通用回退定义。每个文件独立声明参数、状态、转换和状态动作，
参数 ID 只在对应的 `definitionId` 内有意义。编辑器保存前调用运行时解析器校验。

- **保存源文件**：保存工作台中的编辑；不发布产物。
- **校验**：只读取已保存源文件与 Unity、Console 两端产物，按 JSON 内容比较；不会保存窗口中未提交的编辑。
- **发布**：先保存源文件，再点「发布」查看预检。预检校验磁盘源文件并按目录统计将新建、覆盖和清理的文件；覆盖按实际写入的文本比较，不做字段级差异。确认后才将全部 `character_hfsm*.json`、`character_bindings.json`、`actor_state_machines.json` 和 `actor_state_presentations.json` 同步到 Unity Resources、Console Configs 和 Console Luban Configs，并清理这些目录中的旧 `character_hfsm.json` 及其 `.meta`。取消不写入；发布不是事务，失败后应核对版本控制差异再重跑。

角色 HFSM 使用 `StateMachineDefinition`，投射物使用
`HierarchicalProfile<MobaActionSpec>`；两种结构不能互相代替。投射物表中的
`StateMachineProfileId` 引用投射物状态机配置 ID，导表时会校验该引用。
状态机图不再从 `J-角色表.xlsx` 导出。技能数值、投射物参数和效果数值仍由
`LubanConfig/Moba/Production/Datas/` 管理。

角色运行时按英雄 ID 选择 `character_hfsm_<HeroId>`；没有专属文件时使用
`character_hfsm_default`。运行时不构造英雄状态图，也不根据英雄身份、参数编号或状态路径解释玩法。
参数编号与条件声明属于对应定义；其他定义可声明相同机制，必须显式绑定才能启用。

`character_bindings.json` 按 `DefinitionId` 为每张角色图提供唯一的配套声明。
编辑器的角色面板提供声明 JSON 编辑区，保存前联合校验图和声明；发布会检查定义集合完整对应。

| 字段 | 契约 |
|---|---|
| `Conditions` | `Key` 绑定有限表达式：`Fact`、`ParameterId/Comparison/Value`、`All`、`Any`、`Not`，每个表达式最多 64 节点、8 层嵌套。比较支持 eq/ne/lt/le/gt/ge；不允许脚本或覆盖内置条件。 |
| `States` | `Path` 使用完整状态路径；`CastContext` 为 None、Current 或 RetainCommitted，`CastKind` 为 Any、BasicAttack 或 Active。保留的 Cast 仅属于当前状态实例，退出即结束。 |
| `States.AimScope/CaptureAim` | 进入声明的捕获状态时采样权威瞄准；同作用域后续子状态保留该值。退出、控制或死亡时清理，重新进入生成新的实例身份。 |
| `Casts` | 按 `SkillId` 声明 `CastKind`、`AimSource`（默认 Input；StateAimSnapshot 和 InputWithinStateCone 必须引用有效 `AimScope`）及 `ParameterSnapshots`。`CastKind` 支持 Active、BasicAttack 和默认 LoadoutSlot；替换到普攻槽的主动技能必须显式声明 Active，不能仅按当前槽位误判。内容 ID 只绑定配置，不选择算法。 |
| `Casts.AimConeHalfAngleDegrees` | 仅 InputWithinStateCone 使用，必须有限且在 (0,90] 度内。读取既有模式瞄准轴，校验本次非零水平输入；包含边界，越界拒绝且不钳制。校验先于正式 Cast、付款、计数和动作交接。单发方向不改写模式轴，成功后沿既有 Cast/弹体上下文冻结。其它策略必须为 0。 |
| `ParameterSnapshots` | `SourceParameterId > 0` 在准备时采样；为 0 时使用显式候选常量 `Value`。`TargetParameterId` 必须声明并支持 Override；准确 Cast 在 Commit 点发布，结束时仅撤销自己的来源贡献。 |

快照保存声明指纹、状态事实、Cast 上下文、瞄准值和作用域实例序号；恢复不会重放状态进入动作。目标 actor 缺少 machine 时，由 `MobaCharacterHfsmRuntime.CreateRestored` 直接从数据恢复，不先正常 Initialize 再覆盖；预检同样不初始化状态，恢复成功后才注册 Inspection。正常新建仍执行初始进入动作，恢复后的未来状态转换继续执行其正式逻辑动作；不新增补发或事件历史回放。清理及验收见 `local/docs/Moba回滚冗余事件清理验收.2026-11-17.md`。

角色 HFSM 的既有快照校验通过 `IRollbackStatePreflightProvider` 接入 Coordinator，在任何 provider 导入前执行；直接恢复入口也先完成整份 HFSM 载荷校验。预检的候选机器不连接正式参数服务、逻辑效果回调或 Inspection 注册；定义解析和 profile 参数兼容检查不注册/切换现场 profile，不清理参数贡献。实际导入阶段才注册 profile 并恢复机器。此防护覆盖 HFSM 载荷及其既有 profile 约束，不把其他 provider 的导入变成事务，也不扩展跨名单或已销毁对象的恢复范围。

2026-11-17 补齐参数贡献恢复预检：provider 10044 通过 `IRollbackSnapshotPreflightProvider` 在任何导入前检查全部贡献。若同份快照包含 HFSM provider 10017，按其实际导入行为，在隔离的声明副本中模拟 profile 注册及完整基线的遗漏清理，然后检查 Actor/Owner/Parameter 正 ID、参数声明和允许的 Modifier 操作。HFSM 的空 payload 不改变声明，空数组会移除名单内的声明；不能只按当前现场 profile 判断目标帧贡献。只有实际连接同一个参数服务的 HFSM provider 才会影响该服务的声明预测。

参数 provider 的直接 `ValidateImport` 和服务 `RestoreSnapshot` 按当前已注册声明检查完整载荷，后者检查全部条目后才清空并应用。它们不会自动导入 HFSM；手动恢复多个领域仍需调用方保证顺序，整份恢复应使用 Coordinator。贡献的 long OwnerId、同一 Actor/Parameter/Owner 后写替换、来源撤销与数值合成规则保持不变；参数的 null/空 payload、JSON null/空数组继续表示清空贡献但保留声明，与 HFSM 空 payload 的 no-op 不同。此修复没有新增快照字段或版本，不保证所有 provider 导入异常都能事务回退。验收见 `local/docs/MobaHFSM参数恢复预检验收.2026-11-17.md`。

生成方向移动必须输出已有 `MobaActorIntent.MoveDirection` 的世界坐标方向语义，不能用“当前位置 + 一米方向”冒充目标点后进入寻路投影。普通追敌仍使用目标位置意图；两类意图在同一仲裁中显式区分。生成方向保存在 `memory.launchHeading.*` 并参与 Brain 快照，转向与扫描目标不改变该值。施法与移动同时发布由行为树仲裁的 `allowMoveWithCast` 声明。
角色 HFSM 压缩快照版本为 4；不同声明指纹或不完整作用域数据会拒绝恢复。
换弹等最短状态时长使用转换的 `minimumActiveDurationRaw`（Q32.32 秒），由配置决定，运行时没有固定帧数算法。

## 受控打断与参数驱动恢复

硬控（例如眩晕、压制，以及被标签规则映射为 `Stunned` 结果的其他控制）由角色
HFSM 的 `controlled` 事实统一处理。当前动作到 `controlled` 的边优先级最高且不设置
最短状态时长，确保冰冻/石化/压制发生时可以立即打断表现动作；短期
`SkillPipeline` 的取消由战斗门禁完成，不能依赖 Animator 的完成回调。

解除控制后，HFSM 只根据仍然存在的事实和参数重新选择状态：

* 提交点已经发生时，触发器必须先写入持久的槽位/阶段参数。以烬为例，第四发提交后
  `AttackSlot == 5`，解除控制后经 `free.*` 恢复边回到普通状态，再由参数条件进入
  `attack.slot.5` 换弹；不会重播第四发，也不会等待被取消的 Pipeline。
* 提交点尚未发生时，参数仍保持原槽位。解除控制后可以回到该槽位的待施法状态，下一次
  输入再创建新的短 Pipeline；不会凭表现层动作进度推断已经提交。
* `minimumActiveDurationRaw` 约束的是仍停留在原动作状态内的离开转换。控制转移本身必须
  保持可立即打断；恢复目标由参数条件决定，不能把动画时长硬编码到控制恢复逻辑。

因此，新增控制类型时只需在标签规则中把它映射到统一的 `Stunned` 结果并补齐持续对象
生命周期验收；不应为每种控制在烬的 HFSM 中复制一套专属转移。尚未被规则映射的预置
标签（例如仅存在于目录中的 Frozen/Petrified）不会自动产生 `controlled` 事实，必须先
完成其授予源、规则映射和发布检查。

批处理入口：

```powershell
# 从 Unity 工程执行以下 -executeMethod
AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.MobaStateMachinePublisher.Publish
AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.MobaStateMachinePublisher.Check
# 本批涉及状态机、Flow、Trigger 的组合发布及检查
AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.MobaMechanismPublisher.PublishAndCheck
```

完整发布后执行 `python LubanConfig/Moba/export_pipeline.py --check-published`。
不要直接编辑 `Resources/moba` 或 Console 副本。旧 `effect_plans` 已停止加载；
事件条件和动作在 Trigger Authoring 编辑，普通 `effects` 数值表仍由 Luban 发布。

## 正式角色动画同步

模型动作资料与状态 Action 分开维护：`Production/Datas/model-animations.xlsx` 导出独立
`moba/model_animations` 目录，保存 `ModelId / ActionKey / AnimatorState / FrameCount / FrameRate / Layer`。
`ActionKey` 只在模型内唯一，可由作者命名；Animator State 是实际播放入口，Clip 名仅用于资源检查。
目录不包含角色 HFSM 路径、技能 ID 或状态 Action ID，没有角色/HFSM 的模型也可先导出动作资料。

状态动作表中的 `play` 使用引用：

```json
{"id":"pose.play","type":"play","modelId":3002,"animationKey":"Base Layer.AA_Minigun","loop":false}
```

引用节点不再填写 `animatorState/frameCount/frameRate/layer`；这些字段（包括显式 0 值）与引用互斥，
缺失目录项直接失败。状态只配置已有的组合/loop 等策略。未迁移的内联 play 保持兼容。
`ModelAnimationCatalog` 解析资料后由现有表现 Action 消费；不把素材时长自动写成 Flow 时长、状态最短时长或施法锁定时间。

动画同步窗口默认“模型动作目录”：选择模型 → 扫描 → 为新动作设置稳定键 → 勾选导出行 → 应用或同步发布。
新发现动作默认不勾选；已有动作键保持稳定，不因资源改名自动改变。可复制 play 引用到动作配置。
“旧状态节点”页仅维护尚未迁移的内联配置。金克丝原 22 个节点已归并为 9 条共享模型资料。

移动动作接入同样先导出模型动作资料，再在 `M-模型表.xlsx` 的“角色动画动作”中将实体的移动状态变体引用到该动作，并保留 `loop:true`。
金克丝的 `moving.powpow` / `moving.fishbones` 分别引用 `Base Layer.Walk` / `Base Layer.Walk_Rocket`；停止和技能退出仍由现有 HFSM 参数决定。
不要将循环移动 Clip 接成有界技能 Cast。七英雄映射及接入边界见 [七英雄移动动作接入](../plans/SevenHeroMovementAnimationIntegration.md)。

在角色 HFSM 编辑器点击“角色动画同步”，或打开 `Window/AbilityKit/MOBA 角色动画同步`。
模型页直接选择模型扫描，不依赖状态图；旧节点页先保存状态图，再按角色 ID 扫描。
工具从 `M-模型表.xlsx` 读取模型绑定，旧节点页另读 `J-角色表.xlsx`，
通过 Unity 获取模型实际 Controller、Animator State 和 AnimationClip；不按 Clip 文件名猜测 State。

“扫描 / 预览差异”不更新正式配置；首次绑定可从下拉框选择 State。“应用选中差异”更新对应的源工作簿和动画绑定，不发布运行时配置。
“同步并发布”先列出选中的动作数量、目标工作簿和完整 Luban 发布范围，确认后才更新源文件，随后执行动画检查、Luban `--apply` 和 `--check-published`。
取消不写入；发布失败时不撤销已经完成的源文件更新。播放模式或正要进入播放时，不能应用或发布。
模型页更新目录的 `AnimatorState/FrameCount/FrameRate/Layer`；旧节点页更新对应内联字段。
两者均保留状态的 loop、其他动作、Flow 和逻辑 HFSM 时序。
写入采用目标单元格替换、写锁和扫描指纹验证，其他角色的配置不会从旧工作簿快照覆盖。
Excel 占用或源文件变化时需关闭占用/重新扫描。

编辑器侧稳定绑定位于 `Configs/MobaAnimationAuthoring/character_animation_bindings.json`，
模型资料以 `{modelId, animationKey}` 为目标；旧内联动作保留原节点目标以兼容维护。
使用模型、Controller、State、Clip 的 GUID 与本地 fileID 追踪改名；该文件不发布给运行时，
不复制帧数。删除重建对象、替换 Clip、绑定歧义和不支持的 Motion 要求重新选择。
实际播放使用 Animator 完整状态路径，因此 State 名和 Clip 名可以不同。

时长帧数按导入长度乘导入帧率向上取整，近整数先消除浮点误差，不额外加终点帧。
保留逐 Clip 的实际帧率，由现有表现运行时换算为逻辑跨度。带 speed、转换、loop 差异等时序告警
的动作默认不选中；手动选中仅接受所列 Clip 元数据，不代表完整播放验收已通过。

工作台的“仅检查已绑定动画漂移”检查资源身份和配置漂移，不受当前模型或角色筛选影响；旧独立菜单已退役。自动化保留 `MobaCharacterAnimationSync.Check`。
`MobaMechanismPublisher.PublishAndCheck` 在发布前运行同一检查（未建立绑定时跳过）。
检查只覆盖已经登记的动作，不代表所有英雄资源齐全，也不验证技能前摇/后摇或实际播放完成。
差异和时序告警保存在 `local/Logs/character-animation-sync/scan-model-<模型ID>.json`；
旧节点扫描报告为同目录的 `scan-<角色ID>.json`。同一目标保留最近一次扫描，不累计每次运行的副本。

扫描也比较 `ClipSettings.startTime/stopTime` 与 `AnimationClip.length`（0.0001s 容差）。
范围不一致会显示实际起止和长度，并默认取消选中；只接受元数据不会修正资源播放范围。
报告保存 `clipSeconds/clipSettingsStartTime/clipSettingsStopTime`，方便区分曲线跨度与 Animator 播放范围。
此告警适用于任意绑定角色，不按英雄或资源 ID 分支。正式资源应在其生成器中维护明确播放区间；
不能按最后关键帧、最后骨骼变化自动裁剪，或把资源时长直接当作施法锁定时长。

Unity 批处理入口（类全名均为 `AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.MobaCharacterAnimationSync`）：

```text
ScanBatch -mobaAnimationSyncEntities <角色ID,角色ID>
SynchronizeBatch -mobaAnimationSyncEntities <角色ID,角色ID>
Check
```

带时序告警的动作在批处理同步时需要显式追加 `-mobaAnimationSyncIncludeTimingWarnings`。
`SynchronizeBatch` 只同步权威源并检查；之后使用既有 Luban 发布命令。
批处理始终等待前一步成功退出，再启动下一步，不和同工程 Unity 编辑器或其他发布器并发执行。
详细方案和确认范围见 [正式角色动画同步方案](../plans/MobaCharacterAnimationSyncDesign.md)。

独立目录的已确认契约与迁移记录见 [模型动作目录解耦方案](../plans/ModelAnimationCatalogDecoupling.md)。
批处理更新已有模型资料：`MobaCharacterAnimationSync.SynchronizeModelsBatch -mobaAnimationSyncModels <模型ID>`，
有时序告警时仍需显式追加 `-mobaAnimationSyncIncludeTimingWarnings`。此入口只同步已绑定目录项；新增动作由窗口选取。
