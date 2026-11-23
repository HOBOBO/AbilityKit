# Triggering 兼容与下线流程

入口的状态、现有调用证据和逐项门槛统一维护在 [入口清单](FormalApiBoundary.md)。新功能走正式入口；兼容测试保留到对应入口或行为完成迁移。

**首发口径。** Triggering 首次稳定发布没有需要兼容的在用版本。历史开发快照不构成对外兼容基线，旧版升降级和与开发快照的版本号比较不列为首发门槛。首发仍要定清正式 API、核对当前仓库消费方与配置/序列化契约，并验证干净安装和声明支持的平台。此口径不自动授权删除任何入口或修改运行行为。

1. 先核对源码、生成注册、反射、Unity 资产 GUID、程序集/序列化身份。正式发布后，下线入口时另核对已有消费者。仅无源码引用，不足以删除公开入口。
2. 有正式消费者的旧入口先确定替代路径、验证行为，再迁移调用者。没有可替代行为的入口不能按临时代码处理。
3. 对需要下线的公共 API，明确版本与迁移说明后再执行；移除时同批更新入口清单、样例、兼容测试和发布说明。
4. 对不定义类型、不参与注册且确认无引用的临时占位，可连同 `.meta` 删除，不再补空壳。迁移仍保留的 Unity 资源时沿用原 `.meta`。

整理不改变 Trigger 的排序、错误处理、配置覆盖、Scope、调度或恢复语义。涉及 MOBA 共享契约变化时另行走机制准入，不以兼容清理代替审批。

## 集中归档与发布剔除边界

`Runtime/LegacyCompatibility/` 存放兼容实现，不复用 `Runtime/Compatibility/`：后者是根目录占位治理清单，仍被现有校验门面引用。首批迁移保留同一运行时程序集、命名空间、公开成员及原 `.meta`；仍随包编译，不引入发布开关。

| 分组 | 首批范围与处理 |
| --- | --- |
| `LegacyCompatibility/Optional/Dispatcher/` | 旧派发器和旧上下文适配器已迁入。主线的 `ITriggerDispatcherContext` 与 `TriggerPredicate<TArgs>` 留在原位。 |
| `LegacyCompatibility/Optional/ActionSynchronization/` | `TriggerWaitHandle`、`TriggerMutex` 和 `SynchronizedActionExecutor` 已迁入。三者按组管理，避免单独剔除公开签名中的依赖类型。 |
| `LegacyCompatibility/Required/` | `QueuedActionExecutor` 已拆为独立文件。调度工厂、实例策略检查和对象池仍引用它，不能随 `Optional/` 排除；只移除该实现不能维持 `Queued` 配置的既有行为。校验门面、.NET 类型转发与其他待核对的兼容 API 保持原位。 |

**依赖验证。** 迁移后，默认构建和仓库外隔离构建（仅从 `AbilityKit.Triggering` 编译项排除 `Optional/` 下的 5 个源码文件）均通过 47 项 Triggering .NET 测试。迁移后的源码和原 `.meta` 哈希一致。本次未运行 Unity 测试；.NET 排除构建通过不代表 Unity 资产、已发行程序集或仓库外消费者可以无损升级。

**首发裁剪门槛。** 正式主线不得反向依赖 `Optional/`。先核查当前仓库消费方、Unity GUID、程序集引用和配置；再分别验证完整与排除兼容组的构建、UPM 与 .NET 的公开 API 差异和干净消费项目。确定首发范围后，在待发布源码中成组移除对应实现和 `.meta`，不在打包时临时删文件。当前发布工具按完整包子树核对输入，目录分隔本身不会改变发布内容。

## 干净 Unity 消费核验（2026-11-22）

