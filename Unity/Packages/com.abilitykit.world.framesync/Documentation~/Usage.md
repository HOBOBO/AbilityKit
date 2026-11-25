# 帧同步回滚接入与限制

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：已核校本页命令、Provider 装配与恢复边界；不代表任意玩法均具备完整可重演状态。

## 先运行离线帧命令

按[本地帧命令示例](../Samples~/LocalFrameCommands/README.md)导入并执行 `LocalFrameCommandExample.Run()`，预期返回真：已齐备的空帧推进、同帧稳定排序、未齐备帧不推进，最后主动裁剪。环境及依赖见[包入口](../README.md)，命令缓冲默认值和所有权以[基础接入契约](README.md)为准。

## 装配可选回滚

1. 宿主列出完整模拟状态与运行依赖，分别提供状态捕获、结构恢复和静默重绑。安装 Provider 不等于当前会话已启用回滚；关闭回滚时普通执行链仍应可用。
2. 向 `RollbackRegistry` 注册稳定键的 Provider。同实例重复注册可接受，同键不同实例拒绝；捕获按 Key 升序。必须存在的载荷用 `RequireState` 声明，不声明时仍允许部分快照，不能从恢复返回真推断业务状态完整。
3. 如需结构恢复顺序，封存前以 `SetStructureRestoreOrder` 声明全部结构 Provider 的完整顺序；`RollbackCoordinator` 构造会封存并校验注册表。活动期间不 Clear 或重配注册表。
4. 在约定的完整帧末 N 捕获检查点。恢复成功后从 N+1 走正式世界执行链重演；输入资格、同帧顺序、空帧就绪、历史连续性与副作用抑制由宿主证明。

`RollbackSnapshotRingBuffer` 存入和读取均复制条目及 payload；公开 `Capture` 返回独立条目数组，但 Provider 导出字节的独立性仍需自身保证，不能把所有捕获入口统称为任意对象图深拷贝。

## 恢复准入和失败

协调器先检查快照版本、必需载荷、Provider 存在性及已实现的跨快照/单 Provider preflight，再执行结构恢复和普通字段导入。已声明的 preflight 失败发生在任何 import 之前；没有实现预检的业务状态不会被框架自动补齐资格检查。

导入中失败会停止后续导入并报告失败，世界可能已部分恢复，不自动撤销先前 Provider 的副作用。`TryCaptureAndStore` 将捕获/存储异常报告为失败，调用方必须检查结果；不能捕获失败后继续宣称此帧有有效检查点。诊断使用 `RollbackOperationResult` 与 `LastOperationResult`，不按异常文案分支。

`TryAdvanceRestoreFloor` 只提交调用方已经验证的锚点下界，要求本地检查点存在且不倒退；不验证该锚点是否属于权威轨迹。低于下界的恢复拒绝；ClearHistory 只清缓存，不重置已生效的恢复下界。新会话按所有权重建上下文，不能复用旧资格。

## 预测入口不可混同

### 可选帧时钟对齐

[`IFrameTime`](../Runtime/FrameSync/IFrameTime.cs) 保持只读。需要由预测宿主对齐的时钟可额外实现 [`IFrameTimeAligner`](../Runtime/FrameSync/IFrameTimeAligner.cs)，并将同一对象注册为 `IFrameTime`；只读时钟无需实现。`AlignTo` 仅按时钟自身固定步长调整帧号和时间，不执行世界模拟；步长未确定时使用正的备用步长，无法确定则返回假。

默认 [`FrameTime`](../Runtime/FrameSync/FrameTime.cs) 已实现该能力，仍以定点帧号乘固定步长对齐，不经 `float` 重建时间。基线对齐传入零备用步长，因此需要时钟已经确定固定步长。时钟的创建、固定步长和回滚状态仍由宿主及既有 Provider 管理。

### 两套预测调用路径

轻量 `ClientPredictionRunner` 的 hash 路径恢复当前比较帧的检查点；Host 的正式差异输入驱动使用差异帧前一帧检查点，二者不能描述为同一算法。轻量 runner 在重演缺输入时使用空数组，因此宿主必须事先证明历史齐备，不能让丢失历史冒充权威空帧。该 runner 也不能代替完整的会话恢复门禁。

帧同步按权威输入推进；状态同步按权威实体快照基线重建并导入。状态同步的 Snapshot ID、事件水位及输入终结证明不是本包确认帧；可共享世界执行规则，不能互换恢复策略。

## 来源与验证边界

源码：[注册表](../Runtime/FrameSync/Rollback/RollbackRegistry.cs)、[协调器](../Runtime/FrameSync/Rollback/RollbackCoordinator.cs)、[快照缓存](../Runtime/FrameSync/Rollback/RollbackSnapshotRingBuffer.cs)、[轻量 runner](../Runtime/FrameSync/Rollback/ClientPredictionRunner.cs)。本地示例与 Provider 预检等 .NET 测试仅证明局部契约；Unity、正式联机、所有业务 Provider 和跨平台确定性仍须分别验收。
