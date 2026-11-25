# Annie Unity Presentation Acceptance

This note tracks what the current Unity presentation checks prove for Annie. Runtime combat acceptance is covered separately by `AnnieSkillAcceptanceTests`; linked asset existence alone does not prove that a cast event selects or plays the intended asset.

| Skill | Live model action checked | Current VFX evidence | Remaining Unity presentation acceptance |
|---|---|---|---|
| Q (`30030201`) | `Void_Annie_Model_SpellQ` from a real cast snapshot | Q projectile uses configured VFX `90033001`; linked Q projectile and hit prefabs are included in the resource catalog | Consume the real projectile spawn/hit events and verify projectile follow, hit placement, and timing |
| W (`30030301`) | `Void_Annie_Model_SpellW` from a real cast snapshot | Live area cue resolves linked VFX `90033002` | Completed: the real area cue consumes the linked cone prefab with committed cast direction, 45-degree half-angle, 11x11 footprint, 5.5 radius scale, and 700ms lifetime |
| E (`30030401`) | `Void_Annie_Model_SpellE` from a real cast snapshot | The authored Buff `30030402` now resolves template `90033004`; a real PresentationCue snapshot instantiates the linked shield prefab | Add a later expiry/stop assertion at the Unity view layer if the final shield lifecycle presentation requires an explicit stop event |
| R (`30030501`) | `Void_Annie_Model_SpellR` from a real cast snapshot | Tibbers spawn event instantiates lifecycle VFX `90033006` at the authoritative summon position | Verify R cast-area VFX `90033005` if it is intended to be a separate cast presentation |

Q/W/E/R now have published presentation timeline artifacts. Their model actions resolve from the character action catalog. Q's projectile, W's cone, E's shield, and R's summon belong to their projectile, area/effect, Buff cue, and summon event consumers respectively. Add ActionEditor clips only for cast-local timing that is not already represented by those event-driven paths.

The Unity Q/W/E model-action test uses live cast snapshots and confirms the linked Animator state. The Q projectile test consumes the real projectile snapshot and verifies VFX `90033001` follows and releases with the projectile. The W test consumes the real area cue and verifies linked VFX `90033002` uses the committed direction and footprint without being reoriented by the caster's current facing. The E test consumes the real Buff cue snapshot and verifies VFX `90033004` instantiates on the protected actor. A missing timeline is a configuration failure; do not hand-edit exported `*.moba.presentation.json` files to silence it.

## Local gameplay repair — 2026-11-10–11

Reported symptoms: R failed without enemies; with enemies, damage continued but Tibbers was not visible. Loading a linked prefab or submitting an input is insufficient evidence of local gameplay.

| Finding | Source / consumer | Existing contract and repair |
|---|---|---|
| Empty-ground R rejected before its Flow | `Production/Datas/J-技能表.xlsx`, skill `30030501`; `SkillCastPreparationService` | Set `AllowCastWithoutTarget=true`. The impact effect still queries enemies; an empty result must allow the following summon action. W already supports empty casts; Q retains its unit target requirement. |
| Tibbers' basic attack logged a missing presentation timeline | ActionEditor `Resources/moba/action_timeline/skill_30030101.json`; `MonoCharacterHfsmView` | Publish an explicit empty timeline, consistent with other HFSM-driven basic attacks. The existing character action catalog owns Annie/Tibbers attack animations; Flow/Trigger own damage. No duplicate clip is added. |
| Tibbers existed but its skin was invisible | Linked `Tibbers_Goth.mat` uses `Custom/AddMul_SS_Cull_Depth`, sampling `_CameraDepthTexture`; production battle camera had `DepthTextureMode.None` | `BattleViewCameraController.SetCamera` supplies depth input for battle materials. Rebinding/reset releases only the depth flag it added, preserving pre-existing camera flags. No material or hero identity branch is introduced. |
| Previous local HUD scenario asserted submission only | `LocalBattleScenario_AllFourHudSlotsSubmitProductionSkillInput` | Require actual E shield and R summon, active actor-bound shell, Animator, mesh, material, bounds and scale. Resolve shells by `MonoViewHandle.ActorId`; production pooling changes object names. |
| Continuous damage came from summon attacks, with cooldown not committed | `SkillFlowCO.asset`, basic attack `30030101`; `R_ShouldApplyConfiguredImpactDamageAndSpawnConfiguredTibbersSummon` | Compose existing Economy reserve and CommitPoint before damage; resolve the skill table's 800ms cooldown. Assert exactly one R impact, later damage sourced from Tibbers, and at most five basic-attack hits across the first hit plus three additional seconds. |

