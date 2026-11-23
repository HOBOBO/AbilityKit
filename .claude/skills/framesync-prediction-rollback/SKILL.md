---
name: framesync-prediction-rollback
description: AbilityKit 帧同步预测、回滚、对账与 CatchUp 的开发和排查；为 ECS 与非 ECS 业务补齐可选回滚支持时使用。覆盖 System 帧驱动、组件与 Provider 状态恢复、Command 结构回退、静默重绑和重演验收。
---

# 帧同步预测与回滚

客户端帧同步（预测 + 回滚 + 对账 + CatchUp）速查与工作流；业务回滚契约按 2026-11-17 用户确认补充。下文历史实现细节仍需以当前源码核实。

## 业务回滚接入：先判定启用与归属

涉及投射物、移动、技能阶段、调度、对象生命周期或业务 Provider 的回滚处理时，先读 [业务状态回滚契约](business-state-rollback.md)。MOBA 还须遵循 [机制治理](../../../Docs/MobaMechanismGovernance.md) 与 [技能开发规范](../moba-skill-authoring/SKILL.md)。

- 回滚是可选扩展。业务提供恢复能力与当前会话启用回滚是两件事；MOBA 帧同步所用机制须提供支持，关闭时仍可正常运行，不因回滚专用注册、快照或 journal 缺席而失效。
- Entitas WorldSystem 统一帧驱动，可调用非 ECS Service/Runtime 的确定性 Tick；状态按既有归属用组件或 Provider 恢复，不要求将 Launcher、计时器和服务全部迁入 ECS。
- 回滚只沿状态恢复与类型化 Command 结构回退两条路径；运行时引用和索引静默重绑。完整帧末 N 恢复后，从 N+1 通过正式 System 树重演输入，不补发历史玩法事件。
- 仅在接入回滚或修改已声明支持回滚的模拟链时，按业务契约第 2.1、5 节提供受影响状态账本，分清权威状态、派生/临时数据和运行依赖；普通业务不受回滚专用建模约束。接入时诊断具体不支持项并要求修复，不能启用部分恢复。恢复不得借正常激活/停用入口结算玩法，不得吞捕获异常或忽略恢复失败；保留已有结构资格和失败门禁。

按已确认契约补齐遗漏状态可继续；新增/改变机制、所有权或重建范围仍按治理文档确认，既有批准不重复询问。本 skill 记录目标契约，不代表当前全部业务已经具备完整回滚支持。

## 适用场景

- ECS / 非 ECS 业务接入可选回滚，审计延迟任务、分配器、对象创建/销毁和恢复回调
- 预测推进不工作 / 卡住（`predicted` 不前进）
- rollback 未触发或触发风暴（输入差异 rollback、hash reconcile）
- reconcile mismatch 不出现或帧号不对齐
- idealFrame gate 导致 stalls 或 prediction window 被压缩
- 多 world 场景下统计混在一起、无法定位
- 服务端回滚（`ServerRollbackModule`）或服务端驱动（`FrameSyncDriverModule`）问题
- CatchUp 子系统（`FrameSyncCatchUpPolicy`、`WorldStartFrameCatchUpCalculator`）

## 关键架构事实

### ECS 组件层通用序列化回滚（2026-10-12 起）

