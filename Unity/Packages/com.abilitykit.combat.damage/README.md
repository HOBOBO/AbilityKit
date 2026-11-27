# AbilityKit Combat Damage

基于 Dataflow 的同步伤害计算包，提供 `DamageRequest`、`DamageResult`、领域 Context、强类型槽位和八阶段默认管线。只返回计算结果，实际扣血、护盾提交、死亡与表现由项目负责。

## 环境与安装

版本 0.1.0、Unity 2022.3 及直接依赖以 [package.json](package.json) 为准：`core`、`dataflow`、`modifiers`、`attributes`。宿主安装本包及相容依赖；本地可用 Package Manager 的 **Add package from disk** 选择 package.json。自定义 asmdef 引用 `AbilityKit.Combat.Damage`，代码命名空间为 `AbilityKit.Combat`。

本轮通过同源 .NET 10 镜像核对，不代表独立 UPM 安装、Unity 或业务完整结算已通过。

## 首次使用与文档

先运行[完整物理伤害示例](Documentation~/Usage.md#2-完整示例物理减伤计算)：100 基础值加 20 攻击力，100 护甲后得到 60，80 生命下 ActualDamage 为 60。预期输出 `damage calculation contracts passed`。每次计算显式提供并重置 `DamageCalculationContext`。

- [使用导航](Documentation~/README.md)与[使用指南](Documentation~/Usage.md)：默认阶段、字段含义、失败处理及实现限制。
- [历史开发说明](Document/CombatDamage伤害计算模块开发设计文档.md)：保留旧路径，现行接入回到使用指南。
- [Runtime 源码](Runtime/Damage/)与[程序集声明](Runtime/com.abilitykit.combat.damage.asmdef)。

默认管线存在暴击标记跨阶段丢失、护盾后 ActualDamage 可超过生命值、Mixed 不拆分及多项 Flags 未被消费等限制，详见指南。不能据枚举名或 DTO 字段推定完整战斗机制。
