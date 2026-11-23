# Core 验证门禁与发布缺口

本文说明当前 Core 工程门禁、依赖闭包及尚未完成的发布证据。注释整理完成、构建通过和候选版本号不代表正式发布；API 准入与迁移规则仍以 [CoreBoundary](./CoreBoundary.md) 为准。

## 中文注释与 XML 文档

退役 `AbilityKit.Core.Unity` 前，Core 的 81 个 C# 源码已通过中文多行注释检查。工具按常规及一组 Unity Editor 符号解析语法树，不能判断注释事实是否正确，也不覆盖所有平台编译符号组合。

仓库内可执行：

```powershell
dotnet run --project tools/AbilityKit.PackageQuality -- Unity/Packages/com.abilitykit.core
dotnet build src/AbilityKit.Core/AbilityKit.Core.csproj --no-restore --no-incremental
dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore
& tools/test_core_documentation_gate.ps1
```

使用 `--no-restore` 前须已恢复对应工程。`.NET` Core 开启文档生成；工程目录的 `Directory.Build.targets` 先导入 `src/Directory.Build.targets`，再仅为 Core 从 WarningsNotAsErrors 移除以下五类历史豁免并加入 WarningsAsErrors：

| 诊断 | 阻止的问题 |
|---|---|
| CS1570 | XML 文档格式损坏。 |
| CS1573 | 已写部分参数标签时，其他参数缺少匹配说明。 |
| CS1574 | XML 文档中的 cref 无法解析。 |
| CS1587 | XML 文档位于不能附着的语言元素前。 |
| CS1591 | 对外可见的类型或成员缺少 XML 文档。 |

其他历史警告沿用共享基线，不由本批扩大收紧。CS1573 不要求所有方法都必须含 param 标签，中文多行格式仍由注释工具负责。RS0016/RS0017 公共 API 门禁继续有效，Shipped 与 Unshipped 未重生成。

`test_core_documentation_gate.ps1` 用独立最小工程导入真实构建策略，分别验证五类诊断变为错误、正确文档可编译，以及共享策略仍将这五类报告为警告，共七项。夹具和日志保存在本地证据目录，不修改正式源码、不连接公共 API 基线，也不模拟 Unity。该步骤已登记到 `tools/test-gates.json` 的 `core-stability`；完整门禁还包含其他工程与边界审计，不能把七项通过写成整个门禁已通过。

Core 的 .NET 工程只复用纯运行时源码，明确排除 Runtime/Unity，且未包含 Editor。局部 MSBuild 门禁不会自动改变 Unity 编译器配置。平台编译和生命周期验证仍须独立完成。

## 依赖与候选发布工具

当前包版本均为 0.1.0，最低 Unity 声明为 2022.3。依赖关系如下：

| 层次 | Core | Deterministic |
|---|---|---|
| UPM | 声明依赖 com.abilitykit.deterministic@0.1.0。 | 当前没有包依赖。 |
| 纯运行时 asmdef | 仅引用 AbilityKit.Deterministic，noEngineReferences 为真。 | 无程序集引用，noEngineReferences 为真。 |
| .NET | 唯一 ProjectReference 为 AbilityKit.Deterministic；公共 API 分析器为构建期私有依赖。 | 无 ProjectReference；仍有自身的公共 API 分析器和警告配置。 |
| 当前 Unity lock | Core 的依赖指向 Deterministic@0.1.0，两者为本地嵌入包。 | 本地嵌入包。 |

当前发布 manifest 将两者放在同一 candidate 批次。Core 已有直接数值内核依赖，不能继续把它描述为零依赖叶子包；消费安装和发布计划均须包含 Deterministic。仓库中本地嵌入包解析不等于注册表或 git 安装已验证，Core 的文档门禁也不自动证明依赖包达到相同整理标准。

只读版本审计与普通候选 dry-run 通过：版本引用不匹配、BOM 和框架偏离 cohort 均为零，候选计划列出七个包。普通预览只检查声明；严格输入检查另核对源码与提交/标签内容，但不执行 Core 全套测试、注释、真实 Unity、干净消费或负责人签核。