The first run reproduced empty-ground R rejection and missing local summon: **16 tests, 14 passed, 2 failed**, `local/Logs/annie-local-baseline.xml`. Both empty-ground area-skill cases passed after the R configuration correction. Logical summon creation alone does not establish model visibility; the production HUD scenario checks this separately.

The graphics-enabled scenario waits until spawn VFX has expired, renders the real local battle camera, and compares frames with Tibbers' skin enabled/disabled. A diagnostic run measured only **3 differing pixels** without camera depth versus **1305** with depth (`local/Logs/annie-depth-diagnostic.xml`). The final assertion requires the production camera to supply depth and the skin to contribute more than 32 pixels; the test does not enable depth itself. Null-graphics runs skip this image assertion and cannot prove visual visibility.

Normal export also encountered an existing Jinx binding source/publication mismatch: six condition groups lacked the required `Expression` wrapper. Only the schema structure was corrected, preserving facts, parameter comparisons and state graph, before running `MobaStateMachinePublisher.Publish` and the regular Luban export. Generated configuration was not used as the repair source.

Verification artifacts:

- Graphics-enabled Unity gameplay fixture: `AbilityKit.Game.Test.UnitTest.AnnieSkillAcceptanceTests`, **16 passed, 0 failed**; linked presentation fixture: `AbilityKit.Game.Tests.AnnieUnityPresentationConsumptionTests`, **9 passed, 0 failed**. Report: `local/Logs/annie-repair-verified-v2.xml`. The same report initially has one camera-test failure: the test assumed MotionVectors would not implicitly enable Depth; production gameplay/presentation passed. Camera test correction is verified separately below.
- Camera lifecycle fixture `AbilityKit.Game.Test.UnitTest.BattleViewCameraControllerTests`: **11 passed, 0 failed**, `local/Logs/annie-camera-verified.xml`. It covers repeated binding, releasing the controller-added depth flag, preserving unrelated DepthNormals and pre-existing Depth requirements. Only the test's flag setup changed after the combined run; production camera code did not change.
- `.NET` configuration and seven-hero first-skill regression after Flow publication: **32 passed, 0 failed**, `local/Logs/annie-repair-regression/annie-repair-regression.trx`.
- ActionEditor export, Flow publication, Luban `--apply` and `--check-published` completed; logs: `local/Logs/annie-basicattack-timeline-export-retry.log`, `annie-attack-flow-publish.log`, `annie-repair-publication.log`, `annie-repair-publication-check.log`.
- Final Flow source/publication check (`MobaSkillPipelineExporter.Check`) validated 58 flows and exited normally with code 0: `local/Logs/annie-flow-publication-check.log`. Final Luban publication check also passed: `local/Logs/annie-repair-final-check.log`.
- Production-camera screenshot `local/Logs/annie-local-tibbers.png` was captured and visually reviewed after spawn VFX expiry. The linked skin contributes **1305 differing pixels**. Tibbers is visible but pale against this bright test floor; this is evidence of rendering, not approval of final art contrast or scale. Image capture is skipped with a null graphics device.

