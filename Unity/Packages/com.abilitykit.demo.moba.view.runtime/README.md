# AbilityKit MOBA View Runtime

`com.abilitykit.demo.moba.view.runtime` 是 MOBA 示例的客户端流程、会话与 Unity 表现运行时。它负责把运行时数据接到表现，不替代权威战斗模拟。

从项目配置的包来源安装本包，Unity 版本和依赖以 [`package.json`](package.json) 为准。接入已有 MOBA 示例时，从[客户端流程规格](Runtime/Game/App/Flow/MobaFlowSpec.md)和[使用与源码导航](Documentation~/Usage.md)确认装配与表现绑定，不用手工注册旧版 transport 或调试 Feature 代替正式装配。

- [早期专题索引](Document/README.md)：保留历史入口；跨包设计从仓库的 `Docs/design/00-index.md` 查阅。
