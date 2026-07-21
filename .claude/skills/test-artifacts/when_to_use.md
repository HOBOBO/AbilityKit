# When to use

启用本 skill 的典型场景：

## 跑测试前

- 你要跑 moba / shooter / dotnet / Unity 任意测试，想先确认产物会落在哪里
- 你在写新测试，需要决定结果文件输出路径
- 你要加一个新的 test gate，需要遵守 gate 体系约定

## 排查产物位置

- 同事跑完测试后产物找不到了
- CI 上传的 artifact 在哪个路径
- `artifacts/test-gates/` 下哪个时间戳目录是最新的
- dotnet test 的 TRX 文件在哪
- Unity 测试的 NUnit XML 在哪

## 清理产物

- 磁盘空间不够，要清理历史测试产物
- `artifacts/` 目录太大，要识别哪些可以安全删除
- CI 工作流的 artifact 保留策略

## 绕过 gate 体系时

- 你要直接调 Unity batchmode 跑 `MultiplayerHeadlessHeroReplacementCommand`（不走 `run_test_gate.ps1`）
- 你要直接 `dotnet test` 而不经过 gate runner
- 你要直接调 `restart_shooter_state_sync.bat` 启动 shooter 服务器

## 不要在本 skill 找的内容

- 测试本身怎么写（业务测试设计）→ 各 demo 的 skill（[moba-demo](../moba-demo/SKILL.md) / [shooter-demo](../shooter-demo/SKILL.md)）
- gate 定义的语义（P0/P1/P2 级别）→ [gate_runner.md](gate_runner.md) 简述，完整定义在 `tools/test-gates.json`
- 测试内容源码 → 各 Unity 包的 `Runtime/Game/Test/` 或 `Editor/Tests/`
