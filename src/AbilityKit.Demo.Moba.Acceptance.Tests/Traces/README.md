# MOBA 验收真实 trace 基线（Traces/）

本目录存放**真实捕获**的 MOBA 验收 trace（`<caseId>_trace.jsonl`），供 dotnet 验收判定层
（`AbilityKit.Demo.Moba.Acceptance`）做回归。`CompositeTraceSource` 会**优先**读这里，读不到再回退到
`Fixtures/`（合成 fixture）。

## 如何生成 / 刷新

```powershell
powershell -ExecutionPolicy Bypass -File tools\capture_moba_acceptance_traces.ps1
```

脚本在 Unity batchmode 下跑整个期望目录（`MobaAcceptanceWebCommand.RunDirectoryFromCommandLine`），
把每个用例的 `<caseId>_trace.jsonl` 收集到本目录，并写 `capture-manifest.json`。需要本地装 Unity 2022.3.62f1。

## 纪律

- **不要手编**这里的文件——它们是真实 sim 运行的产物。改行为应该改实现，再重跑脚本刷新。
- 捕获后 review `capture-manifest.json` + 脚本输出的 per-case 概览，确认通过用例的 trace 后再 `git add Traces/` 提交。
- 行为**有意变更**后重跑脚本刷新基线；`moba-acceptance-dotnet` 门禁变红 = 真实回归信号（区分：是预期改动→更新基线+期望；是真 bug→修实现）。
- 当前若无真实 trace（本目录除 README/manifest 外为空），门禁对未覆盖用例报 `needs-trace`（不计 failed），由 `Fixtures/` 的合成 fixture 兜底至少一个用例。
