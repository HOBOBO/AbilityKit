# Triggering 产品化验收指南

## 验收目标

| 维度 | 要求 | 验收方式 |
| --- | --- | --- |
| 即时主线 | 事件、条件、计划与动作可独立使用；消费方不加载 JSON、不装配调度、不调用 `Tick` / `Update` | 单独运行主线消费者，验证条件拒绝、同步顺序和退订 |
| 确定性 | 验证注册标记检查与[宿主重演责任](FormalApiBoundary.md#确定性边界)，允许注入可恢复状态的随机源 | 验证非确定性动作和条件函数不能通过检查；从同一随机流游标重新执行，比较分支顺序、执行结果及取样次数 |
| 计划动作 | 立即动作在派发中调用；延迟、周期等通过 `IFlatActionScheduler` 登记，由宿主推进实现 | 分别验证立即与调度路径，不把立即执行视为旧入口 |
| 可选执行树调度 | 宿主显式推进时间节点，即时样例不装配调度 | 执行树到期探针验证到期前不执行、到期仅执行一次 |
| 内置扁平动作调度池 | 动作实例及执行器经 `ActionSchedulerPools` 或显式无池路径管理 | 检查 `TriggeringRuntimePoolingHotspotScanTests` 是否覆盖新增热点 |
| 阶段收口 | 包说明、接入路径、测试命令与验收标准必须能从文档索引找到 | 检查 `StageClosureGuide.md`、`Documentation~/README.md` 与本指南是否同步 |

## 记录要求

- 验收报告只记录正式入口、正式边界和正式样例。
- 兼容入口与下线证据统一记入[正式边界清单](FormalApiBoundary.md)，不写入新接入示例。
- 新增能力必须先落到正式主线，再补充对应验证。
- 正式样例以 [`Samples~/FormalMainline/FormalTriggeringMainlineExample.cs`](../Samples~/FormalMainline/FormalTriggeringMainlineExample.cs) 作为最小可复制入口，通过 Package Manager 可选导入。
- 定时规则样例单独导入，见[样例盘点](SampleInventory.md)。即时样例不装配调度；这是接入和运行行为边界，不是运行时程序集裁剪承诺。
- 运行时诊断以 `TriggeringDiagnosticCollector` 和编辑器诊断菜单作为验收入口。

## 固定验证流程

修改影响 Triggering 行为、样例或验收文档时，从仓库根目录运行：

```powershell
dotnet test src/AbilityKit.Triggering.Tests/AbilityKit.Triggering.Tests.csproj
dotnet run --project tools/triggering-consumer/AbilityKit.Triggering.Consumer.csproj
```

第二条命令通过仓库源码构建独立 .NET 消费者，只编入即时样例，不使用发布包或发布探针，也不替代 Unity 导入 / Player 验证。

`FlatActionSchedulingAssemblyTests` 固定验收类型化与动态注册前拒绝、直接执行无部分副作用及不消耗触发资格、事件派发失败隔离、宿主显式推进同一实例，以及执行根不受未使用扁平列表影响。

Unity 的 `TriggerRunnerMainlineTests`、`TriggeringTestHarnessTests` 不引用调度扩展。`com.abilitykit.triggering.actionscheduling` 的 `ActionSchedulerAdapterTests` 显式注入默认管理器，覆盖延迟和周期；`TriggeringRuntimePoolingHotspotScanTests` 在该扩展包源码中检查池化分配点。该扩展的 .NET 回归使用 `core-stability` 门禁的 `Triggering action scheduling tests` 步骤。

构建通过后，在 Unity Test Runner 中执行 `com.abilitykit.triggering` 的 Editor tests，重点确认以下扫描测试：

- `RuntimeCompatibilityLegacyEntryScanTests`：外部包不得重新引用历史触发器入口。
- `TriggeringProductAcceptanceChecklistTests`：文档、样例、菜单验收入口保持同步。

## 首次稳定发布验收

### 源码依赖与批次

当前目标为 Unity 2022.3，自有闭包中各包均声明 `0.1.0`。以下包名省略 `com.abilitykit.` 前缀；第三方条目只核对当前仓库的版本声明，不展开其包内容。

| 声明方 | 直接依赖 |
| --- | --- |
| `triggering` | `core`、`continuous`、`modifiers`、`world.di`、`deterministic`；作者化程序集使用 `com.unity.nuget.newtonsoft-json@3.2.1` |
| `core` | `deterministic` |
| `modifiers` | `core`、`gameplaytags` |
| `world.di` | `core`、`deterministic`、`com.cysharp.memorypack@1.10.0`、`org.nuget.system.runtime.compilerservices.unsafe@5.0.0` |
| `continuous`、`deterministic`、`gameplaytags` | 无包依赖声明 |

首发计划分三批，均为 `candidate`：`batch-1-leaves` 提供 `deterministic`、`core`、`gameplaytags`，`batch-2-triggering-dependencies` 提供 `continuous`、`modifiers`、`world.di`，`batch-3-triggering` 只包含 `triggering`。从仓库根目录运行 `node tools/publish/release.js --dry-run --include-candidate` 可预览跨批闭包。上游还没有对应版本的本地标签时，不要用 `--batch` 单独核验下游；即使已有标签，按批次交付前仍须确认上游实际可安装。外部安装的作用域与单 Unsafe 提供者要求见[迁移说明](LegacyMigrationPolicy.md#单提供者迁移)。

### 证据分层

| 阶段 | 通过条件 | 不代表 |
| --- | --- | --- |
| 源码与候选封存 | 版本审计、运行时及随包 Editor 的 `.asmdef` 审计通过；`-PrepareCandidate` 记录自有候选摘要与公开依赖声明 | 已安装、已发布、已验证注册表或进入 `ready` |
| 源码切片 Player | 在禁用 UPM 的隔离工程中构建并启动 Player，验证保留的类型化入口 | 完整包可安装，或被排除的程序集和依赖可在 Player 中运行 |
| 干净 Editor 消费 | `triggering-release-consumer` 在独立工程仅以 Triggering 为根依赖，核对锁定来源、候选运行时源码和单 Unsafe 提供者，并执行即时样例、`Timed` 到期及 `WorldId` 往返 | Unity Player 或 AOT 运行通过 |
| Windows x64 Mono Player | 显式运行 `verify-triggering-consumer.ps1 -PlayerBackend Mono`，构建后实际启动 Player，通过同一组探针并检查输出中的单 Unsafe DLL | IL2CPP、其他平台或未经过探针的入口通过 |
| Windows x64 IL2CPP Player | 显式运行 `verify-triggering-consumer.ps1 -PlayerBackend IL2CPP`，构建后实际启动 Player，通过同一组探针；AOT 声明以实测平台、后端和入口为限 | 任意载荷、宿主配置或裁剪设置均受支持 |

即时样例的 `RunOnce` 覆盖类型化 `RegisterPlan`、条件拒绝、顺序和退订；嵌套派发由独立的 `RunNestedDispatchOnce` 覆盖，当前干净安装探针只调用前者。干净安装的 `Timed` 探针覆盖一个执行树到期路径，不覆盖宿主定时任务、队列事件或所有动作模式。接收 `Type` 的 `RegisterPlan` 在运行时构造泛型方法，不列入首发通用 AOT 承诺；有动态绑定需求的宿主应使用实际载荷类型和目标裁剪设置单独验证。旧版本升级、回退不是首次稳定发布的门槛。

**源码核对（2026-11-25）。** 自有七包的运行时与 Editor `.asmdef` 审计通过，World DI 的 `Microsoft.CSharp` 由审计器报告为未知归属；版本审计无内部版本、BOM 或 cohort 差异。源码闭包没有 Analyzer 或旧内嵌 Unsafe。现有消费脚本（包括两个 Player 选项）会先解析公开依赖并核对安装结果；在不接触已发布文件的验证范围内，该脚本只使用 `-PrepareCandidate`，不把主工程、Instance2 或源码切片登记为干净消费证据。

**源码切片实跑（2026-11-25）。** Unity 2022.3.62f1 的 Editor、Windows x64 Mono（禁用托管裁剪）和 IL2CPP（Low 裁剪）均通过即时样例的两个入口，以及 `EventKey<int>` 的类型化计划注册、条件拒绝、动作执行与退订。隔离工程禁用 UPM，未生成包锁文件或 PackageCache。切片仅在临时工程排除 Newtonsoft 相关实现、保留 World DI 的 `IWorldResolver` 源码契约而不装配完整 World DI。结果不覆盖完整包干净安装、配置与快照 JSON、`WorldId`、Unsafe、调度或动态注册。输入摘要、裁剪项和逐后端结果见本地 `local/Logs/tspl-1125-171724/`。

**黑板快照拆分复验（2026-11-25）。** 上述隔离工程现在直接编译未改写的黑板数据与恢复源码，只排除快照 JSON 编解码器。Editor、Windows x64 Mono 和 IL2CPP（Low 裁剪）复验即时主线，并验证快照复制不受原输入修改影响、恢复值写回原黑板、初始化模板变更后拒绝过期计划。结果见 `local/Logs/tspl-1125-185118/results.json`。Triggering .NET 的 196 项及 MOBA 本地回滚和双模式的 40 项通过，证据见 `local/Logs/triggering-blackboard-20261125-184646/`。仍未验证完整包安装或 Player 中的快照 JSON；隔离工程也仍需排除其他格式相关实现。

**加载诊断拆分复验（2026-11-25）。** Triggering .NET 门禁步骤通过 197 项，其中本地构造的严格／宽松加载用例验证实际 JSON 诊断的映射。隔离工程整目录排除计划 JSON 实现，仍排除快照 JSON 编解码器与两个旧数据库入口；复制输入逐文件校验 SHA-256，无需截断或改写源码。Unity 2022.3.62f1 Editor、Windows x64 Mono（禁用托管裁剪）和 IL2CPP（Low 裁剪）实际启动，复验即时主线、黑板恢复和通用加载诊断的计数及清空。证据见 `local/Logs/triggering-load-diagnostics-final-20261125-205837/` 和 `local/Logs/tspl-load-final-1125-205837/`。未运行完整 Unity EditMode 套件或干净安装，不以此结果承诺 Player 中的 JSON 加载、调度或完整包验收。

**旧执行数据库下线复验（2026-11-25）。** 移除 `ExecutableTriggerDatabase` 及专属类型后，Triggering .NET 门禁步骤通过 197 项。隔离探针不再需要排除旧执行数据库；其余裁剪项保持不变。Unity Editor、Windows x64 Mono 和 IL2CPP 构建后实跑通过，复制输入逐文件核对 SHA-256，无源码改写。结果见 `local/Logs/triggering-executable-retirement-20261125-224500/` 和 `local/Logs/tspl-exec-retired-1125-224500/results.json`。本批未改 MOBA 加载或同步机制，未重跑 MOBA 两条同步链、完整 Unity EditMode 或干净安装。

**显式计划校验复验（2026-11-25）。** 移除旧自动校验包装器及配置后，Triggering .NET 门禁步骤通过 199 项。新增的本地 JSON 用例覆盖了执行根中 Timeline 动作拒绝、空组合节点警告及对应诊断路径，并确认校验不改写候选记录。禁用 UPM 的隔离探针保留计划 JSON 目录和快照 JSON 编解码器的排除项，Editor、Windows x64 Mono 与 IL2CPP 实跑通过；这些结果不覆盖 Player 中的 JSON 加载或完整 Unity EditMode。结果见 `local/Logs/triggering-explicit-validation-20261125-231253/` 和 `local/Logs/tspl-explicit-validation-1125-231253/results.json`。

**历史记录：UGC 即时准入试点（2026-11-25，已退出核心）。** 当时 Triggering .NET 门禁步骤通过 214 项；隔离源码探针在 Unity Editor、Windows x64 Mono 和 IL2CPP 验证了试点的白名单和超额拒绝。试点未接入正式宿主，数据仅作为历史记录，不计入当前核心包验收，也不证明 UGC 隔离或成品包安装通过。

## 常用检查

1. 搜索新增代码是否仍然引用旧调度或旧派发入口。
2. 检查示例是否仅使用正式计划、上下文与调度类型。
3. 检查验收测试是否覆盖计划执行、动作调度与业务调度。
4. 检查对象池接入是否只覆盖短生命周期运行时热点。
5. 检查产品说明是否仅描述正式能力，并指向 `StageClosureGuide.md`。
