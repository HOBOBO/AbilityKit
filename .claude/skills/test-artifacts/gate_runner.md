# Gate Runner（推荐入口）

正式回归优先走 [runner](../../../tools/run_test_gate.ps1)；gate 名称、级别、负责人和步骤以 [test-gates.json](../../../tools/test-gates.json) 为准。配置与 CI 接入的关系见[测试门禁与批量回归规范](../../../Docs/AbilityKit测试门禁与批量回归规范.md)。

## 用法

```powershell
# 列出所有 gate
./tools/run_test_gate.ps1 -List

# 跑配置中的默认 gate
./tools/run_test_gate.ps1

# 跑指定 gate
./tools/run_test_gate.ps1 -Gate moba-console-smoke

# 跑指定 step（调试用）
./tools/run_test_gate.ps1 -Gate shooter-performance -StepName "AOI LOD smoke"

# 指定结果目录
./tools/run_test_gate.ps1 -ResultsDirectory local/Logs/test-gates-custom

# 已接入的 CI job 显式指定产物路径
./tools/run_test_gate.ps1 -Gate precheck -CI -ResultsDirectory artifacts/test-gates/precheck
```

## 产物路径

本地默认 `-ResultsDirectory local/Logs/test-gates`，每次运行生成：

```
local/Logs/test-gates/{yyyyMMdd-HHmmss}-{Gate}/
├── gate-summary.json                    ← 总结（executionScope/selectedStepName/status/steps[]）
├── 01-{StepName}.log                    ← 每个 step 的控制台输出
├── 01-{StepName}.error.log              ← stderr（仅 powershell-script step）
├── test-results/                        ← dotnet test step
│   └── 01-{StepName}.trx
└── unity-results/                       ← unity-*-test step
    ├── {StepName}.xml                   ← NUnit XML
    ├── {StepName}.log                   ← Unity Editor log
    └── {StepName}.command.txt           ← 完整命令行
```

nested gate 路径嵌套：`local/Logs/test-gates/{ts}-regression/{ts}-moba-zhaoyun-unity/...`

## 如何选择 gate

用 `-List` 读取当前配置的 scope、requiredBefore 和 failurePolicy，不维护静态清单。进一步看 JSON 中该 gate 的步骤和依赖。`-List` 不验证目标存在，也不执行测试；静态检查用 `./tools/validate_shooter_test_gates.ps1 -ConfigOnly`。

正式验收、`-StepName` 诊断和手工脚本的结果不可互代。报告前按[门禁与手工入口](../../../Docs/AbilityKit测试门禁与批量回归规范.md#门禁与手工入口)核对执行范围，不能只看汇总的 `status`。

## 常见 Step Kind

| Kind | 用法 | 产物 |
|------|------|------|
| `dotnet-build` | `dotnet build {project} -c {Configuration}` | `{idx}-{name}.log` |
| `dotnet-test` | `dotnet test {project} --logger trx;LogFileName={idx}-{name}.trx` | `{idx}-{name}.log` + `test-results/{idx}-{name}.trx` |
| `unity-editmode-test` | Unity batchmode `-runTests -testPlatform EditMode -testFilter {filter}` | `unity-results/{name}.xml` + `.log` + `.command.txt` |
| `unity-playmode-test` | 同上但 `-testPlatform PlayMode` | 同上 |
| `unity-execute-method` | Unity batchmode `-executeMethod {executeMethod}` | `.log` + `.command.txt`，可选 `resultsFile` |
| `gate` | 运行配置中的子 gate | 子 gate 有独立汇总和产物目录 |
| `powershell-script` | 委托给子脚本，`{GateOutputDirectory}` 占位符替换 | `{idx}-{name}.log` + `.error.log` |

## Gate 之间依赖（nested gate）

gate 的 step 可以是另一个 gate：

```json
{
  "name": "regression",
  "steps": [
    { "name": "Zhao Yun Unity acceptance", "kind": "gate", "gate": "moba-zhaoyun-unity" }
  ]
}
```

runner 检测循环依赖（`HashSet<string> Visiting`），抛 `"Circular gate dependency detected"`。

## gate-summary.json 结构

```json
{
  "gate": "precheck",
  "gatePath": ["precheck"],
  "executionScope": "FullGate",
  "selectedStepName": null,
  "level": "P0",
  "owner": "Core/MOBA Runtime",
  "status": "Passed",
  "startedAt": "2026-07-20T...",
  "endedAt": "2026-07-20T...",
  "elapsedSeconds": 12.345,
  "outputDirectory": "local/Logs/test-gates/20260720-...",
  "summaryPath": "local/Logs/test-gates/20260720-.../gate-summary.json",
  "failureMessage": null,
  "steps": [
    { "name": "...", "kind": "dotnet-test", "status": "Passed", "exitCode": 0 }
  ]
}
```

范围和结果分开读取：`executionScope` 表示本次选择的范围，不表示所有步骤都执行到了；`-StepName` 写入 `SingleStep` 和请求的 `selectedStepName`，即使没有找到该步骤也会在失败汇总中保留。判读规则见[门禁与手工入口](../../../Docs/AbilityKit测试门禁与批量回归规范.md#门禁与手工入口)。

修改 runner 汇总契约后，从仓库根目录复验：`python -B -X utf8 -m unittest discover -s tools/tests -p test_gate_runner.py -v`。自测依赖 Windows PowerShell，只跑隔离夹具，不启动 Unity 或服务端。

CI 接入和结果目录见[当前约定](ci_integration.md)。仅列出 gate 或通过静态检查，不代表 gate 已执行通过。
