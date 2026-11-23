# 示例

本目录用于展示 `com.abilitykit.world.di` 的基础与常用用法。

说明：

- 这里的示例以 `Markdown + C# 代码块` 形式提供，默认不会参与 Unity 编译（避免影响 asmdef/程序集）。
- 示例强调：**优先构造函数注入**，`IWorldResolver` 只在组合边界使用。

从[包入口](../README.md)开始。以下片段用于展示项目扩展方式，示意业务类型由项目提供：

- [基础注册与构造注入](01_BasicRegistration.md)
- [模块拆分与组合](02_ModulesAndComposition.md)
- [可选依赖](03_OptionalDependencies.md)
