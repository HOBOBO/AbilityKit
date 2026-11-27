# Game Battle Runtime

`com.abilitykit.game.battle.runtime` 提供战斗逻辑传输接口、请求值对象，以及可组合的阶段图、系统排序、就绪校验和健康报告。

## 安装与首次使用

从项目已配置的包来源安装。版本、Unity 声明和直接依赖见 [package.json](package.json)；本包声明 Unity 2021.3，完整组合还须满足 Host、World、Ability 等传递依赖的更高条件，不能据此承诺整个组合支持 2021.3。

先运行[本地阶段图与就绪示例](Documentation~/Usage.md#2-完整示例阶段图与就绪条件)，预期 load 完成后才开放 battle。接入实际战斗时由组合根提供 transport、世界、阶段执行器与释放策略；本包不会自动创建会话或驱动模拟。

## 文档与兼容

- [文档索引](Documentation~/README.md)
- [使用指南](Documentation~/Usage.md)：请求引用语义、DAG 排序、异常及生命周期。
- [旧 BattleTransport 设计](Document/BattleTransportDesign.md)：历史材料，现行契约已由使用指南取代。

命名空间为 `AbilityKit.Game.Battle`，请求位于 `.Requests`。旧文档中的退役 transport 包、旧命名空间和“struct 即零 GC”不能用于当前接入。独立安装、Unity Player 与完整在线组合尚未据本轮本地验证验收。
