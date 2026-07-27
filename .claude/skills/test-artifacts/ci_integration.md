# CI 集成（GitHub Actions）

## 工作流文件

`.github/workflows/abilitykit-test-gates.yml`

由 `tools/validate_shooter_test_gates.ps1` 验证契约。

## Job 清单（11 个）

| Job | 触发 | 对应 Gate |
|-----|------|----------|
| `contract-validation` | pull_request | （验证 test-gates.json 本身）|
| `precheck` | pull_request + push | `precheck` |
| `shooter-fast` | pull_request + push | `shooter-fast` |
| `shooter-integration` | pull_request + push | `shooter-integration` |
| `shooter-unity-playmode` | pull_request + push | `shooter-unity-playmode` |
| `shooter-performance-smoke` | pull_request + push + schedule | `shooter-performance`（smoke step）|
| `shooter-multiprocess` | push + schedule | `shooter-multiprocess` |
| `shooter-multiprocess-compatibility` | schedule | `shooter-multiprocess-compatibility` |
| `shooter-multiprocess-ownership-cleanup` | schedule | `shooter-multiprocess-ownership-cleanup` |
| `shooter-performance-full` | schedule | `shooter-performance`（full step）|
| `regression` | schedule + manual | `regression` |

## 触发器

```yaml
on:
  pull_request:    # 合并前
  push:            # 合并后（branches: [main]）
  schedule:
    - cron: "0 18 * * *"   # 每天 UTC 18:00 = Asia/Shanghai 02:00
  workflow_dispatch:       # 手动触发（默认 gate=regression）
```

## Artifact 上传（关键）

本地默认输出是 `local/Logs/test-gates/`，但 CI 必须显式传入 `-ResultsDirectory artifacts\\test-gates\\{job}`，再上传同一目录。每个 job 末尾必须：

```yaml
- name: Upload test artifacts
  if: always()    # 即使前面 step 失败也上传
  uses: actions/upload-artifact@v4
  with:
    name: {job-name}-artifacts
    path: artifacts/test-gates/
```

`if: always()` 是契约要求。`validate_shooter_test_gates.ps1` 会检查每个必需 job 都有这个守卫。

CI 使用 `artifacts/` 仅因为 GitHub Actions 的工作区短生命周期且上传步骤需要稳定路径；不得将其作为本地手动测试的默认输出目录。

## 本地复现 CI

```powershell
# 复现 PR 触发的 precheck
./tools/run_test_gate.ps1 -Gate precheck -CI
# 本地结果仍写入 local/Logs/test-gates/；不要复制 CI 的 artifacts/ 覆写参数。

# 复现 push 触发的 shooter-fast
./tools/run_test_gate.ps1 -Gate shooter-fast -CI

# 复现 schedule 的 regression
./tools/run_test_gate.ps1 -Gate regression -CI
```

`-CI` 参数启用 `$ProgressPreference = 'SilentlyContinue'` 与 `--nologo`。

## scheduled 时间

- **UTC 18:00 = Asia/Shanghai 02:00**（深夜跑，不占用工作时段）
- 每天 1 次（`regression` gate）

## artifact 保留策略

GitHub Actions 默认保留 90 天。可在 workflow 用 `retention-days` 调整：

```yaml
- uses: actions/upload-artifact@v4
  with:
    name: ...
    path: ...
    retention-days: 30   # 减少存储
```

## 跨 job artifact 共享

如果 job B 需要 job A 的产物（如 `contract-validation` 后跑 `precheck`）：

```yaml
needs: [contract-validation]
steps:
  - uses: actions/download-artifact@v4
    with:
      name: contract-validation-artifacts
```

## 常见 CI 问题

### 问题：Unity Editor 不存在

`run_test_gate.ps1` 的 `Get-UnityEditorPath` 默认 `C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe`。

CI 必须在 setup 步骤安装 Unity 2022.3.62f1。

### 问题：Python 缺失（PyYAML）

`validate_shooter_test_gates.ps1` 需要 Python 3 + PyYAML 6.x。CI 用 `actions/setup-python@v5`。

### 问题：端口被占

shooter multiprocess gate 用固定端口（44101/44201/44301）。如果并行 job，必须确保端口不冲突（job 间隔离）。
