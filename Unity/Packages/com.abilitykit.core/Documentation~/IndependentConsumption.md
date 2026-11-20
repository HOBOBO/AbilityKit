# Core 与 Deterministic 独立消费验证

Core 的纯运行时依赖 Deterministic。本文说明本地隔离验证的交付形态、输入与结论边界；正式发布准入仍见 [验证门禁与发布缺口](./ValidationAndReleaseReadiness.md)。

## 验证输入与运行方式

仓库提供 `tools/validate_core_consumer.py` 和固定调用源码 `tools/core-consumer/ConsumerProbe.cs`。运行需要 Python 3.12+、.NET 10 SDK，以及已恢复到本机缓存的 Microsoft.CodeAnalysis.PublicApiAnalyzers 3.3.4；真实 Unity 验证使用本机 Unity 2022.3.62f1 Editor。工具默认针对 Windows 开发环境：

```powershell
python tools/validate_core_consumer.py
```

可用 `--unity <Unity.exe 路径>` 指定 Editor。`--mode prepare` 只准备输入；`--mode dotnet` 或 `--mode unity` 可配合 `--run-root <已准备目录>` 分阶段执行，同一目录有运行锁，按阶段串行更新证据。仅完成某一模式不能声明另一模式通过；要求运行 Unity 时，找不到 Editor 会失败，不按跳过成功处理。

工具在系统临时目录建立全新的隔离目录，不从已有 Demo 工程拷贝 Library、obj、bin、manifest 或包缓存。准备两份输入：HEAD 中的基线，以及本轮工作区中的候选。逐文件 SHA256、HEAD 标识、消费源码哈希、SDK、验证器哈希与制品哈希写入报告；工作区修改不冒充已提交发布版本。只有所需包、镜像工程配置、API 基线和构建策略进入输入快照，消费代码在三阶段保持不变。

本地指针为 `local/Logs/core-stabilization/core-consumer-latest.json`，指向隔离目录中的 `report.json`。隔离目录保留原始输入、制品、命令日志、恢复清单及各阶段包锁，便于复查；不写回正式包、版本、注册表或标签。正式发布前应将选定输入和证据归档到版本化制品记录，不能依赖临时目录长期保存。

## .NET 本地制品消费

当前仓库的公开发布流程面向 UPM，并未承诺 NuGet 包 ID、目标框架矩阵或发布源。本工具直接使用真实 Core 与 Deterministic 镜像工程执行 `dotnet pack`，仅在隔离快照中生成本地验证用 NuGet 包，不建立新的正式发布渠道。

验证制品分别使用 `0.1.0-consumer.baseline` 和 `0.1.0-consumer.current`，版本由命令属性传入，正式 package.json、csproj 与 API 基线不改。分析器只用于构建，不进入制品运行依赖；工具核对 Core 的 nuspec 只含相应版本的 Deterministic 依赖。

独立 net10.0 可执行工程仅声明一个 Core PackageReference，不链接仓库源码或镜像工程。NuGet.Config 清空其他源，只保留本地制品目录；每阶段使用新的消费包缓存，再检查 project.assets.json 只解析到 Core 与 Deterministic 两个包。运行后，输出目录中的两份 DLL 还须与相应 nupkg 内容逐字节一致，避免只改恢复清单但继续运行旧 DLL。

同一消费工程依次安装基线、替换为候选、回退到基线，每阶段执行八项最小调用。另建失败工程和空缓存，仅提供 Core 制品，确认缺少 Deterministic 时产生 NU1101，不从使用者全局缓存或外部源补齐依赖。失败工程不会破坏回退后可用的消费工程。

本次 .NET 三阶段及缺依赖拒绝验证通过。这证明本轮 net10.0 本地验证制品可在独立工程中恢复、执行和回退；不证明旧正式 NuGet 版本兼容、其他目标框架或线上安装可用。pack 输出中的缺少 NuGet 自述文件提示仍保留，未为了验证包装新增正式 NuGet 元数据或放宽源码警告。

## 真实 Unity 本地 tarball 消费

工具从两份输入生成标准 `package/` 根目录的 UPM tgz，包含真实源码、asmdef、文档与原有 .meta。独立 Unity 工程只显式安装 Core、Deterministic 两个本地 tarball，以及 Unity 内置的 unitywebrequest 模块，不安装 Demo、测试框架或其他 AbilityKit 包。

首次导入没有 Library，之后在同一工程依次替换候选与回退基线。每阶段都用真实 Editor 批处理编译并执行固定消费入口；须同时满足进程成功、生成本轮成功文件、PackageInfo 显示正确包名与版本、AbilityKit 闭包仅有两个包、包锁指向本阶段 tarball，以及以下四个程序集已生成：

- AbilityKit.Deterministic
- AbilityKit.Core
- AbilityKit.Core.Unity
- AbilityKit.Core.Editor

此外，工具逐文件比对 PackageInfo.resolvedPath 下的 C# 源码与相应输入快照的 SHA256，确保编译来源是安装的目标包。每阶段包含八项固定最小调用和三项包归属检查；最小调用覆盖稳定哈希、经过定点依赖的开方与向量长度、定点公开类型、有序集合及去重、单次释放，以及旧设置空实例兼容入口。该夹具不是新的生产使用方，不能拿它满足 Core 三消费者准入规则。

本次 Unity 2022.3.62f1 三阶段真实导入与最小调用通过，四个程序集均生成。UPM 包版本保持 0.1.0，因此这里验证的是 HEAD 快照到工作区候选的**同版本制品替换与回退**，不冒充已发布历史版本到新版本的升级。两个本地 tarball 都显式声明在 manifest 中，不能由此推断注册表会自动安装传递依赖；包锁和 PackageInfo 的证据仅对应本次本地安装。

本次未构建 Player，未验证 Mono/IL2CPP、运行时平台文件权限、请求中止与协程释放、SceneView 呈现或域重载生命周期。Editor 最小调用通过也不建立 .NET Int128 与 Unity decimal 算术路径的位一致性结论。

## 正式候选还需完成

将实际可发布版本固定到已评审源码与依赖制品后，还需验证注册表或约定 git 安装、真实旧版本的升级/回退、支持平台 Player 构建与生命周期场景。命名空间超额、Observability 准入、定点依赖目标差异和发布工具拒绝条件继续保留，不因本地消费验证通过而取消。其他包尚未获得相同整理或消费验证结论。
