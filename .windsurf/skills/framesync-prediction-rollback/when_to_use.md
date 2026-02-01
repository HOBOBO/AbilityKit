---
name: framesync-prediction-rollback
section: when_to_use
---

# When to use

使用本 skill 的典型场景：

- 你在修改/调试 `ClientPredictionDriverModule`（预测推进、窗口、replay、rollback、hash reconcile）。
- 你在修改/调试 `Rollback/*`（快照捕获、restore、provider payload、ring buffer 容量）。
- 你在修改/调试 time sync/idealFrame，并需要确认其对预测窗口的影响与 stall 归因。
- 你需要把统计做成 per-world，或在 Editor 面板中展示关键指标。