- **声明依赖安装未通过。** Unity 2022.3.62f1 在新工程从本地 tarball 解析声明闭包，但编译 `world.di` 的 `WorldId` 时缺少 `MemoryPack`；开发工程的根清单已提供 `com.cysharp.memorypack`，掩盖了 `world.di` 的声明缺口。
- **补齐依赖后的对照通过。** 仅在两个隔离测试工程显式添加 `com.cysharp.memorypack@1.10.0`。完整制品和去掉 `Optional`（含目录与脚本 `.meta`）的试验制品均通过主线样例及 `Timed` 节点只到期执行一次的探针。核对了实际安装的 307 / 302 个运行时源码文件；两版均保留 `Required/QueuedActionExecutor.cs`。裁剪版少 9 个公开类型（旧派发器组 6 个，动作同步组 3 个），不是无损替换。受检仓库文本中未找到 8 个被裁剪 `.meta` 的 GUID 引用；不据此排除仓库外消费。
- **暂不裁剪正式制品。** Triggering 尚未进入批次清单，声明闭包还包含 `triggering → analyzer → com.abilitykit.thirdparty.unsafe`，末端受发布前缀规则禁止。本轮不修改依赖、版本和发布清单。先补齐 `world.di` 自身的依赖声明，再解决 Analyzer 的发布依赖边界；等目标版本、迁移说明与包外消费核对完成后才考虑剔除 `Optional` 整组。`Required` 及正式调度契约继续保留。

仓库本地证据：`local/Logs/triggering-consumer-20261122/` 下的 `inputs.json`、`supplement.json`、`full.log`、`comparison.json`。本轮基于工作区快照和本地制品，不代表 OpenUPM 安装、已发布版本升级/回退、Player 构建或正式签核。

**后续复验。** `world.di` 已补齐 `com.cysharp.memorypack@1.10.0` 的声明。Unity 2022.3.62f1 的干净工程只声明 Triggering，由本地测试注册表解析 `Triggering → World DI → MemoryPack`；通过主线样例、`Timed` 单次到期执行和 `WorldId` 序列化往返。未在消费工程手动添加 MemoryPack。

**Analyzer 不是可直接剥离的发布修复。** 只在隔离制品移除 Triggering 的 Analyzer 包依赖和程序集引用后，MemoryPack 因缺少 `System.Runtime.CompilerServices.Unsafe` 编译失败；在独立对照工程显式装回 `Unsafe` 但不装 Analyzer，三项探针均通过。这说明所测运行路径不依赖 Analyzer 本身，但原有依赖边间接提供了 MemoryPack 所需的程序集。应先解决 `Unsafe` 的合规来源与依赖归属，再评审移除 Analyzer；不将测试工程的显式安装作为发布方案，也不改变禁发规则。证据见 `local/Logs/triggering-dependency-20261122/` 下的 `report.json`、`without-analyzer.log` 和 `analyzer-unsafe-control.json`。

### 外部依赖候选

- 从 OpenUPM 下载 `org.nuget.system.runtime.compilerservices.unsafe@5.0.0`，验证制品摘要；其中 DLL 与仓库内置 DLL 的 SHA-256 相同。用本地测试注册表重放下载制品，在隔离的 `world.di` 制品声明该外部包，并从隔离的 Triggering 制品移除 Analyzer 包依赖及程序集引用。干净工程仅声明 Triggering 即通过主线样例、`Timed` 到期与 `WorldId` 往返探针；未安装 Analyzer 或内置 Unsafe 包。
- 另一个 Editor 对照同时安装两个 Unsafe 包，探针通过，MemoryPack 的编译响应文件只引用外部包中的 DLL。两份 DLL 的 Unity GUID 不同；单次 Editor 通过不等于现有依赖可以并存发布，也不覆盖 Player、IL2CPP 或升级、回退。
- 下一步按**单一提供者**评审 Unity 依赖迁移：核对 `world.di` 与现有 MemoryPack 消费包的声明归属，补外部 registry scope 及安装说明，并验证只解析一份 Unsafe DLL 后再移除 Triggering 的 Analyzer 引用。Analyzer 作为开发工具的独立消费者不在本次下线范围。当前不改正式 `package.json`、程序集引用或发布清单。