Boundaries of that baseline: local HUD Q/W damage outcomes were not yet asserted by its submission scenario. The newer checks and findings below supersede that limitation. Final indicator appearance, shield stop appearance and hit timing still require visual acceptance. The existing 14-second summon lifetime and current Tibbers attack loadout are the tested configuration, not proof of one-to-one completeness of every Annie mechanic.

## 2026-11-11：真实 HUD 复测与第二批修复

本地用户日志保留于 `local/Logs/annie-user-local-20261111.log`。本批继续使用 `moba-skill-authoring` 与 `MobaMechanismGovernance`：修复已有契约和配置组合，不增加英雄专属执行分支。Q 的用户确认范围为“保持原版锁定目标，修复选敌和表现”，不改为无目标方向弹。

| 问题 | 证据与修复 | 验收 |
|---|---|---|
| Q 无火球 | 用户日志显示 `skill.cast.targetMissing`，未进入 Q。保持 6 单位锁定目标要求；在真实 HUD 测试中放置范围内敌人，不传显式目标，观察 `Vfx_90033001` 与实际扣血。 | 直接施法分别覆盖显式/自动选敌；真实本地 HUD 验证自动选敌和火球出现。此处没有修改 Q 玩法算法。 |
| W 有表现无伤害 | `MobaActionTargetRequest` 调用查询服务时漏传已有 `AimDirection`；HUD 只提交方向、AimPos 为零时，扇形误朝世界原点。模板及内联查询均传递既有方向快照。 | W 用例把施法者移离世界原点，分别验证提供/不提供 AimPos；扇形内扣血，扇形外不扣血。真实 HUD 也断言释放后扣血。 |
| E 盾特效不可见 | 软链接原 prefab 的 `E_Shield` 父节点 `m_IsActive=0`。本地 `Resources/moba/AnnieShield.prefab` 是引用原 prefab 的 variant，仅启用此节点；`T-特效表.xlsx` 的 90033004 改引该 variant。 | Buff Cue 消费和真实本地 HUD 均验证 `E_Shield.activeInHierarchy`，不再仅检查 prefab 或 ParticleSystem 存在。未修改原软链接资源。 |
| 无敌人时不跟随 | 行为树索敌半径原为 1000，远处敌人也持续占用敌人分支；Authoring 调为 12，通过 BT CLI 正常发布。 | 真实 HUD 验证敌人在感知范围外时，主人离开 5 单位停止范围后，召唤实体及可见模型均向主人移动；敌人靠近后实际扣血。 |
| 追到射程边缘却不攻击 | 实测召唤物位置 `(5.999999,0,0)`、目标 `(14,0,0)`，距离 `8.000002`。BT 的施法范围为严格 `<=8`；`MobaBrainOutputApplySystem` 却在 `8+0.0001` 提前停止。移除移动层额外容差，恢复请求的 StopRange 契约。 | 保留追击后实际扣血断言；等待窗口收敛到召唤生命周期内，避免先超时销毁再报告追击结果。 |

测试纠正：第一次本地跟随测试把主人移到距召唤物 4.55 的位置，已在 5 单位停止范围内，不能期待继续移动；现用相对位置保证初始距离 10，再检查距离减少和模型跟随。该纠正没有放宽生产行为断言。`annie-real-hud-v6.xml` 的真实本地 HUD 全链路已通过，追击边界用例仍失败并准确输出上述距离，随后才实施停止距离修复。

发布源为 Excel、BT Authoring 和引用软链接的 prefab variant；没有手改 Luban/BT 生成 JSON。Luban 最终 `--check-published` 通过，日志 `local/Logs/annie-local-final-publication-check.log`。

本批验证结果：

