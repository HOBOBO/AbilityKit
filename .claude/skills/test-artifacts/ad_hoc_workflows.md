# 绕过 gate 体系的注意事项

**首选：永远走 `run_test_gate.ps1`**。本页只覆盖必须绕过的情况。

## 必须显式指定产物路径

### Unity batchmode 直接调

```powershell
# ✗ 错（污染根目录，无 -logFile 则日志写 Unity/.. 即根目录）
& $editor -batchmode -projectPath Unity -executeMethod MyMethod

# ✓ 对（显式 -logFile）
& $editor -batchmode -projectPath Unity `
    -executeMethod MyMethod `
    -logFile artifacts/unity-manual/{yyyyMMdd-HHmmss}.log
```

### dotnet test 直接调

```powershell
# ✗ 错（产物散落默认位置）
dotnet test src/X.csproj

# ✓ 对（显式 --results-directory）
dotnet test src/X.csproj `
    --logger "trx;LogFileName=result.trx" `
    --results-directory artifacts/dotnet/{yyyyMMdd-HHmmss}
```

### Unity executeMethod 产物

`MultiplayerHeadlessHeroReplacementCommand` 等 `-executeMethod` 类的产物路径由类内部决定（通常读 `-xxxResult` 命令行参数）。必须传参或确认类内有合理默认。

## 清理策略

### 一次性清理所有测试产物

```powershell
# 清理 artifacts/ 下所有内容（保留 headless-archive 历史归档）
Remove-Item -Recurse -Force artifacts/test-gates/*
Remove-Item -Recurse -Force artifacts/headless/*
Remove-Item -Recurse -Force artifacts/dotnet/*
Remove-Item -Recurse -Force artifacts/unity-manual/*
Remove-Item -Recurse -Force artifacts/shooter-manual/*

# 清理所有 TestResults
Get-ChildItem -Path . -Filter "TestResults" -Directory -Recurse |
    Where-Object { $_.FullName -notmatch "\\(Library|\.kilo)\\" } |
    Remove-Item -Recurse -Force
```

### 清理超过 7 天的产物

```powershell
Get-ChildItem -Path artifacts/test-gates -Directory |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-7) } |
    Remove-Item -Recurse -Force
```

### 清理 .trx 文件

```powershell
Get-ChildItem -Path . -Filter "*.trx" -Recurse |
    Where-Object { $_.FullName -notmatch "\\(Library|\.kilo)\\" } |
    Remove-Item -Force
```

## 命名规范

### 时间戳格式

统一 `yyyyMMdd-HHmmss`（如 `20260720-143015`）。`run_test_gate.ps1` 默认就是这个格式。

### 描述性后缀

ad-hoc 调试时（如 `MultiplayerHeadlessHeroReplacementCommand`），可以用描述性后缀：

```
artifacts/headless/MultiplayerHeadlessHeroReplacement-20260720-143015-debug-buffer.xml
artifacts/headless/MultiplayerHeadlessHeroReplacement-20260720-143215-rerun.xml
```

**不要**用模糊后缀如 `-final` / `-v2` / `-last`（看不出时间顺序，且容易堆积）。

### 复合命名

多步骤 ad-hoc 测试：`{project}-{scenario}-{timestamp}.{ext}`

```
artifacts/unity-manual/moba-zhaoyun-skill1-20260720-143015.xml
artifacts/shooter-manual/multiprocess-minimal-20260720-143015/
```

## 历史教训（不要再犯）

### 教训 1：默认 resultPath 写死到项目根

`MultiplayerHeadlessHeroReplacementCommand.cs` 旧默认：

```csharp
resultPath = Path.GetFullPath("../MultiplayerHeadlessHeroReplacement.xml");
```

导致根目录累积 24 个调试 XML。2026-07-20 修复为 `artifacts/headless/{timestamp}.xml`。

### 教训 2：Unity batchmode 不指定 -logFile

导致日志默认写到 `Unity/` 上一级（即项目根），产生 `UnityHeadless*.log` 污染。`.gitignore` 已加 `**/*.log` 兜底。

### 教训 3：调试快照无命名规范

24 个 XML 带 `-final` / `-fix` / `-rerun` / `-probe` / `-diagnostic` / `-buffer-replay` / `-wrapper-fix` 等无序后缀，堆积后无法判断时间顺序。统一改用时间戳。

## 如何把 ad-hoc 测试升级为 gate

如果某个 ad-hoc 测试开始定期跑，升级为 gate：

1. 在 `tools/test-gates.json` 加 gate 定义
2. 选择合适的 step kind（dotnet-test / unity-editmode-test / unity-execute-method / powershell-script）
3. 决定 P0/P1/P2 级别与 ciPolicy（runOnPullRequest / runOnPush / runOnSchedule）
4. 用 `./tools/run_test_gate.ps1 -Gate new-gate-name` 验证
5. （可选）在 `.github/workflows/abilitykit-test-gates.yml` 加 job
