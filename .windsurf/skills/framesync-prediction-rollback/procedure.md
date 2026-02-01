---
name: framesync-prediction-rollback
section: procedure
---

# Procedure (how to work on a prediction/rollback/reconcile task)

1. **确认帧推进来源**
   - `remote.TargetFrame` 是否推进？是否能拿到 `confirmed+1` 的 authoritative input？
   - `local.TryDequeue` 是否每 tick 有输入批次（允许为空）。

2. **确认 confirmed/predicted 基线**
   - per-world `confirmed` 是否单调递增。
   - `predicted >= confirmed` 是否被保持。

3. **确认窗口计算与归因**
   - backlog raw 与 EWMA 是否符合预期。
   - `window` 是否被 `MaxPredictionAheadFrames/MinPredictionWindow` clamp。
   - 若存在 idealFrame：确认 `effectiveWindow` 是否被压缩，stall 是否记到 `IdealFrameStalls`。

4. **确认回滚快照是否足够**（若启用 rollback）
   - `rollbackHistoryFrames` 是否覆盖最坏回滚跨度。
   - `rollbackCaptureEveryNFrames` 是否过大导致 restore 失败。

5. **确认 reconcile 的 compare 是否发生**（若启用 hash）
   - predicted hash 是否记录（PostTick）。
   - authoritative hash 是否到达并喂给 `OnAuthoritativeStateHash`。
   - 若 authoritative 先到，是否会在 predicted hash 记录后补一次 compare。

6. **复现与日志**
   - 优先用 Stats/Editor 面板定位（per-world）。
   - 必要时补 `Log.Info/Warning`（不要空 catch）。