- 图形设备启用的 Unity EditMode：`AnnieSkillAcceptanceTests` 与 `AnnieUnityPresentationConsumptionTests` 共 **27 passed / 0 failed / 0 skipped**，报告 `local/Logs/annie-real-hud-final.xml`。包括真实本地 HUD 全链路及曾复现失败的追击边界。真实模型截图 `local/Logs/annie-local-tibbers.png` 已查看；仍可见模型对浅色地板对比不足，不作为最终美术验收。
- .NET：配置、形状查询、查询工厂、召唤行为、ActorIntent、七英雄第一技能共 **56 passed / 0 failed / 0 skipped**，报告 `local/Logs/annie-real-hud-final-regression/annie-real-hud-final-regression-v3.trx`。
- 并行回归先后复现两处已有进程级集合竞争：`MobaBTreeAssetLoader` 的 Dictionary 写入和 `MobaBattlePayloadFields` 的 HashSet 注册。前者换成 ConcurrentDictionary，后者对注册/读取使用同一锁，保持原缓存键及字段注册语义；未串行化测试或删除失败用例。最终 56 项在原并行方式下通过。这两项最后的集合保护由 .NET 编译和回归覆盖；27 项 Unity 报告生成于最后一项集合保护修改之前。

本批尚未宣称一比一完成：W 按下喷火的预览契约待下节确认；提伯斯仍复用当前 8 单位射程的普攻，未在此批改成最终近战数值/独立攻击配置。模型可见、逻辑伤害、模型移动分别验收，不将其等同于所有攻击动画和最终美术均已验收。

## 2026-11-11：W 预览与战斗特效的契约冲突（待确认）

- 事实：`BattleHudAimPreview.ShowPressed/Tick` 从 `Skill.PreviewVfxId` 创建按住预览；安妮 W 当前绑定的 `90033002` 是战斗喷火锥体。W 的正式释放表现另由 Area Cue 创建同一特效。按住时已经出现喷火不能证明释放，更不能证明伤害。
- 冲突：HUD 已支持 `PreviewVfxId=0` 时只绘制几何指示器；但是 `LubanConfig/Moba/business/skills.py:validate_character_skill_preview_vfx` 与 `MobaPresentationResourceTests` 强制所有英雄技能的该字段必须大于零，阻断这种已有组合。
- 建议：允许 `PreviewVfxId=0`，仅在引用的按钮模板确实配置有效几何指示器时通过发布校验；正数继续校验 VFX 引用存在。W 使用既有 90°/5.5 半径扇形，战斗特效仍只由正式释放后的 Area Cue 消费。不新增字段、英雄运行时分支或由 HUD 补发效果。
- 影响：技能发布规则、对应配置校验测试、Unity 资源测试、W 的 Excel 预览引用及正常发布物。战斗 Flow、Trigger、快照/回滚和伤害时序不变。
- 验收：按下和拖动期间有指示器但没有战斗 VFX/伤害/扣费；抬起后恰好一次释放与伤害；取消无战斗结果；非法模板或悬空正数 VFX 引用仍拒绝发布。
- 状态：待用户明确确认；发现冲突后已恢复原 W 预览配置，未修改校验器或发布该变更。Q 用户已明确确认继续原版锁定目标，属于独立工作。

## 2026-11-11：再次手测反馈后的逐帧验收（进行中）

用户再次报告 Q 飞行表现不正确、W 抬起不见伤害、R 不跟随或攻击。本次日志留存于 `local/Logs/annie-user-local-1111-next.log`：Q 已进入 `shoot_projectile`，W 三次目标查询均包含敌人，召唤物也反复提交了普攻。以上仅是链路证据，不能替代实际 HP、HUD 与动画验收。

机制映射：本批排查既有 Projectile → Actor → 快照 → VFX 跟随，以及 Damage → 快照 → HUD 和 Brain → 移动/施法 → 模型动作；不新增英雄身份分支，不修改索敌优先级或 W 预览准入规则。

