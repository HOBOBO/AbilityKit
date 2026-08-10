# Ability-Kit Analyzer 静态约束分析模块开发设计文档

## 一、文档定位

本文是 `com.abilitykit.analyzer` 的 package canonical 设计文档。Analyzer 用于在 Unity 工程和独立 .NET 编译链中检查包依赖、命名空间和程序集引用约束。

当前包同时包含三条运行面：

1. Unity Runtime 配置读取和约束查询；
2. Unity Editor 构建前检查与 Asset/PostProcessor 相关检查；
3. 独立 Roslyn Analyzer。

三条运行面共享约束概念，但不是同一个 Loader、同一个执行时机或同一种失败语义。文档必须按运行面解释结果，不能把一处诊断外推为全工程覆盖。

## 二、模块边界

Analyzer 负责：

- 读取 `PackageConstraints.json`；
- 将包或 asmdef 的约束合并为有效规则；
- 检查禁止命名空间和程序集引用；
- 在 Unity 构建前发现违规并阻断构建；
- 在 Roslyn 编译期间报告 AK1001、AK1002、AK1003 等诊断。

Analyzer 不负责：

- 修复源码、自动重写 using 或 asmdef；
- 证明运行时依赖一定符合架构意图；
- 对未加载、未配置或被跳过的文件提供完整覆盖；
- 替代代码评审、包所有权设计和发布审批；
- 自动保证根目录插件 DLL 与 Roslyn 构建产物一致。

## 三、配置模型与读取路径

### 3.1 约束来源

`ConstraintLoader.ResolveConfigPath()` 按顺序查找：

1. `Assets/Config/PackageConstraints.json`；
2. `Packages/com.abilitykit.analyzer/Config/PackageConstraints.json`；
3. `Packages/com.abilitykit.core/Config/PackageConstraints.json`。

Runtime Loader 找不到文件、读取失败或解析失败时回退到空配置；解析失败记录 Warning，不阻断。asmdef JSON 中的内嵌约束还可以参与合并，实际优先级和字段覆盖应以 Loader 实现为准。

Unity Editor 构建检查器和 Roslyn Analyzer 各自实现配置读取，不应假定它们自动共享 Runtime Loader 的缓存或回退结果。

### 3.2 配置缺失的含义

空配置表示当前运行面没有可执行规则，不表示工程已通过架构约束。为避免“静默通过”误导：

- CI 应单独验证配置文件存在且可解析；
- 发布前应记录实际使用的配置路径和版本；
- Roslyn 工程必须显式注入 `AdditionalFiles`；
- Unity 构建日志应区分“无违规”和“未加载规则”。

## 四、Unity Runtime 运行面

`ConstraintLoader` 面向运行时或共享代码查询约束，提供按包获取约束、判断规则是否启用、判断命名空间是否禁止等 API，并缓存解析后的配置。

它适合为运行时策略或工具查询当前配置，不是编译期完整静态检查器。读取失败回退空配置的设计使运行时不会因配置缺失而崩溃，但也形成 fail-open 边界。

`ConstraintValidator` 可以验证配置模型内部的一致性。它不能证明源码中的所有引用都被扫描，也不能替代 Editor 或 Roslyn 运行面。

## 五、Unity Editor 构建检查面

`NamespaceConstraintBuildChecker` 实现 `IPreprocessBuildWithReport`，在 Unity 构建前扫描程序集源码，并在发现违规时抛出 `BuildFailedException`，从而阻断构建。

### 5.1 当前检查方式

- 逐行识别 `using ` 并提取命名空间；
- 按程序集名称匹配约束；
- 跳过 Editor、Example 和 Test 目录；
- 对每个 asmdef 的处理异常捕获后写日志，可能导致单个程序集静默漏检；
- 当前 `GetEffectiveConstraint()` 没有真正应用 `ApplyToUnlistedPackages`，未列程序集会回退到全局默认约束。

这是一种轻量文本扫描，不是 C# 语义分析，无法完整处理别名、条件编译、全限定名、生成代码或复杂语法。

### 5.2 构建阻断和日志风险

发现违规时构建失败是当前最强的门禁行为。但检查日志硬编码写入：

- `C:\analyzer_build_check.log`；
- `C:\analyzer_build_errors.txt`。

该实现依赖 Windows 根目录写权限，在非 Windows、受限权限或 CI 容器中可能失败或丢失诊断。每个程序集异常只记录日志而不重新抛出，也可能让构建通过但实际漏检。

## 六、Roslyn Analyzer 运行面

`ForbiddenNamespaceAnalyzer` 是独立 Roslyn Analyzer，通过 `AdditionalFiles` 查找 `PackageConstraints.json`。在 Compilation Start 阶段加载配置，并注册以下类别的分析：

- 禁止命名空间引用；
- 禁止程序集引用；
- 未匹配约束项。

当前规则包含 AK1001、AK1002、AK1003。Analyzer 开启并发分析并忽略生成代码，诊断路径独立于 Unity Editor 构建检查器。

### 6.1 注入责任

