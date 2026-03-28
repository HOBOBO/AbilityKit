# AbilityKit HFSM Examples

## 概述

本目录包含 AbilityKit HFSM 模块的代码示例，展示了分层状态机的各种用法。

## 快速开始

1. 在 Unity Package Manager 中导入本示例包
2. 在场景中创建一个空 GameObject
3. 添加以下脚本之一：
   - `BasicStateMachineExample` - 基础状态机
   - `CharacterAIExample` - 角色AI（带分层状态机）
   - `TransitionConditionsExample` - 转换条件
   - `HfsmRuntimeTester` - 运行时测试器

## 示例说明

### BasicStateMachineExample

最简单的状态机示例，演示：
- 添加状态（使用 lambda）
- 添加转换
- 处理输入

**控制**: W 移动, Shift 奔跑, Space 跳跃

### CharacterAIExample

完整的角色AI示例，演示：
- 分层状态机（Patrol 和 Chase 包含子状态）
- 各种转换条件
- 任意状态转换
- 运行时监控集成

**特性**:
- `Patrol` 状态机：`PatrolIdle` <-> `Moving`
- `Chase` 状态机：`Approaching` <-> `Fighting`
- 任意状态转换：发现目标、死亡

### TransitionConditionsExample

转换条件详细示例，演示：
- 普通条件转换（每帧检查）
- 触发器转换（显式触发）
- 任意状态转换
- 双向转换
- 延迟转换（TransitionAfter）
- Exit 转换

**控制**:
- M/N: 开始/停止移动
- J/K: 开始/停止攻击
- T: 受到伤害
- H: 恢复生命
- L: 切换警报状态

### HfsmRuntimeTester

运行时测试器，用于验证状态机行为。

## 与编辑器配置的对比

| 特性 | 代码方式 | 编辑器方式 |
|------|---------|-----------|
| 可读性 | 需要阅读代码 | 图形化，直观 |
| 灵活性 | 动态生成/修改 | 需要打开编辑器 |
| 版本控制 | 代码易追踪 | Asset 文件可能冲突 |
| 调试 | 可设断点 | 支持可视化调试 |
| 导出 | 代码可直接使用 | 需要导出配置 |

## 运行时监控

所有示例都集成了 `UnityHFSM.Visualization.LiveRegistry`：
1. 打开菜单 `Window > AbilityKit > HFSM Runtime Monitor`
2. 进入 Play Mode
3. 查看运行中的状态机状态

## 目录结构

```
Samples/HFSM/Examples/
├── package.json
├── README.md
└── Scripts/
    ├── BasicStateMachineExample.cs      # 基础示例
    ├── CharacterAIExample.cs            # 角色AI（完整分层）
    ├── TransitionConditionsExample.cs   # 转换条件
    ├── ActionStateMachineExample.cs     # 行为树集成
    └── HfsmRuntimeTester.cs            # 运行时测试
```