`release.js` 已修正只按包名判断同计划依赖的问题：ready 包要求依赖具有请求精确版本的本地标签，或为本次所选计划中同版本的 ready 包；candidate 只能显式预览，不能为未打标的 ready 依赖放行。重复归属、未知批次、缺包、非法参数及 Git 失败均有独立拒绝测试，标签创建中途失败会非零退出并保留已成功项。状态选择、版本形式和失败行为以仓库内 [发布工具说明](../../../../tools/publish/README.md) 为权威；这些操作规则不属于 Core Runtime 能力，工具也不随包交付。

输入一致性检查使用只读参数 `--verify-inputs`，`--tag` 自动执行同样的检查。严格模式要求已选包的完整目录、执行工具和清单与固定 HEAD 一致，拒绝 Runtime/暂存修改、未跟踪/忽略文件和外部链接，并核对已有标签的包名、版本、子树及传递依赖。已发布旧依赖按其标签取证，不把未选择包的当前源码当成历史版本；同版本已选包不能复用内容不同的标签。

成功后输出固定提交、工具/清单 blob、包 tree 及依赖 tag/object/commit/tree 的 JSON 证据；新标签显式指向已验证提交。规范化 Git 对象不等于实际制品哈希，外部构建输入、并发修改和注册表状态仍需单独处理，具体边界见发布工具说明。

隔离 CLI 回归继续使用 `core-stability` 的 `Release plan dependency and failure tests` 单步骤，TAP 与工具输入哈希由 `tools/test_release_plan.ps1` 保存，并核对测试期间输入未变化。真实仓库默认与七包候选预览通过，严格候选 dry-run 因工具尚未提交而非零拒绝，未输出通过证据；没有为通过检查自动提交文件。候选状态与真实标签保持，未打标或发布。

## 当前发布缺口

| 项目 | 当前证据与后续要求 |
|---|---|
| 源码注释与 .NET Core | 对象池与事件整理后的 79 个 Runtime 文件注释检查无问题；本批重跑 Core 538 项测试与 Public API 构建门禁通过。 |
| 命名空间所有权 | Collision 和 Record 的两处新增类型已迁回所属命名空间，既有白名单不变。项目直链检查按 Git 源码范围扫描，排除未跟踪且被忽略的临时工程；直接在当前工作区执行完整边界审计通过。 |
| Observability 准入 | 尚缺三个独立框架使用方证据，当前整理不代表正式纳入；见 [观测关联原语准入复核](./Observability.md)。 |
| 定点依赖目标差异 | Fixed64 的 Int128 与 decimal 编译路径在负数乘法舍入和中间范围上有既有差异，见 [定点数学桥接契约](./DeterministicMathBridge.md)，不能据 .NET 构建承诺跨目标位一致。 |
| 独立消费 | 本地 .NET 验证包的三阶段恢复/执行/回退及缺依赖拒绝通过；真实 Unity 本地 tarball 同版本替换与回退通过，详见 [独立消费验证](./IndependentConsumption.md)。注册表安装与正式旧版本升级回退仍待完成。 |
| Unity 平台 | 已在独立 Unity 2022.3.62f1 Editor 中编译 Runtime、Unity 适配与 Editor 并运行最小调用；Player、Mono/IL2CPP、协程/请求退出和编辑器域生命周期仍待验收。 |
| 发布执行 | 状态/精确版本、失败退出和提交内容一致性检查已补齐；当前工作区严格校验拒绝，需按流程固定已提交输入并重跑，绑定实际依赖制品、支持矩阵、回退版本与负责人证据，验证注册表收录。当前仍为 candidate，未在真实仓库打标、推送或发布。 |

测试数量与审计状态应按每次候选重新生成。完整发布要求沿用仓库 Beta 发布检查清单，本文不提升包的成熟度或版本承诺。
