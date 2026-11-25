# Core 与 Deterministic 首轮候选验收

2026-11-20｜`0.1.0`｜结论：本地独立消费通过，仍为 `candidate`，不具备发布签核。

本页保留首轮固定提交证据。当前支持分级和下一步以[首发支持清单](../../../Unity/Packages/com.abilitykit.core/Documentation~/FirstReleaseSupport.md)及[后续复验记录](16-CoreResponsibilityAudit.md#首发支持清单与本地候选复验2026-11-21)为准；Windows Player 不在本候选的验收范围内。

## 候选范围与承诺

- 这两个包是首个验收样板，不改变七包发布清单。Core 的纯 C# 基础程序集直接依赖 Deterministic；Unity 适配与 Editor 不在 .NET 基础程序集内。玩法、宿主、网络和 Demo 不进入消费闭包。
- 候选兼容复核以 `src/AbilityKit.Core` 与 `src/AbilityKit.Deterministic` 各自的 `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` 及包内行为契约为准，不把签名通过当作行为保证，也不自动确认所有待发布条目。Core 的职责、所有权与弃用策略见 [包边界](../../../Unity/Packages/com.abilitykit.core/Documentation~/CoreBoundary.md)。
- 本轮只验证 Windows 上真实 Unity `2022.3.62f1` Editor 消费与 `.NET 10` 本地验证制品。不承诺正式 NuGet 渠道、其他框架或 Player 后端。Fixed64 的 `Int128` 与 `decimal` 路径在负数乘法舍入与中间范围上有已知差异；不承诺 Unity 与 .NET 跨目标位一致，详见 [定点数学桥接契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/DeterministicMathBridge.md)。若首发必须承诺跨目标锁步，该差异须先评审并验证，不能仅作为文档限制放行。

## 复现与验收结果

基线为 `bfdff7c52807b082285077b59790a2bc1d64d1f5`，候选为 `bcc9cea4a2b051baf387d58b7b569d8522227945`。二者都是已提交快照，不是正式历史版本；候选及探针不从当前工作区取材。构建策略随各自提交提取，候选保留 Core 局部 XML 文档错误门禁。

```powershell
python tools/validate_core_consumer.py --baseline-commit bfdff7c52807b082285077b59790a2bc1d64d1f5 --candidate-commit bcc9cea4a2b051baf387d58b7b569d8522227945
```

`--mode prepare` 可先生成快照，再用 `--run-root <目录> --mode dotnet` 或 `--mode unity` 续跑，不能在续跑时替换提交。工具复核快照、制品和探针等哈希，将 .NET 编译输出留在独立副本。另行验证了篡改 tarball、清单、探针和追加快照源码均被拒绝。

| 验收项 | 结果 |
| --- | --- |
| 固定候选包单测 | `.NET SDK 10.0.401`，Release 下 Core 531 项、Deterministic 90 项通过，零失败、零跳过。 |
| 干净 .NET 消费 | 安装基线、替换候选、回退基线均通过，每阶段 8 项最小调用。仅声明 Core 引用即可在离线源解析 Deterministic；移除内核后恢复以 `NU1101` 失败。验证了消费 DLL 与本地制品一致。 |
| 干净 Unity Editor 消费 | 首次工程无 `Library`；本地 tarball 三阶段均通过，每阶段 8 项最小调用及 3 项包归属检查。验证了包锁、程序集和实际解析的 C# 源码哈希。两个包均显式安装，不证明注册表会自动解析传递依赖。 |

本机证据归档：`local/Logs/core-stabilization/core-candidate-bcc9cea4a/`。`report.json` 记录完整输入及制品哈希，`sha256.json` 索引归档副本；目录包含 4 份 UPM tarball、4 份本地 NuGet 验证包、锁文件、日志和两份 TRX。该目录不进入 Git；正式签核需将对应证据归档至发布制品存储。

## 发布处置

| 分类 | 本轮判定 |
| --- | --- |
| 阻断发布 | 七包批次未做正式输入签核。工作区的严格预览此前在 `protocol` 生成文件处拒绝；旧 Core 命名空间边界审计此前在 Collision 和 Record 处失败。本轮未重跑全量 `core-stability` 或处理这些问题；不得用两个包的消费通过替代。正式注册表的安装与传递依赖解析未验收。 |
| 明确限制 | 本轮仅证明同版本 UPM 制品替换与回退；无正式旧版本可验证。未验证 Player 的 Mono/IL2CPP、请求与域生命周期。Observability 没有新增独立使用方证据，待复核准入；不扩大稳定面。 |
| 后续整理 | 目录命名、文档搬迁和内部重排不纳入这轮验收；涉及公开符号、程序集或 Unity GUID 的改动另行评审。 |

下一步先用同一候选 tarball 在独立工程补 Windows Player 构建及最小运行证据，分开记录 Mono、IL2CPP 的结果，再确定首发支持矩阵。本轮未修改运行时、版本或发布清单，未创建标签或发布。
