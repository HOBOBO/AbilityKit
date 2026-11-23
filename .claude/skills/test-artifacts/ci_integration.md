# CI 接入与产物

以[测试门禁与批量回归规范 §8](../../../Docs/AbilityKit测试门禁与批量回归规范.md#8-ci-接入与静态校验)为准。当前检出没有测试门禁工作流，配置中的触发策略不是自动执行事实。接入时从实际工作流查触发条件、job 与产物保留策略，不在 skill 内复制清单或定时值。

在本地运行配置校验：`./tools/validate_shooter_test_gates.ps1 -ConfigOnly`。要调试某项 gate，用 `./tools/run_test_gate.ps1 -Gate <name>`；`-CI` 只改变运行选项，不会接入 CI 或自动上传产物。
