# MOBA 世界逻辑时钟与最近战斗时间的本地回滚方案

状态：已确认并完成本批实现；真实同输入的已登记战斗状态字节/hash 一致，表现事件验收仍待推进，P2 未通过。日期：2026-11-10。用户在本方案提出后回复“按推荐的下一步继续排查推进”，确认 WorldClock＋最近战斗时间两个配套本地 provider、显式时钟恢复、正式 registry required-state 和真实同输入回归升级；表现事件重演及网络恢复语义不在本次范围内。

## 本批已完成的独立工作

按 49 号下一批要求，新增 `MobaRecordedInputReplayTests`，在默认完整 Console MOBA World 中增加安妮与目标，先正常初始化，再捕获正式 registry 检查点。保存 `PlayerInputCommand` 的 frame、player、opcode 和原始 payload；两次重演都从文件读取完全相同的记录，通过现有 `IWorldInputReplaySink.BeginReplay/Replay/EndReplay` 与 Console 固定帧 Tick 执行。

输入包含同一 prediction key 的重复包，用已有 action journal 防止重复技能提交。每次故意将目标 HP 改为 1，再通过 `TryRestoreSimulationFrame` 恢复，验证 HP/法力恢复。每条执行路径的安妮 Q 都恰好产生一次相同帧、相同数值的伤害，终点 HP/法力相同；这些断言不能替代全部状态和外部事件验收。

检查点落盘暴露 `WorldRollbackSnapshotEntry` 缺少 `[MemoryPackable]`，而已有外层 snapshot 和 codec 已声明序列化该条目。已补声明，未新增字段/版本或网络语义，并新增条目 key/任意 payload 字节往返测试。真实 World 的落盘快照也验证解码、重编码字节一致。

## 真实重放差异

固定 30 Hz，checkpoint frame 19、输入 frame 23、Q 伤害 frame 25、终点 frame 79。与不扰动基线相比，两次重放均出现：

| 项目 | 结果 |
|---|---|
| 已登记 provider 的其他状态 | 字节相同 |
| 联合 key `10011` | `continuous` 与 `continuous-local` 两个 section 不同 |
| 具体成员 | 目标 actor 的一个 `MobaTriggerIntervalContinuousRuntime` 缺失；实例身份来自场景实际捕获，不用于共享执行分支 |
| actor roster、HP/资源、技能及 Buff 等其余联合 sections | 相同 |
| 最近战斗时间 | checkpoint 没有目标条目；Q 命中后记录 `0.8306669`；第一次恢复仍保留该时间，没有恢复 checkpoint 时的“未记录”状态 |
| `IWorldClock` | checkpoint 为 `0.63066673`，基线终点为 `2.6306655`；第一次恢复后仍为 `2.6306655`，重演终点变成 `4.630664`；第二次重演继续累加到 `6.630662` |
| 表现事件观察 | 基线 5 条、每次重放 8 条；Counter cue 首次为 Started，重演为 StackChanged，并多出 3 条 Tag Started。原始事件全部保存，不过滤/重编号后宣称相等 |

最初的正向全状态相等断言已实际失败，并据此定位原因。当前测试明确命名为 `Annie_q_same_input_replay_reports_unrestored_combat_activity_as_p2_failure`，属于**已知缺口的复现回归**：它检查上述精确差异与未回退的时间事实，不是 P2 通过门禁。报告仍输出 `stateEqual=false`、`verdict=StateMismatch`、`reliableEventDeduplicationVerified=false`。确认并实现后，应将缺口断言升级为全状态相等，不保留永久的“允许缺失一个持续体”。`baselineHash/replayHash` 是当前已登记 registry 的完整字节 SHA-256，不声称覆盖尚未登记的时钟/战斗事实；这些遗漏通过报告中的独立观察明确列出。

## 因果链与既有契约