- `Generated/Actor/ActorComponentSerialization.cs`（**生成代码**，工具 `tools/AbilityKit.Entitas.CodeGen`）承担值组件及有限纯字段托管数据图的 Write/Restore；`MobaEntitasComponentRollbackProvider` 保留编排、身份预检和派生重绑外壳，并委托生成代码恢复组件字段。
- **覆盖声明**：pin 文件（`tools/entitas-codegen.moba.pin.json`）的 `coverage` 声明组件回滚去向（provider:&lt;名&gt; / immutable / projection / transient），豁免必填、缺失报错；`serialization.mode=auto` = 默认全量+豁免制。以生成器 `--validate` 的实际矩阵为准；声明不验证 Provider 已注册或全部字段已覆盖。`[EntitasBlob]` 既是风险标记，也是有限字典 codec 的显式准入条件；仍须通过形状校验，不自动注册 Provider。
- A+B 已迁移 Transform/MoveInput：组件 payload key=10018/v3 起由生成器唯一恢复完整 Transform3、MoveInput 及其存在性（当前版本见资源批）；原 ActorTransform 已删除。`MobaActorMotionRollbackProvider` key=10001/v5 仅恢复速度/时间与完整碰撞资格，组件导入后静默投影位置/朝向。联合生命周期当前 10011/v8、checkpoint schema=5：`locomotion-local` 为每个有 Pipeline 的 Actor 显式保存空槽或 Input/Speed/Space/Priority；同代存活 Actor、原初始化 Pipeline 上允许 Locomotion 槽位纯骨架重建，身份为 `(ActorId, EntityVersion, Locomotion, 0)`。与受管 FixedDelta 共存时仍由 `PipelineOrder` 唯一保存完整次序；FixedDelta 保留同实例门禁。恢复不取样属性/门禁、不补历史事件，失败补偿恢复原引用、值、绑定和次序；标准结构顺序导入前核验。旧 v7 及以前、过期/外来绑定、Pipeline 替换及未支持来源明确拒绝。不包含 Path/Trajectory/CollisionDash、已结束 FixedDelta 或 Actor/Pipeline 重建。范围和依赖锚点见 `plans/MobaLocomotionSourceRosterDecision.2026-11-18.md`。
- 资源批将组件载荷升级为 key=10018/v4、checkpoint schema=3；ResourceContainer 的存在性、Initialized、Value/Map null/empty、null 元素及所有 scalar 字段唯一由生成组件恢复；HP key=10002/v3 仅恢复属性 BaseValue，旧资源 key=10013 已删除。上一条的 v3 为迁移历史，当前组件统一使用 v4。用户另行批准 D2.1：恢复资源派生上下文使用 `RefreshRestored` 和 `AttributeContext.GetValueSilently`，保留实时上限、缓存和依赖标脏，抑制本次同步求值中的三层属性通知，不补发历史通知。LastMax 不是动态上限缓存，不能替代实时属性；这不代表普通 Modifier 投影及全恢复链都已静默。详见 `local/docs/Moba资源组件统一回滚验收.2026-11-18.md`。
- `moba-ecs-rollback` 门禁包含生成基线/生成器及运行时组件、资源、真实输入重演、Motion、联合生命周期预检/补偿合同，已纳入 `regression`；修改字段后用独立非默认样本逐字段核验，覆盖存在性双向恢复，不只比较往返字节。Brain 等覆盖债务见 `local/docs/MobaECS回滚覆盖与验收收口.2026-11-18.md`。
- 后续 Brain 10006/v3、checkpoint schema=4：选择、Runtime 核心/当前输出、Decision、Manager 次序/allocator 和创建抑制均恢复，拒绝旧 v1/v2。仅支持同会话、同代存活运行时，未知值/依赖仅在显式恢复入口诊断，普通 State/策略扩展仍可运行；本地代数不参与 hash。Brain 不再借 Activate/Deactivate 恢复，不补发历史事件。门禁已加入核心、ML、正式 System 逐帧重演及零导入拒绝合同，详见 `local/docs/MobaBrain完整状态与可选回滚验收.2026-11-18.md`。不表示已完成跨名单重建或全部 Motion 状态。
- 工具入口 `tools/generate-entitas.ps1 -Project moba`；gate：`run_test_gate.ps1 -Gate entitas-codegen`。改组件定义后 `-UpdatePin`；有意改模板后 `-UpdateBaseline`。
- 设计对照（UnityLockstep/ET）与三档模式裁定详见 `tools/AbilityKit.Entitas.CodeGen/README.md`。

### 两套并行的客户端预测 primitives（重要）

| 集合 | 位置 | 使用方 |
|------|------|--------|
| 主集合 | `com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/`（15 个 .cs） | `ClientPredictionDriverModule`（标准客户端预测） |
| Generic 集合 | `com.abilitykit.host.extension/Runtime/Client/FrameSync/`（4 个 .cs） | `ConfirmedAuthorityWorldRuntimeFactory`（确认权威路径） |

两者**不要混用**。详见 [key_files.md](key_files.md)。

### ClientPredictionDriverModule 实现了 4 个接口（旧 skill 只知 3 个）

```csharp
public sealed class ClientPredictionDriverModule :
    IHostRuntimeModule,
    IClientPredictionDriverStats,
    IClientPredictionTuningControl,
    IClientPredictionReconcileTarget,
    IClientPredictionReconcileControl   // ← 新增，旧 skill 未覆盖
```

新接口 `IClientPredictionReconcileControl` 提供 `SetReconcileEnabled / TryGetReconcileEnabled / ResetReconcile`，Reconcile 调试面板有按钮调用。

### 关键 API 字段名修正

旧 skill 写的 `IdealFrameStalls` **实际叫** `TotalIdealFrameStalls`（long）。另有 `IsPredictionStalledByIdealFrame` / `IsPredictionStalledByWindow`（bool）和 per-world 读取 `TryGetIdealFrameStallStats` / `TryGetPredictionWindowStats`。

### `RecordPredictedHash` 在 ClientPredictionReconciler 上

旧 skill 暗示在 Driver 上。实际：Driver 在 `OnPostTick` 中通过 `ctx.Reconciler.RecordPredictedHash(...)` 调用。`ClientPredictionReconciler` 类位置：`com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/ClientPredictionReconciler.cs`。

## 分节索引

- [business-state-rollback.md](business-state-rollback.md) — 业务状态、可选启用、System / Provider / Command 边界及验收
- [when_to_use.md](when_to_use.md) — 何时启用本 skill（含服务端路径）
- [required_context.md](required_context.md) — 改动前必须明确的上下文
- [key_files.md](key_files.md) — Driver、Rollback primitives（两套）、CatchUp、服务端模块、Editor 面板（6 个）
- [procedure.md](procedure.md) — 排查步骤（含正确字段名）
- [examples_and_troubleshooting.md](examples_and_troubleshooting.md) — 典型故障速查

## 相关 skill

- 完整技能/触发/BUFF 模块速查见 [ability-kit](../ability-kit/SKILL.md)。
- Session/Flow 类代码重构（State/Handles/Controllers）见 [state-handles-controllers](../state-handles-controllers/SKILL.md)。
