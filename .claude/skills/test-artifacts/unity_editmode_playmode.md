# Unity EditMode / PlayMode 测试

## 测试源码位置

### moba demo

- **EditMode**（asmdef `AbilityKit.Game.UnitTests`，`UNITY_INCLUDE_TESTS`）：
  - `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/UnitTest/Acceptance/Heroes/{Daji,LianPo,Mozi,XiaoQiao,YingZheng,ZhaoYun}/`
  - `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/UnitTests/`（含 `MultiplayerHeadlessHeroReplacementCommand.cs`）
  - `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/UnitTest/Acceptance/Common/` 与 `Infrastructure/`
  - `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/{Expectations,FrameSync}/`
- **EditMode 诊断**（asmdef `AbilityKit.Demo.Moba.Diagnostics.Core.Tests`，Editor only）：
  - `Unity/Packages/com.abilitykit.demo.moba.editor/Tests/`（23 测试）

### shooter demo

- **PlayMode**（asmdef `AbilityKit.Demo.Shooter.PlayMode.Tests`）：
  - `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Tests/PlayMode/ShooterSynchronizationPlayModeSmokeTests.cs`
  - 这是 shooter 客户端同步唯一的 Unity 测试入口

## 通过 gate runner 跑（推荐）

```json
// tools/test-gates.json
{
  "name": "moba-zhaoyun-unity",
  "steps": [{
    "name": "Zhao Yun Unity acceptance fixture",
    "kind": "unity-editmode-test",
    "projectPath": "Unity",
    "testPlatform": "EditMode",
    "testFilter": "AbilityKit.Game.Test.UnitTest.ZhaoYunSkillAcceptanceTests"
  }]
}
```

`run_test_gate.ps1` 会自动：

```
Unity.exe -batchmode -nographics -projectPath Unity \
    -runTests -testPlatform EditMode \
    -testFilter "AbilityKit.Game.Test.UnitTest.ZhaoYunSkillAcceptanceTests" \
    -testResults local/Logs/test-gates/{ts}-moba-zhaoyun-unity/unity-results/{name}.xml \
    -logFile local/Logs/test-gates/{ts}-moba-zhaoyun-unity/unity-results/{name}.log
```

## 直接跑（绕过 gate）

### Unity Editor GUI

Unity → Window → General → Test Runner → EditMode / PlayMode → Run All / Run Selected

产物默认在 `Unity/Library/` 下（临时），关闭 Editor 后清理。

### 命令行 batchmode

```powershell
$editor = "C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe"
& $editor -batchmode -nographics -projectPath Unity `
    -runTests -testPlatform EditMode `
    -testFilter "AbilityKit.Game.Test.UnitTest.ZhaoYunSkillAcceptanceTests" `
    -testResults local/Logs/unity-manual/{yyyyMMdd-HHmmss}-zhaoYun.xml `
    -logFile local/Logs/unity-manual/{yyyyMMdd-HHmmss}-zhaoYun.log
```

**重要**：必须显式指定 `-testResults` 和 `-logFile`，否则 Unity 会把结果写到默认位置（可能是项目根 `Unity/` 下，污染）。

## NUnit XML 结构

Unity Test Framework 输出 NUnit 格式：

```xml
<test-run name="..." result="Passed/Failed" total="6" passed="6" failed="0">
  <test-suite>...</test-suite>
</test-run>
```

`run_test_gate.ps1` 的 `Invoke-UnityTestStep` 解析 `test-run` 节点的 `result/total/passed/failed` 属性。

## UnityHeadless*.log 污染（已修复）

历史问题：直接调 Unity batchmode 不带 `-logFile` 时，日志默认输出到 `Unity/` 上一级（即项目根），产生 `UnityHeadlessSkillRelease*.log` 污染。

**修复**：2026-07-20 已修复。任何 Unity batchmode 调用必须带 `-logFile {path}`。`.gitignore` 的 `**/*.log` 兜底。

## 常见错误模式（来自 run_test_gate.ps1 源码）

```
Unity exited with code 0 before the command-line Test Runner started.
Log contains 'Batchmode quit successfully invoked - shutting down!'
but no 'Running tests for ...' marker.

This commonly indicates the requested -testFilter batch did not survive
domain reload or was not accepted by the Unity Test Framework command-line runner.
```

→ 检查 testFilter 是否准确；检查测试类是否在 asmdef 内；检查 `UNITY_INCLUDE_TESTS` script define。