1. `Services/Combat/Damage/DamagePipelineService.RecordCombatActivity` 对有效伤害记录攻击者和目标。
2. `Services/Combat/MobaCombatActivityService` 使用 `Dictionary<int,float>` 保存最后战斗时间，时间来自 `IWorldClock.Time`。没有记录时按既有规则视为已脱战；有记录时比较当前时间与最后时间。
3. `Services/Continuous/Triggering/MobaTriggerIntervalContinuousService.CanKeepActive`、`MobaTriggerIntervalContinuousHandler.CanRunInterval` 和资源转治疗动词读取这个服务。
4. 当前 `Rollback/` 没有捕获/恢复该字典的 provider；49 号组内 sections 也不包含它。
5. 进一步查证 `world.di/Runtime/World/Services/WorldClock.cs` 是独立的 float 累加时钟。Console 的 `TryRestoreSimulationFrame` 对齐 Context 与 `IFrameTime`，没有恢复 `IWorldClock.Time/DeltaTime`；现有 registry 也不拥有后者。不能把 `FrameTime` 已恢复推断为所有消费者的时间都已恢复。
6. 因而重演开始时，目标仍带着上轮命中的战斗记录，`IWorldClock` 也没有回退；它们的时间差尚不足配置的脱战阈值，目标持续体提前结束。这是两个遗漏的状态输入，不能只修一个便承诺重放等价。

现象不能通过调整 Annie/目标 ID、删除默认英雄或脱战持续体、清空所有战斗记录、忽略 Continuous 差异解决。必须明确这个既有领域状态的回滚所有权。

## 推荐的最小通用方案：两个独立本地状态 provider

本方案为 `IWorldClock` 与最近战斗时间增加配套的本地回滚所有权，不扩大 49 号生命周期重建范围。最初提出时两项配套范围均需明确确认；用户本轮答复已批准本节范围，确认记录见文首。

### 数据与所有权

- 新增 `MobaCombatActivityRollbackProvider`，由 `MobaCombatActivityService` 提供内部只读捕获与整表恢复接口。只持有最近时间事实，不引入英雄/技能身份判断。
- 使用独立版本化 payload，保存 frame、完整 actor roster 和按 actor ID 排序的已记录条目。没有条目与“时间为零”必须区分；不为从未参战的 actor 制造时间条目。
- 时间保留现有 float 的原始位模式，不在本次迁移为定点或改变脱战阈值规则。候选 registry key `10045`，实施时检查正式清单唯一性。
- 增加 `MobaWorldClockRollbackProvider`（候选 key `10046`），捕获并恢复当前具体 `WorldClock` 的 `Time/DeltaTime` 原始 float 位模式。框架 `WorldClock` 增加显式状态恢复接口，在同步恢复点赋值，不用负 delta 的 `Tick`、反射或把时间重新算成 `frame * delta`。不改变每帧累加算法，也不把独立 clock 偷换成 `FrameTime` 适配器。
- 两个 provider 都通过正式 registry 捕获/预检/导入；只允许可显式恢复的既有 `WorldClock` 实现。正式战斗若时钟不满足契约应明确拒绝，不默默漏登记。检查 `FrameTime`、Context 和 World clock 各自恢复到其 checkpoint 值，不能强行令数值精度不同的时钟值相等。
- 状态仅在本地 rollback provider 中恢复；不新增网络 DTO、修改状态同步快照、ACK/epoch 协议或服务端重连入口。网络权威恢复是否需要此事实另行审查。

### 预检、恢复与失败

- 非变异预检检查格式/版本、目标帧、相同完整 roster、actor 引用合法、重复 actor、条目顺序/数量及有限非负时间。记录时间不得晚于该 payload 捕获的逻辑时间；捕获时间只作校验，不由该 provider 恢复时钟。
- 构造并验证目标字典后同步替换整表，删除预测阶段新增记录，恢复目标已有记录。准备失败不改 live；交换提交不执行伤害、清理、Continuous reconciliation 或任何 gameplay/表现事件。
- 只在所有 provider 完成之后允许下一帧/重演；两个 provider 在普通值恢复阶段接入，实施前复核联合结构准备/提交没有依赖当前 clock 做目标时间推导。不增加跨 provider 目标读取协议；不得在恢复回调里执行依赖尚未恢复状态的 Tick。
- 正式 MOBA registry 登记一次并列入 required-state，缺失时明确失败；不静默跳过。旧的缺少新 provider 的本地快照拒绝，不伪造默认状态。
- 外层仍遵守 coordinator 的部分恢复报告，不增加全 World 原子事务承诺。

### 验收

