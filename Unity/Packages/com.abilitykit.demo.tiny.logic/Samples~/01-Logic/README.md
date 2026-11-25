# 01 逻辑

此示例仅依赖 `com.abilitykit.demo.tiny.logic`。`TinyLogicExample.Run()` 创建两名玩家，推进一个步长，恢复序列化的检查点，再比较后续 40 个步长的哈希。出现分歧时抛出异常，成功时返回最终哈希。

从仓库根运行 `dotnet run --project src/AbilityKit.Demo.Tiny.Logic.Sample`，可在无 Unity 环境执行相同示例。