本地证据：`local/Logs/triggering-dependency-20261122/` 下的 `external-unsafe-inputs.json`、`external-unsafe-report.json` 和 `external-unsafe-coinstall.json`。

### 单提供者迁移

Unity 清单统一请求 OpenUPM 的 `org.nuget.system.runtime.compilerservices.unsafe@5.0.0`，旧嵌入包不再参与导入。`world.di` 显式声明其 MemoryPack 所需的 Unsafe，Triggering 移除 Analyzer 的包声明和程序集引用；直接使用 Analyzer 的项目仍可按独立包安装它。仓库已配置对应 OpenUPM scope；仓库外消费工程也需要同名 scope，并应先移除旧嵌入包或显式文件依赖，不同时安装两个提供者。

干净 Unity 工程仅直接声明 Triggering，AbilityKit 制品由本地测试源提供，Unsafe 从 OpenUPM 解析。通过主线样例、Timed 单次到期和 WorldId 往返；锁文件中没有 Analyzer 或旧 Unsafe 包，实际只导入一份与旧 DLL 摘要一致的 Unsafe。证据见 `local/Logs/triggering-unsafe-migration-20261122/consumer-report.json`。这不是框架包已发布、Player 构建或 IL2CPP、旧版本升级回退的验收，亦不改变 `Optional` 的下线门槛。

仓库 Unity 主工程在 2022.3.62f1 上通过 Triggering EditMode 全量 98 项；锁文件只解析外部 Unsafe，`MemoryPack` 与 `AbilityKit.Record.MemoryPack` 的编译响应文件各只引用该包中的同一份 DLL。回归结果见 `local/Logs/triggering-unsafe-migration-20261122/triggering-editmode-20261122-090631.xml`。

后续发布候选可用 `tools/run_test_gate.ps1 -Gate triggering-release-consumer` 复验干净安装。它会解析公开依赖，不适用于要求完全不接触已发布文件的操作，也不替代实际制品和 Player 验收。

### 历史对照：同一工程升级与回退

Unity 2022.3.62f1 中，从 Git 提交 `bcc9cea4a2b051baf387d58b7b569d8522227945` 原样打包旧依赖闭包，消费工程补齐该提交原清单直接提供的 MemoryPack、Newtonsoft JSON 和 UnityWebRequest。候选来自工作区源码，仅在临时制品中为仓库包及它们之间的依赖分配区分缓存的测试版本 `0.1.1-migration.1`，不变更仓库版本或发布清单。两组制品的 SHA-256 见报告。

同一工程依次安装旧版、升级候选、回退旧版。保留 `Library`、UPM 缓存和 Unity 生成的锁文件，只切换消费清单并显式导入各版本随附的主线示例。三个阶段均重新编译并通过共用的 `Timed` 单次到期与 `WorldId` 序列化往返探针。升级后锁文件不含 Analyzer 或旧 Unsafe，项目只找到一份外部 Unsafe DLL，MemoryPack 编译仅引用该文件；回退后恢复旧提供者且不再解析外部 Unsafe，首尾锁文件 SHA-256 一致。

本地复现脚本为 `local/triggering_migration_probe.py`，报告及逐阶段清单、锁文件和 Editor 日志见 `local/Logs/triggering-upgrade-20261122-131532/migration-report.json` 同级目录。此结果仅覆盖 Git 基线与隔离候选的 Editor 依赖迁移，保留为技术对照，不作为首次稳定发布的验收项。

首发候选需要固定版本与摘要时，使用 `tools/publish/verify-triggering-consumer.ps1 -PrepareCandidate`。此步骤只生成并记录未发布的本地候选，不需要旧基线，不启动 Unity 或接触已发布文件。封存报告不代表消费验证通过。
