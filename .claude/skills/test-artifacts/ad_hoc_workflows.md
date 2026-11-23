# 绕过 gate 体系的注意事项

已有 gate 时首选 `run_test_gate.ps1`。本页覆盖直接运行测试、构建或 Unity 批处理的情况。

## 必须显式指定产物路径

### Unity batchmode 直接调

```powershell
$unityRun = New-Item -ItemType Directory -Path "local/Logs/unity-manual/$(Get-Date -Format yyyyMMdd-HHmmssfff)"
& $editor -batchmode -projectPath (Resolve-Path -LiteralPath Unity).Path `
    -executeMethod MyMethod -logFile (Join-Path $unityRun.FullName 'editor.log')
```

Unity Test Runner 同时传 `-testResults`，指向同一运行目录；直接调用 `-executeMethod` 时，按实际入口传它自己的结果参数。使用绝对输出路径，避免子进程工作目录改变落点。

### dotnet test 直接调

```powershell
$dotnetRun = New-Item -ItemType Directory -Path "local/Logs/dotnet/$(Get-Date -Format yyyyMMdd-HHmmssfff)"
dotnet test src/X.csproj `
    --logger "trx;LogFileName=result.trx" `
    --results-directory $dotnetRun.FullName
```

### Unity executeMethod 产物

`-executeMethod` 类可能另有 JSON、XML 或文本报告，按入口的实际结果参数处理；只传 `-logFile` 不会重定向这些报告。构建诊断、配置校验和发布验证的 stdout/stderr 同样写入 `local/Logs/` 下带时间戳的目录，不改变正式生成物路径。

## 清理策略

只清理明确不再需要的本地运行目录；先核对解析后的路径、消费者与写入状态。不能按年龄、扩展名或目录名在全仓递归删除。散落报告按[仓库结构治理](../repo-structure-governance/SKILL.md)审核后归档；保留原路径、校验值和失败证据，不覆盖同名报告。

## 命名规范

### 时间戳格式

统一 `yyyyMMdd-HHmmss`（如 `20260720-143015`）。`run_test_gate.ps1` 默认就是这个格式。

### 描述性后缀

ad-hoc 调试时（如 `MultiplayerHeadlessHeroReplacementCommand`），可以用描述性后缀：

```
local/Logs/headless/MultiplayerHeadlessHeroReplacement-20260720-143015-debug-buffer.xml
local/Logs/headless/MultiplayerHeadlessHeroReplacement-20260720-143215-rerun.xml
```

**不要**用模糊后缀如 `-final` / `-v2` / `-last`（看不出时间顺序，且容易堆积）。

### 复合命名

多步骤 ad-hoc 测试：`{project}-{scenario}-{timestamp}.{ext}`

```
local/Logs/unity-manual/moba-zhaoyun-skill1-20260720-143015.xml
local/Logs/shooter-manual/multiprocess-minimal-20260720-143015/
```

## 历史教训（不要再犯）

### 教训 1：默认 resultPath 写死到项目根

`MultiplayerHeadlessHeroReplacementCommand.cs` 旧默认：

```csharp
resultPath = Path.GetFullPath("../MultiplayerHeadlessHeroReplacement.xml");
```

导致根目录累积 24 个调试 XML。现已修复为 `local/Logs/headless/{timestamp}.xml`。

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
5. 如果另有明确的 CI 接入任务，按 [CI 接入约定](ci_integration.md) 配置并验收；不要仅由 `ciPolicy` 推断已接线。
