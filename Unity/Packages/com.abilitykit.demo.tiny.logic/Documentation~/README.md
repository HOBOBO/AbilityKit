# Tiny Logic 使用指南

> 文档类型：使用指南
> 适用范围：`com.abilitykit.demo.tiny.logic` 0.0.1；Unity 版本声明见 [package.json](../package.json)。
> 核校状态：已核对规则入口、检查点恢复与 `01 Logic` 样例；不含独立 Unity 安装和联机。
> 事实基线：2026-11-27

## 首次使用

从 Unity Package Manager 导入 [`01 Logic`](../Samples~/01-Logic/README.md)，调用 `AbilityKit.Demo.Tiny.Samples.TinyLogicExample.Run()`。样例无需场景、服务器或其他 AbilityKit 包；它恢复同一份检查点，向原对局与恢复后的对局提交相同输入，每帧比较哈希，连续 40 帧一致后返回最终哈希。

## 生命周期与失败行为

- 调用方用 [`TinyBattle`](../Runtime/Logic/TinyBattle.cs) 管理一至两名玩家，并决定何时提交输入和调用 `Tick()`。同一玩家在同一 Tick 前多次 `Submit`，后一次覆盖前一次；Tick 消费本帧输入。未知玩家或非法输入会抛出 `ArgumentException`。
- 在 Tick 边界调用 `CaptureState()`；检查点保存帧号和角色状态，不保存尚未消费的输入。`RestoreState()` 校验状态并替换当前对局，清空待处理输入；需要重演时由调用方重新提交输入。
- 使用 [`TinyBattleStateCodec`](../Runtime/Logic/TinyBattleStateCodec.cs) 编解码检查点；非法载荷或非法状态不能作为恢复基线。`ComputeHash()` 用于对照相同规则和输入下的状态，不承担网络同步或兼容协商。
