---
name: framesync-prediction-rollback
description: 客户端帧同步：预测窗口、idealFrame 时间门控、回滚/重演、对账（reconcile）与常见故障排查的速查与工作流程。
---

# framesync-prediction-rollback skill (index)

本 skill 用于处理：

- 预测推进不工作 / 卡住
- rollback 未触发或触发风暴
- reconcile mismatch 不出现或帧号不对齐
- idealFrame gate 导致 stalls 或 window 被压缩
- 多 world 场景下统计混在一起、无法定位

## Sections

- [when_to_use.md](when_to_use.md)
- [required_context.md](required_context.md)
- [key_files.md](key_files.md)
- [procedure.md](procedure.md)
- [examples_and_troubleshooting.md](examples_and_troubleshooting.md)
