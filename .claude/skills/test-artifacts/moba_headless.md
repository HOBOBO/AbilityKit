# moba headless 测试

## MultiplayerHeadlessHeroReplacementCommand

源文件：`Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/UnitTests/MultiplayerHeadlessHeroReplacementCommand.cs`

### 用途

Unity Editor `[InitializeOnLoad]` 静态类，通过 `-executeMethod MultiplayerHeadlessHeroReplacementCommand.Run` 触发，验证多人房间流程的英雄替换（玩家用 hero 1001 创建房间 → 替换为 hero 1002 → 等权威帧通过）。

### 入口签名

```csharp
public static class MultiplayerHeadlessHeroReplacementCommand
{
    [InitializeOnLoad] static MultiplayerHeadlessHeroReplacementCommand() { ... }
    public static void Run() { ... }
}
```

### 默认 resultPath（2026-07-20 已修复）

**旧默认**（污染根目录）：

```csharp
resultPath = Path.GetFullPath("../MultiplayerHeadlessHeroReplacement.xml");
// 相对 Unity/ 即项目根，每次跑都污染
```

**新默认**（local/Logs/headless/ + 时间戳）：

```csharp
var headlessDir = Path.GetFullPath("../../local/Logs/headless");
Directory.CreateDirectory(headlessDir);
var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
resultPath = Path.Combine(headlessDir, $"MultiplayerHeadlessHeroReplacement-{stamp}.xml");
```

### 命令行参数

- `-multiplayerHeadlessResult {path}` — 覆盖默认 resultPath（ad-hoc 调试用）

### 调用方式

#### 通过 gate（推荐，但当前未在 test-gates.json 注册）

未注册到 gate。如需加入，可在 `tools/test-gates.json` 添加：

```json
{
  "name": "moba-multiplayer-headless",
  "level": "P1",
  "steps": [{
    "name": "Hero replacement headless",
    "kind": "unity-execute-method",
    "projectPath": "Unity",
    "executeMethod": "AbilityKit.Game.Test.UnitTest.MultiplayerHeadlessHeroReplacementCommand.Run",
    "resultsFile": "MultiplayerHeadlessHeroReplacement-{timestamp}.xml"
  }]
}
```

#### 直接 batchmode（不走 gate）

```powershell
$editor = "C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
& $editor -batchmode -nographics -projectPath Unity `
    -executeMethod AbilityKit.Game.Test.UnitTest.MultiplayerHeadlessHeroReplacementCommand.Run `
    -multiplayerHeadlessResult "local/Logs/headless/MultiplayerHeadlessHeroReplacement-$stamp.xml" `
    -logFile "local/Logs/headless/$stamp.log"
```

### XML 结果结构

```xml
<?xml version="1.0" encoding="utf-8"?>
<multiplayerHeadlessHeroReplacement success="false">
  <message encoding="base64">{base64 编码的诊断信息}</message>
</multiplayerHeadlessHeroReplacement>
```

success=true / false 直接决定测试结果。message base64 解码后是详细异常信息（如 `EXCEPTION: System.TimeoutException: Hero replacement did not return through authoritative frames...`）。

## 其他 moba headless 命令

`MultiplayerHeadlessHeroReplacementCommand` 是当前唯一的 headless 风格测试。其他 moba 测试要么是 `dotnet test`（Console Demo），要么是 Unity Test Framework EditMode / PlayMode。

## 历史归档

2026-07-20 前根目录曾散落 26 个 XML / log（含 24 个带 `-fix/-rerun/-probe/-diagnostic` 后缀的调试快照）。这些本地历史产物不再作为当前路径约定的一部分；新的无头结果统一写入 `local/Logs/headless/`。
