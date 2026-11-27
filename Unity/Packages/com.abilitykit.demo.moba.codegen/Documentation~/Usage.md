# MOBA CodeGen 构建与使用指南

> 文档类型：使用指南 / 编译期契约
> 适用范围：本包 0.0.1 当前源码与 importer 设置；版本要求见 [package.json](../package.json)。
> 核校状态：已核校本文源码、构建副作用和局部 compilation tests；未验证 Unity 独立安装及现有发布 DLL 对应关系。
> 事实基线：2026-11-25。

## 1. 使用前确认

本包没有运行时 `Initialize` 或生成菜单。编译器发现 Roslyn 组件后，在消费项目编译中检查符号并添加生成源码。其输入是消费程序集的 MOBA 特性、类型及 partial 壳，输出是该次编译的派生源码；不要编辑 `.g.cs` 来改变行为。

生成不自动补齐消费契约。例如 [配置表契约](../DotNet~/AbilityKit.Demo.Moba.CodeGen/Contracts/MobaConfigTableContract.cs) 按完整元数据名识别 `AbilityKit.Demo.Moba.Config.Core.MobaConfigTableAttribute` 和 `MobaGeneratedConfigTableManifest`；只有消费编译中存在合适的源码壳时才进入配置 manifest 生成。仅引用含这些类型的已编译程序集，不等于为外部程序集重新生成内部清单。

## 2. 构建与测试

在**本包根目录**执行；先创建日志目录，并将 `<运行目录>` 替换为已存在的绝对路径。仓库内维护时必须使用 `local/Logs/` 下的运行目录。

```powershell
dotnet build 'DotNet~/AbilityKit.Demo.Moba.CodeGen/AbilityKit.Demo.Moba.CodeGen.csproj' --verbosity minimal *> '<运行目录>/build.log'
if ($LASTEXITCODE -ne 0) { throw 'CodeGen build failed; see build.log' }
dotnet test 'DotNet~/AbilityKit.Demo.Moba.CodeGen.Tests/AbilityKit.Demo.Moba.CodeGen.Tests.csproj' --logger 'trx;LogFileName=codegen.trx' --results-directory '<运行目录>/test-results' *> '<运行目录>/test.log'
if ($LASTEXITCODE -ne 0) { throw 'CodeGen tests failed; see test.log' }
```

上述是替换路径后的命令模板。测试需 .NET 10 SDK，构建所需 NuGet 版本由工程声明控制。**Build 成功后的 `CustomAfterBuild` 会复制 DLL 到包根；test 的项目引用构建也可能触发复制。** 排查源码而不更新交付 DLL 时，在完整包的隔离副本中运行并保存源码一致性记录，不临时删除构建目标或覆盖他人的 DLL。

构建成功、Unity 已导入 DLL、消费程序集确实生成预期代码，是三项分别验证的事实。Unity 的 [DLL importer](../AbilityKit.Demo.Moba.CodeGen.dll.meta) 带 `RoslynAnalyzer` 标签，普通平台插件导入被关闭；应保留 `.meta` 及 GUID，不把该 DLL 作为普通游戏运行时程序集加载。消费者使用其他 MSBuild 项目时，必须由其工程正确登记 Analyzer；本包本身不自动改写外部工程。

## 3. 源码与职责

| 使用目标 | 本包入口 | 约束 |
| --- | --- | --- |
| 配置表静态注册与工厂 | [MobaConfigTableManifestGenerator](../DotNet~/AbilityKit.Demo.Moba.CodeGen/Generators/MobaConfigTableManifestGenerator.cs)、[契约](../DotNet~/AbilityKit.Demo.Moba.CodeGen/Contracts/MobaConfigTableContract.cs) | 路径/组非空、DTO/MO 可访问且具体、公开整数键与 DTO 构造函数；按 Order、路径、DTO 名排序并处理重复项 |
| Action、Payload、事件与工厂清单 | [Generators](../DotNet~/AbilityKit.Demo.Moba.CodeGen/Generators)、[Contracts](../DotNet~/AbilityKit.Demo.Moba.CodeGen/Contracts) | 使用对应接口、特性、常量键和构造规则，不能套用配置表的规则替代各域契约 |
| World 与服务注册、回滚 | [MobaWorldManifestGenerator](../DotNet~/AbilityKit.Demo.Moba.CodeGen/Generators/MobaWorldManifestGenerator.cs)、[MobaRegistrationManifestGenerator](../DotNet~/AbilityKit.Demo.Moba.CodeGen/Generators/MobaRegistrationManifestGenerator.cs) | 按项目命名空间、标记与壳发现，运行时生命周期和恢复责任仍归消费者 |
| 编译诊断 | [Analyzers](../DotNet~/AbilityKit.Demo.Moba.CodeGen/Analyzers) | 包含配对声明诊断、Entitas 组件字段与 portable source 约束；以当前 SupportedDiagnostics 与编译位置定位 |
| 回归与预期生成输出 | [契约测试](../DotNet~/AbilityKit.Demo.Moba.CodeGen.Tests) | 包含 Roslyn 内存编译样例；局部测试通过不等于所有生成域均有覆盖 |

配置工厂生成对 `ConfigTableFactory` 的强类型调用及 `new MO(dto)`，ChangedIdCollector 只收集输入 DTO 的键。字段转换归 `MO(DTO)` 或消费项目显式逻辑，生成器不推断伤害规则，也不负责行级差异、缓存刷新或跨表业务引用有效性。

多数成对组件共享声明验证，Analyzer 负责形态/冲突诊断；部分 Generator 自身还报告空清单和生成约束。扩展时按对应现有实现与测试维护所有权，不能以旧文档“诊断只在 Analyzer”的绝对表述删掉当前完整性检查。

## 4. 失败定位与兼容

| 现象 | 核查顺序 |
| --- | --- |
| 改了生成器源码却无变化 | 是否重建并复制 DLL、保留 importer 标签、让消费程序集重新编译 |
| 编译器未产生 manifest | DLL 是否生效、完整元数据名是否吻合、壳是否属于源码、声明是否被过滤；检查编译诊断 |
| 源码可以编译但运行时空清单 | 核对实际消费程序集及其编译配置、生成输出和 fallback 开关，不把反射兼容当生成成功证据 |
| 新声明触发诊断 | 对照配对 Contract、可访问性、非泛型/构造要求和重复键；先修输入，不手改派生源码 |
| DLL 被锁定或拷贝失败 | 构建未完成交付，不用旧 DLL 冒充新构建；保留日志并协调实际持有该文件的宿主 |

Runtime 壳与具体 registry 决定 fallback 的开放范围，本包不承诺“任何外部类型都自动注册”。例如仓库 MOBA 配置壳仅在生成计数为零时考虑反射路径，并受开关及 `!UNITY_EDITOR && !DEVELOPMENT_BUILD` 编译条件禁止；包外项目应明确自己的消费合同。

## 5. 验证范围

2026-11-25 对本包 C# 源码相同的隔离副本执行 `dotnet test`，19 项通过；包括生成排序、空清单、重复回滚键、引用程序集不重新生成、Schema/Payload 与组件/可移植性诊断等具体用例。原包根 DLL 未被重建或替换，也未验证其与当前源码逐项对应。测试通过只记局部 E3。

仓库 `moba-codegen` gate 还涵盖框架分析器构建、所有权与配置语义测试；本批只执行了所有权单步及上述隔离测试，未执行完整 gate。Unity 编译、正式内容发布、跨平台与 CI 持续阻断均不在本次结果内。仓库 gate 命令和跨包链接保留在仓库设计，不作为独立包的本地相对依赖。
