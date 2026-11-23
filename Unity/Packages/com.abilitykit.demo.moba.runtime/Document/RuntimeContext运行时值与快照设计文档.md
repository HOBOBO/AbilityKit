# MOBA Runtime Context 运行时值与快照设计（旧入口）

完整设计、适配器源码与局部测试证据已移到仓库 `Docs/design/09-ImplementationExamples/MOBA/MobaRuntimeContextSnapshotDesign.md`。仅安装本包时，请从[上下文模块指南](../Runtime/Application/Services/Context/README.md)查看包内职责与读写边界。

以下标题保留旧引用的锚点；详细规则见仓库专题的同名章节。

### 7.1 效果执行入口的观察快照

仓库专题 §7.1：效果入口记录及清理边界。

### 7.2 动作执行前后事实与实际 HP 提交

仓库专题 §7.2：动作观察及真实 HP 提交的覆盖范围。

### 7.3 未产生 HP 提交的伤害管线结果

仓库专题 §7.3：无提交时的终态记录和限制。
