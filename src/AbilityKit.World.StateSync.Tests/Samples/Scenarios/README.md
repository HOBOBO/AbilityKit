# 本地状态同步场景

`fixed-baseline-recovery.battlescenario` 由 Battle Scenario 编译器读取。资产只定义命令名称与虚拟时间，命令绑定的快照、输入和可选终结证明由 `LocalProjectionReferenceTests.BattleScenario.cs` 创建。后端只属于测试项目，不注册到 MOBA Runner，也不改变 Battle Scenario 或同步包的运行时依赖。

测试按 100 Tick/s 推进。同一 Tick 按资产声明顺序执行；交付前封存当前 Tick 的输入，其他 Tick 包括无输入 Tick 也显式封存。每次交付记录准入、基线、发布修订、两个恢复门禁、权威与预测投影、待终结序号和实际重演 Tick。两个变体各执行两次并比较完整的有序检查点。

| 虚拟 Tick | 验收点 |
| --- | --- |
| 10 | 初始全量进入权威锚点。 |
| 17 | 后续增量从有效全量重建，不依赖缺失的中间增量；同 Tick 迟到的旧增量不重新导入或重演。 |
| 18 | 基线失配后，最近提交保持不变，但对账器不再提供权威锚点或预测投影；紧接着的有效基线增量也不进入导入。 |
| 20 | 新全量在两种证明变体下都恢复权威；仅有匹配证明时才裁剪已终结的输入并重演余下 Tick。 |

在仓库根目录运行：

```powershell
dotnet test src/AbilityKit.World.StateSync.Tests --no-restore --filter 'FullyQualifiedName~FixedBaselineBattleScenario'
```

首次运行需先从已配置的本地缓存还原依赖。测试只使用本进程的类型化对象；不启动网络服务、不抓包、不读取或解析网络载荷、不访问线上水位、不检查外部进程。它验证测试夹具的固定基线恢复和局部投影对账，不验证 MOBA 生产预测、帧同步整场回滚或线上链路。