如果 `.csproj` 或宿主编译器没有把配置作为 `AdditionalFiles` 注入，Analyzer 会找不到配置并静默不执行规则。当前 `src/AbilityKit.Demo.Moba.Core/AbilityKit.Demo.Moba.Core.csproj` 已显式包含约束文件，可作为真实 .NET 消费证据。

仅存在源码或 DLL 不等于规则已接入。消费方应同时验证：

1. Analyzer DLL 被编译器加载；
2. `AdditionalFiles` 指向预期配置；
3. 诊断编号和严重级别符合工程策略；
4. 生成代码、排除目录和增量编译行为符合预期。

## 七、产物与同步责任

包内同时存在：

- 根目录 `AbilityKit.Analyzer.Plugin.dll`；
- `DotNet~/bin/Debug` 产物；
- `DotNet~/bin/Release` 产物；
- Roslyn 及其依赖 DLL。

源码、构建产物和 Unity 实际加载插件之间没有在本文档范围内确认自动同步机制。发布或升级 Analyzer 时必须记录：

- 使用的源码提交和构建配置；
- 根目录插件 DLL 的生成来源；
- Debug/Release 目录是否与加载 DLL 同步；
- 依赖 DLL 的版本和复制策略；
- Unity 与独立 .NET 消费方分别加载了哪一份产物。

否则可能出现 Unity Editor 与 .NET CI 使用不同规则版本的隐性分叉。

## 八、失败矩阵

| 场景 | Unity Runtime | Unity Build Checker | Roslyn Analyzer |
|---|---|---|---|
| 找不到配置 | 回退空配置 | 使用自身 Loader 行为 | 无 AdditionalFile 时可能静默跳过 |
| JSON 解析失败 | Warning 并回退 | 以自身实现为准 | 以自身实现为准 |
| 发现违规 | 不负责阻断 | 抛 `BuildFailedException` | 报告诊断 |
| 语义复杂 using | 不检查 | 文本扫描可能漏检 | 由 Roslyn 语义模型分析 |
| asmdef 处理异常 | 不适用 | 捕获并可能静默漏检 | 不适用 |
| 未列程序集 | 查询返回配置默认语义 | 当前忽略 `ApplyToUnlistedPackages` | 取决于配置匹配逻辑 |
| 生成代码 | 不适用 | 取决于扫描路径 | 当前忽略生成代码 |
| 日志不可写 | 取决于调用方 | 硬编码 C:\ 路径有风险 | 由编译器报告 |

## 九、采用证据与成熟度

已确认的采用证据：

- `src/AbilityKit.Demo.Moba.Core/AbilityKit.Demo.Moba.Core.csproj` 显式注入 `PackageConstraints.json`，证明 Roslyn 运行面存在真实 .NET 消费者；
- Unity Editor 构建检查器实现了构建前阻断路径。

当前未确认 Analyzer 专项自动测试、跨平台构建验证、插件版本一致性校验或 CI 发布门禁。因此成熟度如下：

| 等级 | 状态 | 说明 |
|---|---|---|
| E0 | 已具备 | Runtime、Editor 和 Roslyn 源码存在 |
| E1 | 已具备 | Unity Editor 检查入口和 .NET Analyzer 可加载 |
| E2 | 局部具备 | Moba Core csproj 使用 AdditionalFiles |
| E3 | 未确认 | 未找到 Analyzer 专项自动测试 |
| E4 | 未确认 | 未找到跨平台 Smoke 或归档 artifact |
| E5 | 未确认 | 构建阻断存在，但配置完整性、产物同步和 CI 责任未闭合 |

## 十、源码阅读路径

1. [ConstraintLoader.cs](../Runtime/AbilityKit.Analyzer/Configuration/ConstraintLoader.cs)：Runtime 配置路径、回退和合并；
2. [NamespaceConstraintBuildChecker.cs](../Editor/ConstraintSettings/NamespaceConstraintBuildChecker.cs)：Unity 构建前扫描和阻断；
3. [ForbiddenNamespaceAnalyzer.cs](../DotNet~/AbilityKit.Analyzer/Analyzer/ForbiddenNamespaceAnalyzer.cs)：Roslyn 诊断、AdditionalFiles 和排除规则；
4. [PackageConstraints.json](../../../../src/Config/PackageConstraints.json)：仓库当前实际提交的约束数据源；
5. [AbilityKit.Demo.Moba.Core.csproj](../../../../src/AbilityKit.Demo.Moba.Core/AbilityKit.Demo.Moba.Core.csproj)：真实 Roslyn 配置消费者。

## 十一、后续治理顺序

1. 抽取或对齐三条运行面的配置加载和版本记录；
2. 修复 `ApplyToUnlistedPackages` 语义与注释不一致问题；
3. 将构建检查日志改为项目相对路径或可配置输出；
4. 对 asmdef 异常从静默日志升级为可观测失败；
5. 增加 Roslyn 诊断、AdditionalFiles 缺失、Unity 构建阻断和配置解析的自动测试；
6. 建立插件 DLL 与 Debug/Release 产物的单一构建来源和一致性校验；
7. 把配置完整性和 Analyzer 版本锁定接入 CI 后，再升级 E4-E5。
