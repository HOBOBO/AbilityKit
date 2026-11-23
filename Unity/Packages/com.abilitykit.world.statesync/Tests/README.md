# 包级验收

直接契约测试放在仓库的 `src/AbilityKit.World.StateSync.Tests`，不在 Unity 包复制 xUnit 测试或引入测试运行时依赖。该工程也编译 `Samples~/LocalCheckpoints/LocalCheckpointExample.cs`，使用同一份示例验证捕获、恢复、容量淘汰与恢复缺失。

`LocalStateSyncContractTests` 使用本地类型化对象固定 AOI 自有结果与临时复用、可见顺序和边界、哈希缺样本兼容行为、关键帧计划与估算。

`LocalSnapshotHistoryExampleTests` 直接编译 `Samples~/LocalSnapshotHistory` 的可导入示例，验证默认稀疏和自定义环形后端的帧零、覆盖、淘汰和对象隔离。只跑这组离线验收可使用 `--filter FullyQualifiedName~LocalSnapshotHistoryExampleTests`。

`LocalStateCommitExampleTests` 直接编译 `Samples~/LocalStateCommit` 的可导入示例，验证非法、重复及迟到候选不导入，首次导入失败无游标，后续失败或异常保留原状态及游标，且同修订可以重试。只跑这组离线验收可使用 `--filter FullyQualifiedName~LocalStateCommitExampleTests`。

`LocalStateCommitReconciliationTests` 在纯内存宿主中联动同一提交示例和 P0 对账原型。导入失败或抛异常不交付候选、不步进、不裁剪待终结输入；成功重试后用导入结果重演，只裁剪独立同截面证明覆盖的前缀。成功提交但缺少匹配证明时只更新权威状态。定向入口为 `--filter FullyQualifiedName~LocalStateCommitReconciliationTests`。

同一夹具验证基线失效后的恢复门禁：增量提交、旧全量和导入失败的全量均不解除等待。成功提交的新全量可恢复权威读数；没有匹配证明仍不裁剪输入或重演，待后续提交提供证明后再恢复预测。

`LocalCommittedStateResultTests` 让准入游标与基线缓存两个步骤分别返回失败，验证顺序、部分提交不交付、重复抑制及新全量恢复。部分提交时夹具保留已写游标，不把它当作权威锚点或输入终结证明。定向入口为 `--filter FullyQualifiedName~LocalCommittedStateResultTests`。

同一夹具在单线程上模拟会话切换：迟到的旧候选不进入导入；已进入提交的旧候选即使在切换后完成，也不发布、不使新会话进入全量等待、不裁剪其输入。

`LocalProjectionReferenceTests` 在 .NET 测试工程内复用通用快照准入管线，用普通移动与据点投影验证固定基线重建、导入失败、旧代际拒绝与成功交付。对账是可选的，缺少独立终结证明时不裁剪输入。仅测试工程引用该管线，不新增 Unity 包依赖；定向入口为 `--filter FullyQualifiedName~LocalProjectionReferenceTests`。

`ClientPredictionEventTests` 使用固定业务槽位的本地类型化实体，验证实体回滚、模块回滚、快照通知与延后发布的槽位变化，以及缺历史和订阅方异常下的行为。

`LocalMovementReconciliationTests` 通过 .NET 测试友元访问包内 P0 原型，验证作用域隔离、终结证明、覆盖与容量、失败回退和已提交全量门禁；不启动游戏、会话或外部服务。

`Editor/` 包含 Unity EditMode 冒烟测试，验证默认容量、快照隔离和按帧淘汰；在 Test Runner 的 EditMode 中执行 `StateSyncPackage_BufferOwnsCopiesAndKeepsLatestFrames`。

有本地依赖缓存时，从仓库根目录运行：

```powershell
dotnet test src/AbilityKit.World.StateSync.Tests/AbilityKit.World.StateSync.Tests.csproj --no-restore
$runtime = 'Unity/Packages/com.abilitykit.world.statesync/Runtime/StateSync'
foreach ($area in 'Buffer', 'Checkpoints', 'Prediction', 'Snapshot', 'Diff', 'Aoi', 'Hash', 'Events') {
    dotnet run --no-restore --project tools/AbilityKit.PackageQuality -- "$runtime/$area"
    if ($LASTEXITCODE -ne 0) { throw "注释检查未通过：$area" }
}
```

上述目录已通过注释检查。全 Runtime 仍有既有债务，未启用包级 XML 文档错误门禁。离线验收只用本仓库源码与测试进程内的类型化对象；不抓包、不查询线上水位、不读写或解析网络载荷、不启动网络服务，也不检查项目外的进程或对象。
