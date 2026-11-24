# 本地状态同步场景

场景资产由 Battle Scenario 编译器读取，只定义命令名称与虚拟时间。夹具将命令绑定到类型化快照、输入和可选终结证明。执行器只属于测试项目，不注册到 MOBA Runner，也不改变 Battle Scenario 或同步包的运行时依赖。

两个夹具都在创建模拟宿主前通过 `FixedTickCommandPlan` 完成整条命令的预检与绑定。末尾命令无效时，前面的动作也不会执行。

测试按 100 Tick/s 推进。同一 Tick 按资产声明顺序执行；交付前封存当前 Tick 的输入，其他 Tick（包括确知无输入）也封存，除非场景显式留下缺口。检查点记录代际、准入位置、基线、发布与对账修订、恢复门禁、权威与预测投影、待终结序号和实际重演 Tick。每个用例各执行两次，比较有序检查点。

## 固定基线恢复

`fixed-baseline-recovery.battlescenario` 运行有、无输入终结证明两个变体。

| 虚拟 Tick | 验收点 |
| --- | --- |
| 10 | 初始全量进入权威锚点。 |
| 17 | 后续增量从有效全量重建，不依赖缺失的中间增量；同 Tick 迟到的旧增量不重新导入或重演。 |
| 18 | 基线失配后，最近提交保持不变，但对账器不再提供权威锚点或预测投影；紧接着的有效基线增量也不进入导入。 |
| 20 | 新全量在两种证明变体下都恢复权威；仅有匹配证明时才裁剪已终结的输入并重演余下 Tick。 |

## 会话换代与历史缺口

| 资产 | 验收点 |
| --- | --- |
| `generation-switch.battlescenario` | 换代清空游标与输入账本。新全量和预测就绪后才交付旧代际已提交的结果，以及一份引用错误基线的旧快照。旧结果在对账入口被忽略；旧快照不进入准入。两者都不能改变新作用域的权威、预测或待终结输入，后续新代际增量正常提交。 |
| `replay-history-gap.battlescenario` | 有可信终结证明但缺少一个 Tick 的覆盖时，仍提交权威并裁剪已终结前缀，但不执行任何重演步进、不等待全量；后续增量把锚点推进到缺口时，可从下一个 Tick 恢复重演。 |

## 导入与部分提交失败

两条场景复用本地双提交夹具。每次交付分别观测导入、准入、基线缓存、发布和对账；失败由夹具返回，未模拟生产世界写入或事务回滚。

| 资产 | 验收点 |
| --- | --- |
| `import-failure.battlescenario` | 隔离导入返回失败，准入与物化值不变，不发布、不裁剪输入、不重演；同修订重试成功后才交付导入结果，使用匹配证明重演。 |
| `partial-commit-failure.battlescenario` | 准入已推进、基线缓存提交失败时，旧物化值保留，不发布、不消耗证明，并进入全量等待。失败修订不能重复提交，等待中增量不导入；新全量无证明也能恢复权威，后续有匹配证明的增量才恢复裁剪与重演。 |

在仓库根目录运行：

```powershell
$run = New-Item -ItemType Directory -Path "local/Logs/dotnet/$(Get-Date -Format yyyyMMdd-HHmmss)-statesync-scenarios"
dotnet test src/AbilityKit.World.StateSync.Tests --no-restore --filter 'FullyQualifiedName~BattleScenario' `
  --logger 'trx;LogFileName=statesync-scenarios.trx' --results-directory $run.FullName *> (Join-Path $run.FullName 'statesync-scenarios.log')
```

首次运行需先从已配置的本地缓存还原依赖。测试只使用本进程的类型化对象；不启动网络服务、不抓包、不读取或解析网络载荷、不访问线上水位、不检查外部进程。它验证测试夹具的固定基线恢复和局部投影对账，不验证 MOBA 生产预测、帧同步整场回滚或线上链路。

## 导出验收回放

可直接在 Battle Scenario 编辑器顶部「状态同步复现样例 → 选样例（6）」查看五种场景的六份内置录制。录制保存于 `Unity/Packages/com.abilitykit.battlescenario/Editor/ReplaySamples/`，测试逐份核对其内容与当前运行输出一致。若有意改变验收契约，先确认新的检查点断言通过，再用本地导出的文件更新录制并重新运行测试；不要手填位置。

默认验证往返及内置录制，不写回放文件。可为单次测试设定显式输出目录，将通过行为断言的六份变体及实际步进前后位置导出为 JSON：

```powershell
$run = New-Item -ItemType Directory -Path "local/Logs/dotnet/$(Get-Date -Format yyyyMMdd-HHmmss)-statesync-replay"
$env:ABILITYKIT_LOCAL_STATESYNC_REPLAY_DIR = Join-Path $run.FullName 'replays'
try {
  dotnet test src/AbilityKit.World.StateSync.Tests --no-restore --filter 'FullyQualifiedName~BattleScenario' `
    --logger 'trx;LogFileName=statesync-replay.trx' --results-directory $run.FullName *> (Join-Path $run.FullName 'statesync-replay.log')
} finally {
  Remove-Item Env:ABILITYKIT_LOCAL_STATESYNC_REPLAY_DIR
}
```

在 Unity 的 Battle Scenario 窗口点「导入验收…」，从该次运行的 `replays/` 选择 `fixed-baseline-recovery-with-proof.statesync-replay.json`。用「跳到交付」定位 Tick 20 的 `recovery-full`：有证明时先看到回退起点 `130`，再单步观看重演 Tick `17、18、19、20` 的候选位置 `130、130、135、135`，最后看到检查点的预测位置 `135`。点击「切换回放…」打开同目录的 `fixed-baseline-recovery-without-proof.statesync-replay.json`，窗口会停在同一次交付的检查点：权威位置 `130`，无预测、无重演。观察同一 Tick 的不同交付时，以「动作 / 交付 Tick」区分交付，以「重演 Tick」区分模拟步进。旧版产物只能看检查点，不会根据 Tick 猜测位置。
