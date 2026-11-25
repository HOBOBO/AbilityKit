# 示例

本目录用于展示 `com.abilitykit.world.di` 的基础与常用用法。

说明：

- 这里的示例以 `Markdown + C# 代码块` 形式提供，默认不会参与 Unity 编译（避免影响 asmdef/程序集）。
- 示例强调：**优先构造函数注入**，`IWorldResolver` 只在组合边界使用。

从[包入口](../README.md)和[使用契约](../Documentation~/Usage.md)开始。以下每篇提供完整类型，可独立复制到测试工程；2026-11-25 已在仓库 .NET 镜像工程编译运行，不代表 Unity Player 验证：

- [基础注册与构造注入](01_BasicRegistration.md)：`BasicRegistrationExample.Run()` 返回 `True:True:True`，观察三种生命周期的实例身份。
- [模块拆分与组合](02_ModulesAndComposition.md)：`ModuleCompositionExample.Run()` 返回 `project`，展示默认注册和显式覆盖。
- [可选依赖](03_OptionalDependencies.md)：`OptionalDependencyExample.Run()` 返回 `disabled`，展示缺省服务的降级路径。
