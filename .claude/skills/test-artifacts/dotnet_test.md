# dotnet test 直接跑

不经过 `run_test_gate.ps1`，直接 `dotnet test` 的产物管理。

## 项目清单

| 项目 | 路径 |
|------|------|
| `AbilityKit.Demo.Moba.Tests` | `src/AbilityKit.Demo.Moba.Tests/`（25 测试） |
| `AbilityKit.Demo.Moba.NetworkCondition.Tests` | `src/AbilityKit.Demo.Moba.NetworkCondition.Tests/`（1 测试） |
| `AbilityKit.Demo.Moba.View.Runtime.Tests` | `src/AbilityKit.Demo.Moba.View.Runtime.Tests/` |
| `AbilityKit.Demo.Shooter.Runtime.Tests` | `src/AbilityKit.Demo.Shooter.Runtime.Tests/` |
| `AbilityKit.Network.Runtime.Tests` | `src/AbilityKit.Network.Runtime.Tests/` |
| `AbilityKit.World.DI.Tests` | `src/AbilityKit.World.DI.Tests/` |
| `AbilityKit.Game.View.Runtime.Tests` | `src/AbilityKit.Game.View.Runtime.Tests/` |
| `AbilityKit.Record.Tests` | `src/AbilityKit.Record.Tests/` |
| `AbilityKit.Demo.Shooter.AoiLodBenchmarks.Tests` | `src/AbilityKit.Demo.Shooter.AoiLodBenchmarks.Tests/` |
| Orleans Server 系列 | `Server/Orleans/src/AbilityKit.Orleans.{Gateway,Grains,ShooterSmoke}.Tests/` |

## 默认产物路径

`dotnet test` 默认输出：

```
{ProjectDir}/TestResults/{Configuration}/{Framework}/_(_(_(net10.0)))/{timestamp}.trx
```

例如：

```
src/AbilityKit.Demo.Moba.Tests/TestResults/Debug/net10.0/_(_(_(net10.0)))/2026-07-20_14_30_15.trx
```

**`.gitignore` 的 `**/TestResults/` 覆盖所有这些路径**。

## 推荐命令（带显式产物路径）

```powershell
# 单项目跑，结果到指定目录
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj `
    --logger "trx;LogFileName=moba.trx" `
    --results-directory artifacts/dotnet/manual/{yyyyMMdd-HHmmss}

# 跑全部
dotnet test

# 带筛选
dotnet test --filter "Gate=MobaConsoleSmoke"
```

## TRX 文件结构

TRX 是 Visual Studio Test Result Format（XML）：

```xml
<TestRun>
  <Results>
    <UnitTestResult testName="..." outcome="Passed/Failed" duration="..." />
  </Results>
  <ResultSummary>
    <Counters total="25" passed="24" failed="1" />
  </ResultSummary>
</TestRun>
```

`run_test_gate.ps1` 的 `Assert-DotNetTrxResult` 解析 Counters 节点判断 pass/fail。

## 清理策略

```
# 清理所有 TestResults
find . -type d -name "TestResults" -not -path "*/Library/*" -not -path "*/.kilo/*" -exec rm -rf {} +

# 清理 7 天前的 TestResults
find . -type d -name "TestResults" -mtime +7 -exec rm -rf {} +
```

## 注意事项

- `dotnet test` 默认会 build，产物散落 `{ProjectDir}/bin/` 与 `obj/`（已被 `**/obj/` 与 `**/Bin/` 忽略）
- 跑 `dotnet test --no-build` 跳过 build（需先手动 `dotnet build`）
- 使用 `dotnet test --logger "console;verbosity=detailed"` 控制台详细输出（不写文件）