已复现的通用错误：`MobaProjectileTickSyncHandler` 只在周期玩法 Tick 时同步 Actor Transform，而 Q 的 `TickIntervalMs=0`。根据 `08-GameplayModules/04-ProjectileSystem.md`，周期 Tick 是可选玩法事件，项目层另负责 Actor 投影；位置同步不能依赖是否配置周期效果。修复为每帧按 ActorId 顺序读取既有 `IProjectileService.TryGetRuntimeState` 投影位置/朝向，Tick 仍仅按既定频率执行 Trigger，不给 Q 补造 Tick 或改变事件协议。真实 HUD 新增飞行距离、伤害后的 HUD HP 和提伯斯 Animator 断言；最初的飞行断言已经稳定复现失败，仍在核对表现侧后续断点，尚未将本批标记为验收通过。

逐帧证据 `annie-second-hud-diagnostic-v7.xml`：Actor 和表现 `BattleTransformComponent` 均从 Z=0.7333 推进至 4.4，VFX 的 Attachment ActorId=5、Follow=true，且确实进入 VFX Tick。HUD 的插值时钟却比当前帧落后 0.2 秒（例如 frame=7909、LogicTime=263.633347、RenderTime=263.433347），相当于 6 帧。Q 在约 6 帧后命中并清理，插值一直取首个样本，故实际火球停留在出生处。这是位置同步修复后仍然存在的第二个断点，不能归因于资源未加载。

W/R 独立验收已执行至最终结果（`annie-second-hud-diagnostic-v4.xml` 至 `v7.xml`；场景最终因 Q 飞行断言失败，不算整条测试通过）：Q HP 50000→49943.33；W HP 49943.33→49896.67，权威 HP 与实际 HUD handle 一致。提伯斯通过无敌人时模型/逻辑跟随、近敌实际扣血及 `Void_Annie_Tibbers_Attack1` 动画断言，皮肤贡献 1379 像素。W 单次减少约 46.67，在 50000 MaxHp、80 像素血条中仅约 0.075 像素；需要区分数值已生效和肉眼难以观察。R 仍为既有“感知范围内优先攻击/追击，无敌人时跟随并在 5 单位内停下”，感知半径 12、攻击射程 8；未悄悄改变这组行为优先级。

### 投射物特效时间策略（待用户确认）

- **建议**：对于既有 `PresentationAttachmentKind.Projectile`，由当前投射物快照的 `BattleTransformComponent` 解析位置/朝向；不经角色外壳插值缓存。`Actor/Socket` 挂接继续沿既有模型/挂点和插值消费。保持 Spawn/Hit/Exit 的权威事件、伤害时序和句柄清理不变，不加英雄或技能 ID 分支。
- **落点**：`BattleVfxFollowController.SyncPresentation` 与 `BattleVfxFollowTargetPositionResolver` 的位置选择；使用已有 AttachmentKind，不新增协议、表字段或玩法算法。投射物没有角色壳时的逻辑投影已存在，但目前先读取 Binder 对所有实体采集的延迟位置。
- **替代**：完整保留投射物延迟插值，同时按 RenderTime 调度其生成、命中和退出。该方案涉及表现事件排队、短生命周期、去重和恢复，范围更大，不能只延迟销毁特效掩盖生成时间不一致。
- **影响**：所有使用 Projectile 挂接的战斗 VFX；角色挂点特效、战斗模拟、回滚状态和协议格式不变。需要验证朝向变化、短距离命中、目标移动、取消/退出清理，以及不同英雄的投射物消费。
- **验收**：真实本地 HUD 保持 6 帧插值配置时，Q 仍实际飞行超过 1 单位后命中，移动目标时朝向/位置追踪正确；W 的 HP、fillAmount、飘字与 R 的模型跟随/攻击动画同场景验证；共享空间/生命周期消费回归。
- **状态**：已向用户提交明确选项，等待确认；本节所述表现策略尚未实施。仓库规范要求修改既有机制的时序先确认，不能把“继续修英雄”视为该策略已批准。

