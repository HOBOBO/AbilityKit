# Changelog — com.abilitykit.host.extension

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。
AbilityKit 整体仍处于开发期，0.x 版本不承诺向后兼容。

## [0.1.0] — 2026-07-31 — Beta

首个 Beta 里程碑。本版本的核心变更是**范围收口**：将 38 个 Moba 专属 host adapter
（Runtime/Moba/ 子树，含 3 个 asmdef：`AbilityKit.Host.Extensions.Moba` / `.Moba.Client` / `.Moba.Server`）
物理提取到新包 `com.abilitykit.demo.moba.host`。提取后本包仅包含通用框架代码
（FrameSync/Rollback/Session/Time/WorldStart/Server/Client），不再依赖任何
Moba protocol 或 demo-share 包。.NET 项目侧也同步拆分出 `src/AbilityKit.Demo.Moba.Host.csproj`。

### 迁移说明
- 如果你的项目依赖 Moba host 类型（`IMobaBattleRuntimePort`、`MobaGameStartSpec`、`MobaRoomOrchestrator` 等）：
  请新增对 `com.abilitykit.demo.moba.host`（Unity）或 `src/AbilityKit.Demo.Moba.Host.csproj`（.NET）的引用。
  Unity asmdef 的 `references` 数组无需改——Moba asmdef 名称未变，只要 package.json 依赖了新包即可解析。
- 如果你的项目仅使用通用 host.extension 类型（FrameSync/Rollback 等）：无需任何更改。
- Shooter / Samples 项目原本通过传递依赖意外继承到了 Moba 类型——本版本后它们不会再看到 Moba 类型。

### 其他变更
- `src/AbilityKit.Host.Extension.csproj`：开 `AbilityKitStable=true`，RootNamespace 修正。
- 本包已具备框架中立性；后续 0.1.0 的验收见通用基础层/同步层 roadmap。
