# Changelog — com.abilitykit.record

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。
AbilityKit 整体仍处于开发期，0.x 版本不承诺向后兼容；重大变更会在对应版本条目里写明迁移要点。

## [0.1.0] — 2026-07-31 — Beta

首个 Beta 里程碑。录制/回放/帧记录适配是确定性回放与对局复盘的基础，已被两个生产级 demo
端到端验证 + 直接契约测试（`src/AbilityKit.Record.Tests`，10 用例）。

### API 边界（本包承诺稳定的部分）
- 帧记录写入：delta/change-set 事件采集与序列化（`FrameRecordFile` 等）。
- 回放：记录文件读取、按帧重放命令。
- 与帧同步/快照的录制适配。

### 构建门槛
- 在 `src/AbilityKit.Record` 上启用 `AbilityKitStable=true`：`TreatWarningsAsErrors` 已开启，
  **本包自身代码非可空/非文档类警告为零**（依赖包仍按各自设置编译）。

### 已知限制 / 不在 0.1.0 承诺范围
- 可空性(CS8xxx)暂为咨询级警告，不计入硬门槛。
- 其依赖 `world.framesync`/`world.snapshot`/`host` 尚未升 0.1.0（本轮随后推进）；待它们升版后同步本包依赖版本。
- 性能基线尚未纳入门禁；不承诺跨 major 版本二进制兼容。

### 变更
- 由 `0.0.1` 提升为 `0.1.0`（Beta）；依赖 `com.abilitykit.core` 同步升至 `0.1.0`。
- 建立 CHANGELOG 与发版基线。
