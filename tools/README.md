# tools/ 脚本索引

> 所有项目工具脚本位于 `tools/` 根目录（平铺）。按命名前缀分类如下。

## 测试 / 验证（test & validate）

| 脚本 | 用途 |
|------|------|
| `run_test_gate.ps1` | 统一测试门禁入口（CI + 本地） |
| `run_shooter_unity_headless_multiplayer.ps1` | Shooter Unity 无头双实例多人冒烟测试 |
| `run_moba_unity_headless_multiplayer.ps1` | MOBA Unity 无头双实例多人冒烟测试 |
| `run_et_battle_smoke.ps1` | ET 对战冒烟测试 |
| `run_shooter_aoi_lod_gate.ps1` | Shooter AOI/LOD 性能门禁 |
| `run_moba_skill_analysis.ps1` | MOBA 技能分析报告 |
| `run_runtime_benchmarks.ps1` | 运行时基准测试 |
| `validate_shooter_test_gates.ps1` | Shooter 测试门禁契约校验 |
| `validate_abilitykit_package_json.ps1` | 包 package.json 格式校验 |
| `validate_moba_codegen_ownership.ps1` | MOBA 代码生成归属校验 |
| `validate_moba_hero_manifest.ps1` | MOBA 英雄清单校验 |
| `validate_moba_hero_acceptance_coverage.ps1` | MOBA 英雄验收覆盖校验 |
| `build_moba_content_report.ps1` | 生成并校验 MOBA 跨表依赖、英雄可达性和资源完整度 JSON 报告（含 `.tests.ps1`） |
| `export_moba_content_ir.ps1` | 从离线报告导出可供 Unity、Web、DOT 和 CI 共同消费的稳定 graph/diagnostics JSON |
| `build_moba_content_graph.ps1` | 从通用 graph/diagnostics JSON 生成可筛选的自包含 HTML 依赖图与 Graphviz DOT |
| `audit_core_boundaries.ps1` | 审计 Core 平台/API 边界与命名空间所有权，并在 Unity Packages、`src`、`Server` 生产源码中阻止旧 Continuous、Config、Reflection、Marker bootstrap、DebugDraw 和 Dispose 消费回流；无 `rg` 时递归剪枝构建目录，以单次包/托管源码扫描复用预加载文本缓存，并复用预编译边界正则 |

## 构建 / 项目设置（build & setup）

| 脚本 | 用途 |
|------|------|
| `clone_unity_project_for_multiplayer.ps1` / `.cmd` | 克隆 Unity 项目为第二实例（多人测试） |
| `update_abilitykit_package_versions.ps1` | 批量更新 AbilityKit 包版本 |
| `sync_unity_runtime_csproj.ps1` | 同步 Unity/Runtime csproj |
| `audit_unity_package_dependencies.ps1` | 审计 Unity 包依赖 |
| `open_samples_web.ps1` / `.cmd` | 打开 Samples Web 页面 |

## 协议 catalog 治理（protocol catalog governance）

| 脚本 | 用途 |
|------|------|
| `compile-protocol-catalogs.ps1` | 协议 catalog 唯一生成/校验入口：把 `Protocols/Catalogs/*.protocol.yaml` 编译为 manifest、`BuiltInProtocolCatalogs.g.cs` 与兼容 metadata 投影。运行时以 `ProtocolCatalogRegistry` 为真源，metadata 可通过生成的 `CreateRegistry(ProtocolCatalogRegistry)` 创建只读视图。默认重新生成写回；`-Check` 只读校验并拦截陈旧产物（CI PR/push 门禁 `protocol-catalogs` job 调用它） |
| `export-protocol-wire.ps1` | 协议 Wire 正式导出唯一生成/校验入口（Shooter/MOBA 复用，含 `.tests.ps1`）：按项目把 `Protocols/WireSchemas/*.wire.yaml` 确定性导出为 Unity 协议包内已提交的 MemoryPack 产物。`-Check` 只读校验（CRLF/LF 归一化后逐文件比较），stale 退出码 3；`-Strict` 把缺失 wire schema 升级为失败（CI `protocol-catalogs` job 调用） |
| `AbilityKit.Protocol.CatalogCompiler/` | codec-neutral 确定性编译工具（.NET 控制台），提供 MemoryPack backend adapter 与 Protobuf backend SPI；Protobuf 可通过 `--wire-input <root> --project <id> --export-protobuf <folder> [--check]` 导出/校验 |

## 配置同步（config sync）

| 脚本 | 用途 |
|------|------|
| `sync_moba_json_configs.ps1` | 同步 MOBA JSON 配置 |
| `moba_business_id.ps1` | MOBA 业务 ID 生成/校验（含 `.tests.ps1`） |
| `new_moba_hero_manifest.ps1` | 新建 MOBA 英雄清单 |
| `moba-content-dependency-contract.json` | 声明 MOBA 配置表、静态引用、外部引用 authority 与资源质量规则 |
| `moba-content-graph.schema.json` / `moba-content-diagnostics.schema.json` | 通用分析数据的 JSON Schema；查看器和 Unity DTO 以此为边界 |

## 文档 / 导出（docs & export）

| 脚本 | 用途 |
|------|------|
| `export_design_docs_for_feishu.ps1` | 导出设计文档到飞书 |
| `sync_design_docs_to_feishu.ps1` / `.cmd` | 同步设计文档到飞书 |
| `export_zhihu_mermaid_assets.ps1` | 导出知乎 Mermaid 图表素材 |
| `generate_abilitykit_ppt_assets.ps1` | 生成 AbilityKit PPT 素材 |

## 子目录

| 目录 | 用途 |
|------|------|
| `AbilityKitPptAssetGenerator/` | PPT 素材生成器（C# 项目） |
| `ai_training/` | AI 训练（Python） |

## 命名规范

- `run_*.ps1` — 执行类脚本（测试/冒烟/基准）
- `validate_*.ps1` — 校验类脚本（门禁/契约）
- `sync_*.ps1` — 同步类脚本（配置/文档）
- `export_*.ps1` — 导出类脚本
- `clone_*.ps1` — 项目设置类
- `update_*.ps1` / `audit_*.ps1` — 维护类

## 注意

如果后续要拆分为子目录（`tools/test/`、`tools/build/` 等），需要同步更新每个脚本内的 `$PSScriptRoot` 路径（当前 18 个脚本用 `$PSScriptRoot\..` 或 `Join-Path $PSScriptRoot '..'` 定位仓库根，移到子目录后需加一层 `..\..`）。
