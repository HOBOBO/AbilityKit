# AbilityKit 第三方 RVO2 包

这是纯 C# RVO2 2.0.1 库的 Unity Package Manager 封装。

## 包边界

本包仅包含上游 RVO2 C# 运行时源码，用于提供互惠式碰撞规避模拟。它不提供 Unity 组件、场景接入、寻路、转向行为编排或 AbilityKit 玩法策略。

- 包名：`com.abilitykit.thirdparty.rvo2`
- 程序集：`AbilityKit.ThirdParty.RVO2`
- 命名空间：`RVO`
- 上游版本：`2.0.1`
- 许可证：Apache-2.0
- 上游项目：<https://gamma.cs.unc.edu/RVO2/>
- 源码仓库：<https://github.com/snape/RVO2-CS>

`Runtime/RVO2` 下的上游算法源码有意保持原样。包元数据、程序集定义和文档属于 AbilityKit 接入文件。

## 引用包

在使用方的程序集定义中添加运行时程序集引用：

```json
{
  "references": [
    "AbilityKit.ThirdParty.RVO2"
  ]
}
```

本包以嵌入包形式存放在 `Unity/Packages` 下，因此不需要在项目的 `manifest.json` 中额外添加条目。

## 最小使用示例

```csharp
using RVO;

Simulator simulator = Simulator.Instance;
simulator.Clear();
simulator.SetNumWorkers(1);
simulator.setTimeStep(0.1f);
simulator.setAgentDefaults(
    neighborDist: 15.0f,
    maxNeighbors: 10,
    timeHorizon: 5.0f,
    timeHorizonObst: 5.0f,
    radius: 0.5f,
    maxSpeed: 2.0f,
    velocity: new RVO.Vector2(0.0f, 0.0f));

int agentId = simulator.addAgent(new RVO.Vector2(0.0f, 0.0f));
simulator.setAgentPrefVelocity(agentId, new RVO.Vector2(1.0f, 0.0f));
simulator.doStep();
RVO.Vector2 position = simulator.getAgentPosition(agentId);
```

## 接入注意事项

- `RVO.Vector2` 与 `UnityEngine.Vector2` 是不同类型。使用方模块应显式限定类型名称，或提供明确的转换辅助方法。
- `Simulator.Instance` 是全局可变状态。应由宿主模块统一负责初始化、步进和清理，避免无关系统直接修改模拟器。
- 配置新的模拟会话前应调用 `Clear()`。
- 首次调用 `doStep()` 前应调用 `SetNumWorkers()`。更改工作线程数会使内部工作线程缓存失效。
- `SetNumWorkers(0)` 会选用 .NET 线程池的最小工作线程数。需要确定性资源占用时，应显式传入正整数。
- 障碍物顶点应按逆时针顺序添加。顺时针顺序表示负障碍物或包围型障碍物。
- 添加完所有静态障碍物后，应先调用 `processObstacles()`，再执行可见性查询或让 Agent 绕障碍物移动。
- 本包不依赖 `UnityEngine`，其他纯 C# 程序集也可以直接使用。

## 升级策略

升级 RVO2 时：

1. 仅使用选定上游版本替换 `Runtime/RVO2` 下的文件。
2. 保留上游版权声明和 `LICENSE.md`。
3. 在 `CHANGELOG.md` 与 `THIRD PARTY NOTICES.md` 中记录上游版本和本地封装变更。
4. 修改依赖模块前，先执行包验证和 RVO2 行为冒烟测试。
