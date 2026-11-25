# AbilityKit 工具入口

这里按框架共用、项目专用和文档工具导航，不是目录文件清单。包内工具随包维护，实际参数与依赖以入口帮助为准。除脚本另有说明，示例从仓库根目录执行。

- 查门禁：`./tools/run_test_gate.ps1 -List`。职责、步骤和策略以 [test-gates.json](test-gates.json) 为准；正式验收通过 runner 执行，[局部调试或直接运行步骤不等于门禁通过](../Docs/AbilityKit测试门禁与批量回归规范.md#门禁与手工入口)。
- 查产物：见 [测试门禁与批量回归规范](../Docs/AbilityKit测试门禁与批量回归规范.md)。本地测试报告统一放 `local/Logs/`。
- 查 Unity 编辑器工具：全局导航从 `Window/AbilityKit/Hub` 进入；新增和整理菜单、上下文操作、批处理前先读[编辑器工具准入规范](../Docs/design/13-FrameworkCore/09-EditorPlatformConvergence.md#52-编辑器工具准入规范)。
- 查框架设计：从 [设计文档总索引](../Docs/design/00-index.md) 进入，不在工具索引维护架构副本。
- 查仓库结构：`python -X utf8 tools/check_repo_structure.py --strict`，检查共享文档与计划、包总索引及包维护指引的 UTF-8、本地链接，并报告散落产物（Python 3.10+、Git，不运行测试、不移动文件）。文档分工与模板见[文档体系与维护规范](../Docs/design/10-EngineeringQuality/20-DocumentationSystemStandard.md)。
- 提交前查本地文件：`python -X utf8 tools/check_repo_structure.py --strict --staged-outputs`，检查新增或修改的暂存运行产物，以及误暂存的 `local/tools/` 文件。运行结果不入库；有消费者的基线放在对应的 `Fixtures/` 或 `TestData/`，保留更新方式。此命令不安装 hook 或接入 CI。
- 查随包文档：在上述命令后添加 `--package com.abilitykit.attributes --package com.abilitykit.world.di`（按改动选择包，可重复指定），同时检查这些包的 Markdown 和越出包目录的本地链接；不默认阻断其余包的存量问题。

## 框架共用工具

### 测试与验证

| 脚本 | 用途 |
|------|------|
| [`run_test_gate.ps1`](run_test_gate.ps1) | 统一测试门禁执行入口；`-List` 只列配置，`-Gate <name>` 执行指定门禁，不传则执行配置中的默认门禁 |
| [check_repo_structure.py](check_repo_structure.py) | 本地结构验收：编码或常见 Markdown 路径错误可阻断、散落的运行产物只提示 |
| [run-unity-editmode-tests.ps1](run-unity-editmode-tests.ps1) | 手工诊断：按 Unity EditMode 程序集和测试名定位问题，默认输出到 `local/Logs/`；不执行 gate 配置中的 Unity 步骤 |
| [`run_runtime_benchmarks.ps1`](run_runtime_benchmarks.ps1) | `runtime-performance-measurement` 的基准步骤；手工可按 `-Module` / `-Scope` 聚焦，使用 `-OutputPath` 显式写入 `local/Logs/` |
| [`test_core_documentation_gate.ps1`](test_core_documentation_gate.ps1) | `core-stability` 的步骤；可局部验证 Core XML 文档准入策略，不编译 Unity |
| [`test_release_plan.ps1`](test_release_plan.ps1) | `core-stability` 的步骤；可局部校验包发布计划约束 |
| [`run_test_gate.ps1 -Gate triggering-release-consumer`](run_test_gate.ps1) | Triggering 发布候选的干净 Unity 安装门禁；[运行条件与 Player 探针](publish/README.md#triggering-干净消费门禁) |
| [`validate_abilitykit_package_json.ps1`](validate_abilitykit_package_json.ps1) | 包 package.json 格式校验 |
| [`validate_battlescenario_artifacts.ps1`](validate_battlescenario_artifacts.ps1) | BattleScenario artifact v1 Schema 样例与兼容性测试；依赖通过 `npm ci --prefix tools/battlescenario-artifact-schema` 安装 |
| [`audit_core_boundaries.ps1`](audit_core_boundaries.ps1) | 审计 Core 平台/API 边界与命名空间所有权，并在 Unity Packages、`src`、`Server` 生产源码中阻止旧基础设施消费回流；项目直链检查在 Git 工作区覆盖已跟踪项目和未忽略的新项目，在无 Git 元数据的源码副本中剪枝构建及临时目录，不依赖 `rg` 的项目枚举行为 |

### 构建与发布

| 脚本 | 用途 |
|------|------|
| [`clone_unity_project_for_multiplayer.ps1`](clone_unity_project_for_multiplayer.ps1) / [`.cmd`](clone_unity_project_for_multiplayer.cmd) | 克隆 Unity 项目为第二实例（多人测试） |
| [`update_abilitykit_package_versions.ps1`](update_abilitykit_package_versions.ps1) | 批量更新 AbilityKit 包版本 |
| [`sync_unity_runtime_csproj.ps1`](sync_unity_runtime_csproj.ps1) | 同步 Unity/Runtime csproj |
| [`audit_unity_package_dependencies.ps1`](audit_unity_package_dependencies.ps1) | 只读审计选定包的直接程序集依赖；默认运行时，`-IncludeEditor` 纳入随包编辑器，`-StrictInternalReferences` 拒绝未解析的 AbilityKit/GUID 引用；自测：`python -B -X utf8 -m unittest discover -s tools/tests -p test_package_dependencies.py -v`，夹具位于 `local/Logs/` |
| [框架包发布工具](publish/README.md) | 发布工具、验证方式及版本契约的专题入口 |

### 协议 Catalog

| 脚本 | 用途 |
|------|------|
| [`compile-protocol-catalogs.ps1`](compile-protocol-catalogs.ps1) | 从 `Protocols/Catalogs/*.protocol.yaml` 生成 manifest、catalog 和 metadata 投影；默认写回，`-Check` 校验现有输出而不写回 |
| [`export-protocol-wire.ps1`](export-protocol-wire.ps1) | 从 `Protocols/WireSchemas/*.wire.yaml` 导出正式的 MemoryPack 产物；`-Check` 校验，`-Strict` 将缺失 schema 视为失败 |
| [`AbilityKit.Protocol.CatalogCompiler/`](AbilityKit.Protocol.CatalogCompiler/) | codec-neutral 确定性编译工具（.NET 控制台），提供 MemoryPack backend adapter 与 Protobuf backend SPI；Protobuf 可通过 `--wire-input <root> --project <id> --export-protobuf <folder> [--check]` 导出/校验 |

## 项目与示例专用工具

### Shooter、ET 与示例

| 脚本 | 用途 |
|------|------|
| [`run_shooter_unity_headless_multiplayer.ps1`](run_shooter_unity_headless_multiplayer.ps1) | Shooter Unity 无头双实例冒烟，验证范围不同于 `shooter-multiprocess` 的服务端多进程预设 |
| [`run_shooter_aoi_lod_gate.ps1`](run_shooter_aoi_lod_gate.ps1) | `shooter-performance` 的共用步骤；正式验收跑门禁，手工可选单个 `-Profile` 并用 `-OutputPath` 写入 `local/Logs/` |
| [`validate_shooter_test_gates.ps1`](validate_shooter_test_gates.ps1) | 静态校验门禁引用和 Shooter 相关约束；当前检出运行 `-ConfigOnly`，严格模式另外要求工作流，详见[门禁校验](../Docs/AbilityKit测试门禁与批量回归规范.md#8-ci-接入与静态校验) |
| [`run_et_battle_smoke.ps1`](run_et_battle_smoke.ps1) | ET 对战冒烟测试 |
| [`verify-tiny-starter.ps1`](verify-tiny-starter.ps1) | Tiny Starter 验收；`-SkipUnity` 跳过 Unity 批处理验证 |
| [`open_samples_web.ps1`](open_samples_web.ps1) / [`.cmd`](open_samples_web.cmd) | 打开 Samples Web 页面 |
| [`fix_sample_mojibake.ps1`](fix_sample_mojibake.ps1) | 按需维护：默认只报告示例源码注释乱码；显式 `-Apply` 才写入示例包。参见[示例目录与 Unity 宿主](../Docs/design/01-OverviewAndGettingStarted/05-SampleCatalogUnityHosts.md) |

### MOBA 测试、分析与配置

| 脚本 | 用途 |
|------|------|
| [`run_moba_unity_headless_multiplayer.ps1`](run_moba_unity_headless_multiplayer.ps1) | MOBA Unity 无头双实例冒烟，不替代 `moba-smoke` / `moba-multiprocess` 的服务端验证 |
| [`run_moba_skill_analysis.ps1`](run_moba_skill_analysis.ps1) | MOBA 技能分析报告 |
| [`validate_moba_codegen_ownership.ps1`](validate_moba_codegen_ownership.ps1) | MOBA 代码生成归属校验 |
| [`validate_moba_hero_manifest.ps1`](validate_moba_hero_manifest.ps1) | MOBA 英雄清单校验 |
| [`validate_moba_hero_acceptance_coverage.ps1`](validate_moba_hero_acceptance_coverage.ps1) | MOBA 英雄验收覆盖与测试证据引用校验；可用 `-ReportPath` 输出证据报告 |
| [`build_moba_content_report.ps1`](build_moba_content_report.ps1) | 生成并校验 MOBA 跨表依赖、英雄可达性和资源完整度 JSON 报告（含 `.tests.ps1`） |
| [`export_moba_content_ir.ps1`](export_moba_content_ir.ps1) | 从离线报告导出可供 Unity、Web、DOT 和 CI 共同消费的稳定 graph/diagnostics JSON |
| [`build_moba_content_graph.ps1`](build_moba_content_graph.ps1) | 从通用 graph/diagnostics JSON 生成可筛选的自包含 HTML 依赖图与 Graphviz DOT |
| [`sync_moba_json_configs.ps1`](sync_moba_json_configs.ps1) | 同步 MOBA JSON 配置 |
| [`moba_business_id.ps1`](moba_business_id.ps1) | MOBA 业务 ID 生成/校验（含 `.tests.ps1`） |
| [`new_moba_hero_manifest.ps1`](new_moba_hero_manifest.ps1) | 新建 MOBA 英雄清单 |
| [`moba-content-dependency-contract.json`](moba-content-dependency-contract.json) | 声明 MOBA 配置表、静态引用、外部引用 authority 与资源质量规则 |
| [`moba-content-graph.schema.json`](moba-content-graph.schema.json) / [`moba-content-diagnostics.schema.json`](moba-content-diagnostics.schema.json) | 通用分析数据的 JSON Schema；查看器和 Unity DTO 以此为边界 |

## 文档与导出工具

| 脚本 | 用途 |
|------|------|
| [`export_design_docs_for_feishu.ps1`](export_design_docs_for_feishu.ps1) | 导出设计文档到飞书 |
| [`sync_design_docs_to_feishu.ps1`](sync_design_docs_to_feishu.ps1) / [`.cmd`](sync_design_docs_to_feishu.cmd) | 同步设计文档到飞书 |
| [`export_zhihu_mermaid_assets.ps1`](export_zhihu_mermaid_assets.ps1) | 导出知乎 Mermaid 图表素材 |
| [`generate_abilitykit_ppt_assets.ps1`](generate_abilitykit_ppt_assets.ps1) | 生成 AbilityKit PPT 素材 |

## 内部支撑与专题目录

`.cmd` 薄包装器、自测、门禁底层步骤、schema 和模板跟随所属工具维护，不另列为正式入口。以下仅导航到部分支撑项目与独立专题。

| 目录 | 用途 |
|------|------|
| [`AbilityKitPptAssetGenerator/`](AbilityKitPptAssetGenerator/) | PPT 素材生成器（C# 项目） |
| [`ai_training/`](ai_training/) | AI 训练（Python） |
| [`battlescenario-artifact-schema/`](battlescenario-artifact-schema/) | BattleScenario artifact Schema 的锁定 Node.js 校验依赖和兼容性测试 |

## 命名规范

- `run_*.ps1` — 执行类脚本（测试/冒烟/基准）
- `validate_*.ps1` — 校验类脚本（门禁/契约）
- `sync_*.ps1` — 同步类脚本（配置/文档）
- `export_*.ps1` — 导出类脚本
- `clone_*.ps1` — 项目设置类
- `update_*.ps1` / `audit_*.ps1` — 维护类

## 临时工具与退役记录

一次性探针、迁移和修补脚本只放 `local/tools/<主题>/`，不入库。正式准入、写入边界和退役流程见[仓库结构治理](../.claude/skills/repo-structure-governance/SKILL.md#工具准入与退役)。已有命令不为统一命名而迁移；确需移动时先检查 `$PSScriptRoot`、构建和门禁等调用方。

退役文件仅留 Git 历史。需要查阅时用 `git show <修订>:<旧路径>`，不要将历史脚本当作现行入口。

| 旧路径 | 退役原因与现行维护方式 | 可查修订 |
|------|------|------|
| `tools/patch_behavior_tree_tests_quality.py` | 对旧版测试源码的硬编码文本改写，无现行调用；后续直接维护 `src/AbilityKit.BehaviorTree.Tests/` 中的测试与帮助代码。 | `ac3c69052` |
| `tools/rebuild_moba_skill_flows_1007_1008.py` | 对指定旧英雄资源的一次性改写，无现行调用；后续按 [MOBA 技能编写 Skill](../.claude/skills/moba-skill-authoring/SKILL.md) 使用当前创作和发布链。 | `b7618e1ae` |