1. 未记录/已记录/预测新增/预测移除/多个 actor 的恢复，精确比较时间位模式；重复恢复相同。
2. 损坏、重复条目、未知版本、非法 actor、非有限/未来时间在预检拒绝且 live 不变。
3. 不改变 `RecordCombat`、脱战阈值边界、actor 清理及现有使用者行为。
4. 独立 clock 捕获/多帧推进/恢复/下一帧、零 delta/非法值/重复恢复，验证 Time/DeltaTime 的原始位模式；场景的两个重演终点 clock 均应与基线相同，不继续累加。
5. 真实安妮 Q 相同输入两次重演的完整 registry 字节/hash 相等；目标持续体身份/计时/捕获一致。另一个 actor/source 组合验证同一语义，不按内容身份分派。
6. 保留原始输入、检查点和差异报告，旧缺口测试升级为正向断言。

## 表现与可靠事件：本批只记录边界

`MobaPresentationCueSnapshotService` 的 active store、pending events 和序列不在当前本地 registry 中；Counter reporter 会读取 active cue 决定 Started/StackChanged，因此回退 Counter 值不等于回退 cue 的派生状态。多出的 Tag 事件还需分别核查首轮和恢复后的有效标签缓存/通知顺序，不能仅凭数量推断根因相同。

已有 `MobaReliableBattleEventCursor` / `ReliableEventDeliveryRuntime` 按 battle/epoch/sequence 过滤**同一批网络事件重传**；它们不能自动把模拟重新生成并得到新序列的事件认作旧事件。Cue reconciliation 对 stable key/revision 的幂等也不能替代可靠事件生产端的重演提交契约。当前场景没有瞬时视觉资源输出，不能用无视觉 cue 的通过宣称 Unity VFX 不会重复。

本方案不授权新增表现快照字段、静默清空 cue、冻结/回退可靠网络序列或生产端重演事务。先修复战斗状态并验证确定性，再对确实仍存在的表现/不可逆事件缺口，基于真实生产/消费链提出独立方案。**即使战斗状态相等，事件验收前 P2 仍未通过。**

## 提案时的确认项与验证（历史记录）

独立验收已重新编译执行：MOBA 定向 **206/206**、FrameSync **21/21**、既有网络故障矩阵 **7/7**。其中新的真实重放测试是缺口复现回归，报告继续为 `StateMismatch`。详细命令范围、日志与 artifact 见 [47 号实施记录](47-MobaRealSkillRollbackReplayAcceptance.md)。

推荐先批准上面的 **WorldClock 与最近战斗时间两个独立本地 provider、WorldClock 显式状态恢复接口、正式 registry required-state 及真实同输入回归升级**。继续保持相同 actor roster / 非 actor Blackboard owner 与订阅集合，不改变时钟推进和脱战算法，不扩大网络恢复或表现重演语义。

提案时根据仓库 `AGENTS.md`、`Docs/MobaMechanismGovernance.md` 与 `.claude/skills/moba-skill-authoring/SKILL.md` 等待新增状态所有权的确认；49 号批准未被用来代替本方案批准。确认后才实施以下内容。

## 本批实施与真实重演结果（2026-11-10）

### 通用机制和接线

- `MobaCombatActivityRollbackProvider` 使用唯一 key `10045`；版本 1 本地 payload 包含 frame、完整有序 roster、捕获时钟值和有序 actor/time 条目。服务只暴露内部副本捕获与已验证整表交换；保留条目缺失与零时间的区别。预检和导入均检查格式、版本、帧、roster、actor 存活、顺序/唯一性及有限非负/不晚于捕获时钟的时间，拒绝尾随字节。准备失败不修改 live。
- `MobaWorldClockRollbackProvider` 使用唯一 key `10046`；版本 1 本地 payload 保存 frame 和原始 float `Time/DeltaTime`。框架 `WorldClock.RestoreState` 先验证两个数值再赋值；不调用 Tick、不改变时间累加算法，也不从帧号推算时间。未知时钟实现的正式战斗明确拒绝并报告实际类型。
- 正式 registry 登记两项并声明 required-state。框架 registry 增加通用 `RequireState(key)`；coordinator 在任何预检/导入之前要求对应条目恰好一次，空快照、遗漏或重复均拒绝。不声明要求的 registry 保留原有部分快照契约。没有新增网络字段或改变网络 ACK/epoch。
- 联合准备/提交只恢复原始计时和投影，不从 live WorldClock 推导目标计时；两项 provider 在普通值阶段导入，正式入口完成整个 coordinator 恢复后才允许下一帧。Context/FrameTime 与独立 WorldClock 分别恢复自身精度下的 checkpoint 值。

### 同输入验收进一步发现并修复的既定契约遗漏