本批共享逻辑回归：投射物控制、真实 Modifier 投射物/命中及回滚、Luban 配置与通用英雄 AI，共 **42 passed / 0 failed**，`local/Logs/annie-second-regression/annie-second-projectile-regression.trx`。

### 独立工程复验与资源画面调查

- 初次整批 `local/Logs/annie-second-final-audit.xml` 为 **28 total / 25 passed / 3 failed**：真实 HUD 的 Q 飞行距离断言失败，另外两项模型动作测试因独立 Unity 工程的当前目录不在仓库祖先目录下，无法找到 Authoring 测试夹具。该报告不能记为验收通过。
- `MobaCharacterHfsmTestFixture.SourceLoader` 现同时识别 Unity 工程下的 `Packages/...` 与仓库下的 `Unity/Packages/...`，继续读取同一正式 Authoring 源，没有硬编码工程名称、复制夹具或放宽动作断言。路径修复后的模型动作/资源消费测试 **10 total / 10 passed / 0 failed / 0 skipped**，`local/Logs/annie-resource-and-actions-audit.xml`。
- Q 资源的图像测试仅证明在 0.03 / 0.1 / 0.2 秒内存在可见像素，**不证明画面正确**。逐渲染器隔离图 `local/Logs/annie-q-renderer-grid.png` 的第 2 格将黑色方块定位到 `PS_Smoke`，材质为软链接资源 `Smoke_5_Stars_4`，Shader 为 `Custom/ParallaxBackground`。其他旋光、闪光、前后光晕能渲染；不能据此误判为所有依赖缺失。
- 已检查该 Shader 的透明混合与 dissolve/alpha 计算，尚未通过替代材质对照证明具体数值缺陷，不直接修改外部 `game-res` 源。后续应以本地资源 variant/材质完成对照修复并正常发布引用，不能用关闭烟雾或只比较像素数量宣布完成。
- 真实 HUD 测试已强化到权威 HP、实际 HUD HP、`HpFill.fillAmount`、伤害飘字与提伯斯攻击 Animator；Q 的飞行距离仍作为整条场景最终断言，确保 Q 失败时 W/R 的独立检查仍执行。临时反射日志和逐渲染器诊断代码已移除，保留图像/报告证据。

本轮最终复验：

- 图形设备启用的独立 Unity 工程：`AnnieSkillAcceptanceTests` 与 `AnnieUnityPresentationConsumptionTests` **28 total / 27 passed / 1 failed / 0 skipped**，`local/Logs/annie-second-reviewed.xml`。唯一失败仍为真实 HUD 的 Q 飞行距离（预期 >1，实际 0）；没有删除断言或将其标记忽略。整批尚未通过。
- 同一真实 HUD 场景中，Q HP **50000→49943.33**、W HP **49943.33→49896.67**；W 的 HUD 数值/填充和飘字，以及 R 的逻辑与模型跟随、实际伤害、`Void_Annie_Tibbers_Attack1` 均在最终 Q 断言前完成。提伯斯皮肤截图贡献 **1381 像素**，不代表最终美术质量通过。
- 夹具路径修复后再跑 .NET 配置与通用 AI：**28 passed / 0 failed / 0 skipped**，`local/Logs/annie-reviewed-regression/annie-reviewed-config-ai.trx`；加上前述投射物等 42 项回归，分别保留报告，不重复合计成不同测试数量。
- 日志仍出现 `emit emitter config not found. emitterId=400001`；当前伤害/攻击动作已发生，不能将其等同于所有派生表现链均正常，后续需继续从权威源追查这条引用。本轮未用临时 emitter 或删除校验来遮盖该警告。
- 本轮只改运行时位置投影与验收夹具/断言，未改 Excel、Trigger、Flow 或 BT 生产源，因此没有为这些改动重复发布配置。Q 特效时间策略和 W 预览契约仍待原提案的明确确认；尚未修复 Q 烟雾材质方块，不将本轮描述为三个手测问题全部解决。