补齐两项状态后，目标脱战持续体已恢复，但正向完整比较暴露 key `10002`/`10013` 的 HP 差异。原始伤害事件表明同帧 Q 的伤害从 `56.666668` 变为 `56.478405`。资源 map 已回退，`AttributeContext` 中 `resource.rage.ratio` 却仍为上一轮命中的投影值，资源依赖的通用 Modifier 因而读取旧输入。

正常 `MobaResourceMutation` 的 Set/Consume/Refund 都调用既有 `MobaResourceAttributeContextProjector.Refresh`；资源 rollback 导入遗漏此步。本批在完整恢复 actor 的资源 map 后复用同一投影器，恢复明确既定的资源→属性上下文契约，不新增状态维度或英雄分支。真实场景增加恢复后资源比例断言，并保持完整 provider 字节、伤害数值和帧比较。

### 验收证据

`Annie_q_same_input_replay_restores_exact_registered_state` 已替换原缺口复现。使用两组真实 source/target ID：`7→8` 与 `8→7`；默认其他英雄、配置和持续体都保留。每组从 frame 19 checkpoint 开始，读取同一份落盘输入，在 frame 23 提交（含重复包）、frame 25 命中、frame 79 比较终态，每组连续恢复并重演两次。

四次重演的结果均为 `stateEqual=true`、`verdict=StateEqual_EventDeliveryPending`；全部已登记 provider 原始 payload、联合十个 section、完整 snapshot 字节/hash、伤害帧/数值、HP/法力、最近战斗时间和 WorldClock 位模式均与对应基线一致。目标持续体没有缺失，checkpoint 恢复后立即重新捕获的完整 hash 也相等。正式 registry 的缺少两项状态的旧快照在任何导入前拒绝，live hash 不变。

两组终态 hash 分别为：

```text
7→8: 4286FB35044523CD822131C62ABF9F35376A4E97A904E78794DE151619F119C7
8→7: 6E75F3A6EAAE7DC75E0AF0F6A5DF904A8E988E219B6AA8B9AB179079D981F15B
```

哈希用于比较各场景自己的基线与重演；不要求不同内容场景的哈希相同，也不将 registry 未覆盖的表现/外部提交状态计入已验证范围。

重新编译执行的测试：MOBA 定向 **236/236**（含新 provider 边界与两组真实重演）、资源/Modifier 真实对象冒烟 **12/12**、FrameSync **25/25**（含 required-state 前置拒绝与兼容性）、World DI **39/39**、既有网络故障矩阵 **7/7**。命令与日志见 [47 号最新记录](47-MobaRealSkillRollbackReplayAcceptance.md)。本批没有运行 Unity 表现验收或服务端断线重连。

artifact 在 `local/Logs/moba-recorded-input-replay/source-7-target-8/` 与 `source-8-target-7/`，分别保存输入、checkpoint/baseline/perturbed/replay 快照、每个联合 section、解码后的 HP/资源值、原始事件和两次报告。所有快照验证解码再编码字节一致；未过滤或归一化差异。

### 剩余事件边界与下一步

四次重演的原始事件仍都是基线 5 条、重演 8 条；Counter 的 `Started(20)` 变为 `StackChanged(23)`；frame 46 多出三个 Tag Started。第一次重演 cue Sequence 为 5–11，第二次为 12–18。`reliableEventDeduplicationVerified` 保持 false，不能将状态一致改写成 P2 已通过。

已继续只读核查：Counter 动作通过 `MobaPresentationCueSnapshotService.TryGetActiveCue` 决定阶段，服务保留的 active store 会直接影响下一次 Report；`NormalizeMetadata` 给每次新报告增加 Sequence/Revision。Tag reporter 则持有独立 `_lastTags`，49 号恢复会从有效标签重新设置比较基线；首轮与重演的通知/缓存顺序还需逐帧跟踪，不能把三条新增 Tag cue 全归因于 active store。`GetEffectiveTags` 返回副本，并非共享容器被覆盖的问题。

下一批建议先制定表现生产端的本地重演契约：区分可回退的模拟 cue 状态、派生标签比较基线和已对外提交的事件/投递游标；明确持久 cue 的目标集合对账、瞬时事件的稳定身份及重演期间缓冲/提交/取消，然后接既有可靠游标验证最终实际消费次数。不能仅回退网络序列、清空 cue、过滤 Tag 或忽略 Revision 来让测试变绿。该契约超出本方案授权，须在具体方案经确认后实施；本批只记录事实与实施入口。
